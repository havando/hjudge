using System;

namespace Server
{
    public class Message
    {
        public int MsgId { get; set; }
        public string Direction { get; set; }
        public DateTime MessageTime { get; set; }
        public string Content { get; set; }
        public string User { get; set; }
        public string Summary => Content.Length > 30 ? Content.Substring(0, 30) + "..." : Content;
        public string DisplayDateTime => MessageTime.ToString("yyyy/MM/dd HH:mm:ss");
        public int State { get; set; }
    }
}