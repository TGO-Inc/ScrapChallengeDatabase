using System.Net;
using System.Text;
using ScrapWorker.Managers;

namespace ScrapWorker.HTTP
{
    internal class UpdateRequestService(WorkshopScraper workshopScraper, CancellationToken tok, ConsoleManager? Logger = null)
    {
        private const int manualTriggerInterval = 30;
        private readonly HttpListener Listener = new();
        private DateTime lastManualTrigger = DateTime.MinValue;

        public void StartService()
        {
            ServicePointManager.DefaultConnectionLimit = 1000;
            ServicePointManager.MaxServicePoints = 1000;
            ServicePointManager.ReusePort = true;

            // Initialize HTTP Listener
            this.Listener.Prefixes.Add("http://127.0.0.1:18251/");
            this.Listener.Start();

            Logger?.WriteLine($"[{this.GetType().FullName}]: Listener Service Started");
            Task.Run(ListenerService, tok);
        }

        public void WaitForExit()
        {
            this.Listener.Abort();
        }

        private async void ListenerService()
        {
            while (!tok.IsCancellationRequested)
            {
                try
                {
                    var context = await Listener.GetContextAsync();
                    var response = context.Response;

                    var request = context.Request;
                    var hasUpdateQuery = request.QueryString["update"] != null;

                    string responseString;
                    var nextTriggerAllowedIn = (this.lastManualTrigger + TimeSpan.FromMinutes(manualTriggerInterval) - DateTime.UtcNow).TotalSeconds;
                    if (hasUpdateQuery && nextTriggerAllowedIn <= 0)
                    {
                        this.lastManualTrigger = DateTime.UtcNow;
                        responseString = $"<script>window.location.href=window.location.protocol + \"//\" + window.location.host</script>";

                        Logger?.WriteLine($"[{this.GetType().FullName}]: Manual Trigger. Calling Tasks.");
                        workshopScraper.ForceRunTasks();
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
                catch
                {
                    Logger?.WriteLine($"[{this.GetType().FullName}]: Listener fail");
                }
            }
        }
    }
}
