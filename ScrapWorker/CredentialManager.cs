using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScrapWorker
{
    internal static class CredentialManager
    {
        public static string GetUsername() => "tgo_inc";

        public static string GetPassword() => File.ReadAllText("Assets/SecuredFiles/priv.password");

        public static string[] GetWebhookUrls() => File.ReadAllLines("Assets/Discord/webhook.url");

        public static char[] GetSteamAPIKey() => File.ReadAllText("Assets/SecuredFiles/priv.key").ToCharArray();
    }
}
