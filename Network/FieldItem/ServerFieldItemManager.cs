using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json.Linq;
using OpenGSCore;

#nullable enable

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
            public EFieldItemType ItemType { get; set; } = EFieldItemType.PowerUpItem;
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
            public EFieldItemType ItemType { get; set; }
            public int MaxActiveCount { get; set; } = 1;
            public float RespawnDelaySec { get; set; } = FieldItemDefaults.DurationSeconds;
            public List<int> PreferredSpawnPointIds { get; } = new List<int>();
        }

        private readonly ConcurrentDictionary<string, FieldItem> _items = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, FieldItemSpawnPoint> _spawnPoints = new();
        private readonly Dictionary<EFieldItemType, FieldItemSpawnRule> _spawnRules = new();
        private readonly Random _random = new();
        private readonly object _itemStateLock = new();

        /// <summary>
        /// マッチID
        /// </summary>
        private string _matchId = "";

        /// <summary>
        /// アイテムを取得された時のアクション（ブロードキャスト用）
        /// </summary>
        public Action<string, string, EFieldItemType>? OnItemPickedUp; // (itemId, playerId, itemType)

        /// <summary>
        /// マッチを開始
        /// </summary>
        public void StartMatch(string matchId)
        {
            lock (_itemStateLock)
            {
                _matchId = matchId;
                _items.Clear();
                OnItemPickedUp = null;
            }
        }

        /// <summary>
        /// マッチを終了
        /// </summary>
        public void EndMatch()
        {
            lock (_itemStateLock)
            {
                _items.Clear();
                _matchId = string.Empty;
                OnItemPickedUp = null;
            }
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
        public void ConfigureSpawnRule(EFieldItemType itemType, int maxActiveCount, float respawnDelaySec, params int[] preferredSpawnPointIds)
        {

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
            ConfigureSpawnRule(EFieldItemType.PowerUpItem, 1, FieldItemDefaults.DurationSeconds, 0);
            ConfigureSpawnRule(EFieldItemType.DefenceUpItem, 1, FieldItemDefaults.DurationSeconds, 1);
            ConfigureSpawnRule(EFieldItemType.SpeedUpItem, 1, FieldItemDefaults.DurationSeconds, 2);
            ConfigureSpawnRule(EFieldItemType.StealthItem, 1, FieldItemDefaults.DurationSeconds, 3);
            ConfigureSpawnRule(EFieldItemType.GrenadePack, 1, FieldItemDefaults.DurationSeconds, 4);
            ConfigureSpawnRule(EFieldItemType.HealItem, 1, FieldItemDefaults.DurationSeconds, 5);
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
        private int PickSpawnPointId(EFieldItemType itemType)
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
        public string SpawnItem(EFieldItemType itemType, float x, float y, float z)
        {
            if (
                !IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                return string.Empty;
            }

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

            lock (_itemStateLock)
            {
                _items[itemId] = item;
            }

            return itemId;
        }

        /// <summary>
        /// スポーン地点ベースでアイテムを出現させる
        /// </summary>
        public string SpawnItem(EFieldItemType itemType, int spawnPointId)
        {
            lock (_itemStateLock)
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
        }

        /// <summary>
        /// 生成ルールがある場合に、現在の上限を考慮して自動生成する
        /// </summary>
        public bool TrySpawnConfiguredItem(EFieldItemType itemType, out string itemId)
        {
            itemId = "";
            itemId = SpawnItem(itemType, PickSpawnPointId(itemType));
            return !string.IsNullOrEmpty(itemId);
        }

        /// <summary>
        /// アイテム種別ごとのアクティブ数を取得する
        /// </summary>
        public int GetActiveItemCountForType(EFieldItemType itemType)
        {
            return _items.Values.Count(item =>
                item.IsActive &&
                item.State == "Spawned" &&
                item.ItemType == itemType);
        }

        /// <summary>
        /// 指定アイテムを取得する
        /// </summary>
        public bool TryGetItem(string itemId, [NotNullWhen(true)] out FieldItem? item)
        {
            return _items.TryGetValue(itemId, out item);
        }

        /// <summary>
        /// アイテムを拾う
        /// </summary>
/// <summary>
        /// How far from an item a player may stand and still claim it.
        /// The client used to decide on its own that it had touched an item and
        /// the server took that at face value, so any client could collect a
        /// spawn on the far side of the map. The radius is generous because the
        /// authoritative position lags the client by a round trip, but it is
        /// small enough to stay local to the spawn point.
        /// </summary>
        public const float PickupRadius = 2.5f;

        /// <summary>
        /// Whether a pickup at the given position may be granted.
        /// <para>
        /// Used by the authoritative path, which already knows where the player
        /// is. The overload without a position stays available for callers that
        /// have no position to offer, but it cannot enforce the radius.
        /// </para>
        /// </summary>
        public bool IsWithinPickupRange(string itemId, float x, float y, float z)
        {
            if (!_items.TryGetValue(itemId, out var item))
            {
                return false;
            }

            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                return false;
            }

            var dx = item.PosX - x;
            var dy = item.PosY - y;
            var dz = item.PosZ - z;
            return (dx * dx) + (dy * dy) + (dz * dz) <= PickupRadius * PickupRadius;
        }

        /// <summary>
        /// Claims an item for a player, optionally checking the distance from a
        /// known player position. Pass a position for the authoritative path and
        /// null when the caller has none.
        /// </summary>
        public bool PickupItem(string itemId, string playerId, float? playerX, float? playerY, float? playerZ)
        {
            if (playerX.HasValue || playerY.HasValue || playerZ.HasValue)
            {
                if (!playerX.HasValue || !playerY.HasValue || !playerZ.HasValue)
                {
                    // A half supplied position is a client bug, not a free pass.
                    return false;
                }

                if (!IsWithinPickupRange(itemId, playerX.Value, playerY.Value, playerZ.Value))
                {
                    return false;
                }
            }

            return PickupItem(itemId, playerId);
        }

        public bool PickupItem(string itemId, string playerId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            EFieldItemType itemType = default;
            bool picked = false;
            lock (_itemStateLock)
            {
                if (_items.TryGetValue(itemId, out var item) && item.IsActive && item.State == "Spawned")
                {
                    item.State = "PickedUp";
                    item.PickedUpByPlayerId = playerId;
                    item.IsActive = false;
                    itemType = item.ItemType;
                    picked = true;
                }
            }

            if (!picked)
            {
                return false;
            }

            OnItemPickedUp?.Invoke(itemId, playerId, itemType);
            Console.WriteLine($"[FieldItem] Picked up: {itemId} by {playerId}");
            return true;
        }

        /// <summary>
        /// アイテムを消滅させる
        /// </summary>
        public void DespawnItem(string itemId)
        {
            lock (_itemStateLock)
            {
                if (_items.TryGetValue(itemId, out var item))
                {
                    item.State = "Despawned";
                    item.IsActive = false;
                }
            }
        }

        /// <summary>
        /// 現在スポーンしているアイテムをすべて消滅させる
        /// </summary>
        public void DespawnAllItems()
        {
            lock (_itemStateLock)
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
        }

        /// <summary>
        /// アイテムをリスポーンさせる
        /// </summary>
        public void RespawnItem(string itemId, float x, float y, float z)
        {
            lock (_itemStateLock)
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
                    ["ItemType"] = FieldItemTypeNames.ToWireName(kvp.Value.ItemType),
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

            if (array == null)
            {
                return;
            }

            foreach (var token in array)
            {
                var itemObj = token as JObject;
                if (itemObj == null) continue;

                if (!FieldItemTypeNames.TryParse(itemObj["ItemType"]?.ToString(), out var parsedType))
                {
                    continue;
                }

                var item = new FieldItem
                {
                    ItemId = itemObj["ItemId"]?.ToString() ?? "",
                    ItemType = parsedType,
                    PosX = itemObj["PositionX"]?.Value<float>() ?? 0,
                    PosY = itemObj["PositionY"]?.Value<float>() ?? 0,
                    PosZ = itemObj["PositionZ"]?.Value<float>() ?? 0,
                    SpawnPointId = itemObj["SpawnPointId"]?.Value<int>() ?? -1,
                    SpawnPointName = itemObj["SpawnPointName"]?.ToString() ?? "",
                    State = itemObj["State"]?.ToString() ?? "Spawned",
                    PickedUpByPlayerId = itemObj["PickedUpByPlayerId"]?.ToString() ?? "",
                    IsActive = itemObj["IsActive"]?.Value<bool>() ?? true
                };

                if (string.IsNullOrWhiteSpace(item.ItemId) ||
                    !IsFinite(item.PosX) || !IsFinite(item.PosY) || !IsFinite(item.PosZ))
                {
                    continue;
                }

                _items[item.ItemId] = item;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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
