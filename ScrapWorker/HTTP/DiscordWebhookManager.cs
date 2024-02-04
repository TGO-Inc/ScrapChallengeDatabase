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
        private Dictionary<uint, string> Apps;
        private static Dictionary<IEnumerable<uint>, string> WebhookUrlList => CredentialManager.GetWebhookUrls();

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

            foreach (var obj in WebhookUrlList)
            foreach(var appid in obj.Key)
            if (this.Apps.ContainsKey(appid))
            {
                string json = JsonConvert.SerializeObject(Apps.Where(i => obj.Key.Contains(i.Key)), Formatting.Indented).Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
                string content = $"{{\"content\":\"SteamDB tracker started for:\\n```\\n{json}\\n```\", \"flags\": 2}}";

                Logger?.WriteLine($"[{this.GetType().FullName}]: Loaded Webhook {obj.Value}");
                await this.HttpClient.PostAsync(obj.Value, new StringContent(content, MediaTypeHeaderValue.Parse("application/json")));
                break;
            }
        }

        public void SendWebhookMessage(string jsonContent)
        {
            foreach (var obj in WebhookUrlList)
            foreach (var appid in obj.Key)
            if (this.Apps.ContainsKey(appid))
            {
                this.HttpClient.PostAsync(obj.Value, new StringContent(jsonContent, MediaTypeHeaderValue.Parse("application/json"))).GetAwaiter().GetResult();
                break;
            }    
        }
    }
}
