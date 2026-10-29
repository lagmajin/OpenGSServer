using System;
using System.Threading;

using Autofac;

#nullable enable

namespace OpenGSServer
{
    /// <summary>
    /// Owns the server bootstrap and shutdown sequence.
    /// <para>
    /// This exists so that <see cref="Program"/> only has to parse arguments and
    /// decide when to stop. Everything that touches a listener, the database, or
    /// the settings file lives here, in a fixed order, so startup and shutdown
    /// are explicit and repeatable.
    /// </para>
    /// </summary>
    public sealed class ServerHost : IDisposable
    {
        private const string InstanceMutexName = @"Global\OpenGSServer";

        private readonly ServerStartupOptions options;
        private readonly ServerBatchService batchService = new();

        private Mutex? instanceMutex;
        private bool ownsMutex;
        private int disposed;

        public ServerHost(ServerStartupOptions options)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Tries to become the single running server instance.
        /// Returns <see langword="false"/> when another instance holds the mutex.
        /// </summary>
        public bool TryAcquireInstanceLock()
        {
            instanceMutex = new Mutex(true, InstanceMutexName, out var createdNew);
            ownsMutex = createdNew;
            return createdNew;
        }

        /// <summary>
        /// Loads configuration and credentials, connects the database, then starts
        /// the lobby, match, and management listeners in that order.
        /// </summary>
        public void Start()
        {
            // Configuration must be available before any network service depends on it.
            ServerManager.Instance.LoadSetting();
            ServerManager.Instance.InitializeAdminAccounts();

            // Touch the crypto singleton so the RSA key pair exists before any
            // listener can hand out a public key.
            _ = EncryptManager.Instance;

            AccountDatabaseManager.GetInstance().Connect();

            var builder = new ContainerBuilder();
            builder.RegisterInstance(LobbyServerManager.Instance).AsSelf().SingleInstance();
            builder.RegisterInstance(batchService).AsSelf().SingleInstance();
            builder.RegisterType<ManagementServer>().AsSelf().SingleInstance();
            builder.RegisterType<AccountEventHandler>().As<IAccountEventHandler>().SingleInstance();

            var container = builder.Build();

            var lobbyServer = container.Resolve<LobbyServerManager>();
            lobbyServer.StartTcpServer(options.LobbyPort);

            var matchServer = MatchServerV2.Instance;
            matchServer.Listen(options.MatchTcpPort, options.MatchUdpPort, options.PublicIp);
            matchServer.EnableMultiCore();

            ManagementServer.Instance.Listen(options.ManagementPort);

            // S3: give the loading handshake a deadline, so a client that never
            // reports completion releases its room instead of locking it.
            WaitRoomEventHandler.StartLoadingTimeoutMonitor();

            if (lobbyServer.IsTcpServerRunning && lobbyServer.TcpPort is int tcpPort)
            {
                batchService.WriteLocalPortToFile(tcpPort);
            }

            batchService.Start();

            ConsoleWrite.WriteMessage("System all green...", ConsoleColor.Green);
        }

        /// <summary>
        /// Stops every service in the reverse of the startup order. Safe to call
        /// more than once; later calls do nothing.
        /// </summary>
        public void Shutdown()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            StopBatchService();

            WaitRoomEventHandler.StopLoadingTimeoutMonitor();

            DisposeServers();

            try
            {
                ServerManager.Instance.SaveSetting();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Setting save failed: {ex.Message}", ConsoleColor.Red);
            }

            ReleaseInstanceLockCore();
        }

        private void StopBatchService()
        {
            try
            {
                batchService.Stop();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Batch shutdown failed: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                batchService.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Batch dispose failed: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Disposes the network listeners. A failure in one listener must not stop
        /// the others from being closed.
        /// </summary>
        private static void DisposeServers()
        {
            try
            {
                LobbyServerManager.Instance.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Lobby shutdown failed: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                MatchServerV2.Instance.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Match shutdown failed: {ex.Message}", ConsoleColor.Red);
            }

            try
            {
                ManagementServer.Instance.Dispose();
            }
            catch (Exception ex)
            {
                ConsoleWrite.WriteMessage($"[ERR] Management shutdown failed: {ex.Message}", ConsoleColor.Red);
            }
        }

        /// <summary>
        /// Releases the instance mutex without touching any service. Used when
        /// startup is abandoned because another instance already owns the mutex.
        /// </summary>
        public void ReleaseInstanceLock() => ReleaseInstanceLockCore();

        private void ReleaseInstanceLockCore()
        {
            if (instanceMutex is null)
            {
                return;
            }

            if (ownsMutex)
            {
                try
                {
                    instanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // The mutex was already released; nothing left to do.
                }

                ownsMutex = false;
            }

            instanceMutex.Close();
            instanceMutex = null;
        }

        public void Dispose() => Shutdown();
    }
}
