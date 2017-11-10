using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Server
{
    public class ClientData
    {
        public readonly ConcurrentQueue<List<byte>> Data = new ConcurrentQueue<List<byte>>();
        public ClientInfo Info;
    }
}