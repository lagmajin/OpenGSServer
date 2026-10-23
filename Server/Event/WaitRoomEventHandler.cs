using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenGSCore;

#nullable enable

namespace OpenGSServer
{
    internal interface IWaitRoomEventHandler
    {
    }

    internal class WaitRoomEventHandler
    {
        private static readonly object LoadingStateLock = new();
        private static readonly Dictionary<string, HashSet<string>> LoadingCompletedPlayers = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoadingRooms = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoadingApprovedRooms = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> LoadingTimedOutRooms = new(StringComparer.OrdinalIgnoreCase);

        // S3: loading start, progress, completion, and map entry approval all
        // run through the server, but a client that never reports completion
        // used to hold the room locked forever. This monitor gives that wait a
        // deadline and releases the room when it passes.
        //
        // The deadline is read once at startup. Tests set
        // OPENGS_LOADING_TIMEOUT_SECONDS to a small value so they do not have
        // to wait a full minute.
        private static readonly TimeSpan LoadingTimeout = ReadLoadingTimeout();

        private static TimeSpan ReadLoadingTimeout()
        {
            var configured = Environment.GetEnvironmentVariable("OPENGS_LOADING_TIMEOUT_SECONDS");
            if (int.TryParse(configured, out var seconds) && seconds > 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return TimeSpan.FromSeconds(60);
        }

        private static readonly LoadingTimeoutMonitor LoadingTimeouts = new(
            LoadingTimeout,
            new Func<IEnumerable<WaitRoom>>[]
            {
                () => WaitRoomManager.Instance().GetAllRooms(),
                () => MissionWaitRoomManager.Instance.GetAllMissionRooms()
            },
            IsRoomWaitingForLoading,
            HandleLoadingTimeout);

        internal static void StartLoadingTimeoutMonitor() => LoadingTimeouts.Start();

        internal static void StopLoadingTimeoutMonitor() => LoadingTimeouts.Dispose();

        /// <summary>
        /// True when the room entered loading but has not been approved yet.
        /// </summary>
        private static bool IsRoomWaitingForLoading(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return false;
            }

            lock (LoadingStateLock)
            {
                return LoadingRooms.Contains(roomId);
            }
        }

        private static void HandleLoadingTimeout(WaitRoom waitRoom, IReadOnlyList<string> pendingPlayers)
        {
            lock (LoadingStateLock)
            {
                if (!LoadingRooms.Remove(waitRoom.RoomId))
                {
                    return;
                }

                var players = waitRoom.AllPlayers();
                pendingPlayers = LoadingCompletedPlayers.TryGetValue(waitRoom.RoomId, out var completedPlayers)
                    ? players.Where(player => !completedPlayers.Contains(player.Id)).Select(player => player.Id).ToList()
                    : players.Select(player => player.Id).ToList();

                LoadingCompletedPlayers.Remove(waitRoom.RoomId);
                LoadingApprovedRooms.Remove(waitRoom.RoomId);
                LoadingTimedOutRooms.Add(waitRoom.RoomId);
            }

            LoadingTimeouts.Forget(waitRoom.RoomId);

            if (!MatchRoomManager.Instance.DiscardPendingMatchForWaitRoom(waitRoom.RoomId))
            {
                return;
            }

            // S3: the MatchRoom created for this attempt is discarded, so the
            // wait room has to be released first. Otherwise the update below
            // still reports NowPlaying true, and CanStartMatch keeps failing.
            waitRoom.CancelPendingMatch();

            BroadcastLoadingNotification(waitRoom, new JObject
            {
                ["MessageType"] = MessageType.LoadingFailed,
                ["RoomID"] = waitRoom.RoomId,
                ["RoomId"] = waitRoom.RoomId,
                ["Success"] = false,
                ["Message"] = "Loading timed out. These players did not report completion: " + string.Join(", ", pendingPlayers),
                ["PendingPlayers"] = new JArray(pendingPlayers)
            });

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
            LobbyEventHandler.BroadcastRoomListUpdate();

            ConsoleWrite.WriteMessage(
                $"[Loading] room {waitRoom.RoomId} released after {LoadingTimeout.TotalSeconds:0}s with {pendingPlayers.Count} player(s) still loading",
                ConsoleColor.Yellow);
        }

