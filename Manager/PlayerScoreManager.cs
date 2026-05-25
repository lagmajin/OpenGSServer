using System.Collections.Generic;

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
    }
}
