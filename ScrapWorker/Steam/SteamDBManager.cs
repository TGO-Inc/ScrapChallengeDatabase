﻿using Newtonsoft.Json;
using SteamWorkshop.WebAPI.Internal;
using System.Timers;
using ScrapWorker.HTTP;
using Newtonsoft.Json.Bson;

namespace ScrapWorker.Steam
{
    internal class SteamDBManager(Steam3Session session, Dictionary<uint, string> WatchList, CancellationToken cancellationToken, ConsoleManager? Logger = null)
    {
        private object LockObj = new();
        private readonly DiscordWebhookManager WebhookManager = new(WatchList, Logger);
        // private Timer? SteamChangeTimer;
        private readonly System.Timers.Timer? CallbackTimer = new()
        {
            Interval = 50,
            AutoReset = false,
            Enabled = true
        };
        private uint LastChangeNumber = uint.MinValue;

        public void StartWatching()
        {
            session.OnPICSChanges += PICSChanged;

            // this.SteamChangeTimer = new Timer(OnTimerInterval, session, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            session.OnClientsLogin += (logon) =>
            {
                Logger?.WriteLine($"[{this.GetType().FullName}]: Client Logged In. Adjusting timer...");
                // this.SteamChangeTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(2));
            };

            session.OnClientsDisconnect += (disconnect) =>
            {
                Logger?.WriteLine($"[{this.GetType().FullName}]: Client Disconnected");
                // this.SteamChangeTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            };

            this.CallbackTimer!.Elapsed += CallbackThread;
            this.CallbackTimer.Start();
        }

        public void StopWatching()
        {
            this.CallbackTimer!.Stop();
            this.CallbackTimer.Close();
            this.CallbackTimer.Dispose();
        }

        public void WaitForExit()
        {
            lock (this.LockObj)
            {
                this.StopWatching();
            }
        }
        private void CallbackThread(object? sender, ElapsedEventArgs e)
        {
            lock (this.LockObj)
            {
                session.callbacks.RunWaitAllCallbacks(TimeSpan.FromSeconds(1));
            }

            if (!cancellationToken.IsCancellationRequested)
                this.CallbackTimer!.Start();
        }

        private async void OnTimerInterval(object? state)
        {
            var session = state as Steam3Session;
            var res = await session!.steamApps.PICSGetChangesSince(this.LastChangeNumber, true, true);
            #if RELEASE_VERBOSE
                Logger?.WriteLine(JsonConvert.SerializeObject(res, Formatting.Indented));
            #endif
        }

        private void PICSChanged(SteamKit2.SteamApps.PICSChangesCallback callback)
        {
            if (callback.LastChangeNumber == callback.CurrentChangeNumber)
                return;
            
            if (callback.CurrentChangeNumber > this.LastChangeNumber)
                this.LastChangeNumber = callback.CurrentChangeNumber;
            
            var apps = callback.AppChanges.Where(app => WatchList.ContainsKey(app.Value.ID)).ToArray();
            if (apps.Length <= 0)
                return;
            
            Logger?.WriteLine($"[{this.GetType().FullName}]: PICS Change Update recieved");
            Logger?.WriteLine(JsonConvert.SerializeObject(callback, Formatting.Indented));
            
            foreach (var (_, app) in apps)
            {
                WatchList.TryGetValue(app.ID, out var appName);
                var content =
                    $"{{\"content\":\"New SteamDB Change for App `{appName} ({app.ID})`\nhttps://steamdb.info/app/{app.ID}/history/?changeid={app.ChangeNumber}\", \"flags\": 2}}}}";
                this.WebhookManager.SendWebhookMessage(content);
            }
        }
    }
}