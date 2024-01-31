using ScrapWorker.Steam;
using SteamWorkshop.WebAPI.Internal;
using ScrapWorker.HTTP;
using ScrapWorker.Managers;
using Newtonsoft.Json;
using SteamWorkshop.WebAPI.Managers;

namespace ScrapWorker
{
    public class Entry
    {        
        private static readonly Dictionary<uint, string> Apps = new() {
            {387990, "Scrap Mechanic"},
            {588870, "Scrap Mechanic Mod Tool"}
        };

        public static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new();

            // Wait for a cancellation request (e.g. user pressing Ctrl+C)
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var uname = CredentialManager.GetUsername();
            var password = CredentialManager.GetPassword();
            var apikey = CredentialManager.GetSteamAPIKey();

            var Logger = new ConsoleManager(cts.Token);
            Logger.StartOutput();

            bool silent = args.Where(x => x.Contains("silent", StringComparison.InvariantCultureIgnoreCase)).Any();

            var SteamSession = new Steam3Session(uname, password, Logger);
            var SteamDB = new SteamDBManager(SteamSession, Apps, cts.Token, Logger, silent);
            
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
                Console.WriteLine("Cancellation requested. Waiting for ongoing tasks to complete...");
                Console.ResetColor();

                // Dispose and perform final tasks
                UpdateService.WaitForExit();

                SteamSession.Disconnect();
                SteamDB.WaitForExit();
                WorkshopTool.WaitForExit();

                Logger.WaitForExit();

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Program Terminated at [{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local)}]");
            }
        }
    }
}