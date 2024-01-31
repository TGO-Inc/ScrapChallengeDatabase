
namespace ScrapWorker.Managers
{
    internal static class CredentialManager
    {
        public static string GetUsername() => "tgo_inc";

        public static string GetPassword() => File.ReadAllText("Assets/SecureFiles/priv.password").Trim();

        public static string[] GetWebhookUrls() => File.ReadAllLines("Assets/Discord/webhook.url");

        public static char[] GetSteamAPIKey() => File.ReadAllText("Assets/SecureFiles/priv.key").Trim().ToCharArray();
    }
}
