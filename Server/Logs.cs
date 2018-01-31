using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public static class Logs
    {
        private static FileStream _fs = new FileStream(AppDomain.CurrentDomain.BaseDirectory + "\\Logs.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        private static object _fsLock = new object();
        public static void CommitLogs(string msg)
        {
            lock (_fsLock)
            {
                _fs.Seek(0, SeekOrigin.End);
                var bytes = Encoding.Unicode.GetBytes($"{DateTime.Now} {msg}\r\n");
                _fs.Write(bytes, 0, bytes.Length);
            }
        }
    }
}
