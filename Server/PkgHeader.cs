using System.Runtime.InteropServices;

namespace Server
{
    [StructLayout(LayoutKind.Sequential)]
    public class PkgHeader
    {
        public int BodySize;
        public int Id;
    }
}