using System;
using System.Collections.Generic;
using System.Linq;
using OpenGSCore;

namespace OpenGSServer
{
    public interface IMatchLogic
    {
        // マッチを開始する
        void StartMatch();

        // マッチを終了する
        void EndMatch();

        // プレイヤーをマッチに追加する
        void AddPlayer(string playerName);

        // プレイヤーをマッチから削除する
        void RemovePlayer(string playerName);

        // 現在のマッチの状態を取得する
        string GetMatchStatus();

        // スコアを更新する
        void UpdateScore(string playerName, int score);

        // 勝利条件をチェックする
        bool CheckWinCondition();

        // 勝者を決定する
        List<string> GetWinners();
    }

    public class SurvivalMode : IMatchLogic
    {
        private List<string> players = new();
        private List<string> alivePlayers = new();
        private bool matchActive = false;
        private DateTime matchStartTime;
        private const int SURVIVAL_TIME_MINUTES = 10;

        public void StartMatch()
        {
            matchActive = true;
            matchStartTime = DateTime.Now;
            Console.WriteLine("Survival Mode started");
        }

        public void EndMatch()
        {
            matchActive = false;
            Console.WriteLine("Survival Mode ended");
        }

        public void AddPlayer(string playerName)
        {
            if (!players.Contains(playerName))
            {
                players.Add(playerName);
                alivePlayers.Add(playerName);
            }
        }

        public void RemovePlayer(string playerName)
        {
            players.Remove(playerName);
            alivePlayers.Remove(playerName);
        }

        public string GetMatchStatus()
        {
            if (!matchActive)
                return "Match not active";

            return $"Survival Mode - Alive: {alivePlayers.Count}/{players.Count}";
        }

        public void UpdateScore(string playerName, int score)
        {
            // Survivalモードではスコアは使用しない（生存時間がメイン）
            // ただし、キル数などの追加スコアは保持可能
        }

        public bool CheckWinCondition()
        {
            if (!matchActive)
                return false;

            // 時間切れまたは最後の生存者のみ
            var timeUp = (DateTime.Now - matchStartTime).TotalMinutes >= SURVIVAL_TIME_MINUTES;
            var lastPlayerStanding = alivePlayers.Count <= 1;

            return timeUp || lastPlayerStanding;
        }

        public List<string> GetWinners()
        {
            // 生存しているプレイヤーが勝者
            return new List<string>(alivePlayers);
        }

        // Survivalモード特有のメソッド
        public void PlayerEliminated(string playerName)
        {
            alivePlayers.Remove(playerName);
        }
    }

    // デスマッチモード
    public class DeathMatchMode : IMatchLogic
    {
        private Dictionary<string, int> playerScores = new();
        private List<string> players = new();
        private bool matchActive = false;
        private const int WIN_KILL_COUNT = 20;

        public void StartMatch()
        {
            matchActive = true;
            Console.WriteLine("Death Match started");
        }

        public void EndMatch()
        {
            matchActive = false;
            Console.WriteLine("Death Match ended");
        }

        public void AddPlayer(string playerName)
        {
            if (!players.Contains(playerName))
            {
                players.Add(playerName);
                playerScores[playerName] = 0;
            }
        }

        public void RemovePlayer(string playerName)
        {
            players.Remove(playerName);
            playerScores.Remove(playerName);
        }

        public string GetMatchStatus()
        {
            if (!matchActive)
                return "Match not active";

            var topPlayer = playerScores.OrderByDescending(x => x.Value).FirstOrDefault();
            return $"Death Match Active - Leader: {topPlayer.Key} ({topPlayer.Value} kills)";
        }

        public void UpdateScore(string playerName, int score)
        {
            if (playerScores.ContainsKey(playerName))
            {
                playerScores[playerName] += score;
            }
        }

        public bool CheckWinCondition()
        {
            if (!matchActive)
                return false;

            return playerScores.Any(x => x.Value >= WIN_KILL_COUNT);
        }

        public List<string> GetWinners()
        {
            var maxScore = playerScores.Max(x => x.Value);
            return playerScores.Where(x => x.Value == maxScore).Select(x => x.Key).ToList();
        }
    }

