﻿namespace Server
{
    public static partial class Connection
    {
        public static readonly object ComparingLock = new object();
        private static readonly object DataBaseLock = new object();
        public static readonly object ResourceLoadingLock = new object();
        private static readonly object BytesLock = new object();
        public static readonly object JudgeListCntLock = new object();
        public static readonly object JudgePointLock = new object();
        private static readonly object ActionCounterLock = new object();
    }
}