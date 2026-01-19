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
        }

        public GameMode(string modeStr)
        {
            var lowerMode = modeStr.ToLower();
            str = "unknown";

            if (lowerMode == "deathmatch" || lowerMode == "dm")
            {
                str = "dm";
                mode = eGameMode.DeathMatch;
            }
            else if (lowerMode == "teamdeathmatch" || lowerMode == "tdm")
            {
                str = "tdm";
                mode = eGameMode.TeamDeathMatch; // OpenGSCoreの正式名
            }
            else if (lowerMode == "survival" || lowerMode == "suv")
            {
                str = "suv";
                mode = eGameMode.Survival; // OpenGSCoreの正式名
            }
            else if (lowerMode == "teamsurvival" || lowerMode == "tsuv")
            {
                str = "tsuv";
                mode = eGameMode.TeamSurvival; // OpenGSCoreの正式名
            }
            else if (lowerMode == "capturetheflag" || lowerMode == "ctf")
            {
                str = "ctf";
                mode = eGameMode.CTF;
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
    }
}
