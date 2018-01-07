using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public static class JudgeHelper
    {
        [Flags]
        public enum ProcessAccess : uint
        {
            /// <summary>
            /// Required to terminate a process using TerminateProcess.
            /// </summary>
            Terminate = 0x1,

            /// <summary>
            /// Required to create a thread.
            /// </summary>
            CreateThread = 0x2,

            /// <summary>
            /// Undocumented.
            /// </summary>
            SetSessionId = 0x4,

            /// <summary>
            /// Required to perform an operation on the address space of a process (see VirtualProtectEx and WriteProcessMemory).
            /// </summary>
            VmOperation = 0x8,

            /// <summary>
            /// Required to read memory in a process using ReadProcessMemory.
            /// </summary>
            VmRead = 0x10,

            /// <summary>
            /// Required to write to memory in a process using WriteProcessMemory.
            /// </summary>
            VmWrite = 0x20,

            /// <summary>
            /// Required to duplicate a handle using DuplicateHandle.
            /// </summary>
            DupHandle = 0x40,

            /// <summary>
            /// Required to create a process.
            /// </summary>
            CreateProcess = 0x80,

            /// <summary>
            /// Required to set memory limits using SetProcessWorkingSetSize.
            /// </summary>
            SetQuota = 0x100,

            /// <summary>
            /// Required to set certain information about a process, such as its priority class (see SetPriorityClass).
            /// </summary>
            SetInformation = 0x200,

            /// <summary>
            /// Required to retrieve certain information about a process, such as its token, exit code, and priority class (see OpenProcessToken, GetExitCodeProcess, GetPriorityClass, and IsProcessInJob).
            /// </summary>
            QueryInformation = 0x400,

            /// <summary>
            /// Undocumented.
            /// </summary>
            SetPort = 0x800,

            /// <summary>
            /// Required to suspend or resume a process.
            /// </summary>
            SuspendResume = 0x800,

            /// <summary>
            /// Required to retrieve certain information about a process (see QueryFullProcessImageName). A handle that has the PROCESS_QUERY_INFORMATION access right is automatically granted PROCESS_QUERY_LIMITED_INFORMATION.
            /// </summary>
            QueryLimitedInformation = 0x1000,

            /// <summary>
            /// Required to wait for the process to terminate using the wait functions.
            /// </summary>
            Synchronize = 0x100000
        }

        [DllImport("ntdll.dll")]
        private static extern uint NtResumeProcess([In] IntPtr processHandle);

        [DllImport("ntdll.dll")]
        private static extern uint NtSuspendProcess([In] IntPtr processHandle);

        [DllImport("ntdll.dll")]
        private static extern uint NtTerminateProcess([In] IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            ProcessAccess desiredAccess,
            bool inheritHandle,
            int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle([In] IntPtr handle);


        public static ConcurrentDictionary<string, Process> Processes = new ConcurrentDictionary<string, Process>();
        private static readonly WqlEventQuery QCreate = new WqlEventQuery("__InstanceCreationEvent",
            TimeSpan.FromSeconds(1),
            "TargetInstance ISA 'Win32_Process'");
        private static readonly ManagementEventWatcher WCreate = new ManagementEventWatcher(QCreate);
        public static void Init()
        {
            WCreate.EventArrived += (sender, e) => GetInfo(e.NewEvent);

            WCreate.Start();

            new Thread(KillWerFault).Start();
        }

        public static void GetInfo(ManagementBaseObject mobj)
        {
            if (Connection.CurJudgingCnt == 0) return;
            var instance = (ManagementBaseObject)mobj["TargetInstance"];
            var pid = Convert.ToInt32(instance["ProcessId"]);
            SuspendProcess(pid);
            var testguid = (string)instance["Name"];
            if (!testguid.Contains("test_hjudge_"))
            {
                ResumeProcess(pid);
                return;
            }

            var index = testguid.IndexOf(".", StringComparison.Ordinal);
            if (index < 0)
            {
                ResumeProcess(pid);
                return;
            }

            testguid = testguid.Substring(0, index);
            if (string.IsNullOrEmpty(testguid) || !Processes.ContainsKey(testguid))
            {
                ResumeProcess(pid);
                return;
            }

            try
            {
                Processes[testguid] = Process.GetProcessById(pid);
            }
            catch
            {
                Processes[testguid] = null;
                TerminateProcess(pid);
            }
        }

        public static void Subscribe(string testguid)
        {
            Processes[testguid] = null;
        }


        public static void Desubscribe(string testguid)
        {
            Processes.TryRemove(testguid, out var res);
        }

        public static void SuspendProcess(int processId)
        {
            IntPtr hProc = IntPtr.Zero;
            try
            {
                // Gets the handle to the Process
                hProc = OpenProcess(ProcessAccess.SuspendResume, false, processId);
                if (hProc != IntPtr.Zero)
                    NtSuspendProcess(hProc);
            }
            finally
            {
                // Don't forget to close handle you created.
                if (hProc != IntPtr.Zero)
                    CloseHandle(hProc);
            }
        }

        public static void ResumeProcess(int processId)
        {
            IntPtr hProc = IntPtr.Zero;
            try
            {
                // Gets the handle to the Process
                hProc = OpenProcess(ProcessAccess.SuspendResume, false, processId);
                if (hProc != IntPtr.Zero)
                    NtResumeProcess(hProc);
            }
            finally
            {
                // Don't forget to close handle you created.
                if (hProc != IntPtr.Zero)
                    CloseHandle(hProc);
            }
        }

        public static void TerminateProcess(int processId)
        {
            IntPtr hProc = IntPtr.Zero;
            try
            {
                // Gets the handle to the Process
                hProc = OpenProcess(ProcessAccess.Terminate, false, processId);
                if (hProc != IntPtr.Zero)
                    NtTerminateProcess(hProc);
            }
            finally
            {
                // Don't forget to close handle you created.
                if (hProc != IntPtr.Zero)
                    CloseHandle(hProc);
            }
        }

        public static void KillWerFault()
        {
            while (!Connection.IsExited)
            {
                if (Connection.CurJudgingCnt != 0)
                {
                    try
                    {
                        var ps = Process.GetProcessesByName("werfault");
                        Parallel.ForEach(ps, item =>
                        {
                            if (item.MainWindowHandle != IntPtr.Zero)
                            {
                                item.WaitForInputIdle();
                                item.CloseMainWindow();
                                item.Kill();
                                item.Close();
                            }
                        });
                    }
                    catch
                    {
                        // ignored
                    }
                    Thread.Sleep(10);
                }
                else Thread.Sleep(100);
            }
        }
    }
}
