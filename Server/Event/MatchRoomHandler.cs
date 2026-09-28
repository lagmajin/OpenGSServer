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
        private static readonly ConcurrentDictionary<string, byte> FlagCarriers = new();
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

                case "PlayerDamaged":
                    Console.WriteLine($"[Match] Ignored client-supplied damage event from '{playerId}'; damage is server-authoritative");
                    break;

                case GameMessageTypes.FlagCaptured:
                    HandleFlagCaptured(room, playerId);
                    break;

                case GameMessageTypes.FlagLost:
                    HandleFlagLost(room, playerId);
                    break;

                case GameMessageTypes.FlagPickup:
                    HandleFlagPickup(room, playerId);
                    break;

                case GameMessageTypes.FlagReturn:
                    var returnedByPlayerId = ReadString(json, "ReturnedByPlayerId", "ReturnedByPlayerID", "PlayerID", "PlayerId");
                    if (!string.IsNullOrWhiteSpace(returnedByPlayerId) &&
                        !string.Equals(returnedByPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"[Match] Ignored flag return with forged player '{returnedByPlayerId}' from '{playerId}'");
                        break;
                    }

                    HandleFlagReturn(room, playerId);
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
                ["MessageType"] = "WeaponDropped",
                ["RoomID"] = room.Id.ToString(),
                ["ItemId"] = itemId,
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
                return;
            }

            var effect = InstantItemRules.EffectOf(itemType);
            if (effect == null)
            {
                Console.WriteLine($"[Match] Ignored item use of {itemType} by '{playerId}': it does nothing");
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
                ["MessageType"] = "ItemUsed",
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

            // Everyone is told, because a claim only works if the other players
            // are the ones honouring it.
            GameMessageDispatcher.BroadcastToRoom(room.Id.ToString(), new JObject
            {
                ["MessageType"] = reserved ? "WeaponReserved" : "WeaponReleased",
                ["RoomID"] = room.Id.ToString(),
                ["ItemId"] = itemId,
                ["WeaponId"] = itemId,
                ["PlayerID"] = playerId,
                ["PlayerId"] = playerId,
                ["ReservedByPlayerId"] = playerId,
                ["WeaponType"] = weaponType,
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
                ["MessageType"] = "PlayerDamaged",
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

        private static void HandleFlagCaptured(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagCaptured, TimeSpan.FromSeconds(2)) ||
                !FlagCarriers.TryRemove(GetFlagCarrierKey(room, playerId), out _))
            {
                Console.WriteLine($"[Match] Ignored flag capture without a server-tracked carrier '{playerId}'");
                return;
            }

            Console.WriteLine($"Team {team} captured the flag");
            room.AddFlagCapture(team);

            GameMessageDispatcher.SendFlagCaptured(room.Id.ToString(), team.ToString());
            GameMessageDispatcher.SendFlagScoreUpdate(
                room.Id.ToString(),
                room.GetFlagScore(ETeam.Red),
                room.GetFlagScore(ETeam.Blue));
        }

        private static void HandleFlagLost(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagLost, TimeSpan.FromMilliseconds(500)))
            {
                return;
            }

            Console.WriteLine($"Team {team} lost the flag");
            FlagCarriers.TryRemove(GetFlagCarrierKey(room, playerId), out _);
            GameMessageDispatcher.SendFlagLost(room.Id.ToString(), team.ToString(), playerId);
        }

        private static void HandleFlagPickup(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagPickup, TimeSpan.FromMilliseconds(500)))
            {
                return;
            }

            Console.WriteLine($"Team {team} picked up the flag");
            FlagCarriers[GetFlagCarrierKey(room, playerId)] = 0;
            GameMessageDispatcher.SendFlagPickup(room.Id.ToString(), team.ToString(), playerId);
        }

        private static void HandleFlagReturn(MatchRoom room, string playerId)
        {
            var team = ResolvePlayerTeam(room, playerId);
            if (team == ETeam.NoTeam || !AcceptFlagEvent(room, playerId, GameMessageTypes.FlagReturn, TimeSpan.FromMilliseconds(500)))
            {
                return;
            }

            Console.WriteLine($"Team {team} returned the flag");
            FlagCarriers.TryRemove(GetFlagCarrierKey(room, playerId), out _);
            GameMessageDispatcher.SendFlagReturn(room.Id.ToString(), team.ToString(), playerId);
        }

        private static ETeam ResolvePlayerTeam(MatchRoom room, string playerId)
        {
            return room.Players.FirstOrDefault(player =>
                string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase))?.Team ?? ETeam.NoTeam;
        }

        private static bool AcceptFlagEvent(MatchRoom room, string playerId, string eventType, TimeSpan cooldown)
        {
            var key = $"{room.Id}:{playerId}:{eventType}";
            var now = DateTime.UtcNow;
            if (LastFlagEvents.TryGetValue(key, out var lastEvent) && now - lastEvent < cooldown)
            {
                Console.WriteLine($"[Match] Ignored repeated {eventType} from '{playerId}' in room '{room.Id}'");
                return false;
            }

            LastFlagEvents[key] = now;
            return true;
        }

        private static string GetFlagCarrierKey(MatchRoom room, string playerId)
        {
            return $"{room.Id}:{playerId}";
        }

        public static void ClearPlayerState(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            foreach (var entry in FlagCarriers.Keys.Where(key => key.EndsWith($":{playerId}", StringComparison.OrdinalIgnoreCase)))
            {
                FlagCarriers.TryRemove(entry, out _);
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

        private static void SendMatchStatus(MatchRoom room, string requestingPlayerId)
        {
            var status = $"Match Active - Players: {room.Players.Count}";
            Console.WriteLine($"Sending status to {requestingPlayerId}: {status}");
            GameMessageDispatcher.SendMatchStatus(room.Id.ToString(), status);
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
                ["MessageType"] = "PlayerDamaged",
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
            // attacker.
            target.Deaths++;
            var isSelfInflicted = string.Equals(attackerId, targetId, StringComparison.OrdinalIgnoreCase);
            if (!isSelfInflicted && room.TryGetPlayer(attackerId, out var attacker) && attacker != null)
            {
                attacker.Kills++;
            }

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
                target.Deaths++;
                shooter.Kills++;
                shooter.Score += 100;
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
