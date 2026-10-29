using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Xunit;

namespace OpenGSServer.Tests
{
    /// <summary>
    /// The account store is a singleton over one database file, so tests that
    /// create accounts have to take turns with everything else rather than write
    /// to it at the same time.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class AccountDatabaseCollection
    {
        public const string Name = "account database";
    }

    /// <summary>
    /// A client that has logged in once reconnects on a token instead of its
    /// password.
    /// <para>
    /// The password belongs to the login screen and is deliberately not kept, so
    /// a client opening the lobby connection had nothing to authenticate with. It
    /// sent its player id alone, which the server could only refuse: accepting it
    /// would have let anyone be anyone, and refusing it meant no client ever got
    /// a player id, so nothing after the login worked either.
    /// </para>
    /// </summary>
    [Collection(AccountDatabaseCollection.Name)]
    public class LobbyResumeTokenTests
    {
        private class FakeSession : IClientSession
        {
            private readonly List<JObject> sent = new();

            public IReadOnlyList<JObject> Sent => sent;
            public string ClientIpAddress() => "127.0.0.1";
            public string ID() => Guid.NewGuid().ToString("N");

            public bool SendAsyncJsonWithTimeStamp(JObject json)
            {
                sent.Add(json);
                return true;
            }

            public JObject? LoginResponse()
            {
                foreach (var message in sent)
                {
                    var type = message["MessageType"]?.ToString();
                    if (type == OpenGSCore.MessageType.LoginResponse)
                    {
                        return message;
                    }
                }

                return null;
            }
        }

        // The account store is process wide and outlives a single test, so each
        // test needs an account of its own rather than one shared name.
        private static string NewAccountId() => "resume-test-" + Guid.NewGuid().ToString("N");

        private const string Password = "correct-horse";

        private static FakeSession CreateAccount(string accountId)
        {
            AccountManager.GetInstance().CreateNewAccount(accountId, Password, "Tester");

            // A login will fail if the account was not really created, and that
            // looks here like a token that does not work.
            Assert.True(
                AccountDatabaseManager.GetInstance().GetAccount(accountId) != null,
                "the test account was not created");

            return new FakeSession();
        }

        private static IDictionary<string, JToken> PasswordLogin(string accountId, string password)
        {
            return new Dictionary<string, JToken>
            {
                ["AccountID"] = accountId,
                ["Password"] = password
            };
        }

        // ---- The reconnect -----------------------------------------------

        [Fact]
        public void ATokenLetsAClientReconnectWithoutItsPassword()
        {
            var accountId = NewAccountId();
            var session = CreateAccount(accountId);
            var first = AccountEventHandler.Login(session, PasswordLogin(accountId, Password));
            var token = (string?)session.LoginResponse()?["ResumeToken"];

            Assert.Equal(accountId, first);
            Assert.False(string.IsNullOrWhiteSpace(token));

            var reconnect = new FakeSession();
            var resumed = AccountEventHandler.Login(
                reconnect, new Dictionary<string, JToken> { ["ResumeToken"] = token! });

            Assert.Equal(accountId, resumed);
            Assert.True((bool?)reconnect.LoginResponse()?["Success"]);
        }

        [Fact]
        public void TheReconnectAnswersWithAFreshToken()
        {
            // A token is spent when it is used, so the client has to be given a new
            // one or it can only ever reconnect once.
            var accountId = NewAccountId();
            var session = CreateAccount(accountId);
            AccountEventHandler.Login(session, PasswordLogin(accountId, Password));
            var token = (string?)session.LoginResponse()?["ResumeToken"];

            var reconnect = new FakeSession();
            AccountEventHandler.Login(reconnect, new Dictionary<string, JToken> { ["ResumeToken"] = token! });
            var next = (string?)reconnect.LoginResponse()?["ResumeToken"];

            Assert.False(string.IsNullOrWhiteSpace(next));
            Assert.NotEqual(token, next);
        }

        [Fact]
        public void ATokenWorksOnlyOnce()
        {
            // A token copied off the wire is good for one connection, not for the
            // life of the account.
            var accountId = NewAccountId();
            var session = CreateAccount(accountId);
            AccountEventHandler.Login(session, PasswordLogin(accountId, Password));
            var token = (string?)session.LoginResponse()?["ResumeToken"];

            var first = new FakeSession();
            AccountEventHandler.Login(first, new Dictionary<string, JToken> { ["ResumeToken"] = token! });

            var second = new FakeSession();
            var replayed = AccountEventHandler.Login(second, new Dictionary<string, JToken> { ["ResumeToken"] = token! });

            Assert.Null(replayed);
            Assert.False((bool?)second.LoginResponse()?["Success"]);
        }

