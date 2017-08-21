using System.Runtime.InteropServices;

namespace Server
{
    [StructLayout(LayoutKind.Sequential)]
    public class PkgHeader
    {
        public int BodySize;
        public int Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class PkgInfo
    {
        public bool IsHeader;
        public int Length;
    }
}