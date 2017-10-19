using System.IO;

namespace Client
{
    internal class FileRecvInfo
    {
        public string FileId { get; set; }
        public string FileName { get; set; }
        public long TotLength { get; set; }
        public long CurrentLength { get; set; }
        public FileStream Fs { get; set; }
    }
}