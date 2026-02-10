using System;
using System.Collections.Generic;
using System.Text;
using OpenGSCore; // OpenGSCore.eGameModeを使用

namespace OpenGSServer
{
    // ===============================================
    // 注意: このファイルは段階的に削除予定
    // OpenGSCore.eGameModeを使用してください
    // ===============================================

    // OpenGSCore.eGameModeを使用するため、ここでは定義しない
    // Deprecation名前空間は削除

    /// <summary>
    /// ゲームモード管理クラス
    /// OpenGSCore.eGameModeの補助クラス
    /// </summary>
    public class GameMode
    {
        private eGameMode mode = eGameMode.Unknown;
        private string str = "";

        public eGameMode Mode
        {
            get => mode;
            set => mode = value;
        }

        public GameMode()
        {
        }

        public GameMode(eGameMode gameMode)
        {
            mode = gameMode;
            str = GetGameModeString(gameMode);
        }

        public GameMode(string modeStr)
        {
            var lowerMode = modeStr.ToLower();

            if (lowerMode == "deathmatch" || lowerMode == "dm")
            {
                str = "dm";
                mode = eGameMode.DeathMatch;
            }
            else if (lowerMode == "teamdeathmatch" || lowerMode == "tdm")
            {
                str = "tdm";
                mode = eGameMode.TeamDeathMatch;
            }
            else if (lowerMode == "survival" || lowerMode == "suv")
            {
                str = "suv";
                mode = eGameMode.Survival;
            }
            else if (lowerMode == "teamsurvival" || lowerMode == "tsuv")
            {
                str = "tsuv";
                mode = eGameMode.TeamSurvival;
            }
            else if (lowerMode == "capturetheflag" || lowerMode == "ctf")
            {
                str = "ctf";
                mode = eGameMode.CTF;
            }
            else if (lowerMode == "oneshotkill" || lowerMode == "osk")
            {
                str = "osk";
                mode = eGameMode.OneShotKill;
            }
            else if (lowerMode == "armsrace" || lowerMode == "ar")
            {
                str = "ar";
                mode = eGameMode.ArmsRace;
            }
            else
            {
                str = "unknown";
                mode = eGameMode.Unknown;
            }
        }

        public string name()
        {
            return str;
        }

        public bool Valid()
        {
            return mode != eGameMode.Unknown;
        }

        private static string GetGameModeString(eGameMode mode)
        {
            switch (mode)
            {
                case eGameMode.DeathMatch:
                    return "dm";
                case eGameMode.TeamDeathMatch:
                    return "tdm";
                case eGameMode.Survival:
                    return "suv";
                case eGameMode.TeamSurvival:
                    return "tsuv";
                case eGameMode.CTF:
                    return "ctf";
                case eGameMode.OneShotKill:
                    return "osk";
                case eGameMode.ArmsRace:
                    return "ar";
                default:
                    return "unknown";
            }
        }
    }
}