        private static JObject CreateRoomError(string messageType, string roomId)
        {
            return new JObject
            {
                ["MessageType"] = messageType,
                ["RoomId"] = roomId ?? string.Empty
            };
        }

        internal static void BroadcastRoomUpdate(WaitRoom waitRoom, string messageType)
        {
            var updateJson = new JObject
            {
                ["MessageType"] = messageType,
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            };

            foreach (var player in waitRoom.AllPlayers())
            {
                var targetSession = LobbyServerManager.Instance.FindSessionByPlayerId(player.Id);
                targetSession?.SendAsyncJsonWithTimeStamp(updateJson);
            }
        }

        private static void BroadcastLoadingNotification(WaitRoom waitRoom, JObject notification)
        {
            foreach (var player in waitRoom.AllPlayers())
            {
                LobbyServerManager.Instance.FindSessionByPlayerId(player.Id)?.SendAsyncJsonWithTimeStamp(notification);
            }
        }

        /// <summary>
        /// Marks a room as waiting on its players to finish loading.
        /// <para>
        /// The match start path calls this when it announces
        /// LoadingStartedNotification, so the AllowEnterMap gate and the loading
        /// timeout both have a room to work with. The client driven
        /// LoadingStarted message goes through the same registration.
        /// </para>
        /// </summary>
        internal static void BeginLoadingForRoom(WaitRoom waitRoom)
        {
            if (waitRoom == null || string.IsNullOrWhiteSpace(waitRoom.RoomId))
            {
                return;
            }

            BeginLoading(waitRoom, forceRestart: true);
        }

