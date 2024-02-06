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
        private Dictionary<uint, string> GlobalAppList = [];
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
            this.GlobalAppList = Apps;
            var (_, Url) = WebhookUrlList.First();

            string json = JsonConvert.SerializeObject(Apps.ToDictionary(), Formatting.Indented).Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
            string content = $"{{\"content\":\"[{Entry.VersionInfo}] => SteamDB tracker started for:\\n```\\n{json}\\n```\", \"flags\": 2}}";

            Logger?.WriteLine($"[{this.GetType().FullName}]: Loaded Webhook {Url}");
            await this.HttpClient.PostAsync(Url, new StringContent(content, MediaTypeHeaderValue.Parse("application/json")));            
        }

        public void SendWebhookMessage(uint appid, string jsonContent)
        {
            Logger?.WriteLine($"[Recieved Message Request]: Appid {appid}");

            foreach (var (AppIds, Url) in WebhookUrlList)
            {
                Logger?.WriteLine($"Processing {Url.Substring(60, 9)}: {{ {string.Join(", ", AppIds)} }}");

                if (!AppIds.Contains(appid))
                    continue;

                this.HttpClient.PostAsync(Url, new StringContent(jsonContent, MediaTypeHeaderValue.Parse("application/json"))).GetAwaiter().GetResult();
            }
        }
    }
}
