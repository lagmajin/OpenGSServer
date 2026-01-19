using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    /// <summary>
    /// UDPベースのリアルタイムゲーム通信マネージャー
    /// </summary>
    public class MatchRUdpServerManager
    {
        private static MatchRUdpServerManager? instance;
        private NetManager? netManager;
        private EventBasedNetListener? listener;
        private Dictionary<string, NetPeer> connectedPlayers = new();
        private Dictionary<string, string> playerRoomMapping = new();

        public static MatchRUdpServerManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new MatchRUdpServerManager();
                }
                return instance;
            }
        }

        private MatchRUdpServerManager()
        {
            InitializeUdpServer();
        }

        private void InitializeUdpServer()
        {
            listener = new EventBasedNetListener();
            netManager = new NetManager(listener);

            // イベントハンドラーの設定
            listener.ConnectionRequestEvent += OnConnectionRequest;
            listener.PeerConnectedEvent += OnPeerConnected;
            listener.PeerDisconnectedEvent += OnPeerDisconnected;
            listener.NetworkReceiveEvent += OnNetworkReceive;

            // UDPサーバー設定
            netManager.Start(63000); // マッチ用UDPポート
            netManager.BroadcastReceiveEnabled = true;
            netManager.UpdateTime = 15; // 15ms更新

            ConsoleWrite.WriteMessage("UDP Game Server started on port 63000", ConsoleColor.Cyan);
        }

        #region UDPイベントハンドラー

        private void OnConnectionRequest(ConnectionRequest request)
        {
            // 接続要求を常に受け入れる（実際の運用では認証が必要）
            request.Accept();
        }

        private void OnPeerConnected(NetPeer peer)
        {
            ConsoleWrite.WriteMessage($"UDP Peer connected: {peer.EndPoint}", ConsoleColor.Green);

            // 接続成功メッセージを送信
            var connectMessage = new JObject
            {
                ["MessageType"] = "UdpConnectionEstablished",
                ["PlayerID"] = peer.Id.ToString(),
                ["Timestamp"] = DateTime.UtcNow.ToString("o")
            };

            SendToPeer(peer, connectMessage);
        }

        private void OnPeerDisconnected(NetPeer peer, DisconnectInfo info)
        {
            ConsoleWrite.WriteMessage($"UDP Peer disconnected: {peer.EndPoint}", ConsoleColor.Red);

            // プレイヤーマッピングから削除
            RemovePlayerMapping(peer);
        }

        private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                // UDPパケットを処理
                var data = reader.GetRemainingBytes();
                var jsonString = System.Text.Encoding.UTF8.GetString(data);

                // JSONとしてパースを試行
                if (TryParseJson(jsonString, out JObject json))
                {
                    // JSONメッセージとして処理
                    ProcessUdpMessage(peer, json);
                }
                else
                {
                    // バイナリデータとして処理（将来的な拡張用）
                    ProcessBinaryData(peer, data);
                }
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"UDP receive error: {ex.Message}", ConsoleColor.Red);
            }
        }

        #endregion

        #region メッセージ処理

        private void ProcessUdpMessage(NetPeer peer, JObject message)
        {
            var messageType = message.GetStringOrNull("MessageType");
            var playerId = message.GetStringOrNull("PlayerID");
            var roomId = message.GetStringOrNull("RoomID");

            if (string.IsNullOrEmpty(messageType) || string.IsNullOrEmpty(playerId))
                return;

            // プレイヤールームマッピングを更新
            if (!string.IsNullOrEmpty(roomId))
            {
                playerRoomMapping[playerId] = roomId;
            }

            // メッセージタイプに応じて処理
            switch (messageType)
            {
                case "PlayerPosition":
                    HandlePlayerPosition(peer, message, playerId);
                    break;

                case "PlayerAction":
                    HandlePlayerAction(peer, message, playerId);
                    break;

                case "GameEvent":
                    HandleGameEvent(peer, message, playerId);
                    break;

                case "Heartbeat":
                    HandleHeartbeat(peer, playerId);
                    break;

                default:
                    ConsoleWrite.WriteMessage($"Unknown UDP message type: {messageType}", ConsoleColor.Yellow);
                    break;
            }
        }

        private void ProcessBinaryData(NetPeer peer, byte[] data)
        {
            // 将来的なバイナリプロトコル拡張用
            ConsoleWrite.WriteMessage($"Binary data received: {data.Length} bytes", ConsoleColor.Gray);
        }

        #endregion

        #region ゲームイベント処理

        private void HandlePlayerPosition(NetPeer peer, JObject message, string playerId)
        {
            var position = message.GetValue("Position") as JObject;
            if (position != null)
            {
                // 位置情報をルーム内の全プレイヤーにブロードキャスト
                BroadcastToRoomExceptSender(playerId, message);
            }
        }

        private void HandlePlayerAction(NetPeer peer, JObject message, string playerId)
        {
            var actionType = message.GetStringOrNull("ActionType");

            switch (actionType)
            {
                case "Shoot":
                    HandlePlayerShot(peer, message, playerId);
                    break;

                case "Grenade":
                    HandlePlayerGrenade(peer, message, playerId);
                    break;

                case "Reload":
                    HandlePlayerReload(peer, message, playerId);
                    break;

                default:
                    // 不明なアクションはブロードキャスト
                    BroadcastToRoomExceptSender(playerId, message);
                    break;
            }
        }

        private void HandleGameEvent(NetPeer peer, JObject message, string playerId)
        {
            // ゲームイベントをMatchRoomHandlerに委譲
            InGameMatchEventHandler.HandleUdpGameEvent(
                System.Text.Encoding.UTF8.GetBytes(message.ToString()),
                playerId
            );
        }

        private void HandleHeartbeat(NetPeer peer, string playerId)
        {
            // ハートビート応答
            var response = new JObject
            {
                ["MessageType"] = "HeartbeatResponse",
                ["PlayerID"] = playerId,
                ["ServerTime"] = DateTime.UtcNow.ToString("o")
            };

            SendToPeer(peer, response);
        }

        private void HandlePlayerShot(NetPeer peer, JObject message, string playerId)
        {
            // 射撃イベントを処理し、ヒット判定を行う
            var targetId = message.GetStringOrNull("TargetID");
            var weaponType = message.GetStringOrNull("WeaponType");

            if (!string.IsNullOrEmpty(targetId))
            {
                // ダメージ計算と適用
                var damage = CalculateWeaponDamage(weaponType);
                var damageMessage = new JObject
                {
                    ["MessageType"] = "PlayerDamaged",
                    ["PlayerID"] = targetId,
                    ["Damage"] = damage,
                    ["AttackerID"] = playerId,
                    ["Timestamp"] = DateTime.UtcNow.ToString("o")
                };

                // ターゲットにダメージ通知
                SendToPlayer(targetId, damageMessage);
            }

            // 全プレイヤーに射撃イベントをブロードキャスト
            BroadcastToRoomExceptSender(playerId, message);
        }

        private void HandlePlayerGrenade(NetPeer peer, JObject message, string playerId)
        {
            // グレネードイベントをブロードキャスト
            BroadcastToRoomExceptSender(playerId, message);
        }

        private void HandlePlayerReload(NetPeer peer, JObject message, string playerId)
        {
            // リロードイベントをブロードキャスト
            BroadcastToRoomExceptSender(playerId, message);
        }

        #endregion

        #region ユーティリティメソッド

        private bool TryParseJson(string jsonString, out JObject json)
        {
            try
            {
                json = JObject.Parse(jsonString);
                return true;
            }
            catch
            {
                json = null;
                return false;
            }
        }

        private int CalculateWeaponDamage(string? weaponType)
        {
            return weaponType switch
            {
                "Pistol" => 25,
                "Rifle" => 35,
                "Sniper" => 80,
                "Shotgun" => 20,
                "SMG" => 15,
                _ => 30
            };
        }

        #endregion

        #region 送信メソッド

        public void SendToPlayer(string playerId, JObject message)
        {
            if (connectedPlayers.TryGetValue(playerId, out NetPeer peer))
            {
                SendToPeer(peer, message);
            }
        }

        public void BroadcastToRoom(string roomId, JObject message)
        {
            foreach (var kvp in playerRoomMapping)
            {
                if (kvp.Value == roomId && connectedPlayers.TryGetValue(kvp.Key, out NetPeer peer))
                {
                    SendToPeer(peer, message);
                }
            }
        }

        public void BroadcastToRoomExceptSender(string senderPlayerId, JObject message)
        {
            if (playerRoomMapping.TryGetValue(senderPlayerId, out string roomId))
            {
                foreach (var kvp in playerRoomMapping)
                {
                    if (kvp.Value == roomId && kvp.Key != senderPlayerId &&
                        connectedPlayers.TryGetValue(kvp.Key, out NetPeer peer))
                    {
                        SendToPeer(peer, message);
                    }
                }
            }
        }

        private void SendToPeer(NetPeer peer, JObject message)
        {
            try
            {
                var jsonString = message.ToString();
                var writer = new NetDataWriter();
                writer.Put(jsonString);
                peer.Send(writer, DeliveryMethod.Unreliable); // UDPなのでUnreliable
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"UDP send error: {ex.Message}", ConsoleColor.Red);
            }
        }

        #endregion

        #region プレイヤー管理

        public void RegisterPlayer(string playerId, NetPeer peer)
        {
            connectedPlayers[playerId] = peer;
        }

        public void UnregisterPlayer(string playerId)
        {
            connectedPlayers.Remove(playerId);
            playerRoomMapping.Remove(playerId);
        }

        private void RemovePlayerMapping(NetPeer peer)
        {
            List<string> playersToRemove = new();
            foreach (var kvp in connectedPlayers)
            {
                if (kvp.Value == peer)
                {
                    playersToRemove.Add(kvp.Key);
                }
            }

            foreach (var playerId in playersToRemove)
            {
                UnregisterPlayer(playerId);
            }
        }

        #endregion

        #region ライフサイクル

        public void Update()
        {
            netManager?.PollEvents();
        }

        public void Shutdown()
        {
            netManager?.Stop();
            connectedPlayers.Clear();
            playerRoomMapping.Clear();
        }

        #endregion
    }
}