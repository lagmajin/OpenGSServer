using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace OpenGSServer
{
    public class Friend
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsOnline { get; set; } = false;
    }

    public class FriendList
    {
        private readonly List<Friend> friends = new();

        public IReadOnlyList<Friend> Friends => friends.AsReadOnly();

        public void Add(Friend friend)
        {
            if (friend == null || string.IsNullOrWhiteSpace(friend.Id))
            {
                return;
            }

            if (friends.Any(x => string.Equals(x.Id, friend.Id, System.StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            friends.Add(friend);
        }

        public bool Remove(string friendId)
        {
            if (string.IsNullOrWhiteSpace(friendId))
            {
                return false;
            }

            return friends.RemoveAll(x => string.Equals(x.Id, friendId, System.StringComparison.OrdinalIgnoreCase)) > 0;
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["Friends"] = new JArray(friends.Select(friend => new JObject
                {
                    ["Id"] = friend.Id,
                    ["Name"] = friend.Name,
                    ["IsOnline"] = friend.IsOnline
                }))
            };
        }
    }
}