    // チームデスマッチモード
    public class TeamDeathMatchMode : IMatchLogic
    {
        private Dictionary<string, int> teamScores = new();
        private Dictionary<string, string> playerTeams = new();
        private List<string> players = new();
        private bool matchActive = false;
        private const int WIN_KILL_COUNT = 50; // チーム合計キル数

        public void StartMatch()
        {
            matchActive = true;
            // 初期チーム作成
            teamScores["Red"] = 0;
            teamScores["Blue"] = 0;
            Console.WriteLine("Team Death Match started");
        }

        public void EndMatch()
        {
            matchActive = false;
            Console.WriteLine("Team Death Match ended");
        }

        public void AddPlayer(string playerName)
        {
            if (!players.Contains(playerName))
            {
                players.Add(playerName);
                // シンプルなチーム割り当て（交互に）
                var team = players.Count % 2 == 1 ? "Red" : "Blue";
                playerTeams[playerName] = team;
            }
        }

        public void RemovePlayer(string playerName)
        {
            players.Remove(playerName);
            playerTeams.Remove(playerName);
        }

        public string GetMatchStatus()
        {
            if (!matchActive)
                return "Match not active";

            var redScore = teamScores.GetValueOrDefault("Red", 0);
            var blueScore = teamScores.GetValueOrDefault("Blue", 0);

            return $"Team Death Match - Red: {redScore}, Blue: {blueScore}";
        }

        public void UpdateScore(string playerName, int score)
        {
            if (playerTeams.ContainsKey(playerName))
            {
                var team = playerTeams[playerName];
                teamScores[team] += score;
            }
        }

        public bool CheckWinCondition()
        {
            if (!matchActive)
                return false;

            return teamScores.Any(x => x.Value >= WIN_KILL_COUNT);
        }

        public List<string> GetWinners()
        {
            var maxScore = teamScores.Max(x => x.Value);
            return teamScores.Where(x => x.Value == maxScore).Select(x => x.Key).ToList();
        }
    }

    // キャプチャー・ザ・フラッグモード
    public class CaptureTheFlagMode : IMatchLogic
    {
        private Dictionary<string, int> teamScores = new();
        private Dictionary<string, string> playerTeams = new();
        private List<string> players = new();
        private bool matchActive = false;
        private const int WIN_CAPTURE_COUNT = 3; // フラッグキャプチャー数

        public void StartMatch()
        {
            matchActive = true;
            // 初期チーム作成
            teamScores["Red"] = 0;
            teamScores["Blue"] = 0;
            Console.WriteLine("Capture The Flag started");
        }

        public void EndMatch()
        {
            matchActive = false;
            Console.WriteLine("Capture The Flag ended");
        }

        public void AddPlayer(string playerName)
        {
            if (!players.Contains(playerName))
            {
                players.Add(playerName);
                // シンプルなチーム割り当て
                var team = players.Count % 2 == 1 ? "Red" : "Blue";
                playerTeams[playerName] = team;
            }
        }

        public void RemovePlayer(string playerName)
        {
            players.Remove(playerName);
            playerTeams.Remove(playerName);
        }

        public string GetMatchStatus()
        {
            if (!matchActive)
                return "Match not active";

            var redScore = teamScores.GetValueOrDefault("Red", 0);
            var blueScore = teamScores.GetValueOrDefault("Blue", 0);

            return $"CTF - Red: {redScore}, Blue: {blueScore}";
        }

        public void UpdateScore(string playerName, int score)
        {
            // CTFではスコアはフラッグキャプチャー数を表す
            if (playerTeams.ContainsKey(playerName))
            {
                var team = playerTeams[playerName];
                teamScores[team] += score;
            }
        }

        public bool CheckWinCondition()
        {
            if (!matchActive)
                return false;

            return teamScores.Any(x => x.Value >= WIN_CAPTURE_COUNT);
        }

        public List<string> GetWinners()
        {
            var maxScore = teamScores.Max(x => x.Value);
            return teamScores.Where(x => x.Value == maxScore).Select(x => x.Key).ToList();
        }

        // CTF特有のメソッド
        public void FlagCaptured(string capturingTeam)
        {
            if (teamScores.ContainsKey(capturingTeam))
            {
                teamScores[capturingTeam]++;
            }
        }
    }
}