using System;

namespace Server
{
    public class UserInfo
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string RegisterDate { get; set; }
        public string Password { get; set; }
        public int Type { get; set; }
        public string Icon { get; set; }
        public string Achievement { get; set; }
        public bool? IsChanged { get; set; }
    }
    public static class UserHelper
    {
        public static UserInfo CurrentUser { get; private set; }

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
        }
    }
}