        private static bool BeginLoading(WaitRoom waitRoom, bool forceRestart = false)
        {
            var trackTimeout = false;
            lock (LoadingStateLock)
            {
                if (forceRestart)
                {
                    LoadingApprovedRooms.Remove(waitRoom.RoomId);
                    LoadingTimedOutRooms.Remove(waitRoom.RoomId);
                    LoadingRooms.Add(waitRoom.RoomId);
                    LoadingCompletedPlayers[waitRoom.RoomId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    trackTimeout = true;
                }
                else
                {
                    if (LoadingApprovedRooms.Contains(waitRoom.RoomId) || LoadingTimedOutRooms.Contains(waitRoom.RoomId))
                    {
                        return false;
                    }

                    if (LoadingRooms.Add(waitRoom.RoomId))
                    {
                        LoadingCompletedPlayers[waitRoom.RoomId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        trackTimeout = true;
                    }
                }
            }

            // Repeated LoadingStarted messages must not extend the deadline.
            if (trackTimeout)
            {
                LoadingTimeouts.TrackLoadingStarted(waitRoom.RoomId);
            }

            return true;
        }

        private static (bool Accepted, bool AllPlayersCompleted) MarkLoadingCompleted(WaitRoom waitRoom, string playerId)
        {
            var allPlayersCompleted = false;
            lock (LoadingStateLock)
            {
                if (!LoadingRooms.Contains(waitRoom.RoomId))
                {
                    return (false, false);
                }

                var players = waitRoom.AllPlayers();
                if (!players.Any(player => string.Equals(player.Id, playerId, StringComparison.OrdinalIgnoreCase)))
                {
                    return (false, false);
                }

                var completedPlayers = LoadingCompletedPlayers[waitRoom.RoomId];
                completedPlayers.Add(playerId);
                if (players.Count == 0)
                {
                    return (false, false);
                }

                foreach (var player in players)
                {
                    if (!completedPlayers.Contains(player.Id))
                    {
                        return (true, false);
                    }
                }

                LoadingRooms.Remove(waitRoom.RoomId);
                LoadingCompletedPlayers.Remove(waitRoom.RoomId);
                LoadingApprovedRooms.Add(waitRoom.RoomId);
                allPlayersCompleted = true;
            }

            if (allPlayersCompleted)
            {
                LoadingTimeouts.Forget(waitRoom.RoomId);
            }

            return (true, allPlayersCompleted);
        }

        private static bool HasLoadingCompleted(string roomId, string playerId)
        {
            lock (LoadingStateLock)
            {
                return LoadingCompletedPlayers.TryGetValue(roomId, out var completedPlayers) &&
                    completedPlayers.Contains(playerId);
            }
        }

        private static bool TryApproveLoadingAfterPlayerRemoved(WaitRoom waitRoom, string playerId)
        {
            if (!waitRoom.NowPlaying)
            {
                return false;
            }

            lock (LoadingStateLock)
            {
                if (!LoadingRooms.Contains(waitRoom.RoomId) ||
                    !LoadingCompletedPlayers.TryGetValue(waitRoom.RoomId, out var completedPlayers))
                {
                    return false;
                }

                completedPlayers.Remove(playerId);
                var remainingPlayers = waitRoom.AllPlayers();
                if (remainingPlayers.Count == 0 || remainingPlayers.Any(player => !completedPlayers.Contains(player.Id)))
                {
                    return false;
                }

                LoadingRooms.Remove(waitRoom.RoomId);
                LoadingCompletedPlayers.Remove(waitRoom.RoomId);
                LoadingApprovedRooms.Add(waitRoom.RoomId);
            }

            LoadingTimeouts.Forget(waitRoom.RoomId);
            if (!MatchRoomManager.Instance.BeginMatchForWaitRoom(waitRoom))
            {
                FailPendingMatchStart(waitRoom);
                return false;
            }

            BroadcastAllowEnterMap(waitRoom);
            return true;
        }

        private static void FailPendingMatchStart(WaitRoom waitRoom)
        {
            if (!MatchRoomManager.Instance.DiscardPendingMatchForWaitRoom(waitRoom.RoomId))
            {
                return;
            }

            waitRoom.CancelPendingMatch();
            lock (LoadingStateLock)
            {
                LoadingApprovedRooms.Remove(waitRoom.RoomId);
                LoadingTimedOutRooms.Add(waitRoom.RoomId);
            }

            BroadcastLoadingNotification(waitRoom, new JObject
            {
                ["MessageType"] = MessageType.LoadingFailed,
                ["RoomID"] = waitRoom.RoomId,
                ["RoomId"] = waitRoom.RoomId,
                ["Success"] = false,
                ["Message"] = "The server could not start the match. Please try again."
            });
            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
            LobbyEventHandler.BroadcastRoomListUpdate();
        }

        private static void ClearLoadingState(string roomId)
        {
            if (string.IsNullOrWhiteSpace(roomId))
            {
                return;
            }

            // S3: the room is no longer waiting on its players.
            LoadingTimeouts.Forget(roomId);

            lock (LoadingStateLock)
            {
                LoadingRooms.Remove(roomId);
                LoadingCompletedPlayers.Remove(roomId);
                LoadingApprovedRooms.Remove(roomId);
                LoadingTimedOutRooms.Remove(roomId);
            }
        }

        private static void BroadcastAllowEnterMap(WaitRoom waitRoom)
        {
            var notification = new JObject
            {
                ["MessageType"] = MessageType.AllowEnterMap,
                ["RoomID"] = waitRoom.RoomId,
                ["RoomId"] = waitRoom.RoomId,
                ["Approved"] = true
            };

            BroadcastLoadingNotification(waitRoom, notification);
        }

        private static float ReadLoadingProgress(IDictionary<string, JToken> dic)
        {
            var token = dic.GetStringOrNull("Progress");
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var progress)
                ? Math.Clamp(progress, 0f, 1f)
                : 0f;
        }

        private static JObject BuildRoomInfoJson(WaitRoom waitRoom)
        {
            var snapshot = waitRoom.ToSnapshot().ToJson();
            snapshot["OwnerId"] = waitRoom.GetFirstPlayerId();
            snapshot["OwnerID"] = snapshot["OwnerId"];
            snapshot["HasPassword"] = !string.IsNullOrWhiteSpace(waitRoom.Password);
            return snapshot;
        }

        private static JObject BuildPublicRoomSettingsJson(JObject roomInfoJson)
        {
            var settings = new JObject
            {
                ["RoomName"] = roomInfoJson["RoomName"],
                ["Capacity"] = roomInfoJson["Capacity"],
                ["GameMode"] = roomInfoJson["GameMode"],
                ["TeamBalance"] = roomInfoJson["TeamBalance"],
                ["Map"] = roomInfoJson["Map"],
                ["HasPassword"] = roomInfoJson["HasPassword"],
                ["RoomId"] = roomInfoJson["RoomId"],
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = roomInfoJson["PlayerCount"],
                ["NowPlaying"] = roomInfoJson["NowPlaying"]
            };

            return settings;
        }

        private static WaitRoom? FindWaitRoomByPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            foreach (var room in WaitRoomManager.Instance().GetAllRooms())
            {
                if (room.Players.ContainsKey(playerId))
                {
                    return room;
                }
            }

            return FindMissionRoomByPlayerId(playerId);
        }

