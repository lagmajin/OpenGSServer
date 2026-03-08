using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OpenGSServer.Network
{
    /// <summary>
    /// プレイヤー位置更新イベントデータ
    /// </summary>
    public class PlayerPositionUpdateEvent
    {
        public string MessageType { get; set; } = "PlayerPositionUpdate";
        public string PlayerID { get; set; } = string.Empty;
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float RotationY { get; set; }
        public float VelocityX { get; set; }
        public float VelocityY { get; set; }
        public float VelocityZ { get; set; }
        public byte SequenceNumber { get; set; }
        public float Timestamp { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["MessageType"] = MessageType,
                ["PlayerID"] = PlayerID,
                ["PositionX"] = PositionX,
                ["PositionY"] = PositionY,
                ["PositionZ"] = PositionZ,
                ["RotationY"] = RotationY,
                ["VelocityX"] = VelocityX,
                ["VelocityY"] = VelocityY,
                ["VelocityZ"] = VelocityZ,
                ["SequenceNumber"] = SequenceNumber,
                ["Timestamp"] = Timestamp
            };
        }

        public static PlayerPositionUpdateEvent? FromJson(JObject json)
        {
            try
            {
                return new PlayerPositionUpdateEvent
                {
                    MessageType = json["MessageType"]?.ToString() ?? "",
                    PlayerID = json["PlayerID"]?.ToString() ?? "",
                    PositionX = json["PositionX"]?.Value<float>() ?? 0,
                    PositionY = json["PositionY"]?.Value<float>() ?? 0,
                    PositionZ = json["PositionZ"]?.Value<float>() ?? 0,
                    RotationY = json["RotationY"]?.Value<float>() ?? 0,
                    VelocityX = json["VelocityX"]?.Value<float>() ?? 0,
                    VelocityY = json["VelocityY"]?.Value<float>() ?? 0,
                    VelocityZ = json["VelocityZ"]?.Value<float>() ?? 0,
                    SequenceNumber = json["SequenceNumber"]?.Value<byte>() ?? 0,
                    Timestamp = json["Timestamp"]?.Value<float>() ?? 0
                };
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// プレイヤー状態同期イベント（位置+武器+HPなど）
    /// </summary>
    public class PlayerStateSyncEvent
    {
        public string MessageType { get; set; } = "GameStateSync";
        public string PlayerID { get; set; } = string.Empty;
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float PositionZ { get; set; }
        public float RotationY { get; set; }
        public int Health { get; set; }
        public int Score { get; set; }
        public byte SequenceNumber { get; set; }
        public float Timestamp { get; set; }
        public bool IsGrounded { get; set; }
        public byte WeaponIndex { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["MessageType"] = MessageType,
                ["PlayerID"] = PlayerID,
                ["PositionX"] = PositionX,
                ["PositionY"] = PositionY,
                ["PositionZ"] = PositionZ,
                ["RotationY"] = RotationY,
                ["Health"] = Health,
                ["Score"] = Score,
                ["SequenceNumber"] = SequenceNumber,
                ["Timestamp"] = Timestamp,
                ["IsGrounded"] = IsGrounded,
                ["WeaponIndex"] = WeaponIndex
            };
        }

        public static PlayerStateSyncEvent? FromJson(JObject json)
        {
            try
            {
                return new PlayerStateSyncEvent
                {
                    MessageType = json["MessageType"]?.ToString() ?? "",
                    PlayerID = json["PlayerID"]?.ToString() ?? "",
                    PositionX = json["PositionX"]?.Value<float>() ?? 0,
                    PositionY = json["PositionY"]?.Value<float>() ?? 0,
                    PositionZ = json["PositionZ"]?.Value<float>() ?? 0,
                    RotationY = json["RotationY"]?.Value<float>() ?? 0,
                    Health = json["Health"]?.Value<int>() ?? 100,
                    Score = json["Score"]?.Value<int>() ?? 0,
                    SequenceNumber = json["SequenceNumber"]?.Value<byte>() ?? 0,
                    Timestamp = json["Timestamp"]?.Value<float>() ?? 0,
                    IsGrounded = json["IsGrounded"]?.Value<bool>() ?? true,
                    WeaponIndex = json["WeaponIndex"]?.Value<byte>() ?? 0
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
