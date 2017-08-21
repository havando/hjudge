using System.Collections.Generic;

namespace Server
{
    public class ObjOperation
    {
        public List<byte[]> Content = new List<byte[]>();
        public string Operation { get; set; }
        public ClientInfo Client { get; set; }
    }
}