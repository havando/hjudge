using System.Collections.Generic;

namespace Server
{
    public class ObjOperation
    {
        public string Operation { get; set; }
        public List<byte[]> Content = new List<byte[]>();
        public ClientInfo Client { get; set; }
    }
}