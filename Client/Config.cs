namespace Client
{
    public class Config
    {
        public string Ip { get; set; }
        public ushort Port { get; set; }
    }
    public class ServerConfig
    {
        public bool AllowRequestDataSet { get; set; }
        public bool AllowCompetitorMessaging { get; set; }
        public int MutiThreading { get; set; }
        public int RegisterMode { get; set; }
    }
}