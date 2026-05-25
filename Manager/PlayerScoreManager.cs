using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    internal interface IPlayerScoreManager
    {
    }

    internal class PlayerScoreManager : IPlayerScoreManager
    {
        private readonly Dictionary<string, int> scores = new();

        public PlayerScoreManager()
        {
        }

        public void AddScore(string playerId, int score)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            scores[playerId] = GetScore(playerId) + score;
        }

        public int GetScore(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) && scores.TryGetValue(playerId, out var score)
                ? score
                : 0;
        }

        public void RemovePlayer(string playerId)
        {
            if (!string.IsNullOrWhiteSpace(playerId))
            {
                scores.Remove(playerId);
            }
        }

        public void Clear()
        {
            scores.Clear();
        }

        public JObject ToJson()
        {
            var result = new JObject();
            foreach (var pair in scores)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }
    }
}
