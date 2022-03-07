using System;
using System.Collections.Generic;
using System.Text;

namespace Deprecation
{




    public enum eGameMode : byte
    {
        DeathMatch=0,
        Practice,
        FreeStyle,
        TDM,
        Ichigeki,
        Sniper,
        TowerMatch,
        SUV,
        TSUV,
        CTF,
        Unknown,

    }

    public class GameMode
    {
        eGameMode mode=eGameMode.Unknown;

        private string str = "";


        public GameMode(eGameMode mode)
        {

        }

        public GameMode(string mode)
        {
            mode = mode.ToLower();
            str = "unkonown";

            

            if(mode=="DeathMatch" || mode=="DM")
            {
                str = "dm";
            }

            if (mode == "TeamDeathMatch" || mode == "TDM")
            {
                str = "tdm";
            }

            if (mode=="Survival"|| mode=="SUV")
            {
                str = "suv";
            }
            if (mode == "TeamSurvival" || mode == "TSUV")
            {
                str = "tsuv";
            }

            if(mode=="CaptureTheFlag" || mode=="CTF")
            {
                str = "ctf";
            }

            

        }

        public string name()
        {
            return str;
        }

        public bool Valid()
        {
            return false;
        }

        internal eGameMode Mode { get => mode; set => mode = value; }
    }


}
