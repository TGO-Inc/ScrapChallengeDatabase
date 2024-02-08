using Newtonsoft.Json;
using ScrapWorker.JSON;
using ScrapWorker.Managers;
using SteamWorkshop.WebAPI;
using SteamWorkshop.WebAPI.Internal;
using SteamWorkshop.WebAPI.IPublishedFileService;
using SteamWorkshop.WebAPI.Managers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ScrapWorker
{
    internal class WorkshopScraper(Steam3Session session, char[] SteamApiKey, CancellationToken tok, ConsoleManager? Logger = null)
    {
        private readonly DownloadTool DLTool = new(session, 387990);
        private const string workshopVdfPath = "Assets/VDF/item_$1.vdf";
        private string LastDirHash = string.Empty;
        private readonly object CriticalLockObj = new();
        private Timer? Worker;

        public static (string, string) GetFullCliCommand()
        {
            var USERNAME = CredentialManager.GetUsername();
            var PASSWORD = CredentialManager.GetPassword();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Replace with the path to steamcmd on your Windows machine
                var steamCmdPath = @"C:\path\to\steamcmd.exe";
                var command = $"{steamCmdPath} +login {USERNAME} '{PASSWORD}' " +
                          $"+workshop_build_item {workshopVdfPath.Replace("$1", "windows")} " +
                          $"+quit";

                return ("cmd.exe", $"/c \"{command}\"");
            }
            else // Assuming Linux if not Windows
            {
                var filepath = workshopVdfPath.Replace("$1", "linux");
                var currentDir = Environment.CurrentDirectory;
                var text = File.ReadAllText(filepath).Replace("{{$1}}", currentDir);
                File.WriteAllText(filepath, text);

                // Path to steamcmd on Linux
                var steamCmdPath = "/usr/games/steamcmd";
                var command = $"{steamCmdPath} +login {USERNAME} '{PASSWORD}' " +
                          $"+workshop_build_item '{Path.Combine(currentDir, filepath)}' " +
                          $"+quit";

                return ("/bin/bash", $"-c \"{command}\"");
            }
        }

        public void ForceRunTasks()
        {
            if (this.Worker is not null)
                this.Worker?.Change(TimeSpan.Zero, TimeSpan.FromHours(12));
            else
                this.Worker = new Timer(this.RunTasks, tok, TimeSpan.Zero, TimeSpan.FromHours(12));
        }

        public void WaitForExit()
        {
            lock (this.CriticalLockObj)
            {
                this.Worker?.Dispose();
                this.Worker = null;
            }
        }

        private async void RunTasks(object? state)
        {
            if (state is CancellationToken tok)
            {
                if (tok.IsCancellationRequested)
                {
                    this.Worker!.Dispose();
                    this.Worker = null;
                    return;
                }

                DateTime localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
                Logger?.WriteLine($"[{this.GetType().FullName}]: Beginning Workshop Collection at [{localTime}]");

                while (this.ScrapeWorkshop(this.DLTool, tok))
                    await Task.Delay(1000);

                localTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
                Logger?.WriteLine($"[{this.GetType().FullName}]: Beginning Workshop Upload at [{localTime}]");
                
                if (!tok.IsCancellationRequested)
                    this.UploadWorkshopItem();
                else
                    return;
                
                Logger?.WriteLine($"[{this.GetType().FullName}]: Next working time [{localTime + TimeSpan.FromHours(12)}]");
                // Reset the timer after tasks completion
                this.Worker = new Timer(this.RunTasks, state, TimeSpan.FromHours(12), TimeSpan.FromHours(12));
            }
        }

        private bool ScrapeWorkshop(DownloadTool dltool, CancellationToken tok)
        {
            var sqtime = DateTime.Now;
            var iFileService = new PublishedFileService(SteamApiKey);
            var (details, remainding_files_exist) = iFileService.SendQuery(new());
            
            Logger?.WriteLine($"[{this.GetType().FullName}]: Query Time: {(DateTime.Now - sqtime).TotalSeconds}s");

            if (!Directory.Exists("challenges"))
                Directory.CreateDirectory("challenges");

            int counter = 0;
            var start = DateTime.Now;
            var membag = new ConcurrentBag<KeyValuePair<string, byte[]>>();
            var failedbag = new ConcurrentBag<string>();

            Logger?.WriteLine($"[{this.GetType().FullName}]: Downloading...");

            Parallel.ForEach(details._PublishedFileDetails, (item, loop, a) =>
            {
                var fname = Path.Combine("challenges", $"{item.Publishedfileid}.json");
                if (File.Exists(fname) || tok.IsCancellationRequested)
                    return;

                try
                {
                    var manifest = dltool.DownloadManifest(387990, 387990, ulong.Parse(item.HcontentFile));
                    var description_data = manifest.Files!.Where(f => f.FileName.Contains("description.json")).First();
                    var content = dltool.DownloadFile(387990, description_data.Chunks.First());

                    membag.Add(new(item.Publishedfileid, content));
                    counter++;
                }
                catch (Exception e)
                {
                    failedbag.Add(item.Publishedfileid);

                    Logger?.Colored.ForegroundColor(ConsoleColor.DarkRed);
                    Logger?.Colored.WriteLine($"[{this.GetType().FullName}]: Failed: {item.Publishedfileid}");
                    Logger?.Colored.ForegroundColor(ConsoleColor.Red);
                    Logger?.Colored.WriteLine(e);
                }
            });

            if (tok.IsCancellationRequested)
                return false;
            
            var failedSet = new HashSet<string>(failedbag);
            var failed_items = details._PublishedFileDetails.Where(i => failedSet.Contains(i.Publishedfileid));
            
            lock (this.CriticalLockObj)
            {
                if (failed_items.Any())
                {
                    Logger?.WriteLine($"[{this.GetType().FullName}]: Cleaning Failed Downloads [{failed_items.Count()}]");
                    foreach (var failedItem in failed_items)
                    {
                        var fname = Path.Combine("challenges", $"{failedItem.Publishedfileid}.json");
                        if (File.Exists(fname))
                        {
                            Logger?.WriteLine($"[{this.GetType().FullName}]: Removing: " + fname);
                            File.Delete(fname);
                        }
                        remainding_files_exist = true;
                    }
                }
            
                List<string> guid_list = [];
                foreach (var file in membag)
                {
                    var data = JsonConvert.DeserializeObject<DescriptionFile>(Encoding.Default.GetString(file.Value));
                    guid_list.Add(data!.localId);

                    var fname = Path.Combine("challenges", $"{file.Key}.json");
                    File.WriteAllBytes(fname, file.Value);
                }

                ChallengeList old = new();
                if (File.Exists(Path.Combine("Mod", "ChallengeList.json")))
                    old = JsonConvert.DeserializeObject<ChallengeList>(File.ReadAllText(Path.Combine("Mod", "ChallengeList.json")))!;

                File.WriteAllText(Path.Combine("Mod", "ChallengeList.json"),
                    JsonConvert.SerializeObject(new ChallengeList() { challenges = guid_list.ToArray().Union(old.challenges).ToArray() }));
            }

            Logger?.WriteLine($"[{this.GetType().FullName}]: Elapsed: {(DateTime.Now - start).TotalSeconds}s");
            Logger?.WriteLine($"[{this.GetType().FullName}]: Proccessed: {counter}/{details.ResultCount}");

            return remainding_files_exist;
        }

        private void UploadWorkshopItem()
        {
            if (this.LastDirHash != ComputeDirectoryHash("Mod", out string nhash))
            {
                this.LastDirHash = nhash;
                var (startCmd, command) = GetFullCliCommand();
                ProcessStartInfo psi = new(startCmd, command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new() { StartInfo = psi };
                process.OutputDataReceived += (sender, e) => Logger?.WriteLine(e.Data);
                process.ErrorDataReceived += (sender, e) => Logger?.Error.WriteLine(e.Data);
                
                lock (this.CriticalLockObj)
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
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
            StringBuilder combinedHashes = new();
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
