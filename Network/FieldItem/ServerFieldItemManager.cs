using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OpenGSServer.Network
{
    /// <summary>
    /// サーバー側のフィールドアイテム管理
    /// </summary>
    public class ServerFieldItemManager
    {
        /// <summary>
        /// アイテムデータ
        /// </summary>
        public class FieldItem
        {
            public string ItemId { get; set; } = "";
            public string ItemType { get; set; } = "PowerUp";
            public float PosX, PosY, PosZ;
            public string State { get; set; } = "Spawned";
            public string PickedUpByPlayerId { get; set; } = "";
            public bool IsActive { get; set; } = true;
            public float SpawnTime { get; set; }
        }

        private readonly Dictionary<string, FieldItem> _items = new();

        /// <summary>
        /// マッチID
        /// </summary>
        private string _matchId = "";

        /// <summary>
        /// アイテムを取得された時のアクション（ブロードキャスト用）
        /// </summary>
        public Action<string, string, string>? OnItemPickedUp; // (itemId, playerId, itemType)

        /// <summary>
        /// マッチを開始
        /// </summary>
        public void StartMatch(string matchId)
        {
            _matchId = matchId;
            _items.Clear();
        }

        /// <summary>
        /// マッチを終了
        /// </summary>
        public void EndMatch()
        {
            _items.Clear();
        }

        /// <summary>
        /// アイテムを出現させる
        /// </summary>
        public string SpawnItem(string itemType, float x, float y, float z)
        {
            string itemId = Guid.NewGuid().ToString("N").Substring(0, 8);

            var item = new FieldItem
            {
                ItemId = itemId,
                ItemType = itemType,
                PosX = x,
                PosY = y,
                PosZ = z,
                State = "Spawned",
                IsActive = true,
                SpawnTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds
            };

            _items[itemId] = item;

            return itemId;
        }

        /// <summary>
        /// アイテムを拾う
        /// </summary>
        public bool PickupItem(string itemId, string playerId)
        {
            if (_items.TryGetValue(itemId, out var item))
            {
                if (item.IsActive && item.State == "Spawned")
                {
                    item.State = "PickedUp";
                    item.PickedUpByPlayerId = playerId;
                    item.IsActive = false;

                    OnItemPickedUp?.Invoke(itemId, playerId, item.ItemType);

                    Console.WriteLine($"[FieldItem] Picked up: {itemId} by {playerId}");
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// アイテムを消滅させる
        /// </summary>
        public void DespawnItem(string itemId)
        {
            if (_items.TryGetValue(itemId, out var item))
            {
                item.State = "Despawned";
                item.IsActive = false;
            }
        }

        /// <summary>
        /// アイテムをリスポーンさせる
        /// </summary>
        public void RespawnItem(string itemId, float x, float y, float z)
        {
            if (_items.TryGetValue(itemId, out var item))
            {
                item.PosX = x;
                item.PosY = y;
                item.PosZ = z;
                item.State = "Spawned";
                item.IsActive = true;
                item.PickedUpByPlayerId = "";
                item.SpawnTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds;
            }
        }

        /// <summary>
        /// 全アイテムをJSONに変換
        /// </summary>
        public JArray ToJson()
        {
            var array = new JArray();

            foreach (var kvp in _items)
            {
                var item = new JObject
                {
                    ["ItemId"] = kvp.Value.ItemId,
                    ["ItemType"] = kvp.Value.ItemType,
                    ["PositionX"] = kvp.Value.PosX,
                    ["PositionY"] = kvp.Value.PosY,
                    ["PositionZ"] = kvp.Value.PosZ,
                    ["State"] = kvp.Value.State,
                    ["PickedUpByPlayerId"] = kvp.Value.PickedUpByPlayerId,
                    ["IsActive"] = kvp.Value.IsActive
                };
                array.Add(item);
            }

            return array;
        }

        /// <summary>
        /// アイテムをJSONから復元
        /// </summary>
        public void LoadFromJson(JArray array)
        {
            _items.Clear();

            foreach (var token in array)
            {
                var itemObj = token as JObject;
                if (itemObj == null) continue;

                var item = new FieldItem
                {
                    ItemId = itemObj["ItemId"]?.ToString() ?? "",
                    ItemType = itemObj["ItemType"]?.ToString() ?? "PowerUp",
                    PosX = itemObj["PositionX"]?.Value<float>() ?? 0,
                    PosY = itemObj["PositionY"]?.Value<float>() ?? 0,
                    PosZ = itemObj["PositionZ"]?.Value<float>() ?? 0,
                    State = itemObj["State"]?.ToString() ?? "Spawned",
                    PickedUpByPlayerId = itemObj["PickedUpByPlayerId"]?.ToString() ?? "",
                    IsActive = itemObj["IsActive"]?.Value<bool>() ?? true
                };

                _items[item.ItemId] = item;
            }
        }

        /// <summary>
        /// アクティブなアイテム数を取得
        /// </summary>
        public int GetActiveItemCount()
        {
            int count = 0;
            foreach (var item in _items.Values)
            {
                if (item.IsActive && item.State == "Spawned")
                    count++;
            }
            return count;
        }
    }
}
