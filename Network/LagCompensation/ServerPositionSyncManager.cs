using System;
using System.Collections.Generic;

namespace OpenGSServer.Network
{
    /// <summary>
    /// プレイヤー位置同期マネージャー
    /// 定期的当前位置情報をクライアントに送信する
    /// </summary>
    public class ServerPositionSyncManager
    {
        /// <summary>
        /// 同期間隔（秒）- デフォルト10Hz (100ms)
        /// </summary>
        public float SyncInterval { get; set; } = 0.1f;

        /// <summary>
        /// 位置更新イベントハンドラ
        /// </summary>
        public event Action<string, PlayerPositionUpdateEvent>? OnPositionUpdateSent;

        /// <summary>
        /// クライアントUDP送信コールバック
        /// </summary>
        private readonly Dictionary<string, Action<byte[]>> m_ClientUdpSenders = new Dictionary<string, Action<byte[]>>();

        /// <summary>
        /// プレイヤー位置データ
        /// </summary>
        private class PlayerPositionData
        {
            public float PosX, PosY, PosZ;
            public float RotY;
            public float VelX, VelY, VelZ;
            public byte SequenceNumber;
            public DateTime LastUpdate;
        }

        private readonly Dictionary<string, PlayerPositionData> m_PlayerPositions = new Dictionary<string, PlayerPositionData>();

        private float m_ElapsedTime = 0f;
        private bool m_IsEnabled = false;
        private int m_SyncCount = 0;

        /// <summary>
        /// 同期を有効にする
        /// </summary>
        public void Enable()
        {
            m_IsEnabled = true;
            m_ElapsedTime = 0f;
            Log.Info($"[PositionSync] Enabled with interval {SyncInterval}s");
        }

        /// <summary>
        /// 同期を無効にする
        /// </summary>
        public void Disable()
        {
            m_IsEnabled = false;
            Log.Info($"[PositionSync] Disabled. Total syncs: {m_SyncCount}");
        }

        /// <summary>
        /// プレイヤーを登録する
        /// </summary>
        public void RegisterPlayer(string playerId)
        {
            if (!m_PlayerPositions.ContainsKey(playerId))
            {
                m_PlayerPositions[playerId] = new PlayerPositionData();
            }
        }

        /// <summary>
        /// プレイヤーを削除する
        /// </summary>
        public void UnregisterPlayer(string playerId)
        {
            m_PlayerPositions.Remove(playerId);
            m_ClientUdpSenders.Remove(playerId);
        }

        /// <summary>
        /// クライアントのUDP送信コールバックを登録する
        /// </summary>
        public void RegisterClientSender(string clientId, Action<byte[]> sender)
        {
            m_ClientUdpSenders[clientId] = sender;
        }

        /// <summary>
        /// クライアントのUDP送信コールバックを解除する
        /// </summary>
        public void UnregisterClientSender(string clientId)
        {
            m_ClientUdpSenders.Remove(clientId);
        }

        /// <summary>
        /// プレイヤー位置を更新する（サーバー側で予測・計算した位置）
        /// </summary>
        public void UpdatePlayerPosition(string playerId, float x, float y, float z, float rotY, float vx, float vy, float vz)
        {
            if (m_PlayerPositions.TryGetValue(playerId, out var data))
            {
                data.PosX = x;
                data.PosY = y;
                data.PosZ = z;
                data.RotY = rotY;
                data.VelX = vx;
                data.VelY = vy;
                data.VelZ = vz;
                data.SequenceNumber = (byte)((data.SequenceNumber + 1) % 256);
                data.LastUpdate = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// クライアントから受信した位置情報を更新する（信任クライアントの位置）
        /// </summary>
        public void UpdatePlayerPositionFromClient(string playerId, PlayerPositionUpdateEvent clientEvent)
        {
            if (m_PlayerPositions.TryGetValue(playerId, out var data))
            {
                // クライアントが送信してきた位置を採用
                data.PosX = clientEvent.PositionX;
                data.PosY = clientEvent.PositionY;
                data.PosZ = clientEvent.PositionZ;
                data.RotY = clientEvent.RotationY;
                data.VelX = clientEvent.VelocityX;
                data.VelY = clientEvent.VelocityY;
                data.VelZ = clientEvent.VelocityZ;
                data.SequenceNumber = clientEvent.SequenceNumber;
                data.LastUpdate = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// 更新（毎フレーム呼び出し）
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!m_IsEnabled) return;

            m_ElapsedTime += deltaTime;

            if (m_ElapsedTime >= SyncInterval)
            {
                BroadcastPositions();
                m_ElapsedTime = 0f;
            }
        }

        /// <summary>
        /// 全プレイヤーの位置をブロードキャストする
        /// </summary>
        private void BroadcastPositions()
        {
            foreach (var kvp in m_PlayerPositions)
            {
                string playerId = kvp.Key;
                var data = kvp.Value;

                // 位置更新イベントを作成
                var positionEvent = new PlayerPositionUpdateEvent
                {
                    PlayerID = playerId,
                    PositionX = data.PosX,
                    PositionY = data.PosY,
                    PositionZ = data.PosZ,
                    RotationY = data.RotY,
                    VelocityX = data.VelX,
                    VelocityY = data.VelY,
                    VelocityZ = data.VelZ,
                    SequenceNumber = data.SequenceNumber,
                    Timestamp = (float)(DateTime.UtcNow - data.LastUpdate).TotalSeconds
                };

                // イベントをJSONに変換
                var json = positionEvent.ToJson();
                var jsonString = json.ToString();
                var bytes = System.Text.Encoding.UTF8.GetBytes(jsonString);

                // 全クライアントに送信
                foreach (var sender in m_ClientUdpSenders)
                {
                    try
                    {
                        sender.Value?.Invoke(bytes);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[PositionSync] Send to client {sender.Key} failed: {ex.Message}");
                    }
                }

                OnPositionUpdateSent?.Invoke(playerId, positionEvent);
            }

            m_SyncCount++;
        }

        /// <summary>
        /// 同期回数を取得
        /// </summary>
        public int SyncCount => m_SyncCount;

        /// <summary>
        /// 登録プレイヤー数を取得
        /// </summary>
        public int PlayerCount => m_PlayerPositions.Count;

        /// <summary>
        /// 同期間隔を設定する
        /// </summary>
        public void SetSyncRate(float hz)
        {
            if (hz > 0)
            {
                SyncInterval = 1.0f / hz;
            }
        }

        /// <summary>
        /// 統計情報を取得（デバッグ用）
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[PositionSync] Players:{m_PlayerPositions.Count} " +
                   $"Interval:{SyncInterval:F3}s " +
                   $"Rate:{1.0f/SyncInterval:F1}Hz " +
                   $"Syncs:{m_SyncCount}";
        }
    }
}
