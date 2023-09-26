using SteamWorkshop.WebAPI;
using SteamWorkshop.WebAPI.IPublishedFileService;
using System.Net;
using System.Collections.Concurrent;
using Newtonsoft.Json;
using ChallengeModeDatabase;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChallengeMode.Database
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

            UploadWorkshopItem();


            Environment.Exit(0);
            return 0;
        }
        public static void ScrapeWorkshop()
        {
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
                var fname = $"challenges/{item.Publishedfileid}.json";
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

            List<string> guid_list = new();
            foreach (var file in membag)
            {
                var data = JsonConvert.DeserializeObject<DescriptionFile>
                    (Encoding.Default.GetString(file.Value));
                guid_list.Add(data.localId);

                var fname = $"challenges/{file.Key}.json";
                File.WriteAllBytes(fname, file.Value);
            }

            File.WriteAllText("challenges/ChallengeList.json",
                JsonConvert.SerializeObject(new ChallengeList() { challenges = guid_list.ToArray() }));

            Console.WriteLine($"Elapsed: {(DateTime.Now - start).TotalSeconds}s");
            Console.WriteLine($"Proccessed: {counter}/{details.ResultCount}");
        }
        public static void UploadWorkshopItem()
        {
            string steamCmdPath;
            string command;
            ProcessStartInfo psi;

            string workshopVdfPath = "item$1.vdf"; // Replace with the path to your .vdf file

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                steamCmdPath = @"C:\path\to\steamcmd.exe"; // Replace with the path to steamcmd on your Windows machine
                command = $"{steamCmdPath} +login {USERNAME} \"{PASSWORD}\" " +
                          $"+workshop_build_item {workshopVdfPath.Replace("$1", "windows")} " +
                          $"+quit";
                psi = new ProcessStartInfo("cmd.exe", $"/c \"{command}\"");
            }
            else // Assuming Linux if not Windows
            {
                steamCmdPath = "/usr/games/steamcmd"; // Path to steamcmd on Linux
                command = $"{steamCmdPath} +login {USERNAME} {PASSWORD} " +
                          $"+workshop_build_item {workshopVdfPath.Replace("$1", "linux")} " +
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