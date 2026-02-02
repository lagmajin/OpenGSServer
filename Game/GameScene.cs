using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    class GameSceneUpdateInfo
    {
        public Time BeforeTime { get; }
        public Time CurrentTime { get; }

        public GameSceneUpdateInfo(Time beforeTime, Time currentTime)
        {
            BeforeTime = beforeTime;
            CurrentTime = currentTime;
        }
    }

    public class GameScene
    {
        // Event raised when the scene wants to notify network layer / other systems
        // about a grenade-related message that should be forwarded to clients.
        public event Action<Newtonsoft.Json.Linq.JObject>? OnGrenadeEvent;

        private readonly List<AbstractGameObject> _objects = new();
        private readonly Dictionary<string, AbstractGameObject> _objectIndex = new(StringComparer.Ordinal);
        private readonly object _syncRoot = new();
        private Time _lastUpdateTime;
        private bool _initialized;

        public int ObjectCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _objects.Count;
                }
            }
        }

        public bool AddBullet(BulletGameObject bullet) => AddObject(bullet);

        public bool AddPlayerCharacter(PlayerGameObject character) => AddObject(character);

        public bool AddFieldItem(AbstractFieldItem item) => AddObject(item);

        public bool AddGrenade(AbstractGameObject grenade) => AddObject(grenade);

        public bool AddFlag(AbstractGameObject flag) => AddObject(flag);

        public bool RemoveObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (!_objectIndex.TryGetValue(objectId, out var obj))
                {
                    return false;
                }

                _objects.Remove(obj);
                _objectIndex.Remove(objectId);
                obj.OnDestroy();
                return true;
            }
        }

        public AbstractGameObject FindObject(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return null;
            }

            lock (_syncRoot)
            {
                return _objectIndex.TryGetValue(objectId, out var obj) ? obj : null;
            }
        }

        public void UpdateFrame()
        {
            foreach (var obj in Snapshot())
            {
                obj.Update();
            }
        }

        public void UpdateFrame(Time currentTime)
        {
            if (!_initialized)
            {
                _lastUpdateTime = currentTime;
                _initialized = true;
            }

            var info = new GameSceneUpdateInfo(_lastUpdateTime, currentTime);
            UpdateFrame(info);
            _lastUpdateTime = currentTime;
        }

        public void UpdateFrame(GameSceneUpdateInfo updateInfo)
        {
            var dt = (updateInfo.CurrentTime - updateInfo.BeforeTime).TotalSeconds();

            foreach (var obj in Snapshot())
            {
                // Update object normally
                obj.Update();

                // If it's a grenade, do not perform server-side collision/explosion
                if (obj is AbstractGrenade grenade)
                {
                    // grenade.UpdateCountdown should not trigger server-side explosion per rule; it only advances timer
                    grenade.UpdateCountdown((float)dt);

                    // Broadcast grenade state to connected clients (so clients remain authoritative)
                    var json = new Newtonsoft.Json.Linq.JObject
                    {
                        ["MessageType"] = "GrenadeState",
                        ["ObjectID"] = grenade.Id,
                        ["Type"] = grenade.GranadeType.ToString(),
                        ["PosX"] = grenade.Posx,
                        ["PosY"] = grenade.Posy,
                        ["RemainingFuseTime"] = grenade.RemainingFuseTime,
                        ["State"] = grenade.State.ToString(),
                        ["ExplosionRadius"] = grenade.ExplosionRadius,
                        ["PlayerID"] = grenade.OwnerId
                    };

                    OnGrenadeEvent?.Invoke(json);
                }
            }
        }

        public JObject ToJson()
        {
            var array = new JArray();
            foreach (var item in Snapshot())
            {
                array.Add(CreateObjectJson(item));
            }

            return new JObject
            {
                ["Objects"] = array
            };
        }

        public JObject AllPlayerDataToJson()
        {
            var players = new JArray();

            foreach (var player in Snapshot().OfType<PlayerGameObject>())
            {
                players.Add(CreateObjectJson(player));
            }

            return new JObject
            {
                ["Players"] = players
            };
        }

        private bool AddObject(AbstractGameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            lock (_syncRoot)
            {
                if (string.IsNullOrWhiteSpace(gameObject.Id))
                {
                    gameObject.Id = Guid.NewGuid().ToString("N");
                }

                if (_objectIndex.ContainsKey(gameObject.Id))
                {
                    return false;
                }

                _objects.Add(gameObject);
                _objectIndex.Add(gameObject.Id, gameObject);
            }

            gameObject.OnCreated();
            return true;
        }

        private List<AbstractGameObject> Snapshot()
        {
            lock (_syncRoot)
            {
                return _objects.ToList();
            }
        }

        private static JObject CreateObjectJson(AbstractGameObject item)
        {
            if (item == null)
            {
                return new JObject();
            }

            return item.ToJSon() ?? new JObject
            {
                ["Name"] = item.Name ?? string.Empty,
                ["ID"] = item.Id ?? string.Empty,
                ["PosX"] = item.Posx,
                ["PosY"] = item.Posy
            };
        }
    }


}
