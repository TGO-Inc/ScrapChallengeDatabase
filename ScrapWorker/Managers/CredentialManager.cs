using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapWorker.Managers
{
    internal static class CredentialManager
    {
        public static string GetUsername() => "tgo_inc";

        public static string GetPassword() => File.ReadAllText("Assets/SecureFiles/priv.password");

        public static string[] GetWebhookUrls() => File.ReadAllLines("Assets/Discord/webhook.url");

        public static char[] GetSteamAPIKey() => File.ReadAllText("Assets/SecureFiles/priv.key").ToCharArray();
    }
}
