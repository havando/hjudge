using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Server
{
    public class ClientData
    {
        public readonly ConcurrentQueue<(List<byte> Content, string Token)> Data = new ConcurrentQueue<(List<byte>, string)>();
        public ClientInfo Info;
    }
}