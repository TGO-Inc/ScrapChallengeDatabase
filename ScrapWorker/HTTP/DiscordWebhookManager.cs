using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using static SteamKit2.Internal.CCloud_EnumerateUserApps_Response;

namespace ScrapWorker.HTTP
{
    internal class DiscordWebhookManager(ConsoleManager? Logger = null)
    {
        private readonly HttpClient HttpClient = new();
        private static string[] WebhookUrlList => CredentialManager.GetWebhookUrls();

        public DiscordWebhookManager(Dictionary<uint, string> Apps, ConsoleManager? Logger = null)
            : this(Logger)
        {
            this.StartupMessage(Apps);
        }

        private async void StartupMessage(Dictionary<uint, string> Apps)
        {
            var json = JsonConvert.SerializeObject(Apps, Formatting.Indented).Replace("\"", "\\\"").Replace("\n", "\\n");
            var content = $"{{\"content\":\"SteamDB tracker started for:\\n```\\n{json}\\n```\", \"flags\": 2}}";

            foreach (var url in WebhookUrlList)
            {
                Logger?.WriteLine($"[{this.GetType().FullName}]: Loaded webhook: {url}");
                await HttpClient.PostAsync(url, new StringContent(content, MediaTypeHeaderValue.Parse("application/json")));
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
