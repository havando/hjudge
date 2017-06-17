using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Server
{
    public class UserInfo : INotifyPropertyChanged
    {
        public int UserId { get; set; }
        public string RegisterDate { get; set; }
        public string Icon { get; set; }
        public string Achievement { get; set; }
        public bool? IsChanged { get; set; }
        public int Type { get; set; }
        public string Password { get; set; }

        private string _userName;

        public event PropertyChangedEventHandler PropertyChanged;

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
    }
    public static class UserHelper
    {
        public static UserInfo CurrentUser { get; private set; }

        public static ObservableCollection<UserInfo> UsersBelongs { get; set; }

        public static void SetCurrentUser(int userId, string userName, string registerDate, string passwordHash, int type,
            string icon, string achievement)
        {
            CurrentUser = new UserInfo
            {
                UserId = userId,
                UserName = userName,
                RegisterDate = registerDate,
                Password = passwordHash,
                Type = type,
                Icon = icon,
                Achievement = achievement,
                IsChanged = true
            };
            GetUserBelongs();
        }

        public static void GetUserBelongs()
        {
            UsersBelongs = Connection.GetUsersBelongs(CurrentUser.Type);
        }
        
    }
}
