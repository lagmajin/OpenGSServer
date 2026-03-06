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

            var result = lobbyManager.PlayerJoinLobby(playerId, playerName);

            return new JObject
            {
                ["Action"] = "JoinLobbyResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
                ["PlayerID"] = playerId,
                ["PlayerName"] = playerName,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleLeaveLobby(string playerId)
        {
            var result = lobbyManager.PlayerLeaveLobby(playerId);

            return new JObject
            {
                ["Action"] = "LeaveLobbyResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
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

            var result = lobbyManager.CreateRoom(roomName, playerId, gameMode);

            var response = new JObject
            {
                ["Action"] = "CreateRoomResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            if (result.IsSuccess && result.Value != null)
            {
                response["Room"] = JObject.FromObject(result.Value);
            }
            else
            {
                response["Room"] = null;
            }

            return response;
        }

        private JObject HandleJoinRoom(JObject request, string playerId)
        {
            var roomId = request.GetStringOrNull("RoomID");

            if (string.IsNullOrEmpty(roomId))
            {
                return CreateErrorResponse("RoomID is required");
            }

            var result = lobbyManager.JoinRoom(roomId, playerId);

            return new JObject
            {
                ["Action"] = "JoinRoomResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
                ["RoomID"] = roomId,
                ["PlayerID"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleLeaveRoom(string playerId)
        {
            var result = lobbyManager.LeaveRoom(playerId);

            return new JObject
            {
                ["Action"] = "LeaveRoomResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
                ["PlayerID"] = playerId,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };
        }

        private JObject HandleGetAvailableRooms()
        {
            var result = lobbyManager.GetAvailableRooms();

            var response = new JObject
            {
                ["Action"] = "GetAvailableRoomsResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            if (result.IsSuccess && result.Value != null)
            {
                response["Rooms"] = JArray.FromObject(result.Value);
            }
            else
            {
                response["Rooms"] = new JArray();
            }

            return response;
        }

        private JObject HandleGetLobbyStats()
        {
            var result = lobbyManager.GetLobbyStats();

            var response = new JObject
            {
                ["Action"] = "GetLobbyStatsResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            if (result.IsSuccess && result.Value != null)
            {
                response["Stats"] = JObject.FromObject(result.Value);
            }
            else
            {
                response["Stats"] = null;
            }

            return response;
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

            var result = lobbyManager.QuickMatch(playerId, gameMode);

            return new JObject
            {
                ["Action"] = "QuickMatchResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
                ["RoomID"] = result.Value,
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

            LobbyResult<bool> result;
            if (!string.IsNullOrEmpty(roomId))
            {
                // ルームチャット
                result = lobbyManager.AddRoomChat(roomId, playerId, message);
            }
            else
            {
                // ロビーチャット
                result = lobbyManager.AddLobbyChat(playerId, message);
            }

            return new JObject
            {
                ["Action"] = "SendChatResponse",
                ["Success"] = result.IsSuccess,
                ["ErrorMessage"] = result.ErrorMessage,
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