        private static WaitRoom? FindMissionRoomByPlayerId(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return null;
            }

            foreach (var room in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                if (room.Players.ContainsKey(playerId))
                {
                    return room;
                }
            }

            return null;
        }

        private static bool TryGetSessionPlayerId(ClientSession? session, out string playerId)
        {
            playerId = session?.PlayerID ?? string.Empty;
            return !string.IsNullOrWhiteSpace(playerId);
        }

        public static void RemoveDisconnectedPlayer(string playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            foreach (var waitRoom in WaitRoomManager.Instance().GetAllRooms())
            {
                var previousOwnerId = waitRoom.GetFirstPlayerId();
                if (!waitRoom.TryRemovePlayer(playerId, out _))
                {
                    continue;
                }

                var newOwnerId = waitRoom.GetFirstPlayerId();
                if (waitRoom.AllPlayers().Count == 0)
                {
                    ClearLoadingState(waitRoom.RoomId);
                    WaitRoomManager.Instance().CloseRoom(waitRoom.RoomId);
                    LobbyEventHandler.BroadcastRoomListUpdate();
                    continue;
                }

                TryApproveLoadingAfterPlayerRemoved(waitRoom, playerId);

                if (!string.Equals(previousOwnerId, newOwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    var ownerChange = new JObject
                    {
                        ["MessageType"] = MessageType.WaitRoomOwnerChange,
                        ["RoomID"] = waitRoom.RoomId,
                        ["RoomId"] = waitRoom.RoomId,
                        ["PreviousOwnerId"] = previousOwnerId,
                        ["NewOwnerId"] = newOwnerId,
                        ["OwnerId"] = newOwnerId,
                        ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
                    };

                    foreach (var player in waitRoom.AllPlayers())
                    {
                        LobbyServerManager.Instance.FindSessionByPlayerId(player.Id)?.SendAsyncJsonWithTimeStamp(ownerChange);
                    }
                }

                BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
                LobbyEventHandler.BroadcastRoomListUpdate();
            }

            foreach (var missionRoom in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                if (!missionRoom.TryRemovePlayer(playerId, out _))
                {
                    continue;
                }

                if (missionRoom.AllPlayers().Count == 0)
                {
                    ClearLoadingState(missionRoom.RoomId);
                    MissionWaitRoomManager.Instance.CloseMissionRoom(missionRoom.RoomId);
                }
                else
                {
                    TryApproveLoadingAfterPlayerRemoved(missionRoom, playerId);
                    BroadcastRoomUpdate(missionRoom, MessageType.WaitRoomUpdateNotification);
                }

                LobbyEventHandler.BroadcastRoomListUpdate();
            }
        }

        private static bool TryResolveWaitRoom(IDictionary<string, JToken> dic, string playerId, out WaitRoom? waitRoom)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            waitRoom = null;

            if (!string.IsNullOrWhiteSpace(roomId))
            {
                waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId)
                    ?? MissionWaitRoomManager.Instance.FindMissionRoom(roomId);
            }

            if (waitRoom != null && !string.IsNullOrWhiteSpace(playerId) && !waitRoom.Players.ContainsKey(playerId))
            {
                waitRoom = null;
            }

            if (waitRoom == null)
            {
                waitRoom = FindWaitRoomByPlayerId(playerId);
            }

