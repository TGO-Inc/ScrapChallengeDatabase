using Newtonsoft.Json;
using ScrapWorker.Managers;
using SteamWorkshop.WebAPI.Managers;
using System;
using System.Net.Http.Headers;

namespace ScrapWorker.HTTP
{
    internal class DiscordWebhookManager(ConsoleManager? Logger = null)
    {
        private readonly HttpClient HttpClient = new();
        private Dictionary<uint, string> Apps = [];
        private static List<(IEnumerable<uint> AppIds, string Url)> WebhookUrlList => CredentialManager.GetWebhookUrls();

        public DiscordWebhookManager(Dictionary<uint, string> Apps, bool silent = false, ConsoleManager? Logger = null)
            : this(Logger)
        {
            if(!silent) this.StartupMessage(Apps);
        }
        public DiscordWebhookManager(Dictionary<uint, string> Apps, ConsoleManager? Logger = null)
            : this(Logger)
        {
            this.StartupMessage(Apps);
        }

        private async void StartupMessage(Dictionary<uint, string> Apps)
        {
            this.Apps = Apps;
            var (AppIds, Url) = WebhookUrlList.First();

            foreach (var appid in AppIds)
            if (this.Apps.ContainsKey(appid))
            {
                string json = JsonConvert.SerializeObject(Apps.Where(i => AppIds.Contains(i.Key)).ToDictionary(), Formatting.Indented).Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
                string content = $"{{\"content\":\"SteamDB tracker started for:\\n```\\n{json}\\n```\", \"flags\": 2}}";

                Logger?.WriteLine($"[{this.GetType().FullName}]: Loaded Webhook {Url}");
                await this.HttpClient.PostAsync(Url, new StringContent(content, MediaTypeHeaderValue.Parse("application/json")));
                break;
            }
        }

        public void SendWebhookMessage(string jsonContent)
        {
            foreach (var (AppIds, Url) in WebhookUrlList)
            foreach (var appid in AppIds)
            if (this.Apps.ContainsKey(appid))
            {
                this.HttpClient.PostAsync(Url, new StringContent(jsonContent, MediaTypeHeaderValue.Parse("application/json"))).GetAwaiter().GetResult();
                break;
            }    
        }
    }
}
