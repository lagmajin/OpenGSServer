using System;
using Newtonsoft.Json.Linq;
using OpenGSCore;
using OpenGSServer.Network;

namespace OpenGSServer.Network
{
    /// <summary>
    /// フィールドアイテム関連のイベントハンドラー
    /// </summary>
    public static class FieldItemEventHandler
    {
        /// <summary>
        /// アイテムピックアップを処理
        /// </summary>
        public static void HandleItemPickup(
            JObject json,
            ServerFieldItemManager itemManager,
            Action<JObject> broadcast)
        {
            string itemId = json["ItemId"]?.ToString() ?? "";
            string playerId = json["PlayerID"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(playerId))
            {
                Console.WriteLine("[FieldItem] Invalid pickup request");
                return;
            }

            // アイテムを拾う
            bool success = itemManager.PickupItem(itemId, playerId);

            if (success)
            {
                // 成功したら全クライアントにブロードキャスト
                var response = new JObject
                {
                    ["MessageType"] = "FieldItemPickup",
                    ["ItemId"] = itemId,
                    ["PlayerID"] = playerId,
                    ["Success"] = true
                };

                broadcast(response);

                Console.WriteLine($"[FieldItem] Pickup broadcast: {itemId} by {playerId}");
            }
            else
            {
                // 失敗
                var errorResponse = new JObject
                {
                    ["MessageType"] = "FieldItemPickup",
                    ["ItemId"] = itemId,
                    ["PlayerID"] = playerId,
                    ["Success"] = false,
                    ["Error"] = "Item not found or already picked up"
                };

                broadcast(errorResponse);
            }
        }

        /// <summary>
        /// アイテム状態同期要求を処理
        /// </summary>
        public static void HandleStateSyncRequest(
            JObject json,
            ServerFieldItemManager itemManager,
            Action<JObject> sendResponse)
        {
            var itemsJson = itemManager.ToJson();

            var response = new JObject
            {
                ["MessageType"] = "FieldItemStateSync",
                ["Items"] = itemsJson
            };

            sendResponse(response);
        }

        /// <summary>
        /// アイテムをスポーンさせる（管理用）
        /// </summary>
        public static string SpawnItem(
            ServerFieldItemManager itemManager,
            string itemType,
            float x, float y, float z,
            Action<JObject> broadcast)
        {
            string itemId = itemManager.SpawnItem(itemType, x, y, z);

            // 全クライアントにスポーンを通知
            var spawnMessage = new JObject
            {
                ["MessageType"] = MessageType.ItemSpawnNotification,
                ["ItemId"] = itemId,
                ["ItemType"] = itemType,
                ["PositionX"] = x,
                ["PositionY"] = y,
                ["PositionZ"] = z
            };

            broadcast(spawnMessage);

            Console.WriteLine($"[FieldItem] Spawned: {itemId} ({itemType}) at ({x}, {y}, {z})");

            return itemId;
        }

        /// <summary>
        /// スポーン地点ベースでアイテムをスポーンさせる
        /// </summary>
        public static string SpawnItem(
            ServerFieldItemManager itemManager,
            string itemType,
            int spawnPointId,
            Action<JObject> broadcast)
        {
            string itemId = itemManager.SpawnItem(itemType, spawnPointId);

            if (!itemManager.TryGetItem(itemId, out var item))
            {
                return itemId;
            }

            var spawnMessage = new JObject
            {
                ["MessageType"] = MessageType.ItemSpawnNotification,
                ["ItemId"] = itemId,
                ["ItemType"] = itemType,
                ["SpawnPointId"] = item.SpawnPointId,
                ["SpawnPointName"] = item.SpawnPointName,
                ["PositionX"] = item.PosX,
                ["PositionY"] = item.PosY,
                ["PositionZ"] = item.PosZ
            };

            broadcast(spawnMessage);

            Console.WriteLine($"[FieldItem] Spawned: {itemId} ({itemType}) at spawn point {item.SpawnPointId}");

            return itemId;
        }

        /// <summary>
        /// アイテムを消す（管理用）
        /// </summary>
        public static void DespawnItem(
            ServerFieldItemManager itemManager,
            string itemId,
            Action<JObject> broadcast)
        {
            itemManager.DespawnItem(itemId);

            var despawnMessage = new JObject
            {
                ["MessageType"] = MessageType.ItemDespawnNotification,
                ["ItemId"] = itemId
            };

            broadcast(despawnMessage);

            Console.WriteLine($"[FieldItem] Despawned: {itemId}");
        }

        /// <summary>
        /// 全スポーン中アイテムを消す
        /// </summary>
        public static void DespawnAllItems(
            ServerFieldItemManager itemManager,
            Action<JObject> broadcast)
        {
            itemManager.DespawnAllItems();

            var despawnMessage = new JObject
            {
                ["MessageType"] = MessageType.ItemDespawnNotification
            };

            broadcast(despawnMessage);

            Console.WriteLine("[FieldItem] Despawned all active items");
        }
    }
}
