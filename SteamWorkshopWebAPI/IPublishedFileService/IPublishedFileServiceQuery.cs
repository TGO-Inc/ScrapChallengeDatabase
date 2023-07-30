using Newtonsoft.Json;
using System.Web;

namespace SteamWorkshop.WebAPI.IPublishedFileService
{
    public class IPublishedFileServiceQuery
    {
        public int QueryType { get; set; } = 1;
        public ulong AppId { get; set; } = 387990;
        public string Cursor { get; set; } = "*";
        public int FileType { get; set; } = 0;
        public string RequiredTags { get; set; } = "Challenge+Pack";
        public int ResultsPerPage { get; set; } = 100;
    }
    public class PublishedFileDetailsQuery
    {
        [JsonProperty("total", NullValueHandling = NullValueHandling.Ignore)]
        public int Total { get; set; }
        [JsonProperty("publishedfiledetails", NullValueHandling = NullValueHandling.Ignore)]
        public List<PublishedFileDetails>? _PublishedFileDetails { get; set; }
        [JsonProperty("next_cursor", NullValueHandling = NullValueHandling.Ignore)]
        public string NextCursor { get => this.next_cursor; set => this.next_cursor = HttpUtility.UrlEncode(value); }
        private string next_cursor;
        public class PublishedFileDetails
        {
            [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
            public int? Result { get; set; }

            [JsonProperty("publishedfileid", NullValueHandling = NullValueHandling.Ignore)]
            public string PublishedFileId { get; set; }

            [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
            public int? Language { get; set; }
        }
    }
}
