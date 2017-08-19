using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Updater
{
    public static class UpdateInfo
    {
        public static string Product { get; set; }
        public static string CurrentVersion { get; set; }
        public static int ProcessId { get; set; }
        public static string RootDirectory { get; set; }
    }
}
