using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

#nullable enable

namespace OpenGSServer
{
    internal interface IAccountEventHandler
    {
    }

    internal sealed class AccountEventHandler : IAccountEventHandler
    {
        /// <summary>
        /// The resume tokens handed out when a login succeeds.
        /// <para>
        /// A client opening the lobby connection has no password to send, and the
        /// player id on its own would let anyone be anyone, so a client that has
        /// logged in once reconnects on the token it was given.
        /// </para>
        /// </summary>
        private static readonly LobbyResumeTokens ResumeTokens = new();
        private static readonly ConcurrentDictionary<string, string> SessionOwners =
            new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, object> SessionGates =
            new(StringComparer.OrdinalIgnoreCase);

        public static void CreateNewAccount(in IClientSession session, IDictionary<string, JToken> dic)
        {
            if (!TryReadString(dic, "AccountID", out var accountId) ||
                !TryReadString(dic, "Password", out var password) ||
                !TryReadString(dic, "DisplayName", out var displayName))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    [ServerMessageTypes.MessageType] = ServerMessageTypes.Error,
                    ["Message"] = "AccountID, Password and DisplayName are required"
                });
                return;
            }

            var result = AccountManager.GetInstance().CreateNewAccount(accountId, password, displayName);
            var response = result.ToJson();
            var account = AccountDatabaseManager.GetInstance().GetAccount(accountId);
            if (account != null)
            {
                response["DisplayName"] = account.DisplayName;
                response["Credits"] = account.Credits;
                response["Level"] = account.Level;
                response["Exp"] = account.Exp;
            }

            session.SendAsyncJsonWithTimeStamp(response);
        }

        public static string? Login(in IClientSession session, in IDictionary<string, JToken> dic)
        {
            // A token replaces the password for the reconnect, never alongside it:
            // a client holding a live token that also sends a password is a
            // client whose token should not decide who it is.
            if (TryReadString(dic, "ResumeToken", out var resumeToken))
            {
                if (!ResumeTokens.TryGetAccountId(resumeToken, out var resumedAccountId))
                {
                    session.SendAsyncJsonWithTimeStamp(new JObject
                    {
                        [ServerMessageTypes.MessageType] = MessageType.LoginResponse,
                        ["Success"] = false,
                        ["Error"] = "ResumeToken is invalid or expired"
                    });
                    return null;
                }

                return CompleteLogin(
                    session, resumedAccountId, replaceStaleSession: true, resumeToken: resumeToken);
            }

            if (!TryReadFirstString(dic, out var id, "id", "AccountID", "AccountId", "PlayerID") ||
                !TryReadFirstString(dic, out var pass, "pass", "Password"))
            {
                session.SendAsyncJsonWithTimeStamp(new JObject
                {
                    [ServerMessageTypes.MessageType] = MessageType.LoginResponse,
                    ["Success"] = false,
                    ["Error"] = "AccountID and Password are required, or a ResumeToken from an earlier login"
                });
                return null;
            }

            var result = AccountManager.GetInstance().Login(id, pass);
            if (!result.succeeded)
            {
                return SendLoginFailure(session, result);
            }

            TryReadFirstString(dic, out var playerName, "PlayerName", "DisplayName");
            return CompleteLogin(session, id, replaceStaleSession: false,
                string.IsNullOrWhiteSpace(playerName) ? null : playerName);
        }

        /// <summary>
        /// Reports a successful login and hands back the token that will stand in
        /// for the password next time.
        /// </summary>
        private static string? CompleteLogin(
            in IClientSession session,
            string accountId,
            bool replaceStaleSession,
            string? playerName = null,
            string? resumeToken = null)
        {
            lock (SessionGates.GetOrAdd(accountId, static _ => new object()))
            {
                return CompleteLoginCore(session, accountId, replaceStaleSession, playerName, resumeToken);
            }
        }

        private static string? CompleteLoginCore(
            in IClientSession session,
            string accountId,
            bool replaceStaleSession,
            string? playerName,
            string? resumeToken)
        {
            if (replaceStaleSession &&
                (string.IsNullOrWhiteSpace(resumeToken) ||
                 !ResumeTokens.TryRedeem(resumeToken, out var tokenAccountId) ||
                 !string.Equals(tokenAccountId, accountId, StringComparison.OrdinalIgnoreCase)))
            {
                return SendLoginFailure(session, new LoginResult(accountId, eLoginResultType.Unknown));
            }

            // The account has to be a known one before a token is minted for it.
            // A resumed token names an account that existed when it was issued, but
            // the account may have been deleted since.
            var account = AccountDatabaseManager.GetInstance().GetAccount(accountId);
            if (account == null)
            {
                if (!replaceStaleSession)
                {
                    AccountManager.GetInstance().Logout(accountId, false);
                }
                return SendLoginFailure(
                    session,
                    new LoginResult(accountId, eLoginResultType.AccountNotFound));
            }

            var manager = AccountManager.GetInstance();

            // A client that resumes is the same client as before, so the session
            // its previous connection left behind is replaced rather than refused.
            // A crash is the usual reason it is still there, and refusing the
            // reconnect would strand the account until it logged out on its own.
            if (replaceStaleSession)
            {
                // Release every lobby, wait-room, and match registration left
                // by the previous connection before the new session joins.
                // The account gate prevents its delayed disconnect from
                // cleaning up the replacement session afterwards.
                if (SessionOwners.TryRemove(accountId, out _))
                {
                    PlayerSessionCleanup.Release(accountId);
                }
                manager.RemoveAccount(accountId);
                // The resumed session replaces stale server-side state. Do not
                // acknowledge the resume unless both account records were added.
                if (!manager.AddNewLogonUser(new DBAccount
                {
                    AccountId = accountId,
                    DisplayName = account.DisplayName,
                    HashedPassword = account.HashedPassword,
                    Salt = account.Salt
                }))
                {
                    return SendLoginFailure(
                        session,
                        new LoginResult(accountId, eLoginResultType.Unknown));
                }
            }

            if (session is ClientSession clientSession)
            {
                if (clientSession.IsDisconnected)
                {
                    manager.Logout(accountId, false);
                    return SendLoginFailure(
                        session,
                        new LoginResult(accountId, eLoginResultType.Unknown));
                }

                clientSession.SetPlayerID(accountId);
                SessionOwners[accountId] = clientSession.ID();
                var lobbyJoin = LobbyServerManager.Instance.PlayerJoinLobby(
                    accountId, string.IsNullOrWhiteSpace(playerName) ? account.DisplayName : playerName);
                if (!lobbyJoin.IsSuccess)
                {
                    SessionOwners.TryRemove(accountId, out _);
                    clientSession.ClearPlayerID();
                    PlayerSessionCleanup.Release(accountId);
                    return SendLoginFailure(
                        session,
                        new LoginResult(accountId, eLoginResultType.Unknown));
                }
            }

            var json = new JObject
            {
                [ServerMessageTypes.MessageType] = OpenGSCore.MessageType.LoginResponse,
                ["Success"] = true,
                ["AccountID"] = accountId,
                ["PlayerID"] = accountId,
                ["GlobalUserId"] = accountId,
                ["DisplayName"] = account.DisplayName,
                ["Credits"] = account.Credits,
                ["Level"] = account.Level,
                ["Exp"] = account.Exp,
                ["YourIPAddress"] = session.ClientIpAddress(),
                ["ResumeToken"] = ResumeTokens.Issue(accountId)
            };

            session.SendAsyncJsonWithTimeStamp(json);

            var encryptManager = EncryptManager.Instance;
            var keyJson = new JObject
            {
                [ServerMessageTypes.MessageType] = ServerMessageTypes.EncryptKey,
                ["RSAPublicKey"] = encryptManager.GetRSAPublicKey()
            };

            session.SendAsyncJsonWithTimeStamp(keyJson);
            return accountId;
        }

        internal static bool ReleaseOwnedSession(
            ClientSession session, string? playerId, bool revokeResumeToken = false)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                return false;
            }

            lock (SessionGates.GetOrAdd(playerId, static _ => new object()))
            {
                if (!SessionOwners.TryGetValue(playerId, out var ownerId) ||
                    !string.Equals(ownerId, session.ID(), StringComparison.Ordinal) ||
                    !((ICollection<KeyValuePair<string, string>>)SessionOwners).Remove(
                        new KeyValuePair<string, string>(playerId, ownerId)))
                {
                    return false;
                }

                PlayerSessionCleanup.Release(playerId, revokeResumeToken);
                return true;
            }
        }

        private static string? SendLoginFailure(in IClientSession session, in LoginResult result)
        {
            session.SendAsyncJsonWithTimeStamp(result.ToJson());
            return null;
        }

        public static void Logout(IClientSession session)
        {
            if (session is ClientSession clientSession && !string.IsNullOrWhiteSpace(clientSession.PlayerID))
            {
                ReleaseOwnedSession(clientSession, clientSession.PlayerID, revokeResumeToken: true);
            }
        }

        internal static void RevokeResumeToken(string accountId) => ResumeTokens.Revoke(accountId);

        internal static bool KickPlayerSession(string playerId, Func<bool>? condition = null)
        {
            if (string.IsNullOrWhiteSpace(playerId)) return false;

            lock (SessionGates.GetOrAdd(playerId, static _ => new object()))
            {
                var accountManager = AccountManager.GetInstance();
                if (!accountManager.ExistID(playerId)) return false;
                if (condition != null && !condition()) return false;

                SessionOwners.TryRemove(playerId, out _);
                PlayerSessionCleanup.Release(playerId, revokeResumeToken: true);
                LobbyServerManager.Instance.FindSessionByPlayerId(playerId)?.Disconnect();
                return true;
            }
        }

        public static void Logout(ClientSession session, in IDictionary<string, JToken> dic)
        {
            var id = dic.TryGetValue("id", out var idToken) ? idToken.ToString() : string.Empty;
            var pass = dic.TryGetValue("pass", out var passToken) ? passToken.ToString() : string.Empty;

            JObject result;
            lock (SessionGates.GetOrAdd(id, static _ => new object()))
            {
                result = AccountManager.GetInstance().LogoutWithPassword(id, pass);
                if ((bool?)result["Success"] == true)
                {
                    SessionOwners.TryRemove(id, out _);
                    PlayerSessionCleanup.Release(id, revokeResumeToken: true);
                    if (session.PlayerID != null &&
                        string.Equals(session.PlayerID, id, StringComparison.OrdinalIgnoreCase))
                    {
                        session.ClearPlayerID();
                    }
                }
            }

            session.SendAsyncJsonWithTimeStamp(result);
        }

        private static bool TryReadString(IDictionary<string, JToken> dic, string key, out string value)
        {
            value = string.Empty;
            if (!dic.TryGetValue(key, out var token))
            {
                return false;
            }

            value = token?.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryReadFirstString(IDictionary<string, JToken> dic, out string value, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (TryReadString(dic, key, out value))
                {
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        internal static bool RunIfOwnedSession(ClientSession session, string playerId, Action action)
        {
            lock (SessionGates.GetOrAdd(playerId, static _ => new object()))
            {
                if (!SessionOwners.TryGetValue(playerId, out var ownerId) ||
                    !string.Equals(ownerId, session.ID(), StringComparison.Ordinal))
                {
                    return false;
                }

                if (session.IsDisconnected)
                {
                    if (((ICollection<KeyValuePair<string, string>>)SessionOwners).Remove(
                            new KeyValuePair<string, string>(playerId, ownerId)))
                    {
                        PlayerSessionCleanup.Release(playerId);
                    }
                    return false;
                }

                action();
                return true;
            }
        }
    }

    [Obsolete("Use AccountEventHandler static methods.")]
    public static class OldAccountEventHandler
    {
        public static void CreateNewAccount(in ClientSession session, IDictionary<string, JToken> dic)
        {
            AccountEventHandler.CreateNewAccount(session, dic);
        }

        public static string? Login(in IClientSession session, in IDictionary<string, JToken> dic)
        {
            return AccountEventHandler.Login(session, dic);
        }

        public static void Logout(IClientSession session)
        {
            AccountEventHandler.Logout(session);
        }

        public static void Logout(ClientSession session, in IDictionary<string, JToken> dic)
        {
            AccountEventHandler.Logout(session, dic);
        }
    }
}

