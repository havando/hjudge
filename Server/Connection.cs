using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows;

namespace Server
{
    public static partial class Connection
    {
        public const string Divpar = "<h~|~j>";
        public static bool IsExited;
        private static Func<string, UIElement, bool, UIElement> _updateMain;
        private static int _id;
        public static bool CanPostJudgTask;
        public static int IntelligentAdditionWorkingThread;

        private static readonly ConcurrentQueue<Task> ActionList = new ConcurrentQueue<Task>();

        public static int CurJudgingCnt = 0;

        public static UIElement UpdateMainPageState(string content)
        {
            return _updateMain.Invoke(content, null, false);
        }

        public static void UpdateMainPageState(string content, UIElement textBlock)
        {
            _updateMain.Invoke(content, textBlock, false);
        }

        public static void RemoveMainPageState(UIElement textBlock)
        {
            _updateMain.Invoke(string.Empty, textBlock, true);
        }
    }
}