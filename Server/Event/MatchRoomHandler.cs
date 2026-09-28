using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.ComponentModel.Design;
using System.Globalization;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;


#nullable enable

namespace OpenGSServer
{

    public enum GameEventType
    {
        LoadingFinished,
        PlayerShot,
        PlayerKilled,
        PlayerDamaged,
        GrenadeThrow,
        ItemUsed,
        FlagCaptured,
        FlagLost,
        FlagPickup,
        FlagReturn,
        FlagScoreUpdate,
        PlayerEliminated,
        MatchStatusRequest,
        PlayerPositionUpdate,
        PlayerRespawn,
        PlayerPose
    }

    public class IInGameMatchRoomHandler
    {

    }
    internal class InGameMatchEventHandler:IInGameMatchRoomHandler
    {
        private static readonly ConcurrentDictionary<string, DateTime> LastFlagEvents = new();

        /// <summary>
        /// Where each room's flags are, and who is carrying them.
        /// <para>
        /// This was a set of carrier records keyed by the carrier, which could
        /// answer who was holding something and nothing else. A flag belongs to a
        /// team, and the capture rule is a question about a team's own flag, so
        /// the state has to be held per team. A record of carriers cannot answer
        /// "is this team's flag still home", which is why a team could score
        /// while its own flag was in the other side's hands.
        /// </para>
        /// </summary>
        private static readonly ConcurrentDictionary<string, FlagRoomState> FlagRooms = new();

        /// <summary>
        /// The flag state of one room.
        /// </summary>
        private sealed class FlagRoomState
        {
            private readonly object sync = new();
            private readonly Dictionary<ETeam, TeamFlag> flags = new()
            {
                [ETeam.Red] = new TeamFlag(ETeam.Red),
                [ETeam.Blue] = new TeamFlag(ETeam.Blue)
            };

            /// <summary>
            /// Hands out a snapshot, so a caller reads a consistent set of flags
            /// rather than one flag at a time while another thread moves one.
            /// </summary>
            public Dictionary<ETeam, TeamFlag> Snapshot()
            {
                lock (sync)
                {
                    return flags.ToDictionary(entry => entry.Key, entry => entry.Value);
                }
            }

            public T WithFlags<T>(Func<Dictionary<ETeam, TeamFlag>, T> action)
            {
                lock (sync)
                {
                    return action(flags);
                }
            }
        }

        private static FlagRoomState FlagStateFor(MatchRoom room)
        {
            return FlagRooms.GetOrAdd(room.Id.ToString(), _ => new FlagRoomState());
        }

        /// <summary>
        /// A clock the client cannot set, for how long a flag has been lying
        /// about.
        /// </summary>
        private static double ServerNowSeconds() => (double)DateTime.UtcNow.TimeOfDay.TotalSeconds;

        /// <summary>
        /// Puts back any flag that has been on the ground long enough.
        /// <para>
        /// The client has a timer for this and the client was the only side that
        /// had one, so a dropped flag came back only if some client lived long
        /// enough to say so. A flag whose return message never arrived stayed on
        /// the ground for the rest of the match, and the team that owned it could
        /// never pick it up again, so it could never score. The server holds the
        /// flag, so the server keeps the time.
        /// </para>
        /// <para>
        /// This runs from the match loop rather than from a claim, because a flag
        /// sitting on the ground waiting is not an event; it is a state that
        /// becomes untrue on a timer.
        /// </para>
        /// </summary>
        public static void ReturnTimedOutFlags(MatchRoom room)
        {
            if (room == null)
            {
                return;
            }

            var now = ServerNowSeconds();
            var team = FlagStateFor(room).WithFlags(flags =>
                CaptureTheFlagRules.ReturnTimedOutFlags(flags, now));

            if (team == ETeam.NoTeam)
            {
                return;
            }

            Console.WriteLine($"[Match] The {team} flag had been on the ground long enough to go home");
            GameMessageDispatcher.SendFlagReturned(
                room.Id.ToString(),
                team.ToString(),
                string.Empty,
                EFlagReturnReason.AutoReturn.ToString());
        }

        /// <summary>
        /// Where each of a room's flags is, for a caller that has to reason about
        /// the flags rather than move them.
        /// <para>
        /// The flags themselves are shared, so this is a view rather than a copy
        /// of the state. It exists so a test can assert on where a flag is without
        /// the assertion itself being a second implementation of the rules.
        /// </para>
        /// </summary>
        public static IReadOnlyDictionary<ETeam, TeamFlag> GetFlagStates(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return new Dictionary<ETeam, TeamFlag>();
            }

            // A room has had two flags since it was created, so asking about them
            // creates them if nothing has touched one yet. Anything else would make
            // a caller conclude a room had no flags rather than that nobody had
            // claimed one.
            return FlagRooms
                .GetOrAdd(roomId, _ => new FlagRoomState())
                .Snapshot();
        }
        private static readonly ConcurrentDictionary<string, DateTime> LastShots = new();

        public InGameMatchEventHandler() { }

        /// <summary>
        /// TCPベースのシステムイベント処理
        /// </summary>
        public static void ParseTcpEvent(JObject json)
        {
            var type = json.GetStringOrNull("MessageType");

            if (type != null)
            {
                MatchRoomManager manager = MatchRoomManager.Instance;

                var playerId = ReadString(json, "PlayerID", "PlayerId");
                var roomId = ReadString(json, "RoomID", "RoomId");

                if (playerId != null && roomId != null)
                {
                    var room = manager.SearchRoomByMemberID(playerId);

                    if (room != null && IsRoomMatch(room, roomId))
                    {
                        ProcessSystemEvent(room, type, json, playerId);
                    }
                    else if (room != null)
                    {
                        Console.WriteLine($"[Match] Rejected TCP event for mismatched room '{roomId}' from '{playerId}'");
                    }
                }
            }
        }

