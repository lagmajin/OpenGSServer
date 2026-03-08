using System;

namespace OpenGSServer.Network
{
    /// <summary>
    /// サーバーサイドラグ補償管理クラス
    /// プレイヤー状態の予測、不正 检测、状態送信を統合管理
    /// </summary>
    public class ServerLagCompensationManager
    {
        /// <summary>プレイヤー状態マネージャー</summary>
        private readonly ServerPlayerStateManager m_PlayerStateManager;

        /// <summary>状態ブロードキャスター</summary>
        private readonly ServerStateBroadcaster m_StateBroadcaster;

        /// <summary>有効かどうか</summary>
        private bool m_IsEnabled = true;

        /// <summary>マッチID</summary>
        private string m_MatchId = string.Empty;

        public ServerLagCompensationManager()
        {
            m_PlayerStateManager = new ServerPlayerStateManager();
            m_StateBroadcaster = new ServerStateBroadcaster(m_PlayerStateManager);
        }

        /// <summary>
        /// マッチを開始する
        /// </summary>
        public void StartMatch(string matchId)
        {
            m_MatchId = matchId;
            m_PlayerStateManager.ClearAll();
            m_IsEnabled = true;
            Log.Info($"[ServerLagCompensation] Match {matchId} started");
        }

        /// <summary>
        /// マッチを終了する
        /// </summary>
        public void EndMatch()
        {
            m_IsEnabled = false;
            m_PlayerStateManager.ClearAll();
            Log.Info($"[ServerLagCompensation] Match {m_MatchId} ended");
        }

        /// <summary>
        /// プレイヤーをマッチに参加させる
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void AddPlayer(string playerId)
        {
            m_PlayerStateManager.RegisterPlayer(playerId);
            Log.Info($"[ServerLagCompensation] Player {playerId} added to match {m_MatchId}");
        }

        /// <summary>
        /// プレイヤーをマッチから退出させる
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        public void RemovePlayer(string playerId)
        {
            m_PlayerStateManager.UnregisterPlayer(playerId);
            m_StateBroadcaster.UnregisterClient(playerId);
            Log.Info($"[ServerLagCompensation] Player {playerId} removed from match {m_MatchId}");
        }

        /// <summary>
        /// クライアントからの入力を処理する
        /// </summary>
        /// <param name="input">クライアント入力データ</param>
        public void ProcessClientInput(ClientInputData input)
        {
            if (!m_IsEnabled) return;

            // 入力をキューに追加
            m_PlayerStateManager.QueueClientInput(input);
        }

        /// <summary>
        /// サーバープレイヤー位置の妥当性を検証する
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="clientX">クライアント主張X座標</param>
        /// <param name="clientY">クライアント主張Y座標</param>
        /// <param name="clientZ">クライアント主張Z座標</param>
        /// <returns>妥当かどうか</returns>
        public bool ValidatePlayerPosition(string playerId, float clientX, float clientY, float clientZ)
        {
            if (!m_IsEnabled) return true;

            return m_PlayerStateManager.ValidateClientPosition(playerId, clientX, clientY, clientZ);
        }

        /// <summary>
        /// 更新処理（毎フレーム呼び出し）
        /// </summary>
        /// <param name="deltaTime">フレーム間経過時間</param>
        public void Update(float deltaTime)
        {
            if (!m_IsEnabled) return;

            // 入力処理
            m_PlayerStateManager.ProcessAllInputs();

            // 状態ブロードキャスト
            m_StateBroadcaster.Update(deltaTime);
        }

        /// <summary>
        /// ブロードキャスト間隔を設定する
        /// </summary>
        /// <param name="intervalSeconds">間隔（秒）</param>
        public void SetBroadcastRate(float intervalSeconds)
        {
            m_StateBroadcaster.SetBroadcastInterval(intervalSeconds);
        }

        /// <summary>
        /// クライアントに状態送信コールバックを登録する
        /// </summary>
        /// <param name="clientId">クライアントID</param>
        /// <param name="callback">送信コールバック</param>
        public void RegisterClientCallback(string clientId, Action<ServerTransformState> callback)
        {
            m_StateBroadcaster.RegisterClient(clientId, callback);
        }

        /// <summary>
        /// プレイヤーの位置を直接設定する（リスポーン時など）
        /// </summary>
        public void SetPlayerPosition(string playerId, float x, float y, float z)
        {
            m_PlayerStateManager.SetPlayerPosition(playerId, x, y, z);
        }

        /// <summary>
        /// プレイヤーの現在状態を取得する
        /// </summary>
        public ServerTransformState GetPlayerState(string playerId)
        {
            return m_PlayerStateManager.GetPlayerState(playerId);
        }

        /// <summary>
        /// 統計情報を取得（デバッグ用）
        /// </summary>
        public string GetDebugInfo()
        {
            return $"[ServerLagCompensation] Match:{m_MatchId} Enabled:{m_IsEnabled}\n" +
                   $"  {m_PlayerStateManager.GetDebugInfo()}\n" +
                   $"  {m_StateBroadcaster.GetDebugInfo()}";
        }
    }
}
