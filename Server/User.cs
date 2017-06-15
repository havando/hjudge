using System;

namespace Server
{
    public class User
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public DateTime RegisterDate { get; set; }
        public string Password { get; set; }
        public int Type { get; set; }
        public byte[] Icon { get; set; }
        public string Achievement { get; set; }
    }
}
