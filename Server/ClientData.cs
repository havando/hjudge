using System.Collections.Generic;

namespace Server
{
    public class ClientData
    {
        public ClientInfo Info;
        public readonly List<byte> Data = new List<byte>();
    }
}