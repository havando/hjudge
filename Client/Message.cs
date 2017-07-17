using System;

namespace Client
{
    public class Message
    {
        public string Direction { get; set; }
        public DateTime MessageTime { get; set; }
        public string Content { get; set; }
        public string Summery => $"{Content.Substring(0, 30)}...";
        public string DisplayDateTime => MessageTime.ToString("yyyy/MM/dd HH:mm:ss:ffff");
    }
}
