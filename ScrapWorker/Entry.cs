using ScrapWorker.Steam;
using SteamWorkshop.WebAPI.Internal;
using ScrapWorker.HTTP;
using ScrapWorker.Managers;
using SteamWorkshop.WebAPI.Managers;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Diagnostics;

namespace ScrapWorker
{
    public class Entry
    {
        public static readonly string VersionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion!;

        private static readonly Dictionary<uint, string> Apps = new() {
            { 387990, "Scrap Mechanic" },
            { 588870, "Scrap Mechanic Mod Tool" },
            { 1409160, "Plasma" }
        };

        public static async Task Main(string[] args)
        {
            Console.WriteLine($"[{DateTime.Now}] Process Start");
            
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => Console.WriteLine($"[{DateTime.Now}] Process Exit");
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                Console.Error.WriteLine($"Unhandled Exception");
                if (e.ExceptionObject.GetType().FullName!.Contains("http", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.Error.WriteLine($"HTTP Exception. Waiting before continue.");
                    Thread.Sleep(5000);
                }
            };

            CancellationTokenSource cts = new();
            // Wait for a cancellation request (e.g. user pressing Ctrl+C)
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.CancelAsync();
            };

            var uname = CredentialManager.GetUsername();
            var password = CredentialManager.GetPassword();
            var apikey = CredentialManager.GetSteamAPIKey();

            var Logger = new ConsoleManager(cts.Token);
            Logger.StartOutput();

            bool silent = args.Where(x => x.Contains("silent", StringComparison.InvariantCultureIgnoreCase)).Any()
                || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            var SteamSession = new Steam3Session(uname, password, Logger);
            var SteamDB = new SteamDBManager(SteamSession, Apps, cts.Token, Logger, silent);

            SteamSession.OnFailedToReconnect += () => SteamSessionOnFailedToReconnect(SteamSession, Logger);
            SteamDB.StartWatching();
            SteamSession.Connect();

            var WorkshopTool = new WorkshopScraper(SteamSession, apikey, cts.Token, Logger);
            var UpdateService = new UpdateRequestService(WorkshopTool, cts.Token, Logger);

            WorkshopTool.ForceRunTasks();
            UpdateService.StartService();

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{DateTime.Now}] Cancellation requested. Waiting for ongoing tasks to complete...");
                Console.ResetColor();

                // Dispose and perform final tasks
                UpdateService.WaitForExit();

                SteamSession.Disconnect();
                SteamDB.WaitForExit();
                WorkshopTool.WaitForExit();

                Logger.WaitForExit();

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[{DateTime.Now}] Program Terminated");
                Console.ResetColor();
            }
        }

        private static System.Threading.Timer? ReconnectionTask;

        private static void SteamSessionOnFailedToReconnect(Steam3Session session, ConsoleManager Logger)
        {
            ReconnectionTask ??= new Timer(_ =>
            {
                if (session.steamClient.IsConnected)
                {
                    session.SubscribeAll();
                    Logger?.WriteLine("[ReconnectTimer]: Steam client is connected.");
                    ReconnectionTask?.Dispose();
                    ReconnectionTask = null;
                }
                else
                {
                    Logger?.WriteLine("[ReconnectTimer]: Attempting to reconnect to Steam...");
                    session.Reconnect();
                }
            }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5));
        }
    }
}