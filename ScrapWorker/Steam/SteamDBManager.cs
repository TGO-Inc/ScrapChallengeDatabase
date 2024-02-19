using Newtonsoft.Json;
using SteamWorkshop.WebAPI.Internal;
using System.Timers;
using ScrapWorker.HTTP;
using Newtonsoft.Json.Bson;
using ScrapWorker.Managers;
using SteamWorkshop.WebAPI.Managers;

namespace ScrapWorker.Steam
{
    internal class SteamDBManager(Steam3Session session, Dictionary<uint, string> WatchList, CancellationToken cancellationToken, ConsoleManager? Logger = null, bool silent = false)
    {
        private readonly object LockObj = new();
        private uint LastChangeNumber = uint.MinValue;
        private readonly DiscordWebhookManager WebhookManager = new(WatchList, silent, Logger);
        private System.Threading.Timer? SteamChangeTimer;

        private readonly System.Timers.Timer? CallbackTimer = new()
        {
            Interval = 1000,
            AutoReset = false,
            Enabled = true
        };

        public void StartWatching()
        {
            this.SteamChangeTimer = new(OnTimerInterval, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            session.OnClientsLogin += (logon) =>
            {
                Logger?.WriteLine($"[{this.GetType().FullName}]: Client Logged In. Adjusting timer...");
                this.SteamChangeTimer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(2));

                this.CallbackTimer!.Elapsed += CallbackThread;
                this.CallbackTimer.Start();
            };

            session.OnClientsDisconnect += (disconnect) =>
            {
                Logger?.WriteLine($"[{this.GetType().FullName}]: Client Disconnected");
                this.SteamChangeTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

                this.CallbackTimer!.Elapsed -= CallbackThread;
                this.CallbackTimer.Stop();
            };

            session.OnPICSChanges += PICSChanged;
        }

        public void StopWatching()
        {
            this.CallbackTimer!.Stop();
            this.CallbackTimer.Close();
            this.CallbackTimer.Dispose();

            this.SteamChangeTimer!.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            this.SteamChangeTimer.Dispose();
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
            if (cancellationToken.IsCancellationRequested)
                return;

            try
            {
                var res = await session!.steamApps.PICSGetChangesSince(this.LastChangeNumber, true, true);
                #if RELEASE_VERBOSE
                    Logger?.WriteLine(JsonConvert.SerializeObject(res, Formatting.Indented));
                #endif
            }
            catch
            {

            }
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
                    $"{{\"content\":\"New SteamDB Change for App `{appName} ({app.ID})`\\nhttps://steamdb.info/app/{app.ID}/history/?changeid={app.ChangeNumber}\", \"flags\": 2}}}}";
                Logger?.WriteLine($"Message Content: {content}");

                this.WebhookManager.SendWebhookMessage(app.ID, content);
            }
        }
    }
}
