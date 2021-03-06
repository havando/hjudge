﻿using System;
using System.Threading;

namespace Server
{
    public static partial class Connection
    {
        public static readonly object ComparingLock = new object();
        private static readonly UsingLock DataBaseLock = new UsingLock();
        public static readonly object ResourceLoadingLock = new object();
        public static readonly object JudgeListCntLock = new object();
        private static readonly object ActionCounterLock = new object();
        public static readonly object AdditionWorkingThreadLock = new object();
        public static readonly object RemoveClientLock = new object();
        public static readonly object StdinWriterLock = new object();
        public static readonly object KillWerfaultLock = new object();
        public static readonly object FileProcessingLock = new object();
    }
}