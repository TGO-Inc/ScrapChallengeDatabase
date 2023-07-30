using SteamKit2;
using SteamKit2.CDN;
using SteamWorkshop.WebAPI.Internal;

namespace SteamWorkshop.WebAPI
{
    public class DownloadTool
    {
        private readonly Steam3Session Steam3;
        private readonly CDNClientPool CDNClientPool;
        private readonly Server CDNConnection;
        private ulong ManifestRequestCode;
        public DownloadTool(string username, string password, uint appid)
        {
            this.Steam3 = new Steam3Session(new SteamUser.LogOnDetails()
            {
                Username = username,
                Password = password,
                ShouldRememberPassword = true,
                LoginID = 0x534B32
            });
            this.CDNClientPool = new CDNClientPool(this.Steam3, appid);
            this.CDNConnection = this.CDNClientPool.GetConnection(new());
            this.Steam3.RequestDepotKey(appid, appid);
        }
        public DepotManifest DownloadManifest(uint depotid, uint appid, ulong manifestid, int timeout = 0)
        {
            if (timeout > 100)
                return null;

            try
            {
                return this.CDNClientPool.CDNClient.DownloadManifestAsync(
                                    depotid,
                                    manifestid,
                                    this.ManifestRequestCode,
                                    this.CDNConnection,
                                    this.Steam3.DepotKeys[depotid],
                                    this.CDNClientPool.ProxyServer).GetAwaiter().GetResult();
            }
            catch
            {
                this.ManifestRequestCode = this.Steam3.GetDepotManifestRequestCodeAsync(
                    depotid,
                    appid,
                    manifestid,
                "public").GetAwaiter().GetResult();

                // If we could not get the manifest code, this is a fatal error
                if (this.ManifestRequestCode == 0)
                {
                    Console.WriteLine("No manifest request code was returned for {0} {1}", depotid, manifestid);
                    return null;
                }

                return this.DownloadManifest(depotid, appid, manifestid, timeout++);
            }
        }
        public byte[] DownloadFile(uint depotid, DepotManifest.ChunkData data)
        {
            var chunkData = this.CDNClientPool.CDNClient.DownloadDepotChunkAsync(
                        depotid,
                        data,
                        this.CDNConnection,
                        this.Steam3.DepotKeys[depotid],
                        this.CDNClientPool.ProxyServer).GetAwaiter().GetResult();

            return chunkData.Data;
        }
    }
}
