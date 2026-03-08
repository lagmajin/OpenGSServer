using System;
using System.Collections.Generic;

namespace OpenGSServer.Network
{
    /// <summary>
    /// サーバーサイド予測システム
    /// クライアント入力をサーバー側でシミュレートし、不正 检测を行う
    /// </summary>
    public class ServerPlayerStateManager
    {
        /// <summary>
        /// プレイヤーのサーバー状態を保持する構造体
        /// </summary>
        private class PlayerServerState
        {
            public string PlayerId = string.Empty;
            public float PosX, PosY, PosZ;
            public float RotX, RotY, RotZ, RotW;
            public float VelX, VelY, VelZ;
            public float LastInputTimestamp;
            public byte LastProcessedSequence;
            public Queue<ClientInputData> InputQueue = new Queue<ClientInputData>();
            public DateTime LastUpdateTime = DateTime.UtcNow;
            public int PendingInputs => InputQueue.Count;
        }

        private readonly Dictionary<string, PlayerServerState> m_PlayerStates = new Dictionary<string, PlayerServerState>();

        /// <summary>最大入力キューサイズ</summary>
        private const int MaxInputQueueSize = 128;

        /// <summary>許容される最大的位置誤差</summary>
        private const float MaxPositionTolerance = 2.0f;

        /// <summary>
        /// プレイヤーを登録する
        /// </summary>
        public void RegisterPlayer(string playerId)
        {
            if (!m_PlayerStates.ContainsKey(playerId))
            {
                m_PlayerStates[playerId] = new PlayerServerState
                {
                    PlayerId = playerId,
                    PosX = 0,
                    PosY = 0,
                    PosZ = 0,
                    RotX = 0,
                    RotY = 0,
                    RotZ = 0,
                    RotW = 1
                };
            }
        }

        /// <summary>
        /// プレイヤーを削除する
        /// </summary>
        public void UnregisterPlayer(string playerId)
        {
            m_PlayerStates.Remove(playerId);
        }

        /// <summary>
        /// クライアントからの入力をキューに追加する
        /// </summary>
        public void QueueClientInput(ClientInputData input)
        {
            if (m_PlayerStates.TryGetValue(input.PlayerId, out var state))
            {
                // シーケンスが古ければスキップ
                if (IsSequenceNewer(input.SequenceNumber, state.LastProcessedSequence))
                {
                    state.InputQueue.Enqueue(input);

                    // キューサイズ制限
                    while (state.InputQueue.Count > MaxInputQueueSize)
                    {
                        state.InputQueue.Dequeue();
                    }
                }
            }
        }

        /// <summary>
        /// 全てのプレイヤーの入力を処理する
        /// </summary>
        public void ProcessAllInputs()
        {
            foreach (var kvp in m_PlayerStates)
            {
                ProcessPlayerInputs(kvp.Value);
            }
        }

        /// <summary>
        /// 特定のプレイヤーの入力を処理する
        /// </summary>
        public void ProcessPlayerInputs(string playerId)
        {
            if (m_PlayerStates.TryGetValue(playerId, out var state))
            {
                ProcessPlayerInputs(state);
            }
        }

        /// <summary>
        /// プレイヤー入力を処理する
        /// </summary>
        private void ProcessPlayerInputs(PlayerServerState state)
        {
            while (state.InputQueue.Count > 0)
            {
                var input = state.InputQueue.Dequeue();

                // 予測移動を適用（実際のゲームロジックに置き換えが必要）
                ApplyServerMovement(state, input);

                state.LastProcessedSequence = input.SequenceNumber;
                state.LastInputTimestamp = input.Timestamp;
            }
        }

        /// <summary>
        /// サーバー側の移動を適用（プレースホルダー実装）
        /// 実際のサーバーサイド物理演算に置き換えること
        /// </summary>
        private void ApplyServerMovement(PlayerServerState state, ClientInputData input)
        {
            // TODO: 実際のサーバーサイド移動ロジックを実装
            // ここでは簡易的な移動のみ

            float moveSpeed = 10f; // 移動速度
            float newPosX = state.PosX + input.MoveX * moveSpeed * input.DeltaTime;
            float newPosY = state.PosY + input.MoveY * moveSpeed * input.DeltaTime;
            float newPosZ = state.PosZ + input.MoveZ * moveSpeed * input.DeltaTime;

            state.PosX = newPosX;
            state.PosY = newPosY;
            state.PosZ = newPosZ;

            state.RotX = input.LookX;
            state.RotY = input.LookY;
        }

        /// <summary>
        /// クライアントの現在位置と предполагаемую位置を比較する
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="clientPosition">クライアントが主張する位置</param>
        /// <returns>許容範囲内かどうか</returns>
        public bool ValidateClientPosition(string playerId, float clientX, float clientY, float clientZ)
        {
            if (!m_PlayerStates.TryGetValue(playerId, out var state))
            {
                return true; // 未知のプレイヤーは OK
            }

            float dx = state.PosX - clientX;
            float dy = state.PosY - clientY;
            dz = state.PosZ - clientZ;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            return distance <= MaxPositionTolerance;
        }

        /// <summary>
        /// プレイヤーの現在のサーバー状態をシリアライズして送信用にする
        /// </summary>
        public ServerTransformState GetPlayerState(string playerId)
        {
            if (m_PlayerStates.TryGetValue(playerId, out var state))
            {
                return new ServerTransformState
                {
                    PlayerId = playerId,
                    PositionX = state.PosX,
                    PositionY = state.PosY,
                    PositionZ = state.PosZ,
                    RotationX = state.RotX,
                    RotationY = state.RotY,
                    RotationZ = state.RotZ,
                    RotationW = state.RotW,
                    VelocityX = state.VelX,
                    VelocityY = state.VelY,
                    VelocityZ = state.VelZ,
                    Timestamp = (float)(DateTime.UtcNow - state.LastUpdateTime).TotalSeconds,
                    SequenceNumber = state.LastProcessedSequence
                };
            }

            return default;
        }

        /// <summary>
        /// プレイヤーの位置を直接設定する（リスポーン時など）
        /// </summary>
        public void SetPlayerPosition(string playerId, float x, float y, float z)
        {
            if (m_PlayerStates.TryGetValue(playerId, out var state))
            {
                state.PosX = x;
                state.PosY = y;
                state.PosZ = z;
            }
        }

        /// <summary>
        /// シーケンス番号が新しいかどうかを比較
        /// </summary>
        private bool IsSequenceNewer(byte newSeq, byte oldSeq)
        {
            return (byte)(newSeq - oldSeq) < 128;
        }

        /// <summary>
        /// 全てのプレイヤーデータをクリア
        /// </summary>
        public void ClearAll()
        {
            m_PlayerStates.Clear();
        }

        /// <summary>
        /// 統計情報を取得（デバッグ用）
        /// </summary>
        public string GetDebugInfo()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"[ServerPlayerState] Players: {m_PlayerStates.Count}");

            foreach (var kvp in m_PlayerStates)
            {
                sb.AppendLine($"  Player:{kvp.Key} Seq:{kvp.Value.LastProcessedSequence} Pos:({kvp.Value.PosX:F2},{kvp.Value.PosY:F2},{kvp.Value.PosZ:F2}) Pending:{kvp.Value.PendingInputs}");
            }

            return sb.ToString();
        }
    }
}