        /// <summary>
        /// UDPベースのリアルタイムゲームイベント処理
        /// </summary>
        public static void ParseUdpEvent(byte[] udpData, string remoteEndPoint)
        {
            try
            {
                // UDPデータをJSONに変換（実際の実装では適切なデシリアライズ）
                var jsonString = System.Text.Encoding.UTF8.GetString(udpData);
                var json = JObject.Parse(jsonString);

                var type = json.GetStringOrNull("MessageType");
                var playerId = ReadString(json, "PlayerID", "PlayerId");
                var roomId = ReadString(json, "RoomID", "RoomId");

                if (type != null && playerId != null && roomId != null)
                {
                    MatchRoomManager manager = MatchRoomManager.Instance;
                    var room = manager.SearchRoomByMemberID(playerId);

                    if (room != null && IsRoomMatch(room, roomId))
                    {
                        ProcessRealtimeGameEvent(room, type, json, playerId, remoteEndPoint);
                    }
                    else if (room != null)
                    {
                        Console.WriteLine($"[Match] Rejected UDP event for mismatched room '{roomId}' from '{playerId}'");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UDP event parsing error: {ex.Message}");
            }
        }

        private static void ProcessSystemEvent(MatchRoom room, string eventType, JObject json, string playerId)
        {
            switch (eventType)
            {
                case GameMessageTypes.LoadingStarted:
                    Console.WriteLine($"[Match] Player {playerId} started loading");
                    break;

                case GameMessageTypes.LoadingProgress:
                    var progress = json.GetValue("Progress")?.ToString() ?? json.GetValue("LoadingProgress")?.ToString() ?? "0";
                    Console.WriteLine($"[Match] Player {playerId} loading progress: {progress}");
                    break;

                case GameMessageTypes.LoadingCompleted:
                case GameMessageTypes.LoadingFinished:
                    room.SetPlayerReady(playerId);
                    break;

                case GameMessageTypes.MatchStatusRequest:
                    SendMatchStatus(room, playerId);
                    break;

                case GameMessageTypes.PlayerRespawn:
                    HandlePlayerRespawn(room, playerId, json);
                    break;

                case GameMessageTypes.PlayerPose:
                    HandlePlayerPose(room, playerId, json);
                    break;

                case GameMessageTypes.ObjectSpawned:
                    Console.WriteLine($"[Match] Ignored client-supplied object spawn from '{playerId}'; objects are server-authoritative");
                    break;

                case GameMessageTypes.ObjectDestroyed:
                    Console.WriteLine($"[Match] Ignored client-supplied object destroy from '{playerId}'; objects are server-authoritative");
                    break;

                default:
                    Console.WriteLine($"[Match] Unknown system event type: {eventType}");
                    break;
            }
        }

        private static void ProcessRealtimeGameEvent(MatchRoom room, string eventType, JObject json, string playerId, string remoteEndPoint)
        {
            if (room == null || string.IsNullOrWhiteSpace(playerId) ||
                !room.Players.Any(player => string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"[Match] Ignored realtime event from non-member '{playerId}' in room '{room?.Id}'");
                return;
            }

            switch (eventType)
            {
                case GameMessageTypes.PlayerKilled:
                    Console.WriteLine($"[Match] Ignored client-supplied kill event from '{playerId}'; kills are server-authoritative");
                    break;

                // A client asserting its own damage, and a client reporting a
                // ruling it was told about, are told apart by the trailing d the
                // same way a kill and a death are. Only the client's assertion is
                // refused: a ruling the server itself sent is not a claim.
                case GameMessageTypes.PlayerDamaged:
                    Console.WriteLine($"[Match] Ignored client-supplied damage event from '{playerId}'; damage is server-authoritative");
                    break;

                case GameMessageTypes.FlagCaptured:
                    HandleFlagCaptured(room, playerId, json);
                    break;

                case GameMessageTypes.FlagLost:
                    HandleFlagLost(room, playerId, json);
                    break;

                case GameMessageTypes.FlagPickup:
                    HandleFlagPickup(room, playerId, json);
                    break;

                case GameMessageTypes.FlagReturn:
                    var returnedByPlayerId = ReadString(json, "ReturnedByPlayerId", "ReturnedByPlayerID", "PlayerID", "PlayerId");
                    if (!string.IsNullOrWhiteSpace(returnedByPlayerId) &&
                        !string.Equals(returnedByPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[Match] Ignored flag return with forged player '{returnedByPlayerId}' from '{playerId}'");
                        break;
                    }

                    HandleFlagReturn(room, playerId, json);
                    break;

                // A client asserting a flag it destroyed. A flag going is a rule
                // outcome, so the server decides it and says so instead of
                // believing a client about its own flag.
                case GameMessageTypes.FlagBurst:
                    Console.WriteLine($"[Match] Ignored client-supplied flag burst from '{playerId}'; a flag going is the server's call");
                    break;

                case GameMessageTypes.FlagScoreUpdate:
                    HandleFlagScoreUpdate(room);
                    break;

                case "PlayerEliminated":
                    HandlePlayerEliminated(room, playerId);
                    break;

                case "PlayerPositionUpdate":
                    var position = json.GetValue("Position") as JObject;
                    if (position != null)
                    {
                        HandlePositionUpdate(room, playerId, position);
                    }
                    break;

                case GameMessageTypes.PlayerShot:
                    HandlePlayerShot(room, playerId, json);
                    break;

                case GameMessageTypes.GrenadeThrow:
                    HandleGrenadeThrow(room, playerId, json);
                    break;
                case GameMessageTypes.FieldItemPickup:
                // The client sends ItemPickup on the realtime channel and
                // FieldItemPickup on the reliable one. Accept both so either route
                // reaches the same authoritative check.
                case "ItemPickup":
                    HandleFieldItemPickup(room, playerId, json);
                    break;

                case "WeaponDrop":
                    HandleWeaponDrop(room, playerId, json);
                    break;

                // Reserve, release, and the client's own view of having taken it.
                // All three are the same fact about who is reaching for a weapon,
                // so they share one handler.
                case "WeaponReserve":
                case "WeaponRelease":
                case "WeaponPickup":
                    HandleWeaponReservation(room, playerId, json);
                    break;

                // The client sends ItemUse on one path and ItemUseRequest on the
                // other. Both arrive here, and both are treated the same, because
                // the client is the only one that distinguishes them and the
                // server has no reason to.
                case "ItemUse":
                case "ItemUseRequest":
                    HandleItemUse(room, playerId, json);
                    break;

                default:
                    Console.WriteLine($"Unknown realtime game event type: {eventType}");
                    break;
            }
        }

        /// <summary>
        /// Grants a field item to a player, or refuses.
        /// <para>
        /// The client sends the item id and the server used to have no route for
        /// this message at all, so the item never became claimed and the client
        /// only saw its own optimistic state. Nothing about the pickup was
        /// authoritative: the client decided it had touched an item and applied
        /// the effect locally.
        /// </para>
        /// <para>
        /// The claim is checked against the position the server already tracks
        /// for the player, so an id cannot be redeemed from across the map. The
        /// player id in the message is ignored in favour of the connection's
        /// player, so one player cannot claim on behalf of another.
        /// </para>
        /// </summary>
        /// <summary>
        /// The pickup budget, shared by the reliable and the realtime route so a
        /// client cannot get twice the allowance by alternating between them.
        /// </summary>
        private static readonly FieldItemPickupRateLimiter ItemPickupBudget = new();


        private static void HandleFieldItemPickup(MatchRoom room, string playerId, JObject json)
        {
            // Both routes draw on one budget, so alternating between them does
            // not buy a second allowance.
            if (!ItemPickupBudget.TryConsume(playerId))
            {
                return;
            }

            var itemId = ReadString(json, "ItemId", "itemId", "ItemID") ?? string.Empty;
            if (string.IsNullOrWhiteSpace(itemId))
            {
                Console.WriteLine($"[Match] Ignored field item pickup with no item id from '{playerId}'");
                return;
            }

            // A client supplied player id is not evidence of who is asking.
            var claimed = ReadString(json, "PlayerID", "PlayerId");
            if (!string.IsNullOrWhiteSpace(claimed) &&
                !string.Equals(claimed, playerId, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Match] Ignored field item pickup for '{claimed}' sent by '{playerId}'");
                return;
            }

            var itemManager = MatchRoomManager.Instance.GetFieldItemManager(room.Id);
            if (itemManager == null)
            {
                Console.WriteLine($"[Match] Field item pickup for '{playerId}' with no item manager in room '{room.Id}'");
                return;
            }

            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (string.IsNullOrEmpty(state.PlayerId) || !state.HasAuthoritativePosition)
            {
                // A registered player who has never reported a position still reads as
                // the origin, so checking the id alone would treat "position unknown"
                // as "standing at 0,0,0" and hand out an item that is spawning there.
                Console.WriteLine($"[Match] Refused field item pickup for '{playerId}': no authoritative position");
                return;
            }

            var granted = itemManager.PickupItem(itemId, playerId, state.PositionX, state.PositionY, state.PositionZ);
            if (!granted)
            {
                Console.WriteLine($"[Match] Refused field item pickup of '{itemId}' by '{playerId}'");

                // A refusal is told as well as a grant. The client applies the
                // pickup before the server rules on it, so a claim answered only
                // by silence leaves the effect in place until the client's own
                // timeout takes it back. The player should not have to wait out a
                // timer to find out the server said no.
                GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
                {
                    ["MessageType"] = GameMessageTypes.FieldItemPickup,
                    ["RoomID"] = room.Id.ToString(),
                    ["ItemId"] = itemId,
                    ["PlayerID"] = playerId,
                    ["Success"] = false,
                    ["Reason"] = "OutOfReachOrTaken",
                    ["Timestamp"] = DateTime.UtcNow.ToString("o")
                });
                return;
            }

            if (!itemManager.TryGetItem(itemId, out var item))
            {
                return;
            }

            var type = item!.ItemType;
            var duration = FieldItemTypeNames.IsTimedBuff(type)
                ? FieldItemDefaults.DurationSeconds
                : 0f;

            // The duration is decided here rather than taken from the client.
            // A timed item used to carry its own thirty second lifetime that
            // only the client knew about, so the server could neither shorten
            // it nor answer a question about it.
            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.FieldItemPickup,
                ["RoomID"] = room.Id.ToString(),
                ["ItemId"] = itemId,
                ["PlayerID"] = playerId,
                ["ItemType"] = FieldItemTypeNames.ToWireName(type),
                ["Duration"] = duration,
                ["IsTimedBuff"] = FieldItemTypeNames.IsTimedBuff(type),
                ["ExpiresAtSeconds"] = duration > 0f
                    ? NowSeconds() + duration
                    : 0.0d,
                ["Success"] = true,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }
        /// <summary>
        /// Seconds on a clock the client cannot influence, used for effect
        /// expiry stamps in the messages sent to clients.
        /// </summary>
        private static double NowSeconds() => (double)DateTime.UtcNow.TimeOfDay.TotalSeconds;

        /// <summary>
        /// Puts a weapon the player let go of on the ground.
        /// <para>
        /// The weapon is placed at the position the server is already holding for
        /// the player rather than one from the message, and the type and the
        /// magazine come from the message because those are facts about the weapon
        /// the player is holding, which only they can know. A player the server
        /// has never seen a position for cannot drop, since there would be nowhere
        /// to put it.
        /// </para>
        /// </summary>
        private static void HandleWeaponDrop(MatchRoom room, string playerId, JObject json)
        {
            var weaponType = ReadString(json, "WeaponType", "WeaponID", "WeaponId");
            if (string.IsNullOrWhiteSpace(weaponType))
            {
                Console.WriteLine($"[Match] Ignored weapon drop with no weapon type from '{playerId}'");
                return;
            }

            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (string.IsNullOrEmpty(state.PlayerId) || !state.HasAuthoritativePosition)
            {
                Console.WriteLine($"[Match] Refused weapon drop for '{playerId}': no authoritative position");
                return;
            }

            var itemManager = MatchRoomManager.Instance.GetFieldItemManager(room.Id);
            if (itemManager == null)
            {
                Console.WriteLine($"[Match] Weapon drop for '{playerId}' with no item manager in room '{room.Id}'");
                return;
            }

            var magazine = ReadInt(json, -1, "MagazineAmmo", "Ammo", "CurrentAmmo");
            var slot = ReadInt(json, -1, "SlotIndex", "WeaponSlot");
            var itemId = itemManager.DropWeapon(
                weaponType,
                state.PositionX,
                state.PositionY,
                state.PositionZ,
                magazine,
                slot);

            if (string.IsNullOrEmpty(itemId))
            {
                Console.WriteLine($"[Match] Refused weapon drop of '{weaponType}' by '{playerId}'");
                return;
            }

            Console.WriteLine($"[Match] {playerId} dropped {weaponType} as '{itemId}' in room {room.Id}");

            // Everyone is told, because a weapon lying on the ground that only the
            // dropper can see is not a weapon anybody can pick up.
            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.WeaponDropped,
                ["RoomID"] = room.Id.ToString(),
                ["ItemId"] = itemId,
                ["ItemType"] = FieldItemTypeNames.ToWireName(EFieldItemType.WeaponItem),
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["WeaponType"] = weaponType,
                ["MagazineAmmo"] = magazine,
                ["SlotIndex"] = slot,
                ["PosX"] = state.PositionX,
                ["PosY"] = state.PositionY,
                ["PosZ"] = state.PositionZ,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Spends an instant item the player is carrying.
        /// <para>
        /// The message names the item and nothing else. It used to name the effect
        /// too, as a free form string, and the client applied it to itself, so how
        /// much a kit healed was a claim by whoever sent the message. The effect
        /// and its strength are now looked up from the item type, and no amount in
        /// the message is read at all.
        /// </para>
        /// <para>
        /// The item has to be one the player is actually carrying, and it is
        /// spent whether or not it did anything: a full health player who uses a
        /// kit has used it.
        /// </para>
        /// </summary>
        private static void HandleItemUse(MatchRoom room, string playerId, JObject json)
        {
            // A player id in the message is a claim about who is spending it, and
            // the connection's own player is the one that counts. Both spellings
            // are checked: the message carries PlayerId and PlayerID for the same
            // value, and honouring one while ignoring the other would let a
            // message name somebody else in the field that is not consulted.
            foreach (var claimed in new[] { json["PlayerID"], json["PlayerId"] })
            {
                var claimedText = claimed?.ToString();
                if (string.IsNullOrWhiteSpace(claimedText) ||
                    string.Equals(claimedText, playerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Console.WriteLine($"[Match] Ignored item use for '{claimedText}' sent by '{playerId}'");
                return;
            }

            if (!TryParseInstantItem(ReadString(json, "ItemType", "ItemId"), out var itemType))
            {
                Console.WriteLine($"[Match] Ignored item use with an unknown item from '{playerId}'");
                return;
            }

            if (!room.TryGetPlayer(playerId, out var player) || player == null)
            {
                return;
            }

            // Carrying it is the difference between using an item and asking for
            // one. Without this a client could spend an item it never picked up.
            if (!player.EquipInstantItems.Contains(itemType))
            {
                Console.WriteLine($"[Match] Refused item use of {itemType} by '{playerId}': not carried");
                RefuseItemUse(room, playerId, itemType, "NotCarried");
                return;
            }

            var effect = InstantItemRules.EffectOf(itemType);
            if (effect == null)
            {
                Console.WriteLine($"[Match] Ignored item use of {itemType} by '{playerId}': it does nothing");
                RefuseItemUse(room, playerId, itemType, "NoEffect");
                return;
            }

            var healthBefore = player.Health;
            var restored = 0;
            if (effect.Value == EInstantItemEffect.Heal)
            {
                restored = HealPlayer(player);
            }

            // What a shot carries is a property of the weapon, and the server does
            // not track a magazine yet, so an ammo item is spent and reported here
            // rather than pretended for.
            player.EquipInstantItems.Remove(itemType);

            Console.WriteLine(
                $"[Match] {playerId} used {itemType} ({effect.Value}), " +
                $"health {healthBefore} -> {player.Health}");

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.ItemUsed,
                ["RoomID"] = room.Id.ToString(),
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["ItemType"] = itemType.ToString(),
                ["Effect"] = effect.Value.ToString(),
                ["RestoredHealth"] = restored,
                ["Health"] = player.Health,
                ["MaxHealth"] = player.MaxHealth,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Answers a claim to spend an instant item that the server turned down.
        /// <para>
        /// A client spends the item on its own screen the moment the key is
        /// pressed, so a refusal that is only written to the log leaves the
        /// client believing it healed when nothing happened. The answer carries
        /// the health the server actually holds, which is what the client should
        /// have shown all along.
        /// </para>
        /// </summary>
        private static void RefuseItemUse(MatchRoom room, string playerId, EInstantItemType itemType, string reason)
        {
            if (!room.TryGetPlayer(playerId, out var player) || player == null)
            {
                return;
            }

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.ItemUseRefused,
                ["RoomID"] = room.Id.ToString(),
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["ItemType"] = itemType.ToString(),
                ["Reason"] = reason,
                ["RestoredHealth"] = 0,
                ["Health"] = player.Health,
                ["MaxHealth"] = player.MaxHealth,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Restores health up to the maximum, and reports how much went in.
        /// </summary>
        private static int HealPlayer(PlayerInfo player)
        {
            if (player.Health >= player.MaxHealth)
            {
                // Already full. The item is still spent by the caller; there is
                // simply nothing to restore, and saying so is what stops a client
                // topping off early and banking the difference.
                return 0;
            }

            var missing = player.MaxHealth - player.Health;
            var restored = Math.Min(missing, InstantItemRules.HealAmount);
            player.Health += restored;
            return restored;
        }

        /// <summary>
        /// Parses an item name into a typed instant item.
        /// <para>
        /// Both spellings are accepted because the client and the shared enum do
        /// not agree on them: the client writes BandAid where the enum calls it
        /// HealthKit, and PowerGrenade where the enum calls it PowerGrenadePack,
        /// among others. Rejecting a name the client actually sends would refuse
        /// every use, so the alias is part of the wire contract rather than a
        /// concession to it.
        /// </para>
        /// </summary>
        private static bool TryParseInstantItem(string? name, out EInstantItemType type)
        {
            type = EInstantItemType.None;
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            switch (name.Trim().ToLowerInvariant())
            {
                case "bandaid":
                case "healthkit":
                case "heal":
                    type = EInstantItemType.HealthKit;
                    return true;

                case "firebullet":
                case "fire_bullet":
                    type = EInstantItemType.FireBullet;
                    return true;

                case "poisonbullet":
                case "poison_bullet":
                    type = EInstantItemType.PoisonBullet;
                    return true;

                case "powergrenade":
                case "powergrenadepack":
                case "power_grenade_pack":
                    type = EInstantItemType.PowerGrenadePack;
                    return true;

                case "clustergrenade":
                case "clustergrenadepack":
                case "cluster_grenade_pack":
                    type = EInstantItemType.ClusterGrenadePack;
                    return true;

                case "magneticgrenade":
                case "magnetgrenadepack":
                case "magnet_grenade_pack":
                    type = EInstantItemType.MagnetGrenadePack;
                    return true;

                case "landminegrenade":
                case "minegrenadepack":
                case "mine_grenade_pack":
                    type = EInstantItemType.MineGrenadePack;
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Records, or drops, the claim a player has on a weapon lying about.
        /// <para>
        /// The client names the weapon by its type and where it says the weapon
        /// is, because that is how it identifies one locally. The position in the
        /// message is a claim about where the weapon is, so it is not used: the
        /// item is found by its type among the weapons the server knows about, and
        /// the nearest one wins, which is what the client does for itself.
        /// </para>
        /// </summary>
        private static void HandleWeaponReservation(MatchRoom room, string playerId, JObject json)
        {
            var messageType = json["MessageType"]?.ToString() ?? "WeaponReserve";
            var reserved = !string.Equals(messageType, "WeaponRelease", StringComparison.OrdinalIgnoreCase);

            var weaponType = ReadString(json, "WeaponType", "WeaponID", "WeaponId");
            if (string.IsNullOrWhiteSpace(weaponType))
            {
                Console.WriteLine($"[Match] Ignored weapon reservation with no weapon type from '{playerId}'");
                return;
            }

            var itemManager = MatchRoomManager.Instance.GetFieldItemManager(room.Id);
            if (itemManager == null)
            {
                Console.WriteLine($"[Match] Weapon reservation for '{playerId}' with no item manager in room '{room.Id}'");
                return;
            }

            var itemId = FindWeaponItem(itemManager, weaponType, playerId);
            if (string.IsNullOrEmpty(itemId))
            {
                Console.WriteLine($"[Match] No weapon of type '{weaponType}' for '{playerId}' to claim in room {room.Id}");
                return;
            }

            if (!itemManager.TrySetReservation(itemId, playerId, reserved))
            {
                Console.WriteLine($"[Match] Refused weapon {(reserved ? "reservation" : "release")} of '{itemId}' by '{playerId}'");
                return;
            }

            Console.WriteLine(
                $"[Match] {playerId} {(reserved ? "claimed" : "released")} weapon '{itemId}' in room {room.Id}");

            if (!itemManager.TryGetItem(itemId, out var item) || item == null)
            {
                return;
            }

            // Everyone is told, because a claim only works if the other players
            // are the ones honouring it. Where the weapon is comes from the item
            // the server holds rather than from the claim, for the same reason
            // the position in the claim was not used: a client finds the weapon
            // it is being told about by type and position, so a ruling without
            // one names a weapon nobody can locate.
            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = reserved
                    ? GameMessageTypes.WeaponReserved
                    : GameMessageTypes.WeaponReleased,
                ["RoomID"] = room.Id.ToString(),
                ["ItemId"] = itemId,
                ["WeaponId"] = itemId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["ReservedByPlayerId"] = playerId,
                ["WeaponType"] = weaponType,
                ["PosX"] = item.PosX,
                ["PosY"] = item.PosY,
                ["PosZ"] = item.PosZ,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Finds the weapon of a type that the claiming player is standing next to.
        /// <para>
        /// The claim only means something next to the weapon, so the search is
        /// limited to what the player could actually be reaching for. Without
        /// that, a player could claim a weapon on the far side of the map and lock
        /// it for everybody standing there.
        /// </para>
        /// </summary>
        private static string FindWeaponItem(
            OpenGSServer.Network.ServerFieldItemManager itemManager,
            string weaponType,
            string playerId)
        {
            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (string.IsNullOrEmpty(state.PlayerId) || !state.HasAuthoritativePosition)
            {
                return string.Empty;
            }

            // How far a player can be from a weapon and still be reaching for it.
            // The client matches a weapon to itself within half a unit, so this is
            // the same neighbourhood widened for the lag.
            const float reach = 2.5f;

            string? bestId = null;
            var bestDistance = float.MaxValue;

            foreach (var item in itemManager.ToJson())
            {
                if (item["ItemType"]?.ToString() != nameof(EFieldItemType.WeaponItem) ||
                    item["State"]?.ToString() != "Spawned")
                {
                    continue;
                }

                if (!string.Equals(item["WeaponType"]?.ToString(), weaponType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var dx = (item["PositionX"]?.Value<float>() ?? 0f) - state.PositionX;
                var dy = (item["PositionY"]?.Value<float>() ?? 0f) - state.PositionY;
                var distance = MathF.Sqrt((dx * dx) + (dy * dy));
                if (distance > reach || distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestId = item["ItemId"]?.ToString();
            }

            return bestId ?? string.Empty;
        }

        private static void HandlePlayerKilled(MatchRoom room, string killerId, string killedPlayerId)
        {
            Console.WriteLine($"Player {killedPlayerId} was killed by {killerId}");
            GameMessageDispatcher.SendPlayerKilled(room.Id.ToString(), killerId, killedPlayerId);
        }

        private static void HandlePlayerDamaged(
            MatchRoom room,
            string damagedPlayerId,
            int damage,
            string attackerId = "",
            JObject? hitPosition = null,
            int? remainingHealth = null)
        {
            Console.WriteLine($"Player {damagedPlayerId} took {damage} damage");

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                // The same name the projectile path uses. A client that adopts the
                // health on one of them and not the other would show two different
                // numbers for the same hit depending on which weapon landed it.
                ["MessageType"] = GameMessageTypes.PlayerDamaged,
                ["RoomID"] = room.Id.ToString(),
                ["DamagedPlayerID"] = damagedPlayerId,
                ["TargetId"] = damagedPlayerId,
                ["AttackerID"] = attackerId,
                ["AttackerId"] = attackerId,
                ["Damage"] = damage,
                ["RemainingHealth"] = remainingHealth,
                // The client adopts this instead of its own prediction, so it
                // needs the ceiling as well as the current value.
                ["MaxHealth"] = room.TryGetPlayer(damagedPlayerId, out var damaged) ? damaged!.MaxHealth : 0,
                ["IsDown"] = remainingHealth.HasValue && remainingHealth.Value <= 0,
                ["HitPosition"] = hitPosition,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }

        /// <summary>
        /// Reads which team's flag a claim is about.
        /// <para>
        /// A flag belongs to a team, and the team's flag is what the claim is
        /// about, so the message has to name that team. A client that only sends
        /// the carrier's own team is saying which side it is on, which is not the
        /// same fact: a red player picks up the blue flag. Without the flag's own
        /// team the server has to guess, and guessing which flag somebody is
        /// holding is how a team ends up carrying a flag it does not own.
        /// </para>
        /// </summary>
        private static ETeam ResolveFlagTeam(JObject json, string playerId)
        {
            var named = ReadString(json, "FlagTeam", "FlagOwnerTeam", "FlagOwner");
            if (!string.IsNullOrWhiteSpace(named) && Enum.TryParse(named, ignoreCase: true, out ETeam namedTeam))
            {
                if (namedTeam == ETeam.Red || namedTeam == ETeam.Blue)
                {
                    return namedTeam;
                }
            }

            // Older clients did not name the flag, and the only team in the
            // message was the carrier's. A pickup is then the other side's flag,
            // which is the only flag a player may pick up.
            var carrierTeam = ResolvePlayerTeam(MatchRoomManager.Instance.SearchRoomByMemberID(playerId), playerId);
            return carrierTeam == ETeam.NoTeam ? ETeam.NoTeam : CaptureTheFlagRules.OpposingTeam(carrierTeam);
        }

        private static void HandleFlagCaptured(MatchRoom room, string playerId, JObject json)
        {
            var scoringTeam = ResolvePlayerTeam(room, playerId);
            if (scoringTeam == ETeam.NoTeam)
            {
                return;
            }

            var flagState = FlagStateFor(room);

            // The claim is judged against the flags the server holds, not against
            // anything the message says. The own flag has to be home, which is
            // the half of the rule that was missing, and the enemy flag has to be
            // in the claimant's hands rather than merely somewhere in the room.
            var refusal = flagState.WithFlags(flags => CaptureTheFlagRules.RefusalFor(scoringTeam, flags));
            if (refusal != EFlagRefusal.None)
            {
                Console.WriteLine(
                    $"[Match] Refused a capture by {scoringTeam} in room {room.Id}: {refusal}");
                GameMessageDispatcher.SendFlagCaptureRefused(
                    playerId,
                    room.Id.ToString(),
                    scoringTeam.ToString(),
                    refusal.ToString());
                return;
            }

            var enemyFlag = flagState.WithFlags(flags => CaptureTheFlagRules.OpposingFlag(scoringTeam, flags));
            if (enemyFlag == null || !enemyFlag.IsCarried ||
                !string.Equals(enemyFlag.CarrierId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                // The flag exists and is carried, but not by this player. A
                // delivery is a statement about who is holding it, and a message
                // from somebody who is not holding it is not that statement.
                Console.WriteLine(
                    $"[Match] Ignored a capture claim from '{playerId}', who is not carrying the flag");
                return;
            }

            flagState.WithFlags(flags => flags[CaptureTheFlagRules.OpposingTeam(scoringTeam)].Return(
                EFlagReturnReason.CapturedAtBase));

            Console.WriteLine($"Team {scoringTeam} captured the flag");
            room.AddFlagCapture(scoringTeam, playerId);

            GameMessageDispatcher.SendFlagCaptured(room.Id.ToString(), scoringTeam.ToString());

            // The flag that was carried is the one that is now gone, and it
            // belonged to the other team. A client cannot say which flag it
            // destroyed: a flag going is the rule's outcome, so the server says
            // whose flag it was and everyone is told.
            GameMessageDispatcher.SendFlagBurst(
                room.Id.ToString(),
                CaptureTheFlagRules.OpposingTeam(scoringTeam).ToString());

            // The rule resets the flag state when a team scores, so both flags go
            // back to their stands. Without this the captured flag is still gone
            // and the next delivery has nothing to carry.
            FlagStateReset(room);

            GameMessageDispatcher.SendFlagScoreUpdate(
                room.Id.ToString(),
                room.GetFlagScore(ETeam.Red),
                room.GetFlagScore(ETeam.Blue));
        }

        /// <summary>
        /// Puts both flags back on their stands and tells the room, because a
        /// flag that is home again is something every player can see.
        /// </summary>
        private static void FlagStateReset(MatchRoom room)
        {
            var flagState = FlagStateFor(room);
            flagState.WithFlags(flags =>
            {
                CaptureTheFlagRules.ResetAll(flags);
                return true;
            });

            foreach (var team in new[] { ETeam.Red, ETeam.Blue })
            {
                GameMessageDispatcher.SendFlagReturned(
                    room.Id.ToString(),
                    team.ToString(),
                    string.Empty,
                    EFlagReturnReason.AutoReturn.ToString());
            }
        }

        /// <summary>
        /// Puts a flag a player was carrying onto the ground, because that player
        /// is out of the match.
        /// <para>
        /// The rule says a carrier who dies drops the flag, so the server does it
        /// from the death it has already ruled on rather than waiting to be told.
        /// A client can still report it, and that report is refused because the
        /// player is no longer the carrier, which keeps one drop from becoming
        /// two.
        /// </para>
        /// </summary>
        private static void DropFlagCarriedBy(MatchRoom room, string playerId)
        {
            var now = ServerNowSeconds();
            var dropped = FlagStateFor(room).WithFlags(flags =>
            {
                foreach (var flag in flags.Values)
                {
                    if (flag.IsCarried &&
                        string.Equals(flag.CarrierId, playerId, StringComparison.OrdinalIgnoreCase) &&
                        flag.Drop(now))
                    {
                        return flag.Team;
                    }
                }

                return ETeam.NoTeam;
            });

            if (dropped == ETeam.NoTeam)
            {
                return;
            }

            // A carrier who went down is where the flag lands, so the position
            // comes from the place the server already holds for them rather than
            // from anything the client said.
            if (TryGetAuthoritativePosition(playerId, out var x, out var y, out var z))
            {
                NoteFlagPosition(room, dropped, x, y, z);
            }

            Console.WriteLine($"[Match] {playerId} went down carrying the {dropped} flag");
            GameMessageDispatcher.SendFlagLost(room.Id.ToString(), dropped.ToString(), playerId);
        }

        /// <summary>
        /// Whether a claim about a flag came from the player it names as the one
        /// holding it.
        /// <para>
        /// Every client simulates every player, so a flag standing in the world
        /// fires its trigger on each client in the room, not only on the one whose
        /// player touched it. That means several clients report the same pickup
        /// and each names whoever their own copy says is carrying it. Without
        /// this check the first report to arrive wins and the flag is recorded
        /// against a player who never went near it, which then makes that player
        /// the carrier the server believes: their own death drops nothing, and a
        /// team can be denied a capture for the rest of the match by a claim made
        /// on its behalf.
        /// </para>
        /// </summary>
        private static bool IsClaimFromTheCarrier(MatchRoom room, string playerId, JObject json)
        {
            var named = ReadString(json, "CarrierId", "CarrierID", "PickedUpByPlayerId");
            if (string.IsNullOrWhiteSpace(named))
            {
                // Nothing named a carrier. The player speaking is the only
                // evidence there is, and that is the claim this is about.
                return true;
            }

            if (string.Equals(named, playerId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Console.WriteLine(
                $"[Match] Ignored a flag claim from '{playerId}' naming carrier '{named}': a client reports on behalf of its own player");
            return false;
        }

        private static void HandleFlagLost(MatchRoom room, string playerId, JObject json)
        {
            if (!IsClaimFromTheCarrier(room, playerId, json))
            {
                return;
            }

            var flagTeam = ResolveFlagTeam(json, playerId);
            if (flagTeam == ETeam.NoTeam)
            {
                return;
            }

            var flagState = FlagStateFor(room);

            // Only the flag this player is actually holding can be dropped. A
            // claim that a flag was lost is a claim about a carrier, so a player
            // holding nothing has nothing to drop. The server drops it itself when
            // it rules the carrier down, so a report that arrives after that is
            // refused here rather than counting the drop twice.
            var now = ServerNowSeconds();
            var reported = ReadReportedPosition(json);
            var dropped = flagState.WithFlags(flags =>
                flags.TryGetValue(flagTeam, out var flag) && flag.IsCarried &&
                string.Equals(flag.CarrierId, playerId, StringComparison.OrdinalIgnoreCase) &&
                flag.Drop(now));

            if (!dropped)
            {
                Console.WriteLine(
                    $"[Match] Ignored a lost claim from '{playerId}', who is not carrying the {flagTeam} flag");
                return;
            }

            // Where a flag lands is where it lies. A client that says where is
            // believed, because it is the client whose player dropped it, and one
            // that does not is answered from the place the server already holds
            // for the carrier. Without a position the server would know the flag
            // was loose and not where, so a later pickup has nothing to be
            // measured against and is taken on trust.
            if (reported != null)
            {
                NoteFlagPosition(room, flagTeam, reported.Value.X, reported.Value.Y, 0f);
            }
            else if (TryGetAuthoritativePosition(playerId, out var dx, out var dy, out var dz))
            {
                NoteFlagPosition(room, flagTeam, dx, dy, dz);
            }

            Console.WriteLine($"The {flagTeam} flag was dropped by {playerId}");
            GameMessageDispatcher.SendFlagLost(room.Id.ToString(), flagTeam.ToString(), playerId);
        }

        private static void HandleFlagPickup(MatchRoom room, string playerId, JObject json)
        {
            if (!IsClaimFromTheCarrier(room, playerId, json))
            {
                return;
            }

            var flagTeam = ResolveFlagTeam(json, playerId);
            if (flagTeam == ETeam.NoTeam)
            {
                return;
            }

            var carrierTeam = ResolvePlayerTeam(room, playerId);

            // A player takes the other side's flag. A claim naming its own team's
            // flag is not a pickup: a team recovering its own flag is a return,
            // which is a different rule and a different event.
            if (CaptureTheFlagRules.OpposingTeam(carrierTeam) != flagTeam)
            {
                Console.WriteLine(
                    $"[Match] Refused a pickup by '{playerId}': a {carrierTeam} player cannot pick up the {flagTeam} flag");
                return;
            }

            // A flag sitting on its own stand is the one thing a player must be
            // standing next to in order to take. The other two claims are checked
            // against state the server already holds, and this one was the only
            // flag message that was not, so a client could assert that it had
            // picked up a flag it had never walked to.
            if (!IsStandingAtAFlag(room, playerId, flagTeam))
            {
                return;
            }

            var flagState = FlagStateFor(room);
            var picked = flagState.WithFlags(flags =>
                flags.TryGetValue(flagTeam, out var flag) && flag.PickUp(playerId));

            if (!picked)
            {
                // A flag that is already carried is one object, so the second
                // claim names a flag that is not on the ground.
                Console.WriteLine(
                    $"[Match] Refused a pickup of the {flagTeam} flag by '{playerId}': it is not available");
                return;
            }

            Console.WriteLine($"{playerId} picked up the {flagTeam} flag");
            GameMessageDispatcher.SendFlagPickup(room.Id.ToString(), carrierTeam.ToString(), playerId);
        }

        /// <summary>
        /// Where each team's flag is when it is on the ground or being carried.
        /// <para>
        /// A flag on its own stand is not placed here: the stand is a thing in the
        /// scene, and a room with no flag stand registered has no known position
        /// for a flag to be on. That is a real state for a headless room, so a
        /// claim in one is judged on the carrier check alone rather than refused
        /// for want of a position nobody has.
        /// </para>
        /// </summary>
        private static readonly ConcurrentDictionary<string, System.Numerics.Vector3> FlagPositions =
            new();

        private static string FlagPositionKey(MatchRoom room, ETeam team) => $"{room.Id}:{team}";

        /// <summary>
        /// Where a message says a thing is, or null when it says nothing usable.
        /// <para>
        /// A position in a message is a claim about where something is, so both
        /// coordinates have to be present and finite. A missing one would place a
        /// flag at the origin, and the origin is where nothing is.
        /// </para>
        /// </summary>
        private static (float X, float Y)? ReadReportedPosition(JObject json)
        {
            if (json == null)
            {
                return null;
            }

            if (!TryReadFiniteFloat(json, new[] { "PosX", "PositionX", "X" }, out var x) ||
                !TryReadFiniteFloat(json, new[] { "PosY", "PositionY", "Y" }, out var y))
            {
                return null;
            }

            return (x, y);
        }

        private static bool TryReadFiniteFloat(JObject json, string[] keys, out float value)
        {
            value = 0f;
            if (json == null || keys == null)
            {
                return false;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var token = json.GetValue(key);
                if (token == null)
                {
                    continue;
                }

                if (!float.TryParse(
                        token.ToString(),
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var parsed) ||
                    !float.IsFinite(parsed))
                {
                    continue;
                }

                value = parsed;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Records where a team's flag is, from a report that a flag is on the
        /// ground or in somebody's hands.
        /// <para>
        /// A dropped flag is a position like any other, and this is where it comes
        /// from. A client saying where its own copy of a flag is cannot create one,
        /// so a flag whose position nobody has reported is judged on the carrier
        /// check alone.
        /// </para>
        /// </summary>
        public static void NoteFlagPosition(MatchRoom room, ETeam team, float x, float y, float z)
        {
            if (room == null || team == ETeam.NoTeam)
            {
                return;
            }

            FlagPositions[FlagPositionKey(room, team)] = new System.Numerics.Vector3(x, y, z);
        }

        private static void ForgetFlagPosition(MatchRoom room, ETeam team)
        {
            FlagPositions.TryRemove(FlagPositionKey(room, team), out _);
        }

        /// <summary>
        /// Whether a player is close enough to a flag to be reaching for it.
        /// <para>
        /// A flag on its own stand is not checked here, because a stand is scenery
        /// and its position is not something the server was told. A flag on the
        /// ground or in somebody's hands has a position the server holds, and that
        /// is the one a claim is measured against.
        /// </para>
        /// </summary>
        private static bool IsStandingAtAFlag(MatchRoom room, string playerId, ETeam flagTeam)
        {
            if (!TryGetAuthoritativePosition(playerId, out var px, out var py, out var pz))
            {
                Console.WriteLine(
                    $"[Match] Refused a {flagTeam} flag pickup by '{playerId}': no authoritative position");
                return false;
            }

            var flagState = FlagStateFor(room);
            var isOnItsStand = flagState.WithFlags(flags =>
                !flags.TryGetValue(flagTeam, out var flag) || flag.IsAtBase);

            if (isOnItsStand)
            {
                return true;
            }

            if (!FlagPositions.TryGetValue(FlagPositionKey(room, flagTeam), out var position))
            {
                // A flag whose position nobody has reported cannot be measured
                // against, so the carrier check stands on its own rather than a
                // claim being refused for the lack of a number.
                return true;
            }

            var dx = position.X - px;
            var dy = position.Y - py;
            if (MathF.Sqrt((dx * dx) + (dy * dy)) > FlagPickupReach)
            {
                Console.WriteLine(
                    $"[Match] Refused a {flagTeam} flag pickup by '{playerId}': they are not next to it");
                return false;
            }

            return true;
        }

        /// <summary>
        /// How close a player has to be to a flag to be able to pick it up.
        /// <para>
        /// A pickup claim is the one flag message with nothing to check it against:
        /// a field item is refused unless the server's own position for the player
        /// is near the spawn, and a shot is re-derived rather than taken from the
        /// message. Without the same here, a client can assert that it picked up a
        /// flag sitting safely on the other team's stand, and that team can then
        /// never score.
        /// </para>
        /// </summary>
        public const float FlagPickupReach = 3.0f;

        /// <summary>
        /// Whether the server holds a position for a player, and where.
        /// <para>
        /// A player who has never reported a position still reads as the origin,
        /// so checking an id alone would treat "position unknown" as "standing at
        /// 0,0,0" and hand out a flag that is spawning there.
        /// </para>
        /// </summary>
        private static bool TryGetAuthoritativePosition(string playerId, out float x, out float y, out float z)
        {
            var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (string.IsNullOrEmpty(state.PlayerId) || !state.HasAuthoritativePosition)
            {
                x = y = z = 0f;
                return false;
            }

            x = state.PositionX;
            y = state.PositionY;
            z = state.PositionZ;
            return true;
        }

        private static void HandleFlagReturn(MatchRoom room, string playerId, JObject json)
        {
            // A flag that came back by itself is not a player's claim at all, so
            // there is no carrier to check. A friendly recovery names one, and it
            // has to be the player speaking: every client simulates every player,
            // so a claim naming somebody else is one of them reporting for a peer.
            var reason = ParseFlagReturnReason(ReadString(json, "ReturnReason"));
            if (reason != EFlagReturnReason.AutoReturn && !IsClaimFromTheCarrier(room, playerId, json))
            {
                return;
            }

            var flagTeam = ResolveFlagTeam(json, playerId);
            if (flagTeam == ETeam.NoTeam)
            {
                return;
            }

            var flagState = FlagStateFor(room);
            var returned = flagState.WithFlags(flags =>
            {
                if (!flags.TryGetValue(flagTeam, out var flag))
                {
                    return false;
                }

                // A team recovering its own flag is a return. A player of the
                // other side returning it is not: that flag is theirs to carry
                // and theirs to deliver, and a return would hand it back home for
                // free.
                var carrierTeam = ResolvePlayerTeam(room, playerId);
                if (CaptureTheFlagRules.OpposingTeam(carrierTeam) == flagTeam)
                {
                    return false;
                }

                return flag.Return(reason);
            });

            if (!returned)
            {
                // A flag that is already home is not put back, a flag in no state
                // this player can return is not put back, and the other side's flag
                // is not the returning player's to put back.
                Console.WriteLine(
                    $"[Match] Ignored a return of the {flagTeam} flag by '{playerId}': it is not off its stand");
                return;
            }

            Console.WriteLine($"The {flagTeam} flag was returned by {playerId} ({reason})");

            // A return restores state and is not a score, which is what the mode
            // rule says. The score is not touched here on purpose.
            GameMessageDispatcher.SendFlagReturned(
                room.Id.ToString(),
                flagTeam.ToString(),
                playerId,
                reason.ToString());
        }

        private static EFlagReturnReason ParseFlagReturnReason(string? named)
        {
            if (!string.IsNullOrWhiteSpace(named) && Enum.TryParse(named, ignoreCase: true, out EFlagReturnReason reason))
            {
                return reason;
            }

            // A client that does not say why is recovering a flag its own team
            // owns, which is the one return a player can actually perform.
            return EFlagReturnReason.FriendlyRecovered;
        }

        private static ETeam ResolvePlayerTeam(MatchRoom room, string playerId)
        {
            return room?.Players.FirstOrDefault(player =>
                string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase))?.Team ?? ETeam.NoTeam;
        }

        /// <summary>
        /// No per event cooldown is applied to a flag claim.
        /// <para>
        /// There used to be one, and it blocked legitimate play: a carrier who
        /// dropped a flag and picked it straight back up was refused, because the
        /// second pickup was the same message type inside half a second. The
        /// state machine now decides every one of these claims on its own terms,
        /// and a claim that changes nothing returns before anything is broadcast,
        /// so a repeating message costs one dictionary lookup. The transport
        /// already rate limits a player to a fixed number of match events a
        /// second, which is the bound that actually matters.
        /// </para>
        /// </summary>
        private static void RememberFlagEvent(MatchRoom room, string playerId, string eventType)
        {
            LastFlagEvents[$"{room.Id}:{playerId}:{eventType}"] = DateTime.UtcNow;
        }

        private static void ClearFlagStateForRoom(MatchRoom room)
        {
            FlagRooms.TryRemove(room.Id.ToString(), out _);
        }

        /// <summary>
        /// Forgets a room's flags, for use when the room itself is going away.
        /// </summary>
        public static void ClearRoomFlagState(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            FlagRooms.TryRemove(roomId, out _);

            foreach (var key in LastFlagEvents.Keys
                .Where(key => key.StartsWith($"{roomId}:", StringComparison.OrdinalIgnoreCase))
                .ToList())
            {
                LastFlagEvents.TryRemove(key, out _);
            }
        }

        public static void ClearPlayerState(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            // A player who left cannot be holding a flag, so the flag they were
            // holding goes back to its stand rather than staying in the air. This
            // used to drop the carrier record, which left the flag itself with no
            // state at all: it was neither home, nor dropped, nor carried.
            foreach (var entry in FlagRooms)
            {
                var room = MatchRoomManager.Instance.GetRoomById(entry.Key);
                if (room == null)
                {
                    continue;
                }

                var released = entry.Value.WithFlags(flags =>
                {
                    foreach (var flag in flags.Values)
                    {
                        if (flag.IsCarried &&
                            string.Equals(flag.CarrierId, playerId, StringComparison.OrdinalIgnoreCase) &&
                            flag.Return(EFlagReturnReason.AutoReturn))
                        {
                            return flag.Team;
                        }
                    }

                    return ETeam.NoTeam;
                });

                if (released != ETeam.NoTeam)
                {
                    GameMessageDispatcher.SendFlagReturned(
                        room.Id.ToString(),
                        released.ToString(),
                        playerId,
                        EFlagReturnReason.AutoReturn.ToString());
                }
            }

            foreach (var entry in LastFlagEvents.Keys.Where(key => key.Contains($":{playerId}:", StringComparison.OrdinalIgnoreCase)))
            {
                LastFlagEvents.TryRemove(entry, out _);
            }

            LastShots.TryRemove(playerId, out _);
        }

        private static void HandleFlagScoreUpdate(MatchRoom room)
        {
            // Score is derived from server-side flag events. Never relay the
            // client-provided score fields as authoritative state.
            var currentRed = room.GetFlagScore(ETeam.Red);
            var currentBlue = room.GetFlagScore(ETeam.Blue);
            GameMessageDispatcher.SendFlagScoreUpdate(room.Id.ToString(), currentRed, currentBlue);
        }

        private static void HandlePlayerEliminated(MatchRoom room, string playerId)
        {
            Console.WriteLine($"Player {playerId} was eliminated");
        }

        /// <summary>
        /// Answers the player who asked, rather than the room.
        /// <para>
        /// The status was broadcast to the whole room, so a question about one
        /// player's view of the match was answered into every other player's
        /// screen. A client also had no case for the label, so the answer was
        /// dropped everywhere it went. It is one player's answer, so it goes to
        /// that player.
        /// </para>
        /// </summary>
        private static void SendMatchStatus(MatchRoom room, string requestingPlayerId)
        {
            var status = $"Match Active - Players: {room.Players.Count}";
            Console.WriteLine($"Sending status to {requestingPlayerId}: {status}");
            GameMessageDispatcher.SendMatchStatus(
                requestingPlayerId,
                room.Id.ToString(),
                status,
                room.Playing,
                room.PlayerCount);
        }

        private static void HandlePositionUpdate(MatchRoom room, string playerId, JObject position)
        {
            Console.WriteLine($"Player {playerId} position updated");
        }

        private static void HandlePlayerRespawn(MatchRoom room, string playerId, JObject json)
        {
            // The client may request a respawn, but it never chooses the
            // position. Until map-specific spawn tables are configured, keep
            // the authoritative server position and ignore client coordinates.
            var serverState = MatchServerV2.Instance?.ServerLagCompensationManager.GetPlayerState(playerId) ?? default;
            if (!string.Equals(serverState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Match] Ignored respawn for unregistered player '{playerId}'");
                return;
            }

            var spawnX = serverState.PositionX;
            var spawnY = serverState.PositionY;
            var spawnZ = serverState.PositionZ;
            var respawnedPlayer = room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase));
            if (respawnedPlayer == null || respawnedPlayer.Health > 0)
            {
                Console.WriteLine($"[Match] Ignored respawn request from living or unknown player '{playerId}'");
                return;
            }

            // Whether a dead player comes back is the rule's call, and asking it
            // is the only thing that makes the answer mean anything. A survival
            // rule says a death is permanent, and it said so to nobody: this
            // handler restored the health of anybody who asked, so the one mode
            // where dying should end your participation was the one mode that
            // would hand a player their life back on request.
            if (room.Rule == null || !room.Rule.CanReSpawn())
            {
                Console.WriteLine(
                    $"[Match] Refused a respawn for '{playerId}': {room.Setting?.Mode} does not respawn");
                return;
            }

            if (ServerManager.Instance.Settings.TryGetRespawnPoint(
                    respawnedPlayer.Team,
                    room.Players.IndexOf(respawnedPlayer),
                    out var configuredSpawn))
            {
                spawnX = configuredSpawn.X;
                spawnY = configuredSpawn.Y;
                spawnZ = configuredSpawn.Z;
            }

            var spawnPosition = new JObject
            {
                ["X"] = spawnX,
                ["Y"] = spawnY,
                ["Z"] = spawnZ
            };
            MatchServerV2.Instance?.ServerLagCompensationManager.SetPlayerPosition(
                playerId, spawnX, spawnY, spawnZ);

            respawnedPlayer.Health = Math.Max(1, respawnedPlayer.MaxHealth);

            GameMessageDispatcher.SendPlayerRespawn(room.Id.ToString(), playerId, spawnPosition);
        }

        private static void HandlePlayerPose(MatchRoom room, string playerId, JObject json)
        {
            var poseState = ReadString(json, "PoseState", "Pose", "Posture") ?? "Stand";
            if (!Enum.TryParse<EPlayerPoseState>(poseState, true, out var pose) ||
                !Enum.IsDefined(typeof(EPlayerPoseState), pose))
            {
                Console.WriteLine($"[Match] Ignored invalid pose '{poseState}' from '{playerId}'");
                return;
            }

            var canonicalPose = pose.ToString();
            Console.WriteLine($"Player {playerId} pose changed to {canonicalPose}");
            room.SetPlayerPoseState(playerId, pose);
            GameMessageDispatcher.SendPlayerPose(room.Id.ToString(), playerId, canonicalPose);
        }

        private static void HandlePlayerShot(MatchRoom room, string playerId, JObject shotData)
        {
            // 射撃処理 - ヒット判定、ダメージ計算など
            var targetId = shotData.GetStringOrNull("TargetID");
            var weaponType = NormalizeWeaponType(shotData.GetStringOrNull("WeaponType"));

            if (weaponType == null)
            {
                Console.WriteLine($"[Match] Ignored shot with unknown weapon from '{playerId}'");
                return;
            }

            if (!AcceptShot(playerId, weaponType))
            {
                return;
            }

            Console.WriteLine($"Player {playerId} shot with {weaponType}");

            if (!string.IsNullOrWhiteSpace(targetId) &&
                string.Equals(targetId, playerId, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[Match] Ignored self-targeted shot from '{playerId}'");
            }

            // The aim decides who was hit, not the message.
            //
            // A target named in the message used to be taken at face value, with
            // only a distance check behind it. That let a client choose its target:
            // it could name a player it was not pointing at, so long as the two
            // were within range, and the shot landed. The direction in the message
            // was not consulted at all on that path.
            //
            // The message a real client builds carries no target, so ignoring this
            // one changes nothing for it. It matters for anyone who sends one,
            // which is exactly the case that has to be server owned.
            var resolved = ResolveShotTarget(room, playerId, shotData, weaponType);
            if (resolved != null)
            {
                HandleShotHit(room, playerId, resolved, weaponType);
            }

            // 全プレイヤーに射撃イベントをブロードキャスト（UDP）
            BroadcastShotEvent(room, playerId, shotData);
        }

        /// <summary>
        /// How far off the shot line a player can be and still be hit.
        /// <para>
        /// This stands in for the width of a player. It is a world unit, matching
        /// the unit the positions and the weapon ranges are already expressed in.
        /// </para>
        /// </summary>
        private const float ShotHitRadius = 1.5f;

        /// <summary>
        /// Works out which player a shot reached, from the server's own positions.
        /// <para>
        /// This is the server's half of a server authoritative hit. The client
        /// says where it aimed and the server decides who that was, using the
        /// positions it has been tracking rather than anything in the message, so
        /// a client cannot pick its target or a damage value.
        /// </para>
        /// <para>
        /// A shooter the server has never seen a position for cannot aim, because
        /// the origin it would project from is not a place the player was ever at.
        /// A registered player with no position still reads as the origin, so
        /// without this an unseen shooter would fire from the middle of the map.
        /// </para>
        /// </summary>
        private static string? ResolveShotTarget(
            MatchRoom room,
            string shooterId,
            JObject shotData,
            string weaponType)
        {
            var stateManager = MatchServerV2.Instance.ServerLagCompensationManager;
            var shooterState = stateManager.GetPlayerState(shooterId);
            if (!string.Equals(shooterState.PlayerId, shooterId, StringComparison.OrdinalIgnoreCase) ||
                !shooterState.HasAuthoritativePosition)
            {
                Console.WriteLine($"[Match] Ignored shot with no authoritative origin from '{shooterId}'");
                return null;
            }

            var originX = shooterState.PositionX;
            var originY = shooterState.PositionY;

            // The direction is read from the message because aiming is the one
            // thing a client is the authority on, but a degenerate one is refused
            // rather than normalised: a zero vector is not an aim.
            var direction = shotData.GetValue("Direction") as JObject;
            var dirX = GetFloat(shotData.GetValue("DirX") ?? direction?.GetValue("X") ?? direction?.GetValue("x"), 0f);
            var dirY = GetFloat(shotData.GetValue("DirY") ?? direction?.GetValue("Y") ?? direction?.GetValue("y"), 0f);
            if (!IsFinite(dirX) || !IsFinite(dirY))
            {
                return null;
            }

            var length = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
            if (length < 0.001f)
            {
                Console.WriteLine($"[Match] Ignored shot with no usable direction from '{shooterId}'");
                return null;
            }

            dirX /= length;
            dirY /= length;

            var range = WeaponRange(weaponType);
            var rangeSquared = range * range;
            var hitRadiusSquared = ShotHitRadius * ShotHitRadius;

            string? bestId = null;
            var bestDistanceSquared = float.MaxValue;

            foreach (var candidate in room.Players)
            {
                if (string.Equals(candidate.Id, shooterId, StringComparison.OrdinalIgnoreCase) ||
                    candidate.Health <= 0)
                {
                    continue;
                }

                var candidateState = stateManager.GetPlayerState(candidate.Id);
                if (!string.Equals(candidateState.PlayerId, candidate.Id, StringComparison.OrdinalIgnoreCase) ||
                    !candidateState.HasAuthoritativePosition)
                {
                    // A player the server has never seen is not anywhere in
                    // particular, so it cannot be the answer.
                    continue;
                }

                var dx = candidateState.PositionX - originX;
                var dy = candidateState.PositionY - originY;
                var along = (dx * dirX) + (dy * dirY);

                // Behind the shooter, and past the weapon's reach.
                if (along < 0f || along * along > rangeSquared)
                {
                    continue;
                }

                // The square of the miss distance, from the right triangle the
                // projection and the offset form.
                var offsetSquared = ((dx * dx) + (dy * dy)) - (along * along);
                if (offsetSquared > hitRadiusSquared)
                {
                    continue;
                }

                // The closest one down the line is the one that was hit, so a
                // player standing in front of another absorbs the shot.
                if (along < bestDistanceSquared)
                {
                    bestDistanceSquared = along;
                    bestId = candidate.Id;
                }
            }

            return bestId;
        }

        private static float WeaponRange(string? weaponType)
        {
            return weaponType switch
            {
                "Pistol" => 45f,
                "SMG" => 55f,
                "Shotgun" => 25f,
                "Rifle" => 90f,
                "Sniper" => 180f,
                _ => 60f
            };
        }

        /// <summary>
        /// Server side flight of bullets and grenades. The live match path drives
        /// it, so a grenade is simulated rather than only echoed back.
        /// </summary>
        private static readonly OpenGSServer.Network.ServerProjectileSimulator Projectiles =
            new();

        /// <summary>
        /// Applies a simulation tick to every live projectile.
        /// </summary>
        public static void UpdateProjectiles(float deltaSeconds)
        {
            if (deltaSeconds <= 0f || !float.IsFinite(deltaSeconds))
            {
                return;
            }

            // Only players the server has actually seen a position for are handed
            // to the simulation. The lookup returns a plain point, so a player
            // whose position was never established would read as the origin, and
            // a grenade going off near the origin would damage them. That is the
            // same "position unknown is not position zero" problem the pickup and
            // the shot already had to be told about.
            Projectiles.PlayerPositionLookup = playerId =>
            {
                var state = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
                return new Vector2(state.PositionX, state.PositionY);
            };
            Projectiles.PlayerIds = () => GetLocatedMatchPlayerIds();
            Projectiles.OnSpawn = (projectile, spawnType) => BroadcastProjectileSpawn(projectile, spawnType);
            Projectiles.OnExpire = projectile => BroadcastProjectileExpire(projectile);
            Projectiles.OnDamage = (targetId, attackerId, damage, hit) =>
                HandleProjectileDamage(targetId, attackerId, damage, hit);

            Projectiles.Update(deltaSeconds);
        }

        /// <summary>
        /// How many projectiles the live match is currently simulating.
        /// <para>
        /// Exposed so a test can ask whether a message that arrived over a socket
        /// reached the simulation the match loop actually steps. Reading the
        /// static here is the point: a test that built its own simulator would not
        /// be asking whether the realtime route works.
        /// </para>
        /// </summary>
        public static int LiveProjectileCount()
        {
            return Projectiles.ActiveCount;
        }
        /// <summary>
        /// Every player in a live match whose position the server actually has.
        /// <para>
        /// The simulator asks for this each tick rather than caching it, so a
        /// player who joins or leaves is reflected immediately. A player with no
        /// reported position is left out rather than included at the origin.
        /// </para>
        /// </summary>
        private static List<string> GetLocatedMatchPlayerIds()
        {
            var ids = new List<string>();
            var stateManager = MatchServerV2.Instance.ServerLagCompensationManager;
            foreach (var room in MatchRoomManager.Instance.AllRooms().OfType<MatchRoom>())
            {
                if (!room.Playing)
                {
                    continue;
                }

                foreach (var player in room.Players)
                {
                    if (string.IsNullOrWhiteSpace(player.Id))
                    {
                        continue;
                    }

                    var state = stateManager.GetPlayerState(player.Id);
                    if (string.Equals(state.PlayerId, player.Id, StringComparison.OrdinalIgnoreCase) &&
                        state.HasAuthoritativePosition)
                    {
                        ids.Add(player.Id);
                    }
                }
            }

            return ids;
        }

        private static void BroadcastProjectileSpawn(
            OpenGSServer.Network.ServerProjectileState projectile,
            string spawnType)
        {
            var room = MatchRoomManager.Instance.GetRoomById(projectile.RoomId);
            if (room == null)
            {
                return;
            }

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectSpawned,
                ["ObjectId"] = projectile.ProjectileId,
                ["ObjectType"] = spawnType,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = projectile.Position.X,
                ["PosY"] = projectile.Position.Y,
                ["ProjectileId"] = projectile.ProjectileId
            });
        }

        private static void BroadcastProjectileExpire(OpenGSServer.Network.ServerProjectileState projectile)
        {
            var room = MatchRoomManager.Instance.GetRoomById(projectile.RoomId);
            if (room == null)
            {
                return;
            }

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectDestroyed,
                ["ObjectId"] = projectile.ProjectileId,
                ["RoomID"] = room.Id.ToString()
            });
        }

        private static void HandleProjectileDamage(
            string targetId,
            string attackerId,
            int damage,
            Vector2 hitPosition)
        {
            var room = MatchRoomManager.Instance.SearchRoomByMemberID(targetId);
            if (room == null)
            {
                return;
            }

            if (!room.TryGetPlayer(targetId, out var target) || target == null)
            {
                return;
            }

            var pose = room.GetPlayerPoseState(targetId);
            var outcome = ServerDamageResolver.Apply(target, damage, pose);

            if (outcome.WasAlreadyDown)
            {
                // A hit on someone who is already out is not news.
                return;
            }

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                // The projectile path and the shot path both land a hit on the
                // same player, so they have to answer under the same name. They did
                // not: this one said PlayerDamage, which is the name a client sends
                // to claim damage about itself, so a client reading the ruling
                // could not tell the two apart.
                ["MessageType"] = GameMessageTypes.PlayerDamaged,
                ["RoomID"] = room.Id.ToString(),
                ["DamagedPlayerID"] = targetId,
                ["AttackerID"] = attackerId,
                ["Damage"] = outcome.Applied,
                ["RequestedDamage"] = outcome.Requested,
                ["RemainingHealth"] = outcome.RemainingHealth,
                ["MaxHealth"] = target.MaxHealth,
                ["IsDown"] = outcome.IsNowDown,
                ["PoseMultiplier"] = ServerDamageResolver.PoseMultiplier(pose),
                ["HitPosition"] = new JObject { ["X"] = hitPosition.X, ["Y"] = hitPosition.Y },
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });

            if (!outcome.IsNowDown)
            {
                return;
            }

            // The health change is the authority for a kill now, not a client
            // claiming one. A self hit counts as a death without crediting an
            // attacker. Recording on the room as well as the player is what puts
            // the best kill count in the match, which is the number the death
            // match and survival rules end a match on: it was never written, so
            // those rules could only ever end on the clock.
            room.RecordDeath(targetId);
            var isSelfInflicted = string.Equals(attackerId, targetId, StringComparison.OrdinalIgnoreCase);
            if (!isSelfInflicted)
            {
                room.RecordKill(attackerId);
            }

            // A player who goes down leaves whatever they were carrying on the
            // ground. The server has to do this itself: a client used to report
            // it, and a message that never arrived, because the link was down or
            // the claim was refused for want of a carrier, left the flag recorded
            // as carried by somebody who was out of the match. That team could
            // then never score, because the capture rule asks whether a flag is
            // still in the room and the server's answer was permanently no.
            DropFlagCarriedBy(room, targetId);

            Console.WriteLine(
                $"[Match] {targetId} is down in room {room.Id} (hit by {attackerId})");

            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerKilled,
                ["RoomID"] = room.Id.ToString(),
                ["KillerID"] = attackerId,
                ["KilledPlayerID"] = targetId,
                // The client reads the dead player from PlayerId on the death
                // message it was built around, so both spellings travel together.
                ["DeadPlayerID"] = targetId,
                ["PlayerId"] = targetId,
                ["IsSelfInflicted"] = isSelfInflicted,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            });
        }
        /// <summary>
        /// Reads a two component position out of a message, accepting both the
        /// nested and the flat shape clients have used.
        /// </summary>
        private static Vector2 ReadVector2(JObject json, params string[] keys)
        {
            if (json == null || keys == null)
            {
                return Vector2.Zero;
            }

            foreach (var key in keys)
            {
                var nested = json[key] as JObject;
                if (nested != null)
                {
                    return new Vector2(
                        GetFloat(nested["X"] ?? nested["x"], 0f),
                        GetFloat(nested["Y"] ?? nested["y"], 0f));
                }
            }

            foreach (var key in keys)
            {
                // The two components are read by composing the name, so the
                // prefix itself does not have to exist as a key. That matters
                // because the client writes DirX and DirY and writes no "Dir",
                // so requiring it here skipped the only pair the message has and
                // every grenade arrived with no direction.
                var x = GetFloat(json[key + "X"] ?? json[key + "x"], float.NaN);
                var y = GetFloat(json[key + "Y"] ?? json[key + "y"], float.NaN);
                if (!float.IsNaN(x) || !float.IsNaN(y))
                {
                    return new Vector2(
                        float.IsNaN(x) ? 0f : x,
                        float.IsNaN(y) ? 0f : y);
                }
            }

            return Vector2.Zero;
        }

        /// <summary>
        /// Pose state scaling applied to damage, so a crouching or rolling player
        /// takes less. Unknown pose falls back to full damage.
        /// </summary>
        private static float GetPoseDamageMultiplier(string roomId, string playerId)
        {
            var room = MatchRoomManager.Instance.GetRoomById(roomId);
            if (room == null)
            {
                return 1f;
            }

            var pose = room.GetPlayerPoseState(playerId);
            return pose switch
            {
                EPlayerPoseState.Sit => 0.5f,
                EPlayerPoseState.LieDown => 0.75f,
                _ => 1f
            };
        }
        private static void HandleGrenadeThrow(MatchRoom room, string playerId, JObject grenadeData)
        {
            var grenadeType = grenadeData.GetStringOrNull("GrenadeType")
                ?? grenadeData.GetStringOrNull("WeaponType")
                ?? "Normal";
            var origin = ReadVector2(grenadeData, "Position", "Origin");
            // The client spells the direction DirX and DirY, as
            // RUDPMessageTypes.CreateGrenadeThrow and ClientNetworkManager both
            // write it. The reader was only asked for "Direction" and
            // "AimDirection", which it expands to DirectionX and DirectionY, so
            // every grenade a real client threw arrived with no direction at all
            // and was simulated as a grenade that went nowhere.
            var direction = ReadVector2(grenadeData, "Direction", "AimDirection", "Dir");

            // The thrower has to have been seen somewhere. A registered player
            // with no position still reads as the origin, so a client that never
            // reported one could otherwise lob a grenade out of the middle of the
            // map, or past the fight, and the blast would land wherever it said.
            var throwerState = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(playerId);
            if (!string.Equals(throwerState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) ||
                !throwerState.HasAuthoritativePosition)
            {
                Console.WriteLine($"[Match] Ignored grenade throw from '{playerId}' with no authoritative origin");
                return;
            }

            // The grenade leaves the hand, not wherever the message says the
            // hand is. A client supplied origin would let a throw start on top of
            // a target, which is the same thing a client naming its own target
            // would buy.
            origin = new Vector2(throwerState.PositionX, throwerState.PositionY);

            if (direction == Vector2.Zero)
            {
                Console.WriteLine($"[Match] Ignored grenade throw with no direction from '{playerId}'");
                return;
            }

            // The server now owns the flight, the fuse, and the blast. The throw
            // used to be logged and echoed to the room, so the grenade existed
            // only as a message and nothing was ever simulated.
            var projectileId = Projectiles.CreateGrenade(
                room.Id.ToString(), playerId, origin, direction, grenadeType);

            Console.WriteLine($"Player {playerId} threw {grenadeType} ({projectileId})");
            BroadcastGrenadeEvent(room, playerId, grenadeData, projectileId);
        }
        private static void HandleObjectSpawned(MatchRoom room, string playerId, JObject objectData)
        {
            var objectType = objectData.GetStringOrNull("ObjectType") ?? "Unknown";
            var objectId = objectData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            var position = objectData.GetValue("Position") as JObject;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} spawned {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectSpawned,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["PlayerID"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = GetFloat(objectData.GetValue("PosX") ?? position?.GetValue("X") ?? position?.GetValue("x"), 0f),
                ["PosY"] = GetFloat(objectData.GetValue("PosY") ?? position?.GetValue("Y") ?? position?.GetValue("y"), 0f),
                ["Rotation"] = GetFloat(objectData.GetValue("Rotation"), 0f),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void HandleObjectDestroyed(MatchRoom room, string playerId, JObject objectData)
        {
            var objectId = objectData.GetStringOrNull("ObjectId") ?? Guid.NewGuid().ToString("N");
            var objectType = objectData.GetStringOrNull("ObjectType") ?? "Unknown";
            var position = objectData.GetValue("Position") as JObject;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} destroyed {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.ObjectDestroyed,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["DestroyedBy"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = GetFloat(objectData.GetValue("PosX") ?? position?.GetValue("X") ?? position?.GetValue("x"), 0f),
                ["PosY"] = GetFloat(objectData.GetValue("PosY") ?? position?.GetValue("Y") ?? position?.GetValue("y"), 0f),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void HandleShotHit(MatchRoom room, string shooterId, string targetId, string? weaponType)
        {
            var shooter = room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, shooterId, StringComparison.OrdinalIgnoreCase));
            var target = room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, targetId, StringComparison.OrdinalIgnoreCase));

            if (shooter == null || target == null)
            {
                Console.WriteLine($"[Match] Ignored shot against non-member '{targetId}' in room '{room.Id}'");
                return;
            }

            if (shooter.Health <= 0 || target.Health <= 0 ||
                !IsServerValidatedShot(shooterId, targetId, weaponType))
            {
                Console.WriteLine($"[Match] Ignored invalid shot {shooterId}->{targetId} in room '{room.Id}'");
                return;
            }

            // ダメージ計算（武器タイプによる）
            int damage = CalculateWeaponDamage(weaponType);
            LobbyServerManager.Instance.RecordDamageDailyProgress(shooterId, damage);

            target.Health = Math.Max(0, target.Health - damage);
            var targetState = MatchServerV2.Instance.ServerLagCompensationManager.GetPlayerState(targetId);
            var authoritativeHitPosition = new JObject
            {
                ["X"] = targetState.PositionX,
                ["Y"] = targetState.PositionY,
                ["Z"] = targetState.PositionZ
            };
            HandlePlayerDamaged(room, targetId, damage, shooterId, authoritativeHitPosition, target.Health);

            if (target.Health <= 0)
            {
                // Same reason as the projectile path: the room is where the best
                // kill count lives, and the rules end a match on it.
                room.RecordDeath(targetId);
                room.RecordKill(shooterId);
                shooter.Score += 100;

                // The same reason as the projectile path: a carrier who goes down
                // leaves the flag on the ground, and the server is the one that
                // knows the player went down.
                DropFlagCarriedBy(room, targetId);
                HandlePlayerKilled(room, shooterId, targetId);
            }
        }

        private static bool IsServerValidatedShot(string shooterId, string targetId, string? weaponType)
        {
            var stateManager = MatchServerV2.Instance.ServerLagCompensationManager;
            var shooterState = stateManager.GetPlayerState(shooterId);
            var targetState = stateManager.GetPlayerState(targetId);
            if (!string.Equals(shooterState.PlayerId, shooterId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(targetState.PlayerId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var dx = shooterState.PositionX - targetState.PositionX;
            var dy = shooterState.PositionY - targetState.PositionY;
            var dz = shooterState.PositionZ - targetState.PositionZ;
            var distanceSquared = (dx * dx) + (dy * dy) + (dz * dz);

            // The same table the shot resolver reads, so a target picked from the
            // message and one the server found are held to one reach.
            var range = WeaponRange(weaponType);

            return !float.IsNaN(distanceSquared) &&
                   !float.IsInfinity(distanceSquared) &&
                   distanceSquared <= range * range;
        }

        private static int CalculateWeaponDamage(string? weaponType)
        {
            return weaponType switch
            {
                "Pistol" => 25,
                "Rifle" => 35,
                "Sniper" => 80,
                "Shotgun" => 20,
                "SMG" => 15,
                _ => 30 // デフォルトダメージ
            };
        }

        private static bool AcceptShot(string playerId, string weaponType)
        {
            var cooldown = weaponType switch
            {
                "Pistol" => TimeSpan.FromMilliseconds(200),
                "SMG" => TimeSpan.FromMilliseconds(80),
                "Shotgun" => TimeSpan.FromMilliseconds(800),
                "Rifle" => TimeSpan.FromMilliseconds(150),
                "Sniper" => TimeSpan.FromSeconds(1),
                _ => TimeSpan.FromMilliseconds(200)
            };
            var now = DateTime.UtcNow;
            if (LastShots.TryGetValue(playerId, out var lastShot) && now - lastShot < cooldown)
            {
                Console.WriteLine($"[Match] Ignored rapid {weaponType} shot from '{playerId}'");
                return false;
            }

            LastShots[playerId] = now;
            return true;
        }

        // UDPブロードキャストメソッド群
        private static void BroadcastShotEvent(MatchRoom room, string playerId, JObject shotData)
        {
            var serverState = MatchServerV2.Instance?.ServerLagCompensationManager.GetPlayerState(playerId) ?? default;
            if (!string.Equals(serverState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) ||
                !IsFinite(serverState.PositionX) || !IsFinite(serverState.PositionY) || !IsFinite(serverState.PositionZ))
            {
                Console.WriteLine($"[Match] Ignored shot broadcast from unregistered player '{playerId}'");
                return;
            }

            var objectId = Guid.NewGuid().ToString("N");
            var weaponType = NormalizeWeaponType(shotData.GetStringOrNull("WeaponType")) ?? "Unknown";
            var direction = shotData.GetValue("Direction") as JObject;
            var dirX = GetFloat(shotData.GetValue("DirX") ?? direction?.GetValue("X") ?? direction?.GetValue("x"), 1f);
            var dirY = GetFloat(shotData.GetValue("DirY") ?? direction?.GetValue("Y") ?? direction?.GetValue("y"), 0f);
            var directionLength = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
            if (!IsFinite(dirX) || !IsFinite(dirY) || !IsFinite(directionLength) || directionLength < 0.001f)
            {
                Console.WriteLine($"[Match] Ignored shot with invalid direction from '{playerId}'");
                return;
            }

            dirX /= directionLength;
            dirY /= directionLength;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} fired shot ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.PlayerShot,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["ObjectId"] = objectId,
                ["PosX"] = serverState.PositionX,
                ["PosY"] = serverState.PositionY,
                ["PosZ"] = serverState.PositionZ,
                ["DirX"] = dirX,
                ["DirY"] = dirY,
                ["WeaponType"] = weaponType,
                ["ShotData"] = new JObject
                {
                    ["ObjectId"] = objectId,
                    ["WeaponType"] = weaponType,
                    ["PosX"] = serverState.PositionX,
                    ["PosY"] = serverState.PositionY,
                    ["PosZ"] = serverState.PositionZ,
                    ["DirX"] = dirX,
                    ["DirY"] = dirY
                },
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            // UDPブロードキャスト（ルーム内の全プレイヤー）
            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static void BroadcastGrenadeEvent(MatchRoom room, string playerId, JObject grenadeData, string objectId)
        {
            var objectType = NormalizeGrenadeObjectType(grenadeData.GetStringOrNull("GrenadeType"));
            var serverState = MatchServerV2.Instance?.ServerLagCompensationManager.GetPlayerState(playerId) ?? default;
            if (!string.Equals(serverState.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) ||
                !IsFinite(serverState.PositionX) || !IsFinite(serverState.PositionY) || !IsFinite(serverState.PositionZ))
            {
                Console.WriteLine($"[Match] Ignored grenade from unregistered player '{playerId}' in room '{room.Id}'");
                return;
            }

            var direction = grenadeData.GetValue("Direction") as JObject;
            var dirX = GetFloat(grenadeData.GetValue("DirX") ?? direction?.GetValue("X") ?? direction?.GetValue("x"), float.NaN);
            var dirY = GetFloat(grenadeData.GetValue("DirY") ?? direction?.GetValue("Y") ?? direction?.GetValue("y"), float.NaN);
            var directionLength = MathF.Sqrt((dirX * dirX) + (dirY * dirY));
            if (!IsFinite(dirX) || !IsFinite(dirY) || !IsFinite(directionLength) || directionLength < 0.001f)
            {
                Console.WriteLine($"[Match] Ignored grenade with invalid direction from '{playerId}'");
                return;
            }

            dirX /= directionLength;
            dirY /= directionLength;
            Console.WriteLine($"[Match] Room {room.Id} player {playerId} threw {objectType} ({objectId})");

            var message = new JObject
            {
                ["MessageType"] = GameMessageTypes.GrenadeThrow,
                ["ObjectId"] = objectId,
                ["ObjectType"] = objectType,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["RoomID"] = room.Id.ToString(),
                ["PosX"] = serverState.PositionX,
                ["PosY"] = serverState.PositionY,
                ["PosZ"] = serverState.PositionZ,
                ["DirX"] = dirX,
                ["DirY"] = dirY,
                ["GrenadeType"] = objectType,
                ["Direction"] = new JObject
                {
                    ["X"] = dirX,
                    ["Y"] = dirY
                },
                ["Rotation"] = MathF.Atan2(dirY, dirX),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            UdpBroadcastToRoom(room.Id.ToString(), message);
        }

        private static float GetFloat(JToken? token, float fallback)
        {
            return token != null && float.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static string NormalizeGrenadeObjectType(string? grenadeType)
        {
            return grenadeType?.ToLowerInvariant() switch
            {
                "power" => "PowerGrenade",
                "magnetic" => "MagneticGrenade",
                "magnet" => "MagneticGrenade",
                "mine" => "MineGrenade",
                "cluster" => "ClusterGrenade",
                "clusterchild" => "ChildClusterGrenade",
                "fire" => "FireGrenade",
                "smoke" => "SmokeGrenade",
                _ => "NormalGrenade"
            };
        }

        private static string? NormalizeWeaponType(string? weaponType)
        {
            var normalized = weaponType?.Trim().ToLowerInvariant()
                .Replace("(clone)", string.Empty, StringComparison.Ordinal)
                .Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);

            return normalized switch
            {
                "pistol" or "handgun" or "glock" or "deserteagle" => "Pistol",
                "smg" or "submachinegun" or "scorpion" or "fnp90" or "uzi" or "mp5" => "SMG",
                "shotgun" => "Shotgun",
                "rifle" or "assaultrifle" or "ak47" or "m16" or "famas" or "f2000" or
                    "steyraug" or "mg42" or "m60" or "fnminimisaw" => "Rifle",
                "sniper" or "sniperrifle" or "scout" or "dragunov" or "psg1" or "awp" => "Sniper",
                _ => null
            };
        }

        private static void UdpBroadcastToRoom(string roomId, JObject message)
        {
            GameMessageDispatcher.BroadcastToRoom(roomId, message);
        }

        /// <summary>
        /// 後方互換性のためのメソッド - TCPイベントとして処理
        /// </summary>
        public static void ParseEvent(JObject json)
        {
            ParseTcpEvent(json);
        }

        /// <summary>
        /// UDPサーバーからゲームイベントを受信した際に呼び出す
        /// </summary>
        public static void HandleUdpGameEvent(byte[] data, string remoteEndPoint)
        {
            ParseUdpEvent(data, remoteEndPoint);
        }

        /// <summary>
        /// TCPサーバーからシステムイベントを受信した際に呼び出す
        /// </summary>
        public static void HandleTcpSystemEvent(JObject json)
        {
            ParseTcpEvent(json);
        }

        private static string ReadString(JObject json, params string[] keys)
        {
            if (json == null || keys == null)
            {
                return null;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var value = json.GetValue(key)?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads an integer, falling back when the message does not carry a
        /// readable one.
        /// <para>
        /// A value that does not parse is not treated as zero. A magazine count of
        /// zero means an empty weapon, which is a different claim from saying
        /// nothing, so an unreadable count has to fall through to the default
        /// rather than empty the weapon.
        /// </para>
        /// </summary>
        private static int ReadInt(JObject json, int fallback, params string[] keys)
        {
            if (json == null || keys == null)
            {
                return fallback;
            }

            foreach (var key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var token = json.GetValue(key);
                if (token == null)
                {
                    continue;
                }

                if (int.TryParse(token.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }

        private static bool IsRoomMatch(MatchRoom room, string roomId)
        {
            return string.Equals(room.Id.ToString(), roomId, StringComparison.OrdinalIgnoreCase);
        }

    }
}
