using System.Runtime.InteropServices;

namespace Client
{
    [StructLayout(LayoutKind.Sequential)]
    public class PkgHeader
    {
        public int BodySize;
        public int Id;
    }
}