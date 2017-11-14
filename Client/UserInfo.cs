using System.ComponentModel;

namespace Client
{
    public class UserInfo : INotifyPropertyChanged
    {
        private string _userName;
        public int UserId { get; set; }
        public string RegisterDate { get; set; }
        public string Icon { get; set; }
        public string Achievement { get; set; }
        public bool? IsChanged { get; set; }
        public int Type { get; set; }
        public string Password { get; set; }
        public int Coins { get; set; }
        public int Experience { get; set; }

        public string UserName
        {
            get => _userName;
            set
            {
                _userName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("UserName"));
            }
        }

        public string Type2
        {
            get
            {
                switch (Type)
                {
                    case 1: return "BOSS";
                    case 2: return "管理员";
                    case 3: return "教师";
                    case 4: return "选手";
                }
                return "未知";
            }
            set
            {
                switch (value)
                {
                    case "BOSS":
                        Type = 1;
                        break;
                    case "管理员":
                        Type = 2;
                        break;
                    case "教师":
                        Type = 3;
                        break;
                    case "选手":
                        Type = 4;
                        break;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Type2"));
            }
        }
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}