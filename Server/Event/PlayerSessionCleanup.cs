using System;

#nullable enable

namespace OpenGSServer
{
    /// <summary>
    /// The single place that releases the server side state a player holds when
    /// their lobby session ends.
    /// <para>
    /// S1 asks for one backend authority for lobby state. The explicit
    /// LogoutRequest path and the dropped connection path each had their own
    /// inline cleanup, and they had already drifted: the disconnect path also
    /// cleared in-game match state and invalidated the account, while the
    /// logout path did neither. Routing both through here keeps one ordering
    /// and one failure policy.
    /// </para>
    /// </summary>
    public static class PlayerSessionCleanup
    {
        /// <summary>
        /// Releases every piece of server side state a player holds: match
        /// state, lobby membership, wait room membership, and the account
        /// session itself.
        /// <para>
        /// Invalidating the account on a dropped connection is deliberate. The
        /// account manager treats a second login of an already active account
        /// as a duplicate, so leaving the account held would make every
        /// reconnect after a dropped socket fail.
        /// </para>
        /// </summary>
        /// <param name="playerId">The player whose state should be released.</param>
        /// <returns>True when a player id was given and cleanup ran.</returns>
        public static bool Release(string? playerId)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            // Each step is isolated so one failing subsystem cannot leave the
            // player half registered somewhere else.
            try
            {
                InGameMatchEventHandler.ClearPlayerState(playerId);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Cleanup] Match state clear failed for {playerId}: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                LobbyServerManager.Instance.PlayerLeaveLobby(playerId);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Cleanup] Lobby leave failed for {playerId}: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                WaitRoomEventHandler.RemoveDisconnectedPlayer(playerId);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Cleanup] Wait room removal failed for {playerId}: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                AccountManager.GetInstance().Logout(playerId, false);
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[Cleanup] Account logout failed for {playerId}: {ex.Message}", ConsoleColor.Red);
            }

            return true;
        }
    }
}