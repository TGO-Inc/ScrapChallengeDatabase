
namespace ScrapWorker.Managers
{
    internal static class CredentialManager
    {
        public static string GetUsername() => "tgo_inc";

        public static string GetPassword() => File.ReadAllText("Assets/SecureFiles/priv.password").Trim();

        public static Dictionary<IEnumerable<uint>, string> GetWebhookUrls() => File.ReadAllLines("Assets/Discord/webhook.url")
                                                                            .Select(s => s.Split("="))
                                                                            .Select(sr => KeyValuePair.Create(
                                                                                sr[0]
                                                                                    .Split(",")
                                                                                    .Select(f => uint.Parse(f)),
                                                                                sr[1])).ToDictionary();

        public static char[] GetSteamAPIKey() => File.ReadAllText("Assets/SecureFiles/priv.key").Trim().ToCharArray();
    }
}
