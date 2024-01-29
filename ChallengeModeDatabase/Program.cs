using SteamWorkshop.WebAPI;
using SteamWorkshop.WebAPI.IPublishedFileService;
using System.Net;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using ChallengeModeDatabase;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Security.Cryptography;
using System;
using SteamKit2;
using Steamworks;
using System.Net.Http.Headers;
using System.Net.Http;

namespace ChallengeMode.Database
{
    public class WorkshopScraper
    {
        private static readonly char[] STEAM_API_KEY = File.ReadAllText("priv.key").Trim().ToCharArray();
        private static readonly string USERNAME = "tgo_inc";
        private static readonly string PASSWORD = File.ReadAllText("priv.password").Trim();
        
        private static readonly object ConsoleLock = new();
        private static readonly object SyncLock = new();

        private static string steamCmdPath = string.Empty;
        private static string command = string.Empty;
        private static string startCmd = string.Empty;
        private static string lastHash = string.Empty;
        private static readonly string workshopVdfPath = "item_$1.vdf";
        private static DateTime lastManualTrigger = DateTime.MinValue;
        private static readonly TimeSpan manualTriggerInterval = TimeSpan.FromMinutes(30);
        private static Timer? timer = null;

        private static string[] webhook_uri = File.ReadLines("webhook.url").ToArray();
        private static uint _lastChangeNumber;
        private static readonly HttpClient _httpClient = new();
        private static readonly Dictionary<uint, string> Apps = new() {
            {387990, "Scrap Mechanic"},
            {588870, "Scrap Mechanic Mod Tool"},
            {2528270, "TinkerTech Playtest" }
        };
        public static async Task Main()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePoints = 1000;
            ServicePointManager.ReusePort = true;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                steamCmdPath = @"C:\path\to\steamcmd.exe"; // Replace with the path to steamcmd on your Windows machine
                command = $"{steamCmdPath} +login {USERNAME} '{PASSWORD}' " +
                          $"+workshop_build_item {workshopVdfPath.Replace("$1", "windows")} " +
                          $"+quit";
                startCmd = "cmd.exe";
                command = $"/c \"{command}\"";
            }
            else // Assuming Linux if not Windows
            {
                var filepath = workshopVdfPath.Replace("$1", "linux");
                var cd = Environment.CurrentDirectory;
                var text = File.ReadAllText(filepath).Replace("{{$1}}", cd);
                File.WriteAllText(filepath, text);

                steamCmdPath = "/usr/games/steamcmd"; // Path to steamcmd on Linux
                command = $"{steamCmdPath} +login {USERNAME} '{PASSWORD}' " +
                          $"+workshop_build_item '{Path.Combine(cd, filepath)}' " +
                          $"+quit";
                command = $"-c \"{command}\"";
                startCmd = "/bin/bash";
            }

            // Initialize HTTP Listener
            HttpListener listener = new();
            listener.Prefixes.Add("http://127.0.0.1:18251/");
            listener.Start();
            var dltool = new DownloadTool(USERNAME, PASSWORD, 387990);

            dltool.Steam3.OnPICSChanges += PICSChanges;

            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var context = await listener.GetContextAsync();
                    var response = context.Response;

                    var request = context.Request;
                    var hasUpdateQuery = request.QueryString["update"] != null;

