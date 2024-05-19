using Newtonsoft.Json;
using ScrapWorker.JSON;
using SteamWorkshop.WebAPI;
using SteamWorkshop.WebAPI.Internal;
using SteamWorkshop.WebAPI.IPublishedFileService;
using SteamWorkshop.WebAPI.Managers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static SteamKit2.Internal.CContentBuilder_GetMissingDepotChunks_Response;

namespace ScrapWorker.Steam
{
    internal partial class SteamIdScraper(char[] SteamApiKey, ConsoleManager? Logger = null)
    {
        internal readonly static string uGET_PUBLISHED_FILE_DETAILS
            = "https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/";
        internal readonly static string uQUERY_FILES
            = "https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/";

        private static T Request<T>(string query)
        {
            var Results = new HttpClient().GetAsync(query).GetAwaiter().GetResult();
            var Json = Results.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonConvert.DeserializeObject<T>(Json[12..^1])!;
        }

        private const string BASE_URL = "https://steamcommunity.com/workshop/browse/?appid=387990&browsesort=lastupdated&section=&actualsort=lastupdated&numperpage=30&p=";
        // Regular expression to match the Steam Community workshop URLs
        private const string PATTERN = @"https://steamcommunity\.com/(id|profiles)/[a-zA-Z0-9]+/myworkshopfiles";

        readonly HttpClient Client = new();

        public void StartScraping()
        {
            ConcurrentDictionary<string, byte> SteamIds = [];
            ConcurrentDictionary<string, byte> nameCache = [];

            StringBuilder QueryString = new();

            QueryString.Append(uQUERY_FILES);
            QueryString.Append($"?key={new string(SteamApiKey)}");

            QueryString.Append("&query_type=1");
            QueryString.Append("&numperpage=30");
            QueryString.Append("&appid=387990");
            QueryString.Append("&page=1");

            int total = Request<PublishedFileDetailsQuery>(QueryString.ToString()).Total;
            double loop = total / 30d;
            if (loop > Math.Floor(loop))
                loop = Math.Floor(loop) + 1;

            Logger?.WriteLine($"[{this.GetType().FullName}]: Found {total} workshop items...");
            Logger?.WriteLine($"[{this.GetType().FullName}]: Preparing for {loop} pages of 30 items in batches of 10\n");

            const int chunk = 10;
            int skipped_duplicates = 0;
            double avgMiliseconds = 0;
            int iterations = 0;
            int totalUrlsCollected = 0;
            int _ = 0;

            bool hasExited = false;
            void LogSaveAndClose()
            {
                if (hasExited)
                {
                    Task.Delay(10).Wait();
                    return;
                }

                hasExited = true;
                Logger?.WriteLine($"Exiting...");
                Logger?.WriteLine($"Total Skipped Duplicates: {skipped_duplicates}");
                Logger?.WriteLine($"Pages Completed: {_}");
                Logger?.WriteLine($"Steam IDs Collected: {SteamIds.Keys.Count}");
                Logger?.WriteLine($"Average T(s)/Page: {(avgMiliseconds / (1000.0 * chunk * 30)):F5}");
                File.WriteAllText("steamids.json", JsonConvert.SerializeObject(SteamIds.Keys));
                Environment.Exit(0);
            }

            Console.CancelKeyPress += (sender, e) => LogSaveAndClose();
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => LogSaveAndClose();
            
            for (_ = 16_50; _ < loop; _ += chunk)
            {
                List<Task> tasks = [];
                var sw = Stopwatch.StartNew();
                int prev_duplicates = skipped_duplicates;

                // 10 batches of 30 per page
                Parallel.ForEach(Enumerable.Range(_+1, chunk), i => 
                { 
                    int attempts = 5;
                Retry:
                    try
                    {
                        this.Client.GetStringAsync($"{BASE_URL}{i}").ContinueWith(async content =>
                        {
                            MatchCollection matches = UrlRegex().Matches(content.Result);
                            ConcurrentBag<string> urls = [];
                            List<Task> tasks = [];

                            foreach (Match match in matches.Cast<Match>())
                            {
                                string steamid = match.Value[..^16].Trim();
                                Interlocked.Increment(ref totalUrlsCollected);

                                if (steamid.StartsWith("https://steamcommunity.com/id/", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    string nid = steamid[30..];
                                    if (nameCache.ContainsKey(nid))
                                        continue;

                                    tasks.Add(this.Client.GetStringAsync(steamid + "?xml=1").ContinueWith(async tsk =>
                                    {
                                        string content = tsk.Result;
                                        int start = content.IndexOf("<steamID64>") + 11;
                                        int end = content.IndexOf("</steamID64>");
                                        steamid = content[start..end];
                                        urls.Add(steamid);
                                        nameCache.TryAdd(nid, 0);
                                    }));
                                }
                                else
                                {
                                    urls.Add(steamid[36..]);
                                }
                            }

                            Task.WaitAll(tasks.ToArray());

                            foreach (var steamid in urls)
                                if (SteamIds.TryAdd(steamid, 0) == false)
                                    Interlocked.Increment(ref skipped_duplicates);
                        }).Wait();
                    }
                    catch
                    {
                        Logger?.Error.WriteLine($"[Page-{i}]: Failed to fetch page. Retrying...");
                        if (--attempts > 0)
                        {
                            Task.Delay(1000).Wait();
                            goto Retry;
                        }
                    }
                });

                // finished 300 items
                Task.WaitAll(tasks.ToArray());
                sw.Stop();

                avgMiliseconds = (avgMiliseconds * iterations + sw.ElapsedMilliseconds) / ++iterations;
                var avgSecondsPerPage = avgMiliseconds / (1000.0 * chunk * 30); // 1000 ms/s in 10 batches of 30 per page
                Logger?.WriteLine($"[Page-{_+chunk}] [Items-{(_ + chunk)*30}]: Collected {SteamIds.Keys.Count} Steam IDs. Skipped {skipped_duplicates-prev_duplicates} Duplicates. " +
                    $"Total Items Scanned {totalUrlsCollected}. " +
                    $"{avgSecondsPerPage:F3}s/page. ETA: {DateTime.UtcNow + TimeSpan.FromMilliseconds(avgMiliseconds * ((loop/chunk) - iterations))}");
            }

            LogSaveAndClose();
        }

        [GeneratedRegex(PATTERN)]
        private static partial Regex UrlRegex();
    }
}


