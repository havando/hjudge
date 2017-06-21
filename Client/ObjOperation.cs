using System.Collections.Generic;

namespace Client
{
    public class ObjOperation
    {
        public string Operation { get; set; }
        public List<byte[]> Content = new List<byte[]>();
    }
}