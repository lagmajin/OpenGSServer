using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OpenGSServer
{
    internal interface IAccountEventHandler
    {
    }

    internal sealed class AccountEventHandler : IAccountEventHandler
    {
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
            session.SendAsyncJsonWithTimeStamp(result.ToJson());
        }

        public static string? Login(in IClientSession session, in IDictionary<string, JToken> dic)
        {
            if (!TryReadString(dic, "id", out var id) || !TryReadString(dic, "pass", out var pass))
            {
                return null;
            }

            var result = AccountManager.GetInstance().Login(id, pass);
            var json = result.ToJson();
            json["YourIPAddress"] = session.ClientIpAddress();
            session.SendAsyncJsonWithTimeStamp(json);

            var encryptManager = EncryptManager.Instance;
            var keyJson = new JObject
            {
                [ServerMessageTypes.MessageType] = ServerMessageTypes.EncryptKey,
                ["RSAPublicKey"] = encryptManager.GetRSAPublicKey()
            };

            session.SendAsyncJsonWithTimeStamp(keyJson);
            return result.succeeded ? id : null;
        }

        public static void Logout(IClientSession session)
        {
            if (session is ClientSession clientSession && !string.IsNullOrWhiteSpace(clientSession.PlayerID))
            {
                AccountManager.GetInstance().Logout(clientSession.PlayerID, false);
            }
        }

        public static void Logout(ClientSession session, in IDictionary<string, JToken> dic)
        {
            var id = dic.TryGetValue("id", out var idToken) ? idToken.ToString() : string.Empty;
            var pass = dic.TryGetValue("pass", out var passToken) ? passToken.ToString() : string.Empty;

            var result = AccountManager.GetInstance().LogoutWithPassword(id, pass);
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

