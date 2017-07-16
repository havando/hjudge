using System;

namespace Server
{
    public class ClientInfo
    {
        public int UserId { get; set; }
        public IntPtr ConnId { get; set; }
        public string IpAddress { get; set; }
        public ushort Port { get; set; }
        public bool IsChecked { get; set; }
        public string UserName => UserId == 0 ? string.Empty : Connection.GetUserName(UserId);
        public string Address => IpAddress + ":" + Convert.ToString(Port);
    }
}