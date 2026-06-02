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
        private const float MaxInputDeltaTime = 0.25f;
        private const float MaxMovementMagnitude = 1.15f;
        private const float MaxHorizontalSpeed = 10f;
        private const float MaxVerticalSpeed = 12f;
        private const float MoveAcceleration = 30f;
        private const float MoveDrag = 18f;
        private const float Gravity = 24f;
        private const float JumpImpulse = 8.5f;
        private const float GroundHeight = 0f;
        private const float PositionTolerance = 2.0f;
        private const float TeleportAllowancePerSecond = 18f;
        private const float ViolationClampThreshold = 3.0f;

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
            public float LastClientTimestamp;
            public byte LastProcessedSequence;
            public Queue<ClientInputData> InputQueue = new Queue<ClientInputData>();
            public DateTime LastUpdateTime = DateTime.UtcNow;
            public bool IsGrounded = true;
            public float ViolationScore = 0f;
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
            if (string.IsNullOrWhiteSpace(playerId) || m_PlayerStates.ContainsKey(playerId))
            {
                return;
            }

            m_PlayerStates[playerId] = new PlayerServerState
            {
                PlayerId = playerId,
                PosX = 0,
                PosY = 0,
                PosZ = GroundHeight,
                RotX = 0,
                RotY = 0,
                RotZ = 0,
                RotW = 1,
                IsGrounded = true
            };
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
        public bool QueueClientInput(ClientInputData input, out string rejectionReason)
        {
            rejectionReason = string.Empty;

            if (string.IsNullOrWhiteSpace(input.PlayerId))
            {
                rejectionReason = "PlayerId is missing";
                return false;
            }

            if (m_PlayerStates.TryGetValue(input.PlayerId, out var state))
            {
                if (!IsFiniteInput(input))
                {
                    rejectionReason = "Input contains invalid numeric values";
                    RecordViolation(state, rejectionReason);
                    return false;
                }

                if (input.DeltaTime < 0f || input.DeltaTime > MaxInputDeltaTime * 2f)
                {
                    rejectionReason = $"DeltaTime out of range: {input.DeltaTime:F3}";
                    RecordViolation(state, rejectionReason);
                    return false;
                }

                // シーケンスが古ければスキップ
                if (IsSequenceNewer(input.SequenceNumber, state.LastProcessedSequence))
                {
                    NormalizeClientInput(ref input, out var clampedReason);
                    if (!string.IsNullOrWhiteSpace(clampedReason))
                    {
                        rejectionReason = clampedReason;
                    }

                    state.InputQueue.Enqueue(input);

                    // キューサイズ制限
                    while (state.InputQueue.Count > MaxInputQueueSize)
                    {
                        state.InputQueue.Dequeue();
                    }

                    return true;
                }

                rejectionReason = $"Stale sequence ignored: {input.SequenceNumber} <= {state.LastProcessedSequence}";
                return false;
            }

            rejectionReason = $"Unknown player '{input.PlayerId}'";
            return false;
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
                state.LastClientTimestamp = input.Timestamp;
                state.LastUpdateTime = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// サーバー側の移動を適用（プレースホルダー実装）
        /// 実際のサーバーサイド物理演算に置き換えること
        /// </summary>
        private void ApplyServerMovement(PlayerServerState state, ClientInputData input)
        {
            var dt = Clamp(input.DeltaTime, 0f, MaxInputDeltaTime);
            if (dt <= 0f)
            {
                return;
            }

            var startX = state.PosX;
            var startY = state.PosY;
            var startZ = state.PosZ;

            var moveMagnitude = MathF.Sqrt(input.MoveX * input.MoveX + input.MoveY * input.MoveY + input.MoveZ * input.MoveZ);
            var normalizedMoveX = input.MoveX;
            var normalizedMoveY = input.MoveY;
            var normalizedMoveZ = input.MoveZ;
            if (moveMagnitude > 1f)
            {
                var inv = 1f / moveMagnitude;
                normalizedMoveX *= inv;
                normalizedMoveY *= inv;
                normalizedMoveZ *= inv;
                RecordViolation(state, "Movement vector was clamped");
            }

            var targetVelX = normalizedMoveX * MaxHorizontalSpeed;
            var targetVelY = normalizedMoveY * MaxHorizontalSpeed;
            var targetVelZ = normalizedMoveZ * MaxVerticalSpeed;

            state.VelX = MoveTowards(state.VelX, targetVelX, MoveAcceleration * dt);
            state.VelY = MoveTowards(state.VelY, targetVelY, MoveAcceleration * dt);

            if (input.Jump && state.IsGrounded)
            {
                state.VelZ = MathF.Max(state.VelZ, JumpImpulse);
                state.IsGrounded = false;
            }

            if (!state.IsGrounded)
            {
                state.VelZ -= Gravity * dt;
            }

            state.VelZ = MoveTowards(state.VelZ, targetVelZ, MoveAcceleration * dt);

            if (!input.Jump && state.IsGrounded && MathF.Abs(normalizedMoveZ) < 0.01f)
            {
                state.VelZ = MoveTowards(state.VelZ, 0f, MoveDrag * dt);
            }

            state.PosX += state.VelX * dt;
            state.PosY += state.VelY * dt;
            state.PosZ += state.VelZ * dt;

            if (state.PosZ <= GroundHeight)
            {
                state.PosZ = GroundHeight;
                state.VelZ = 0f;
                state.IsGrounded = true;
            }

            state.RotX = input.LookX;
            state.RotY = input.LookY;

            if (float.IsNaN(state.PosX) || float.IsInfinity(state.PosX) ||
                float.IsNaN(state.PosY) || float.IsInfinity(state.PosY) ||
                float.IsNaN(state.PosZ) || float.IsInfinity(state.PosZ))
            {
                state.PosX = startX;
                state.PosY = startY;
                state.PosZ = startZ;
                state.VelX = 0f;
                state.VelY = 0f;
                state.VelZ = 0f;
                state.IsGrounded = true;
                RecordViolation(state, "Invalid transform state detected");
                return;
            }

            var movedDistance = MathF.Sqrt(
                (state.PosX - startX) * (state.PosX - startX) +
                (state.PosY - startY) * (state.PosY - startY) +
                (state.PosZ - startZ) * (state.PosZ - startZ));

            var expectedMax = TeleportAllowancePerSecond * dt + PositionTolerance;
            if (movedDistance > expectedMax)
            {
                state.PosX = startX + Clamp(state.PosX - startX, -expectedMax, expectedMax);
                state.PosY = startY + Clamp(state.PosY - startY, -expectedMax, expectedMax);
                state.PosZ = startZ + Clamp(state.PosZ - startZ, -expectedMax, expectedMax);
                RecordViolation(state, $"Movement exceeded expected range: {movedDistance:F2} > {expectedMax:F2}");
            }

            state.ViolationScore = MathF.Max(0f, state.ViolationScore - (dt * 0.25f));
        }

        /// <summary>
        /// クライアントの現在位置と предполагаемую位置を比較する
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="clientPosition">クライアントが主張する位置</param>
        /// <returns>許容範囲内かどうか</returns>
        public bool ValidateClientPosition(string playerId, float clientX, float clientY, float clientZ)
        {
            return ValidateClientPosition(playerId, clientX, clientY, clientZ, 0f);
        }

        public bool ValidateClientPosition(string playerId, float clientX, float clientY, float clientZ, float deltaTime)
        {
            if (!m_PlayerStates.TryGetValue(playerId, out var state))
            {
                return true; // 未知のプレイヤーは OK
            }

            float dx = state.PosX - clientX;
            float dy = state.PosY - clientY;
            float dz = state.PosZ - clientZ;
            float distance = MathF.Sqrt(dx * dx + dy * dy + dz * dz);
            float allowed = PositionTolerance + TeleportAllowancePerSecond * MathF.Max(0f, deltaTime);

            if (distance > allowed)
            {
                RecordViolation(state, $"Position validation failed: {distance:F2} > {allowed:F2}");
                return false;
            }

            return true;
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
                state.VelX = 0f;
                state.VelY = 0f;
                state.VelZ = 0f;
                state.IsGrounded = MathF.Abs(z - GroundHeight) < 0.01f;
                state.LastUpdateTime = DateTime.UtcNow;
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
                sb.AppendLine($"  Player:{kvp.Key} Seq:{kvp.Value.LastProcessedSequence} Pos:({kvp.Value.PosX:F2},{kvp.Value.PosY:F2},{kvp.Value.PosZ:F2}) Vel:({kvp.Value.VelX:F2},{kvp.Value.VelY:F2},{kvp.Value.VelZ:F2}) Grounded:{kvp.Value.IsGrounded} Viol:{kvp.Value.ViolationScore:F2} Pending:{kvp.Value.PendingInputs}");
            }

            return sb.ToString();
        }

        private static void NormalizeClientInput(ref ClientInputData input, out string reason)
        {
            reason = string.Empty;

            var magnitude = MathF.Sqrt(input.MoveX * input.MoveX + input.MoveY * input.MoveY + input.MoveZ * input.MoveZ);
            if (magnitude > MaxMovementMagnitude)
            {
                var inv = MaxMovementMagnitude / magnitude;
                input.MoveX *= inv;
                input.MoveY *= inv;
                input.MoveZ *= inv;
                reason = "Movement vector clamped";
            }

            if (input.DeltaTime < 0f)
            {
                input.DeltaTime = 0f;
            }
            else if (input.DeltaTime > MaxInputDeltaTime)
            {
                input.DeltaTime = MaxInputDeltaTime;
            }
        }

        private static bool IsFiniteInput(in ClientInputData input)
        {
            return IsFinite(input.MoveX) &&
                   IsFinite(input.MoveY) &&
                   IsFinite(input.MoveZ) &&
                   IsFinite(input.LookX) &&
                   IsFinite(input.LookY) &&
                   IsFinite(input.Timestamp) &&
                   IsFinite(input.DeltaTime);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (current < target)
            {
                return MathF.Min(current + maxDelta, target);
            }

            return MathF.Max(current - maxDelta, target);
        }

        private static float Clamp(float value, float min, float max)
        {
            return MathF.Max(min, MathF.Min(max, value));
        }

        private static void RecordViolation(PlayerServerState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            state.ViolationScore += 1f;
            if (state.ViolationScore >= ViolationClampThreshold)
            {
                ConsoleWrite.WriteMessage($"[LAG] Suspicious movement for {state.PlayerId}: {reason} (score {state.ViolationScore:F2})", ConsoleColor.Yellow);
                state.ViolationScore *= 0.5f;
            }
        }
    }
}