        [Fact]
        public void AClientCanResumeAfterItsPreviousSessionWasLeftBehind()
        {
            // The usual reason to resume is a crash or a dropped connection, which
            // leaves the account still marked as logged on. Refusing the reconnect
            // because of that would strand the account until it logged out itself.
            var accountId = NewAccountId();
            var session = CreateAccount(accountId);
            AccountEventHandler.Login(session, PasswordLogin(accountId, Password));
            var token = (string?)session.LoginResponse()?["ResumeToken"];

            var reconnect = new FakeSession();
            var resumed = AccountEventHandler.Login(
                reconnect, new Dictionary<string, JToken> { ["ResumeToken"] = token! });

            Assert.Equal(accountId, resumed);
            Assert.True((bool?)reconnect.LoginResponse()?["Success"]);
        }

        [Fact]
        public void TheTokenFromTheEarlierSessionIsSpentByTheResume()
        {
            var accountId = NewAccountId();
            var session = CreateAccount(accountId);
            AccountEventHandler.Login(session, PasswordLogin(accountId, Password));
            var first = (string?)session.LoginResponse()?["ResumeToken"];

            var reconnect = new FakeSession();
            AccountEventHandler.Login(reconnect, new Dictionary<string, JToken> { ["ResumeToken"] = first! });
            var second = (string?)reconnect.LoginResponse()?["ResumeToken"];

            var replay = new FakeSession();
            var result = AccountEventHandler.Login(replay, new Dictionary<string, JToken> { ["ResumeToken"] = first! });

            Assert.Null(result);
            Assert.False(string.IsNullOrWhiteSpace(second));
        }

        [Fact]
        public void ATokenThatWasNeverIssuedIsRefused()
        {
            var session = new FakeSession();

            var result = AccountEventHandler.Login(
                session, new Dictionary<string, JToken> { ["ResumeToken"] = new string('a', 64) });

            Assert.Null(result);
            Assert.False((bool?)session.LoginResponse()?["Success"]);
        }

        [Fact]
        public void APlayerIdOnItsOwnIsStillRefused()
        {
            // The whole point of the token: without one, the player id is a claim
            // rather than a credential, so it must not be enough.
            var accountId = NewAccountId();
            CreateAccount(accountId);
            var session = new FakeSession();

            var result = AccountEventHandler.Login(
                session, new Dictionary<string, JToken> { ["PlayerID"] = accountId });

            Assert.Null(result);
            Assert.False((bool?)session.LoginResponse()?["Success"]);
        }

        [Fact]
        public void AClientHoldingNoTokenAndNoPasswordIsToldWhatItNeeds()
        {
            var session = new FakeSession();

            AccountEventHandler.Login(session, new Dictionary<string, JToken>());

            var error = (string?)session.LoginResponse()?["Error"];
            Assert.Contains("ResumeToken", error ?? string.Empty);
        }

        [Fact]
        public void AWrongPasswordStillDoesNotGetAToken()
        {
            var accountId = NewAccountId();
            var session = CreateAccount(accountId);

            AccountEventHandler.Login(session, PasswordLogin(accountId, "wrong"));

            Assert.False((bool?)session.LoginResponse()?["Success"]);
            Assert.True(string.IsNullOrWhiteSpace((string?)session.LoginResponse()?["ResumeToken"]));
        }

        [Fact]
        public void ATokenIsBoundToTheAccountItWasIssuedFor()
        {
            // A token names who it is for. Handing one account's token to another
            // must not log in the account that presented it.
            var issuerId = NewAccountId();
            var otherId = NewAccountId();
            CreateAccount(issuerId);
            CreateAccount(otherId);

            var session = new FakeSession();
            AccountEventHandler.Login(session, PasswordLogin(issuerId, Password));
            var token = (string?)session.LoginResponse()?["ResumeToken"];

            var reconnect = new FakeSession();
            var resumed = AccountEventHandler.Login(
                reconnect, new Dictionary<string, JToken> { ["PlayerID"] = otherId, ["ResumeToken"] = token! });

            Assert.Equal(issuerId, resumed);
        }
    }
}
