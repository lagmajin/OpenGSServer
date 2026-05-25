using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace OpenGSServer
{
    public class MissionScoreManager
    {
        private readonly Dictionary<string, int> scores = new();

        public void AddScore(string playerId, int score)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return;
            }

            if (scores.TryGetValue(playerId, out var current))
            {
                scores[playerId] = current + score;
            }
            else
            {
                scores[playerId] = score;
            }
        }

        public int GetScore(string playerId)
        {
            return !string.IsNullOrWhiteSpace(playerId) && scores.TryGetValue(playerId, out var score)
                ? score
                : 0;
        }

        public JObject ToJson()
        {
            var result = new JObject();
            foreach (var pair in scores.OrderByDescending(x => x.Value))
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }
    }
}
