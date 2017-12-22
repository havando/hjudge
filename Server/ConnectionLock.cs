using System;
using System.Threading;

namespace Server
{
    public static partial class Connection
    {
        public static readonly object ComparingLock = new object();
        private static UsingLock DataBaseLock = new UsingLock();
        public static readonly object ResourceLoadingLock = new object();
        public static readonly object JudgeListCntLock = new object();
        private static readonly object ActionCounterLock = new object();
        public static readonly object AdditionWorkingThreadLock = new object();
        public static readonly object RemoveClientLock = new object();
        public static readonly object StdinWriterLock = new object();
    }
}