            return waitRoom != null;
        }

        private static bool TryParsePlayerCharacter(JToken token, out EPlayerCharacter playerCharacter)
        {
            playerCharacter = default;
            if (token == null)
            {
                return false;
            }

            var raw = token.ToString();
            if (Enum.TryParse(raw, true, out EPlayerCharacter parsed))
            {
                playerCharacter = parsed;
                return true;
            }

            if (int.TryParse(raw, out var numeric) && Enum.IsDefined(typeof(EPlayerCharacter), numeric))
            {
                playerCharacter = (EPlayerCharacter)numeric;
                return true;
            }

            return false;
        }

        private static List<EInstantItemType> ResolveEquippedInstantItems(string accountId)
        {
            var equipped = new EInstantItemType[3];
            var storedItems = AccountDatabaseManager.GetInstance().GetEquippedInstantItems(accountId);
            foreach (var entry in storedItems)
            {
                if (entry.Slot < 0 || entry.Slot >= equipped.Length ||
                    string.IsNullOrWhiteSpace(entry.ItemId) ||
                    !Enum.TryParse(entry.ItemId, true, out EInstantItemType itemType) ||
                    !InstantItemRules.IsUsable(itemType))
                {
                    continue;
                }

                equipped[entry.Slot] = itemType;
            }

            if (equipped.All(item => item == EInstantItemType.None))
            {
                equipped[0] = EInstantItemType.HealthKit;
                equipped[1] = EInstantItemType.PowerGrenadePack;
            }

            return equipped.ToList();
        }

        public static void ChangePlayerSettting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId))
            {
                return;
            }

            if (!TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            var equippedInstantItems = dic.ContainsKey("EquipInstantItems")
                ? ResolveEquippedInstantItems(playerId)
                : null;
            bool changed = false;
            lock (waitRoom)
            {
                if (!waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    return;
                }

                if (dic.TryGetValue("PlayerCharacter", out var playerCharacterToken) &&
                    TryParsePlayerCharacter(playerCharacterToken, out var playerCharacter))
                {
                    player.playerCharacter = playerCharacter;
                    changed = true;
                }

                if (equippedInstantItems != null)
                {
                    // The client field remains the sync trigger for wire compatibility;
                    // ownership and slot selection come from the account's persisted shop state.
                    player.EquipInstantItems = equippedInstantItems;
                    changed = true;
                }
            }

            if (changed)
            {
                BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
            }

            foreach (var missionRoom in MissionWaitRoomManager.Instance.GetAllMissionRooms())
            {
                if (!missionRoom.TryRemovePlayer(playerId, out _))
                {
                    continue;
                }

                if (missionRoom.AllPlayers().Count == 0)
                {
                    MissionWaitRoomManager.Instance.CloseMissionRoom(missionRoom.RoomId);
                }
                else
                {
                    BroadcastRoomUpdate(missionRoom, MessageType.WaitRoomUpdateNotification);
                }

                LobbyEventHandler.BroadcastRoomListUpdate();
            }
        }

        public static void LoadingStartedRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId))
            {
                return;
            }

            if (!TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            if (!BeginLoading(waitRoom))
            {
                return;
            }

            if (HasLoadingCompleted(waitRoom.RoomId, playerId))
            {
                return;
            }

            bool changed = false;
            lock (waitRoom)
            {
                if (waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    player.IsReady = false;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            BroadcastLoadingNotification(waitRoom, new JObject
            {
                ["MessageType"] = MessageType.LoadingStartedNotification,
                ["PlayerID"] = playerId,
                ["Progress"] = 0f,
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            });

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void LoadingCompletedRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId))
            {
                return;
            }

            if (!TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            var completion = MarkLoadingCompleted(waitRoom, playerId);
            if (!completion.Accepted)
            {
                return;
            }

            bool changed = false;
            lock (waitRoom)
            {
                if (waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    player.IsReady = true;
                    changed = true;
                }
            }

            if (changed)
            {
                BroadcastLoadingNotification(waitRoom, new JObject
                {
                    ["MessageType"] = MessageType.LoadingCompletedNotification,
                    ["PlayerID"] = playerId,
                    ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
                });
            }

            if (completion.AllPlayersCompleted && waitRoom.AllPlayers().Count > 0)
            {
                // S2: entry is approved, so the match room can actually start
                // running. Without this the game loop skipped the room because
                // Playing was never set.
                if (MatchRoomManager.Instance.BeginMatchForWaitRoom(waitRoom))
                {
                    BroadcastAllowEnterMap(waitRoom);
                }
                else
                {
                    FailPendingMatchStart(waitRoom);
                }
            }

            if (changed)
            {
                BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
                LobbyEventHandler.BroadcastRoomListUpdate();
            }
        }

        public static void LoadingProgressRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId) ||
                !TryResolveWaitRoom(dic, playerId, out var waitRoom) || waitRoom == null)
            {
                return;
            }

            BroadcastLoadingNotification(waitRoom, new JObject
            {
                ["MessageType"] = MessageType.LoadingProgressNotification,
                ["PlayerID"] = playerId,
                ["Progress"] = ReadLoadingProgress(dic)
            });
        }

        public static void ChangeRoomSetting(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (session is null)
            {
                return;
            }

            var roomSetting = WaitRoomSetting.FromDictionary(dic);
            var roomId = !string.IsNullOrWhiteSpace(roomSetting.RoomId)
                ? roomSetting.RoomId
                : JsonHelper.GetStringOrNull(dic, "RoomId");

            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.InvalidRoomId, string.Empty));
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            waitRoom ??= MissionWaitRoomManager.Instance.FindMissionRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            if (!string.Equals(waitRoom.GetFirstPlayerId(), session.PlayerID, StringComparison.OrdinalIgnoreCase))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.RoomSettingChanged,
                    ["RoomId"] = roomId,
                    ["RoomID"] = roomId,
                    ["Success"] = false,
                    ["Error"] = "Only the room owner can change settings",
                    ["ErrorMessage"] = "Only the room owner can change settings"
                });
                return;
            }

            roomSetting.ApplyTo(waitRoom);

            var roomInfoJson = BuildRoomInfoJson(waitRoom);
            var responseJson = new JObject
            {
                ["MessageType"] = MessageType.RoomSettingChanged,
                ["RoomId"] = roomId,
                ["RoomID"] = roomId,
                ["RoomInfo"] = roomInfoJson,
                ["Settings"] = BuildPublicRoomSettingsJson(roomInfoJson),
                ["RoomName"] = roomInfoJson["RoomName"],
                ["Capacity"] = roomInfoJson["Capacity"],
                ["GameMode"] = roomInfoJson["GameMode"],
                ["Map"] = roomInfoJson["Map"],
                ["TeamBalance"] = roomInfoJson["TeamBalance"],
                ["HasPassword"] = roomInfoJson["HasPassword"],
                ["OwnerId"] = roomInfoJson["OwnerId"],
                ["PlayerCount"] = roomInfoJson["PlayerCount"],
                ["NowPlaying"] = roomInfoJson["NowPlaying"]
            };

            session.SendAsyncJsonWithTimeStamp(responseJson);

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void SendUpdateWaitRoom(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.InvalidRoomId, string.Empty));
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.WaitRoomUpdateNotification,
                ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
            });
        }

        public static void CloseRoomRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            if (string.IsNullOrWhiteSpace(roomId))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.InvalidRoomId, string.Empty));
                return;
            }

            var removed = WaitRoomManager.Instance().CloseRoom(roomId);
            if (removed)
            {
                ClearLoadingState(roomId);
                LobbyEventHandler.BroadcastRoomListUpdate();
            }
            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = removed ? MessageType.RoomDeleted : MessageType.RoomNotFound,
                ["RoomId"] = roomId
            });
        }

        public static void ExitRoomRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryGetSessionPlayerId(session, out var playerId))
            {
                return;
            }

            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");

            var waitRoom = string.IsNullOrWhiteSpace(roomId)
                ? FindWaitRoomByPlayerId(playerId)
                : WaitRoomManager.Instance().FindWaitRoom(roomId);
            var isMissionRoom = false;
            if (waitRoom == null)
            {
                waitRoom = string.IsNullOrWhiteSpace(roomId)
                    ? FindMissionRoomByPlayerId(playerId)
                    : MissionWaitRoomManager.Instance.FindMissionRoom(roomId);
                isMissionRoom = waitRoom != null;
            }
            if (waitRoom == null)
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            roomId = waitRoom.RoomId;
            var previousOwnerId = waitRoom.GetFirstPlayerId();
            if (!waitRoom.TryRemovePlayer(playerId, out _))
            {
                session.SendAsyncJsonWithTimeStamp(CreateRoomError(MessageType.RoomNotFound, roomId));
                return;
            }

            // A result-screen leave also removes the player from the active
            // MatchRoom. Wait-room membership alone is not enough: otherwise
            // the old UDP peer and server-owned combat state survive into the
            // player's next match.
            MatchServerV2.Instance.DisconnectPlayerSession(playerId);
            InGameMatchEventHandler.ClearPlayerState(playerId);

            session.SendAsyncJsonWithTimeStamp(new JObject
            {
                ["MessageType"] = MessageType.LeaveRoomResponse,
                ["RoomId"] = roomId,
                ["RoomID"] = roomId,
                ["PlayerId"] = playerId,
                ["PlayerID"] = playerId,
                ["Success"] = true
            });

            if (waitRoom.AllPlayers().Count == 0)
            {
                ClearLoadingState(roomId);
                if (isMissionRoom)
                {
                    MissionWaitRoomManager.Instance.CloseMissionRoom(roomId);
                }
                else
                {
                    WaitRoomManager.Instance().CloseRoom(roomId);
                }
                LobbyEventHandler.BroadcastRoomListUpdate();
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    ["MessageType"] = MessageType.RoomDeleted,
                    ["RoomId"] = roomId,
                    ["RoomID"] = roomId
                });
                return;
            }

            TryApproveLoadingAfterPlayerRemoved(waitRoom, playerId);

            var newOwnerId = waitRoom.GetFirstPlayerId();
            if (string.Equals(previousOwnerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(previousOwnerId, newOwnerId, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(newOwnerId))
            {
                var ownerChange = new JObject
                {
                    ["MessageType"] = MessageType.WaitRoomOwnerChange,
                    ["RoomId"] = roomId,
                    ["RoomID"] = roomId,
                    ["PreviousOwnerId"] = previousOwnerId,
                    ["NewOwnerId"] = newOwnerId,
                    ["OwnerId"] = newOwnerId,
                    ["RoomInfo"] = BuildRoomInfoJson(waitRoom)
                };

                foreach (var player in waitRoom.AllPlayers())
                {
                    LobbyServerManager.Instance.FindSessionByPlayerId(player.Id)?.SendAsyncJsonWithTimeStamp(ownerChange);
                }
            }

            BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
        }

        public static void ReadyRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            if (!TryGetSessionPlayerId(session, out var playerId))
            {
                return;
            }
            var type = dic.GetStringOrNull("MessageType");

            if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            var waitRoom = WaitRoomManager.Instance().FindWaitRoom(roomId);
            if (waitRoom == null)
            {
                return;
            }

            lock (waitRoom)
            {
                if (waitRoom.Players.TryGetValue(playerId, out var player))
                {
                    player.IsReady = string.Equals(type, MessageType.WaitRoomPlayerReady, StringComparison.OrdinalIgnoreCase);
                    BroadcastRoomUpdate(waitRoom, MessageType.WaitRoomUpdateNotification);
                }
            }
        }

        public static void KickPlayerRequest(in ClientSession session, IDictionary<string, JToken> dic)
        {
            var roomId = dic.GetStringOrNull("RoomId") ?? dic.GetStringOrNull("RoomID");
            var targetPlayerId = dic.GetStringOrNull("PlayerId") ?? dic.GetStringOrNull("PlayerID");
            var reason = dic.GetStringOrNull("Reason") ?? "Kicked by room owner";
            var room = WaitRoomManager.Instance().FindWaitRoom(roomId ?? string.Empty);

            if (room == null || string.IsNullOrWhiteSpace(targetPlayerId))
            {
                return;
            }

            if (!string.Equals(room.GetFirstPlayerId(), session?.PlayerID, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(room.GetFirstPlayerId(), targetPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!room.TryRemovePlayer(targetPlayerId, out _))
            {
                return;
            }

            var kickNotification = new JObject
            {
                ["MessageType"] = MessageType.WaitRoomKickPlayer,
                ["Success"] = true,
                ["PlayerID"] = targetPlayerId,
                ["RoomID"] = room.RoomId,
                ["Reason"] = reason
            };
            LobbyServerManager.Instance.FindSessionByPlayerId(targetPlayerId)?.SendAsyncJsonWithTimeStamp(kickNotification);
            BroadcastRoomUpdate(room, MessageType.WaitRoomUpdateNotification);
            LobbyEventHandler.BroadcastRoomListUpdate();
        }
    }
}
