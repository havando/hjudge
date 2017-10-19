﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class FileRecvInfo
    {
        public string FileId { get; set; }
        public string FileName { get; set; }
        public long TotLength { get; set; }
        public long CurrentLength { get; set; }
        public FileStream Fs { get; set; }
    }
}
