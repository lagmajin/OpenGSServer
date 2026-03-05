using System;
using OpenGSCore;

namespace OpenGSServer
{
    public static class GameModeFactory
    {
        public static IMatchLogic CreateGameMode(EGameMode mode)
        {
            switch (mode)
            {
                case EGameMode.Survival:
                    return new SurvivalMode();
                case EGameMode.DeathMatch:
                    return new DeathMatchMode();
                case EGameMode.TeamDeathMatch:
                    return new TeamDeathMatchMode();
                case EGameMode.CaptureTheFlag:
                    return new CaptureTheFlagMode();
                case EGameMode.OneShotKill:
                    return new OneShotKillMode();
                case EGameMode.ArmsRace:
                    return new ArmsRaceMode();
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }
    }
}
