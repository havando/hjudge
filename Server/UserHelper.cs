using System.Collections.ObjectModel;

namespace Server
{
    public static class UserHelper
    {
        public static UserInfo CurrentUser { get; private set; }

        public static ObservableCollection<UserInfo> UsersBelongs { get; set; }

        public static void SetCurrentUser(int userId, string userName, string registerDate, string passwordHash,
            int type,
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