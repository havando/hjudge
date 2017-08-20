using System;
using System.ComponentModel;

namespace Server
{
    public class ClientInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private bool _isChecked;
        public int UserId { get; set; }
        public IntPtr ConnId { get; set; }
        public string IpAddress { get; set; }
        public ushort Port { get; set; }
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }
        public string UserName => UserId == 0 ? string.Empty : Connection.GetUserName(UserId);
        public string Address => IpAddress + ":" + Convert.ToString(Port);
        public PkgInfo PkgInfo { get; set; }
    }
}