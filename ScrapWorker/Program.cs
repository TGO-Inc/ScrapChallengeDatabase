using SteamWorkshop.WebAPI;
using SteamWorkshop.WebAPI.IPublishedFileService;
using System.Net;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using ScrapWorker;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Security.Cryptography;
using System;
using SteamKit2;
using Steamworks;
using System.Net.Http.Headers;
using System.Net.Http;
using ScrapWorker.Steam;
using SteamWorkshop.WebAPI.Internal;
using ScrapWorker.HTTP;

namespace ScrapWorker
{
    public class Program
    {        
        private static readonly Dictionary<uint, string> Apps = new() {
            {387990, "Scrap Mechanic"},
            {588870, "Scrap Mechanic Mod Tool"}
        };
        public static async Task Main(string[] args)
        {
            CancellationTokenSource cts = new();

            var uname = CredentialManager.GetUsername();
            var password = CredentialManager.GetPassword();
            var apikey = CredentialManager.GetSteamAPIKey();

            var Logger = new ConsoleManager(cts.Token);
            var SteamSession = new Steam3Session(uname, password);
            var SteamDB = new SteamDBManager(SteamSession, Apps, cts.Token, Logger);
            var WorkshopTool = new WorkshopScraper(SteamSession, apikey, cts.Token, Logger);
            var UpdateService = new UpdateRequestService(WorkshopTool, cts.Token, Logger);

            Logger.StartOutput();
            SteamDB.StartWatching();
            SteamSession.Connect();
            WorkshopTool.ForceRunTasks();
            UpdateService.StartService();

            // Wait for a cancellation request (e.g. user pressing Ctrl+C)
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Cancellation requested. Waiting for ongoing tasks to complete...");

                // Dispose and perform final tasks
                UpdateService.WaitForExit();

                SteamSession.Disconnect();
                SteamDB.WaitForExit();
                WorkshopTool.WaitForExit();

                Logger.WaitForExit();

                Console.WriteLine($"Program Terminated at [{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local)}]");
                Environment.Exit(0);
            }
        }
    }
}