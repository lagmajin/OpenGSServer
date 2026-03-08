using System;
using System.Collections.Generic;

namespace OpenGSServer.Network
{
    /// <summary>
    /// サーバー側からクライアントへの状態ブロードキャスト管理
    /// 一定間隔でプレイヤー状態をクライアントに送信する
    /// </summary>
    public class ServerStateBroadcaster
    {
        /// <summary>ブロードキャスト間隔（秒）</summary>
        private float m_BroadcastInterval = 0.033f; // ~30 FPS

        /// <summary>現在の経過時間</summary>
        private float m_ElapsedTime = 0f;

        /// <summary>プレイヤー状態マネージャー</summary>
        private readonly ServerPlayerStateManager m_PlayerStateManager;

        /// <summary>アクティブなクライアントセッション</summary>
        private readonly Dictionary<string, Action<ServerTransformState>> m_ClientCallbacks = new Dictionary<string, Action<ServerTransformState>>();

        /// <summary>ブロードキャスト回数</summary>
        private int m_BroadcastCount = 0;

        /// <summary>最終ブロードキャスト時刻</summary>
        private DateTime m_LastBroadcastTime = DateTime.UtcNow;

        public ServerStateBroadcaster(ServerPlayerStateManager playerStateManager)
        {
            m_PlayerStateManager = playerStateManager;
        }

        /// <summary>
        /// ブロードキャスト間隔を設定する
        /// </summary>
        /// <param name="intervalSeconds">間隔（秒）</param>
        public void SetBroadcastInterval(float intervalSeconds)
        {
            m_BroadcastInterval = Math.Max(0.01f, intervalSeconds);
        }

        /// <summary>
        /// クライアントコールバックを登録する
        /// </summary>
        /// <param name="clientId">クライアントID</param>
        /// <param name="callback">状態送信コールバック</param>
        public void RegisterClient(string clientId, Action<ServerTransformState> callback)
        {
            if (!m_ClientCallbacks.ContainsKey(clientId))
            {
                m_ClientCallbacks[clientId] = callback;
            }
            else
            {
                m_ClientCallbacks[clientId] = callback;
            }
        }

        /// <summary>
        /// クライアントコールバックを解除する
        /// </summary>
        public void UnregisterClient(string clientId)
        {
            m_ClientCallbacks.Remove(clientId);
        }

        /// <summary>
        /// ブロードキャストを更新する（毎フレーム呼び出し）
        /// </summary>
        /// <param name="deltaTime">フレーム間経過時間</param>
        public void Update(float deltaTime)
        {
            m_ElapsedTime += deltaTime;

            if (m_ElapsedTime >= m_BroadcastInterval)
            {
                BroadcastState();
                m_ElapsedTime = 0f;
            }
        }

        /// <summary>
        /// プレイヤー状態をブロードキャストする
        /// </summary>
        private void BroadcastState()
        {
            // 全プレイヤーの状態を取得して送信
            foreach (var callback in m_ClientCallbacks)
            {
                string clientId = callback.Key;
                var state = m_PlayerStateManager.GetPlayerState(clientId);

                try
                {
                    callback.Value?.Invoke(state);
                }
                catch (Exception ex)
                {
                    Log.Warning($"Broadcast to client {clientId} failed: {ex.Message}");
                }
            }

            m_BroadcastCount++;
            m_LastBroadcastTime = DateTime.UtcNow;
        }

        /// <summary>
        /// ブロードキャストを強制的に実行する
        /// </summary>
        public void ForceBroadcast()
        {
            BroadcastState();
        }

        /// <summary>
        /// 現在のブロードキャスト回数を取得
        /// </summary>
        public int BroadcastCount => m_BroadcastCount;

        /// <summary>
        /// 現在のブロードキャスト間隔（秒）を取得
        /// </summary>
        public float BroadcastInterval => m_BroadcastInterval;

        /// <summary>
        /// 登録されているクライアント数を取得
        /// </summary>
        public int ClientCount => m_ClientCallbacks.Count;

        /// <summary>
        /// 統計情報を取得（デバッグ用）
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[ServerBroadcaster] Clients:{m_ClientCallbacks.Count} " +
                   $"Interval:{m_BroadcastInterval:F3}s " +
                   $"Broadcasts:{m_BroadcastCount}";
        }
    }
}
