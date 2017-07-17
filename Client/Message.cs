using System;

namespace Client
{
    public class Message
    {
        public string Direction { get; set; }
        public DateTime MessageTime { get; set; }
        public string Content { get; set; }

        public string Summery => Content.Length > 30 ? Content.Substring(0, 30) + "..." : Content;
        public string DisplayDateTime => MessageTime.ToString("yyyy/MM/dd HH:mm:ss:ffff");
    }
}
