using SteamWorkshop.WebAPI;
using smSteamUtility;
using System;
using System.Collections;
using SteamWorkshop.WebAPI.IPublishedFileService;
using Newtonsoft.Json;
using SteamKit2;
using SteamKit2.CDN;
using System.Net;
using static SteamKit2.CDN.Server;
using static SteamKit2.Internal.CContentBuilder_CommitAppBuild_Request;
using System.Text;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using System.Reflection.Metadata;

namespace SteamWorkshopScraper
{
    public class WorkshopScraper
    {
        private static readonly char[] STEAM_API_KEY = File.ReadAllText("priv.key").ToCharArray();
        private static readonly string USERNAME = "AutomatedTool";
        private static readonly string PASSWORD = File.ReadAllText("priv.password");
        public static int Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePoints = 1000;
            ServicePointManager.ReusePort = true;

            var sqtime = DateTime.Now;
            var iFileService = new PublishedFileService(STEAM_API_KEY);
            var details = iFileService.SendQuery(new());
            Console.WriteLine($"Query Time: {(DateTime.Now - sqtime).TotalSeconds}s");

            if (!Directory.Exists("challenges")) Directory.CreateDirectory("challenges");
            var start = DateTime.Now;

            int counter = 0;
            var dltool = new DownloadTool(USERNAME, PASSWORD, 387990);
            var membag = new ConcurrentBag<KeyValuePair<string, byte[]>>();
            var failedbag = new ConcurrentBag<string>();

            Parallel.ForEach(details._PublishedFileDetails, (item, loop, a) =>
            {
                var fname = $"./challenges/{item.Publishedfileid}.json";
                if (File.Exists(fname)) return;
                try
                {
                    var manifest = dltool.DownloadManifest(387990, 387990, ulong.Parse(item.HcontentFile));
                    var description_data = manifest.Files!.Where(f => f.FileName.Contains("description.json")).First();
                    var content = dltool.DownloadFile(387990, description_data.Chunks.First());

                    membag.Add(new(item.Publishedfileid, content));
                    Console.WriteLine($"Downloaded: {item.Publishedfileid}");
                    counter++;
                }
                catch
                {
                    failedbag.Add(item.Publishedfileid);
                    Console.WriteLine($"Failed: {item.Publishedfileid}");
                }
            });

            foreach(var file in membag)
            {
                var fname = $"./challenges/{file.Key}.json";
                File.WriteAllBytes(fname, file.Value);
            }

            Console.WriteLine($"Elapsed: {(DateTime.Now - start).TotalSeconds}s");
            Console.WriteLine($"Proccessed: {counter}/{details.ResultCount}");
            Environment.Exit(0);
            return 0;
        }
    }
}