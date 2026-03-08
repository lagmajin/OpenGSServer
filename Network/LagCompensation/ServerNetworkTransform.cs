using System;

namespace OpenGSServer.Network
{
    /// <summary>
    /// サーバー側のネットワークトランスフォーム状態
    /// </summary>
    public struct ServerTransformState
    {
        public uint NetworkId;
        public string PlayerId;
        public float PositionX;
        public float PositionY;
        public float PositionZ;
        public float RotationX;
        public float RotationY;
        public float RotationZ;
        public float RotationW;
        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;
        public float Timestamp;
        public byte SequenceNumber;

        public static ServerTransformState Create(
            uint networkId,
            string playerId,
            float posX, float posY, float posZ,
            float rotX, float rotY, float rotZ, float rotW,
            float velX, float velY, float velZ,
            float timestamp,
            byte sequence)
        {
            return new ServerTransformState
            {
                NetworkId = networkId,
                PlayerId = playerId,
                PositionX = posX,
                PositionY = posY,
                PositionZ = posZ,
                RotationX = rotX,
                RotationY = rotY,
                RotationZ = rotZ,
                RotationW = rotW,
                VelocityX = velX,
                VelocityY = velY,
                VelocityZ = velZ,
                Timestamp = timestamp,
                SequenceNumber = sequence
            };
        }

        public override string ToString()
        {
            return $"[ServerTransform] Player:{PlayerId} Seq:{SequenceNumber} Pos:({PositionX:F2},{PositionY:F2},{PositionZ:F2}) Time:{Timestamp:F3}";
        }
    }

    /// <summary>
    /// クライアントからの入力データ
    /// </summary>
    public struct ClientInputData
    {
        public string PlayerId;
        public float MoveX;
        public float MoveY;
        public float MoveZ;
        public float LookX;
        public float LookY;
        public bool Jump;
        public bool Fire;
        public byte SequenceNumber;
        public float Timestamp;
        public float DeltaTime;
    }
}
