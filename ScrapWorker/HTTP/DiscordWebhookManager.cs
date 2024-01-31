using Newtonsoft.Json;
using ScrapWorker.Managers;
using System.Net.Http.Headers;

namespace ScrapWorker.HTTP
{
    internal class DiscordWebhookManager(ConsoleManager? Logger = null)
    {
        private readonly HttpClient HttpClient = new();
        private static string[] WebhookUrlList => CredentialManager.GetWebhookUrls();

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
            string json = JsonConvert.SerializeObject(Apps, Formatting.Indented).Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
            string content = $"{{\"content\":\"SteamDB tracker started for:\\n```\\n{json}\\n```\", \"flags\": 2}}";

            foreach (var url in WebhookUrlList)
            {
                Logger?.WriteLine($"[{this.GetType().FullName}]: Loaded Webhook {url}");
                await this.HttpClient.PostAsync(url, new StringContent(content, MediaTypeHeaderValue.Parse("application/json")));
            }
        }

        public void SendWebhookMessage(string jsonContent)
        {
            foreach (var url in WebhookUrlList)
            {
                this.HttpClient.PostAsync(url, new StringContent(jsonContent, MediaTypeHeaderValue.Parse("application/json"))).GetAwaiter().GetResult();
            }    
        }
    }
}
