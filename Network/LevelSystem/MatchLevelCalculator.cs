using System;

namespace OpenGSServer.Network
{
    /// <summary>
    /// マッチ結果のレベル/XP計算
    /// </summary>
    public class MatchLevelCalculator
    {
        /// <summary>
        /// レベルアップに必要なXPテーブル
        /// </summary>
        private static readonly int[] LevelUpXpTable = new int[]
        {
            100, 200, 350, 550, 800,
            1100, 1450, 1850, 2300, 2800,
            3350, 3950, 4600, 5300, 6050,
            6850, 7700, 8600, 9550, 10550
        };

        public const int MaxLevel = 20;

        /// <summary>
        /// マッチ結果から獲得XPを計算
        /// </summary>
        /// <param name="matchScore">マッチスコア</param>
        /// <param name="kills">キル数</param>
        /// <param name="deaths">デス数</param>
        /// <param name="isVictory">勝利かどうか</param>
        /// <returns>獲得XP</returns>
        public static int CalculateMatchXp(int matchScore, int kills, int deaths, bool isVictory)
        {
            // 基础XP = スコア + キル * 10
            int baseXp = matchScore + (kills * 10);

            // 死亡ペナルティ
            int deathPenalty = deaths * 2;

            // 勝利ボーナス
            int victoryBonus = isVictory ? 50 : 0;

            // アシスト報酬
            int assistBonus = Math.Max(0, kills - deaths) * 5;

            int totalXp = Math.Max(0, baseXp - deathPenalty + victoryBonus + assistBonus);

            return totalXp;
        }

        /// <summary>
        /// レベルを计算
        /// </summary>
        public static int CalculateLevel(int totalXp)
        {
            int level = 1;
            int xpRemaining = totalXp;

            for (int i = 0; i < LevelUpXpTable.Length; i++)
            {
                if (xpRemaining >= LevelUpXpTable[i])
                {
                    xpRemaining -= LevelUpXpTable[i];
                    level++;
                }
                else
                {
                    break;
                }
            }

            return Math.Min(level, MaxLevel);
        }

        /// <summary>
        /// レベル内の残りのXPを计算
        /// </summary>
        public static int CalculateLevelXp(int totalXp)
        {
            int xpRemaining = totalXp;

            for (int i = 0; i < LevelUpXpTable.Length; i++)
            {
                if (xpRemaining >= LevelUpXpTable[i])
                {
                    xpRemaining -= LevelUpXpTable[i];
                }
                else
                {
                    break;
                }
            }

            return xpRemaining;
        }

        /// <summary>
        /// 次のレベルまでの必要XP
        /// </summary>
        public static int GetRequiredXpForLevel(int currentLevel)
        {
            if (currentLevel >= MaxLevel) return 0;
            return LevelUpXpTable[Math.Clamp(currentLevel - 1, 0, LevelUpXpTable.Length - 1)];
        }

        /// <summary>
        /// レベルの進捗を計算（0.0 - 1.0）
        /// </summary>
        public static float CalculateLevelProgress(int totalXp)
        {
            int level = CalculateLevel(totalXp);
            int levelXp = CalculateLevelXp(totalXp);

            if (level >= MaxLevel) return 1f;

            int required = GetRequiredXpForLevel(level);
            return required > 0 ? (float)levelXp / required : 1f;
        }

        /// <summary>
        /// マッチ結果レスポンスを作成
        /// </summary>
        public static MatchResultResponse CreateMatchResultResponse(
            string playerId,
            int matchScore,
            int kills,
            int deaths,
            bool isVictory,
            int currentTotalXp)
        {
            int xpGained = CalculateMatchXp(matchScore, kills, deaths, isVictory);
            int newTotalXp = currentTotalXp + xpGained;

            int newLevel = CalculateLevel(newTotalXp);
            int levelXp = CalculateLevelXp(newTotalXp);

            return new MatchResultResponse
            {
                PlayerId = playerId,
                XpGained = xpGained,
                NewTotalXp = newTotalXp,
                NewLevel = newLevel,
                LevelXp = levelXp,
                IsLevelUp = newLevel > CalculateLevel(currentTotalXp)
            };
        }
    }

    /// <summary>
    /// マッチ結果レスポンス
    /// </summary>
    public class MatchResultResponse
    {
        public string PlayerId { get; set; } = "";
        public int XpGained { get; set; }
        public int NewTotalXp { get; set; }
        public int NewLevel { get; set; }
        public int LevelXp { get; set; }
        public bool IsLevelUp { get; set; }
    }
}
