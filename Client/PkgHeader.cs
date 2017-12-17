using System.Runtime.InteropServices;

namespace Client
{
    [StructLayout(LayoutKind.Sequential)]
    public class PkgHeader
    {
        public int BodySize;
        public int Id;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public char[] Token;
    }
}