                    string responseString;
                    var nextTriggerAllowedIn = (lastManualTrigger + manualTriggerInterval - DateTime.UtcNow).TotalSeconds;
                    if (hasUpdateQuery && nextTriggerAllowedIn <= 0 && timer != null)
                    {
                        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan); // Stop the timer
                        timer.Dispose();
                        timer = null;
                        lastManualTrigger = DateTime.UtcNow;
                        responseString = $"<script>window.location.href=window.location.protocol + \"//\" + window.location.host</script>";
                        Console.WriteLine($"Manual Trigger");
                        Task.Run(() => RunTasks(dltool));
                    }
                    else
                    {
                        responseString = $"{{\"nextManualDelay\":\"{Math.Max(nextTriggerAllowedIn, 0)}\"}}";
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    var output = response.OutputStream;
                    await output.WriteAsync(buffer);
                    output.Close();
                }
            });

            CancellationTokenSource cts = new();

            var content =
                $"{{\"content\":\"Scrapbot booted.\\nTracking:\\n```\\n{
                    JsonConvert.SerializeObject(Apps, Formatting.Indented).Replace("\"", "\\\"").Replace("\n", "\\n")
                    }\\n```\", \"flags\": 2}}";
            Console.WriteLine($"Content: {content}");
            foreach (var url in webhook_uri)
            {
                Console.WriteLine($"Loaded webhook: {url}");
                await _httpClient.PostAsync(url, new StringContent(content, MediaTypeHeaderValue.Parse("application/json")));
            }

            // Initial run
            RunTasks(dltool);

            // Wait for a cancellation request (e.g. user pressing Ctrl+C)
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Cancellation requested. Waiting for ongoing tasks to complete...");

                lock (SyncLock)
                {
                    // Dispose and perform final tasks
                    listener.Abort();
                    Console.WriteLine($"Program Terminated at [{TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local)}]");
                    Environment.Exit(0);
                }
            }
        }

        private static async void PICSChanges(SteamKit2.SteamApps.PICSChangesCallback callback)
        {
            Console.WriteLine("PICS Change Update recieved");
            Console.WriteLine(JsonConvert.SerializeObject(callback, Formatting.Indented));
            if (callback.LastChangeNumber == callback.CurrentChangeNumber) return;
            if (callback.CurrentChangeNumber > _lastChangeNumber) _lastChangeNumber = callback.CurrentChangeNumber;
            var apps = callback.AppChanges.Where(app => Apps.ContainsKey(app.Value.ID)).ToArray();
            if (apps.Length <= 0) return;
            foreach (var (_, app) in apps)
            {
                Apps.TryGetValue(app.ID, out var appName);
                var content =
                    $"{{\"content\":\"New SteamDB Change for App `{appName} ({app.ID})`\nhttps://steamdb.info/app/{app.ID}/history/?changeid={app.ChangeNumber}\", \"flags\": 2}}}}";
                foreach (var url in webhook_uri)
                    await _httpClient.PostAsync(url, new StringContent(content, MediaTypeHeaderValue.Parse("application/json")));
            }
        }

        private static void RunTasks(DownloadTool dltool)
        {
#if TEST_BOT
            return;
#endif

            lock (SyncLock)
            {
                DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
                Console.WriteLine($"Beginning Workshop Collection at [{localTime}]");
                while (ScrapeWorkshop(dltool))
                {
                    Task.Delay(1000).Wait();
                }
                localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
                Console.WriteLine($"Beginning Workshop Upload at [{localTime}]");
                UploadWorkshopItem();
                Console.WriteLine($"Next working time [{localTime + TimeSpan.FromHours(12)}]");
            }

            timer = new Timer(_ => RunTasks(dltool), null, TimeSpan.FromHours(12), TimeSpan.FromHours(12)); // Reset the timer after tasks completion
        }
        private static bool ScrapeWorkshop(DownloadTool dltool)
        {
            var sqtime = DateTime.Now;
            var iFileService = new PublishedFileService(STEAM_API_KEY);
            var (details, remainding_files_exist) = iFileService.SendQuery(new());
            Console.WriteLine($"Query Time: {(DateTime.Now - sqtime).TotalSeconds}s");

            if (!Directory.Exists("challenges"))
                Directory.CreateDirectory("challenges");
            
            var start = DateTime.Now;
            int counter = 0;
            
            var membag = new ConcurrentBag<KeyValuePair<string, byte[]>>();
            var failedbag = new ConcurrentBag<string>();
            Console.WriteLine($"Downloading...");

            Parallel.ForEach(details._PublishedFileDetails, (item, loop, a) =>
            {
                var fname = Path.Combine("challenges", $"{item.Publishedfileid}.json");
                if (File.Exists(fname)) return;
                try
                {
                    var manifest = dltool.DownloadManifest(387990, 387990, ulong.Parse(item.HcontentFile));
                    var description_data = manifest.Files!.Where(f => f.FileName.Contains("description.json")).First();
                    var content = dltool.DownloadFile(387990, description_data.Chunks.First());

                    membag.Add(new(item.Publishedfileid, content));
                    //Console.WriteLine($"Downloaded: {item.Publishedfileid}");
                    counter++;
                }
                catch (Exception e)
                {
                    failedbag.Add(item.Publishedfileid);
                    lock (ConsoleLock)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Failed: {item.Publishedfileid}");
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(e);
                        Console.ResetColor();
                    }
                }
            });
            var failedSet = new HashSet<string>(failedbag);
            var failed_items = details._PublishedFileDetails.Where(i => failedSet.Contains(i.Publishedfileid));
            if (failed_items.Any())
            {
                Console.WriteLine($"Cleaning Failed Downloads [{failed_items.Count()}]");
                //var old = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText("challenges/.steam.ids"))!;
                foreach(var failedItem in failed_items)
                {
                    var fname = Path.Combine("challenges", $"{failedItem.Publishedfileid}.json");
                    //old.Remove(failedItem.Publishedfileid);
                    if (File.Exists(fname))
                    {
                        Console.WriteLine($"Removing: " + fname);
                        File.Delete(fname);
                    }
                    remainding_files_exist = true;
                }
            }

            List<string> guid_list = new();
            foreach (var file in membag)
            {
                var data = JsonConvert.DeserializeObject<DescriptionFile>
                    (Encoding.Default.GetString(file.Value));
                guid_list.Add(data.localId);

                var fname = Path.Combine("challenges",$"{file.Key}.json");
                File.WriteAllBytes(fname, file.Value);
            }

            ChallengeList old = new();
            if (File.Exists(Path.Combine("Mod","ChallengeList.json")))
                old = JsonConvert.DeserializeObject<ChallengeList>(File.ReadAllText(Path.Combine("Mod","ChallengeList.json")));

            File.WriteAllText(Path.Combine("Mod","ChallengeList.json"),
                JsonConvert.SerializeObject(new ChallengeList() { challenges = guid_list.ToArray().Union(old.challenges).ToArray() }));

            Console.WriteLine($"Elapsed: {(DateTime.Now - start).TotalSeconds}s");
            Console.WriteLine($"Proccessed: {counter}/{details.ResultCount}");

            return remainding_files_exist;
        }
        private static void UploadWorkshopItem()
        {
            if (lastHash != ComputeDirectoryHash("Mod", out string nhash))
            {
                lastHash = nhash;
                ProcessStartInfo psi = new(startCmd, command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = psi };
                process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                process.ErrorDataReceived += (sender, e) => Console.Error.WriteLine(e.Data);

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
        }

        private static string ComputeDirectoryHash(string directoryPath, out string ovar)
        {
            // Create a new instance of SHA256
            using SHA256 sha256 = SHA256.Create();

            // Get all files in the directory, ordered by name
            var filePaths = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories)
                                     .OrderBy(p => p).ToList();

            // Hash the content of each file and concatenate the hashes
            StringBuilder combinedHashes = new StringBuilder();
            foreach (var filePath in filePaths)
            {
                byte[] fileHash;
                using (var stream = File.OpenRead(filePath))
                {
                    fileHash = sha256.ComputeHash(stream);
                }
                combinedHashes.Append(BitConverter.ToString(fileHash).Replace("-", "").ToLower());
            }

            // Compute the final hash of the concatenated hashes
            byte[] finalHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedHashes.ToString()));
            ovar = BitConverter.ToString(finalHash).Replace("-", "").ToLower();
            return ovar;
        }
    }
}