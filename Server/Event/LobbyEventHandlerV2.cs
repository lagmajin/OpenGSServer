using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// ロビーイベントハンドラー - クライアントからのロビー関連リクエスト処理
    /// </summary>
    public class LobbyEventHandlerV2
    {
        private readonly LobbyServerManager lobbyManager = LobbyServerManager.Instance;

        /// <summary>
        /// ロビーイベントを処理
        /// </summary>
        public JObject ProcessLobbyEvent(JObject request)
        {
            var action = request.GetStringOrNull("Action");
            var playerId = request.GetStringOrNull("PlayerID");

            if (string.IsNullOrEmpty(action) || string.IsNullOrEmpty(playerId))
            {
                return CreateErrorResponse("Invalid request format");
            }

            try
            {
                switch (action)
                {
                    case "JoinLobby":
                        return HandleJoinLobby(request, playerId);

                    case "LeaveLobby":
                        return HandleLeaveLobby(playerId);

                    case "CreateRoom":
                        return HandleCreateRoom(request, playerId);

                    case "JoinRoom":
                        return HandleJoinRoom(request, playerId);

                    case "LeaveRoom":
                        return HandleLeaveRoom(playerId);

                    case "GetAvailableRooms":
                        return HandleGetAvailableRooms();

                    case "GetLobbyStats":
                        return HandleGetLobbyStats();

                    case "QuickMatch":
                        return HandleQuickMatch(request, playerId);

                    case "SendChat":
                        return HandleSendChat(request, playerId);

                    default:
                        return CreateErrorResponse($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[LOBBY] Error processing {action}: {ex.Message}", ConsoleColor.Red);
                return CreateErrorResponse("Internal server error");
            }
        }

        #region イベントハンドラー実装

        private JObject HandleJoinLobby(JObject request, string playerId)
        {
            var playerName = request.GetStringOrNull("PlayerName") ?? $"Player_{playerId}";

            var success = lobbyManager.PlayerJoinLobby(playerId, playerName);

            return new JObject
            {
                ["Action"] = "JoinLobbyResponse",
                ["Success"] = success,
                ["PlayerID"] = playerId,
                ["PlayerName"] = playerName,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleLeaveLobby(string playerId)
        {
            var success = lobbyManager.PlayerLeaveLobby(playerId);

            return new JObject
            {
                ["Action"] = "LeaveLobbyResponse",
                ["Success"] = success,
                ["PlayerID"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleCreateRoom(JObject request, string playerId)
        {
            var roomName = request.GetStringOrNull("RoomName");
            var gameModeStr = request.GetStringOrNull("GameMode");

            if (string.IsNullOrEmpty(roomName) || string.IsNullOrEmpty(gameModeStr))
            {
                return CreateErrorResponse("RoomName and GameMode are required");
            }

            if (!Enum.TryParse<EGameMode>(gameModeStr, out var gameMode))
            {
                return CreateErrorResponse("Invalid GameMode");
            }

            var room = lobbyManager.CreateRoom(roomName, playerId, gameMode);

            return new JObject
            {
                ["Action"] = "CreateRoomResponse",
                ["Success"] = room != null,
                ["Room"] = room != null ? JObject.FromObject(room) : null,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleJoinRoom(JObject request, string playerId)
        {
            var roomId = request.GetStringOrNull("RoomID");

            if (string.IsNullOrEmpty(roomId))
            {
                return CreateErrorResponse("RoomID is required");
            }

            var success = lobbyManager.JoinRoom(roomId, playerId);

            return new JObject
            {
                ["Action"] = "JoinRoomResponse",
                ["Success"] = success,
                ["RoomID"] = roomId,
                ["PlayerID"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleLeaveRoom(string playerId)
        {
            var success = lobbyManager.LeaveRoom(playerId);

            return new JObject
            {
                ["Action"] = "LeaveRoomResponse",
                ["Success"] = success,
                ["PlayerID"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleGetAvailableRooms()
        {
            var rooms = lobbyManager.GetAvailableRooms();

            return new JObject
            {
                ["Action"] = "GetAvailableRoomsResponse",
                ["Rooms"] = JArray.FromObject(rooms),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleGetLobbyStats()
        {
            var stats = lobbyManager.GetLobbyStats();

            return new JObject
            {
                ["Action"] = "GetLobbyStatsResponse",
                ["Stats"] = JObject.FromObject(stats),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleQuickMatch(JObject request, string playerId)
        {
            var gameModeStr = request.GetStringOrNull("GameMode");

            if (string.IsNullOrEmpty(gameModeStr))
            {
                return CreateErrorResponse("GameMode is required");
            }

            if (!Enum.TryParse<EGameMode>(gameModeStr, out var gameMode))
            {
                return CreateErrorResponse("Invalid GameMode");
            }

            var roomId = lobbyManager.QuickMatch(playerId, gameMode);

            return new JObject
            {
                ["Action"] = "QuickMatchResponse",
                ["Success"] = roomId != null,
                ["RoomID"] = roomId,
                ["PlayerID"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleSendChat(JObject request, string playerId)
        {
            var message = request.GetStringOrNull("Message");
            var roomId = request.GetStringOrNull("RoomID");

            if (string.IsNullOrEmpty(message))
            {
                return CreateErrorResponse("Message is required");
            }

            if (!string.IsNullOrEmpty(roomId))
            {
                // ルームチャット
                lobbyManager.AddRoomChat(roomId, playerId, message);
            }
            else
            {
                // ロビーチャット
                lobbyManager.AddLobbyChat(playerId, message);
            }

            return new JObject
            {
                ["Action"] = "SendChatResponse",
                ["Success"] = true,
                ["PlayerID"] = playerId,
                ["Message"] = message,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        #endregion

        #region ユーティリティメソッド

        private JObject CreateErrorResponse(string errorMessage)
        {
            return new JObject
            {
                ["Action"] = "ErrorResponse",
                ["Success"] = false,
                ["ErrorMessage"] = errorMessage,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        #endregion
    }
}