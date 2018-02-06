using System.IO;

namespace Server
{
    internal class FileRecvInfo
    {
        public string FileId { get; set; }
        public string FileName { get; set; }
        public int TotLength { get; set; }
        public int CurrentLength { get; set; }
        public FileStream Fs { get; set; }
    }
}