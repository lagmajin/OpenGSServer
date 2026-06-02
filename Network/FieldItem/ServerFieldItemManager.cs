using System;
using System.Collections.Generic;
using System.Linq;
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
            public int SpawnPointId { get; set; } = -1;
            public string SpawnPointName { get; set; } = "";
            public string State { get; set; } = "Spawned";
            public string PickedUpByPlayerId { get; set; } = "";
            public bool IsActive { get; set; } = true;
            public float SpawnTime { get; set; }
        }

        /// <summary>
        /// スポーン地点定義
        /// </summary>
        public class FieldItemSpawnPoint
        {
            public int SpawnPointId { get; set; }
            public string Name { get; set; } = "";
            public float PosX { get; set; }
            public float PosY { get; set; }
            public float PosZ { get; set; }
            public bool IsEnabled { get; set; } = true;
        }

        /// <summary>
        /// アイテムごとの生成ルール
        /// </summary>
        public class FieldItemSpawnRule
        {
            public string ItemType { get; set; } = "";
            public int MaxActiveCount { get; set; } = 1;
            public float RespawnDelaySec { get; set; } = 30.0f;
            public List<int> PreferredSpawnPointIds { get; } = new List<int>();
        }

        private readonly Dictionary<string, FieldItem> _items = new();
        private readonly Dictionary<int, FieldItemSpawnPoint> _spawnPoints = new();
        private readonly Dictionary<string, FieldItemSpawnRule> _spawnRules = new(StringComparer.OrdinalIgnoreCase);
        private readonly Random _random = new();

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
        /// スポーン地点を登録する
        /// </summary>
        public void RegisterSpawnPoint(int spawnPointId, float x, float y, float z, string name = "", bool isEnabled = true)
        {
            _spawnPoints[spawnPointId] = new FieldItemSpawnPoint
            {
                SpawnPointId = spawnPointId,
                Name = name,
                PosX = x,
                PosY = y,
                PosZ = z,
                IsEnabled = isEnabled
            };
        }

        /// <summary>
        /// アイテムごとの生成ルールを設定する
        /// </summary>
        public void ConfigureSpawnRule(string itemType, int maxActiveCount, float respawnDelaySec, params int[] preferredSpawnPointIds)
        {
            if (string.IsNullOrWhiteSpace(itemType))
            {
                return;
            }

            var rule = new FieldItemSpawnRule
            {
                ItemType = itemType,
                MaxActiveCount = Math.Max(1, maxActiveCount),
                RespawnDelaySec = Math.Max(0.1f, respawnDelaySec)
            };

            if (preferredSpawnPointIds != null)
            {
                foreach (var id in preferredSpawnPointIds)
                {
                    rule.PreferredSpawnPointIds.Add(id);
                }
            }

            _spawnRules[itemType] = rule;
        }

        /// <summary>
        /// 現在のマッチに対して有効な自動生成ルールを準備する
        /// </summary>
        public void ConfigureDefaultSpawnRules()
        {
            ConfigureSpawnRule("PowerUpItem", 1, 30.0f, 0);
            ConfigureSpawnRule("DefenceUpItem", 1, 30.0f, 1);
            ConfigureSpawnRule("SpeedUpItem", 1, 30.0f, 2);
            ConfigureSpawnRule("StealthItem", 1, 30.0f, 3);
            ConfigureSpawnRule("GrenadePack", 1, 30.0f, 4);
            ConfigureSpawnRule("HealItem", 1, 30.0f, 5);
        }

        /// <summary>
        /// 登録済みのスポーン地点から実際の位置を決定する
        /// </summary>
        private bool TryResolveSpawnPoint(int requestedSpawnPointId, out FieldItemSpawnPoint spawnPoint)
        {
            spawnPoint = null!;

            if (requestedSpawnPointId >= 0 &&
                _spawnPoints.TryGetValue(requestedSpawnPointId, out var exactPoint) &&
                exactPoint.IsEnabled)
            {
                spawnPoint = exactPoint;
                return true;
            }

            var enabledPoints = _spawnPoints.Values.Where(p => p.IsEnabled).ToList();
            if (enabledPoints.Count > 0)
            {
                spawnPoint = enabledPoints[_random.Next(enabledPoints.Count)];
                return true;
            }

            return false;
        }

        /// <summary>
        /// 指定された生成ルールに従って新しいスポーン地点IDを選ぶ
        /// </summary>
        private int PickSpawnPointId(string itemType)
        {
            if (_spawnRules.TryGetValue(itemType, out var rule))
            {
                var preferred = rule.PreferredSpawnPointIds
                    .Where(id => _spawnPoints.TryGetValue(id, out var point) && point.IsEnabled)
                    .ToList();

                if (preferred.Count > 0)
                {
                    return preferred[_random.Next(preferred.Count)];
                }
            }

            var enabledPoints = _spawnPoints.Values.Where(p => p.IsEnabled).ToList();
            if (enabledPoints.Count > 0)
            {
                return enabledPoints[_random.Next(enabledPoints.Count)].SpawnPointId;
            }

            return 0;
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
                SpawnPointId = -1,
                SpawnPointName = "",
                State = "Spawned",
                IsActive = true,
                SpawnTime = (float)DateTime.UtcNow.TimeOfDay.TotalSeconds
            };

            _items[itemId] = item;

            return itemId;
        }

        /// <summary>
        /// スポーン地点ベースでアイテムを出現させる
        /// </summary>
        public string SpawnItem(string itemType, int spawnPointId)
        {
            if (_spawnRules.TryGetValue(itemType, out var rule) &&
                GetActiveItemCountForType(itemType) >= rule.MaxActiveCount)
            {
                return "";
            }

            if (spawnPointId < 0)
            {
                spawnPointId = PickSpawnPointId(itemType);
            }

            if (!TryResolveSpawnPoint(spawnPointId, out var spawnPoint))
            {
                return SpawnItem(itemType, 0, 0, 0);
            }

            var itemId = SpawnItem(itemType, spawnPoint.PosX, spawnPoint.PosY, spawnPoint.PosZ);
            if (_items.TryGetValue(itemId, out var item))
            {
                item.SpawnPointId = spawnPoint.SpawnPointId;
                item.SpawnPointName = spawnPoint.Name;
            }

            return itemId;
        }

        /// <summary>
        /// 列挙型ベースでアイテムを出現させる
        /// </summary>
        public string SpawnItem(OpenGSCore.EFieldItemType itemType, int spawnPointId = 0)
        {
            return SpawnItem(itemType.ToString(), spawnPointId);
        }

        /// <summary>
        /// 生成ルールがある場合に、現在の上限を考慮して自動生成する
        /// </summary>
        public bool TrySpawnConfiguredItem(string itemType, out string itemId)
        {
            itemId = "";
            itemId = SpawnItem(itemType, PickSpawnPointId(itemType));
            return !string.IsNullOrEmpty(itemId);
        }

        /// <summary>
        /// アイテム種別ごとのアクティブ数を取得する
        /// </summary>
        public int GetActiveItemCountForType(string itemType)
        {
            return _items.Values.Count(item =>
                item.IsActive &&
                item.State == "Spawned" &&
                string.Equals(item.ItemType, itemType, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 指定アイテムを取得する
        /// </summary>
        public bool TryGetItem(string itemId, out FieldItem item)
        {
            return _items.TryGetValue(itemId, out item);
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
        /// 現在スポーンしているアイテムをすべて消滅させる
        /// </summary>
        public void DespawnAllItems()
        {
            foreach (var item in _items.Values)
            {
                if (item.IsActive && item.State == "Spawned")
                {
                    item.State = "Despawned";
                    item.IsActive = false;
                }
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
                    ["SpawnPointId"] = kvp.Value.SpawnPointId,
                    ["SpawnPointName"] = kvp.Value.SpawnPointName,
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
                    SpawnPointId = itemObj["SpawnPointId"]?.Value<int>() ?? -1,
                    SpawnPointName = itemObj["SpawnPointName"]?.ToString() ?? "",
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
