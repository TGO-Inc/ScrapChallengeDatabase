
namespace ScrapWorker.JSON
{
    internal class DescriptionFile
    {
        public string description { get; set; }
        public long fileId { get; set; }
        public string localId { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public int version { get; set; }
    }

    internal class ChallengeList
    {
        public string[] challenges { get; set; } = new string[0];
    }
}
