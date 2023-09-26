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

namespace ChallengeMode.Database
{
    public class WorkshopScraper
    {
        private static readonly char[] STEAM_API_KEY = File.ReadAllText("priv.key").ToCharArray();
        private static readonly string USERNAME = "AutomatedTool";
        private static readonly string PASSWORD = File.ReadAllText("priv.password");
        private static readonly object ConsoleLock = new();
        private static readonly object SyncLock = new();
        public static async Task Main(string[] args)
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePoints = 1000;
            ServicePointManager.ReusePort = true;

            CancellationTokenSource cts = new();

            // Timer to run ScrapeWorkshop every 12 hours
            Timer timer = new(async _ =>
            {
                lock (SyncLock)
                {
                    Console.WriteLine($"Begining Workshop Collection at [{DateTime.UtcNow}]");
                    while (ScrapeWorkshop())
                    {
                        Task.Delay(1000).Wait();
                    }
                    UploadWorkshopItem();
                }
            }, null, TimeSpan.Zero, TimeSpan.FromHours(12));

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
                    // Dispose of the timer and perform final tasks
                    timer.Dispose();
                    // end
                    Console.WriteLine($"Program Terminated at [{DateTime.UtcNow}]");
                    Environment.Exit(0);
                }
            }
        }
        public static bool ScrapeWorkshop()
        {
            var sqtime = DateTime.Now;
            var iFileService = new PublishedFileService(STEAM_API_KEY);
            var (details, remainding_files_exist) = iFileService.SendQuery(new());
            Console.WriteLine($"Query Time: {(DateTime.Now - sqtime).TotalSeconds}s");

            if (!Directory.Exists("challenges")) Directory.CreateDirectory("challenges");
            var start = DateTime.Now;

            int counter = 0;
            var dltool = new DownloadTool(USERNAME, PASSWORD, 387990);
            var membag = new ConcurrentBag<KeyValuePair<string, byte[]>>();
            var failedbag = new ConcurrentBag<string>();
            Console.WriteLine($"Downloading...");

            Parallel.ForEach(details._PublishedFileDetails, (item, loop, a) =>
            {
                var fname = $"challenges/{item.Publishedfileid}.json";
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
                    var fname = $"challenges/{failedItem.Publishedfileid}.json";
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

                var fname = $"challenges/{file.Key}.json";
                File.WriteAllBytes(fname, file.Value);
            }

            ChallengeList old = new();
            if (File.Exists("Mod/ChallengeList.json"))
                JsonConvert.DeserializeObject<ChallengeList>(File.ReadAllText("Mod/ChallengeList.json"));

            File.WriteAllText("Mod/ChallengeList.json",
                JsonConvert.SerializeObject(new ChallengeList() { challenges = guid_list.ToArray().Union(old.challenges).ToArray() }));

            Console.WriteLine($"Elapsed: {(DateTime.Now - start).TotalSeconds}s");
            Console.WriteLine($"Proccessed: {counter}/{details.ResultCount}");

            return remainding_files_exist;
        }
        public static void UploadWorkshopItem()
        {
            string steamCmdPath;
            string command;
            ProcessStartInfo psi;

            string workshopVdfPath = "item_$1.vdf"; // Replace with the path to your .vdf file

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                steamCmdPath = @"C:\path\to\steamcmd.exe"; // Replace with the path to steamcmd on your Windows machine
                command = $"{steamCmdPath} +login {USERNAME} '{PASSWORD}' " +
                          $"+workshop_build_item {workshopVdfPath.Replace("$1", "windows")} " +
                          $"+quit";
                psi = new ProcessStartInfo("cmd.exe", $"/c \"{command}\"");
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
                psi = new ProcessStartInfo("/bin/bash", $"-c \"{command}\"");
            }

            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            using Process process = new() { StartInfo = psi };
            process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (sender, e) => Console.Error.WriteLine(e.Data);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
    }
}