using System;
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
            if (TryReadString(dic, "ResumeToken", out var resumeToken) &&
                ResumeTokens.TryRedeem(resumeToken, out var resumedAccountId))
            {
                return CompleteLogin(session, resumedAccountId, replaceStaleSession: true);
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

            return CompleteLogin(session, id, replaceStaleSession: false);
        }

        /// <summary>
        /// Reports a successful login and hands back the token that will stand in
        /// for the password next time.
        /// </summary>
        private static string? CompleteLogin(
            in IClientSession session,
            string accountId,
            bool replaceStaleSession)
        {
            // The account has to be a known one before a token is minted for it.
            // A resumed token names an account that existed when it was issued, but
            // the account may have been deleted since.
            var account = AccountDatabaseManager.GetInstance().GetAccount(accountId);
            if (account == null)
            {
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
                manager.RemoveAccount(accountId);
            }

            // The resumed session is logged on the same way a password login is,
            // so a client that reconnects is not a second session for one account.
            manager.AddNewLogonUser(new DBAccount
            {
                AccountId = accountId,
                DisplayName = account.DisplayName,
                HashedPassword = account.HashedPassword,
                Salt = account.Salt
            });

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

        private static string? SendLoginFailure(in IClientSession session, in LoginResult result)
        {
            session.SendAsyncJsonWithTimeStamp(result.ToJson());
            return null;
        }

        public static void Logout(IClientSession session)
        {
            if (session is ClientSession clientSession && !string.IsNullOrWhiteSpace(clientSession.PlayerID))
            {
                AccountManager.GetInstance().Logout(clientSession.PlayerID, false);

                // A token handed out before the logout must not be a way back in.
                ResumeTokens.Revoke(clientSession.PlayerID);
            }
        }

        public static void Logout(ClientSession session, in IDictionary<string, JToken> dic)
        {
            var id = dic.TryGetValue("id", out var idToken) ? idToken.ToString() : string.Empty;
            var pass = dic.TryGetValue("pass", out var passToken) ? passToken.ToString() : string.Empty;

            var result = AccountManager.GetInstance().LogoutWithPassword(id, pass);
            ResumeTokens.Revoke(id);

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

