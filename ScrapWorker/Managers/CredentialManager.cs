
namespace ScrapWorker.Managers
{
    internal static class CredentialManager
    {
        public static string GetUsername() => "tgo_inc";

        public static string GetPassword() => File.ReadAllText("Assets/SecureFiles/priv.password").Trim();

        public static List<(IEnumerable<uint>, string)> GetWebhookUrls() => File.ReadAllLines("Assets/Discord/webhook.url")
                                                                            .Select(s => s.Split("="))
                                                                            .Select(sr => (
                                                                                sr[0].Split(",").Select(f => uint.Parse(f)),
                                                                                sr[1])).ToList();

        public static char[] GetSteamAPIKey() => File.ReadAllText("Assets/SecureFiles/priv.key").Trim().ToCharArray();
    }
}
