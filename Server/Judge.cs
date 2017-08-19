using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public class Judge
    {
        private readonly string _workingdir;
        private readonly Problem _problem;

        private bool _isfault;
        private bool _isexited;
        private int _cur;

        public readonly JudgeInfo JudgeResult = new JudgeInfo();

        public static string GetEngName(string origin)
        {
            var re = new Regex("[A-Z]|[a-z]|[0-9]");
            return re.Matches(origin).Cast<object>().Aggregate(string.Empty, (current, t) => current + t);
        }

        private string GetRealString(string origin, int cur)
        {
            return origin.Replace("${woringdir}", _workingdir)
                .Replace("${datadir}", Environment.CurrentDirectory + "\\Data")
                .Replace("${name}", GetEngName(_problem.ProblemName))
                .Replace("${index0}", cur.ToString())
                .Replace("${index}", (cur + 1).ToString())
                .Replace("${file}", _workingdir + "\\test.cpp")
                .Replace("${targetfile}", _workingdir + "\\test_hjudge.exe");
        }

        public Judge(int problemId, int userId, string code)
        {
            if (Configuration.Configurations.MutiThreading == 0)
            {
                var flag = false;
                while (!flag)
                {
                    try
                    {
                        var cpuCounter = new PerformanceCounter
                        {
                            CategoryName = "Processor",
                            CounterName = "% Processor Time",
                            InstanceName = "_Total"
                        };
                        var ramCounter = new PerformanceCounter("Memory", "Available KBytes");
                        var maxMemoryNeeded = _problem.DataSets.Select(i => i.MemoryLimit).Concat(new long[] { 0 }).Max();
                        if (cpuCounter.NextValue() <= 75 && ramCounter.NextValue() > maxMemoryNeeded + 262144 &&
                            Connection.CurJudgingCnt < 5)
                        {
                            flag = true;
                        }
                    }
                    catch
                    {
                        if (Connection.CurJudgingCnt < 5)
                        {
                            flag = true;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            else
            {
                while (Connection.CurJudgingCnt >= Configuration.Configurations.MutiThreading)
                {
                    Thread.Sleep(100);
                }
            }

            if (Connection.CurJudgingCnt == 0)
            {
                Killwerfault();
            }

            Connection.CurJudgingCnt++;

            while (Connection.IsLoadingProblem)
            {
                Thread.Sleep(100);
            }
            Connection.IsLoadingProblem = true;
            try
            {
                _problem = Connection.GetProblem(problemId);
                var id = new Guid().ToString().Replace("-", string.Empty) +
                         DateTime.Now.ToString("_yyyyMMddHHmmssffff");
                JudgeResult.JudgeDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
                JudgeResult.JudgeId = Connection.NewJudge();
                JudgeResult.ProblemId = _problem.ProblemId;
                JudgeResult.Code = code;
                JudgeResult.UserId = userId;
                JudgeResult.Exitcode = new int[_problem.DataSets.Length];
                JudgeResult.Result = new string[_problem.DataSets.Length];
                JudgeResult.Score = new float[_problem.DataSets.Length];
                JudgeResult.Timeused = new long[_problem.DataSets.Length];
                JudgeResult.Memoryused = new long[_problem.DataSets.Length];
                _workingdir = Environment.GetEnvironmentVariable("temp") + "\\Judge_hjudge_" + id;
                if (string.IsNullOrEmpty(_problem.CompileCommand))
                {
                    _problem.CompileCommand = Dn(_workingdir + "\\test.cpp") + " -o " +
                                              Dn(_workingdir + "\\test_hjudge.exe");
                }
                else
                {
                    _problem.CompileCommand = GetRealString(_problem.CompileCommand, 0);
                }
                _problem.SpecialJudge = GetRealString(_problem.SpecialJudge, 0);
                for (var i = 0; i < _problem.ExtraFiles.Length; i++)
                {
                    _problem.ExtraFiles[i] = GetRealString(_problem.ExtraFiles[i], i);
                }
                for (var i = 0; i < _problem.DataSets.Length; i++)
                {
                    _problem.DataSets[i].InputFile = GetRealString(_problem.DataSets[i].InputFile, i);
                    _problem.DataSets[i].OutputFile = GetRealString(_problem.DataSets[i].OutputFile, i);
                }
                _problem.InputFileName = GetRealString(_problem.InputFileName, 0);
                _problem.OutputFileName = GetRealString(_problem.OutputFileName, 0);

                Connection.IsLoadingProblem = false;

                Connection.UpdateMainPageState(
                    $"{DateTime.Now} 新评测，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}");

                BeginJudge();

                Connection.UpdateJudgeInfo(JudgeResult);
            }
            catch
            {
                //ignored
            }
            Connection.CurJudgingCnt--;

            Thread.Sleep(100);

            try
            {
                DeleteFiles(_workingdir);
            }
            catch
            {
                //ignored
            }
            GC.Collect();
        }

        private void DeleteFiles(string path)
        {
            try
            {
                foreach (var t in Directory.GetDirectories(path))
                {
                    DeleteFiles(t);
                }
                foreach (var t in Directory.GetFiles(path))
                {
                    File.Delete(t);
                }
                Directory.Delete(path);
            }
            catch
            {
                //ignored
            }
        }

        private void BeginJudge()
        {
            try
            {
                while (Connection.IsCopying)
                {
                    Thread.Sleep(100);
                }
                Connection.IsCopying = true;
                Directory.CreateDirectory(_workingdir);
                File.WriteAllText(_workingdir + "\\test.cpp", JudgeResult.Code);
                foreach (var t in _problem.ExtraFiles)
                {
                    if (string.IsNullOrEmpty(t))
                    {
                        continue;
                    }
                    File.Copy(t, _workingdir + "\\" + Path.GetFileName(t), true);
                }
                Connection.IsCopying = false;
            }
            catch
            {
                Connection.IsCopying = false;
                for (_cur = 0; _cur < JudgeResult.Result.Length; _cur++)
                {
                    JudgeResult.Result[_cur] = "Unknown Error";
                    JudgeResult.Exitcode[_cur] = 0;
                    JudgeResult.Score[_cur] = 0;
                    JudgeResult.Timeused[_cur] = 0;
                    JudgeResult.Memoryused[_cur] = 0;
                }
                return;
            }
            if (Compile())
            {
                Connection.IsCompiling = false;
                Judging();
            }
            else
            {
                Connection.IsCompiling = false;
                for (var i = 0; i < JudgeResult.Result.Length; i++)
                {
                    JudgeResult.Result[i] = "Compile Error";
                    JudgeResult.Exitcode[i] = 0;
                    JudgeResult.Score[i] = 0;
                    JudgeResult.Timeused[i] = 0;
                    JudgeResult.Memoryused[i] = 0;
                }
            }
        }

        private void Judging()
        {
            for (_cur = 0; _cur < _problem.DataSets.Length; _cur++)
            {
                if (!File.Exists(_problem.DataSets[_cur].InputFile) || !File.Exists(_problem.DataSets[_cur].OutputFile))
                {
                    JudgeResult.Result[_cur] = "Problem Configuration Error";
                    JudgeResult.Exitcode[_cur] = 0;
                    JudgeResult.Score[_cur] = 0;
                    JudgeResult.Timeused[_cur] = 0;
                    JudgeResult.Memoryused[_cur] = 0;
                }
                else
                {
                    while (Connection.IsCopying)
                    {
                        Thread.Sleep(100);
                    }
                    Connection.IsCopying = true;
                    try
                    {
                        File.Copy(_problem.DataSets[_cur].InputFile, _workingdir + "\\" + _problem.InputFileName, true);
                    }
                    catch
                    {
                        Connection.IsCopying = false;
                        JudgeResult.Result[_cur] = "Unknown Error";
                        JudgeResult.Exitcode[_cur] = 0;
                        JudgeResult.Score[_cur] = 0;
                        JudgeResult.Timeused[_cur] = 0;
                        JudgeResult.Memoryused[_cur] = 0;
                        continue;
                    }
                    try
                    {
                        File.Delete(_workingdir + "\\" + _problem.OutputFileName);
                    }
                    catch
                    {
                        //ignored
                    }
                    Connection.IsCopying = false;
                    _isfault = false;
                    _isexited = false;
                    var excute = new Process
                    {
                        StartInfo =
                        {
                            FileName = _workingdir + "\\test_hjudge.exe",
                            WorkingDirectory  = _workingdir,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            ErrorDialog = false,
                            CreateNoWindow = true,
                            UseShellExecute = true
                        },
                        EnableRaisingEvents = true
                    };
                    excute.Exited += Exithandler;
                    try
                    {
                        excute.Start();
                    }
                    catch
                    {
                        JudgeResult.Result[_cur] = "Unknown Error";
                        JudgeResult.Exitcode[_cur] = 0;
                        JudgeResult.Score[_cur] = 0;
                        JudgeResult.Timeused[_cur] = 0;
                        JudgeResult.Memoryused[_cur] = 0;
                        continue;
                    }
                    while (!_isexited)
                    {
                        long dt = 0;
                        try
                        {
                            excute.Refresh();
                            JudgeResult.Timeused[_cur] = Convert.ToInt64(excute.TotalProcessorTime.TotalMilliseconds);
                            JudgeResult.Memoryused[_cur] = excute.PeakWorkingSet64 / 1024;
                            dt = Convert.ToInt64((DateTime.Now - excute.StartTime).TotalMilliseconds);
                        }
                        catch
                        {
                            try
                            {
                                excute.Kill();
                                excute.Close();
                            }
                            catch
                            {
                                //ignored
                            }
                            _isexited = true;
                        }
                        if (JudgeResult.Timeused[_cur] > _problem.DataSets[_cur].TimeLimit || dt >= _problem.DataSets[_cur].TimeLimit * 10)
                        {
                            _isfault = true;
                            try
                            {
                                excute.Kill();
                                excute.Close();
                                _isexited = true;
                            }
                            catch
                            {
                                // ignored
                            }
                            JudgeResult.Result[_cur] = "Time Limit Exceeded";
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Exitcode[_cur] = 0;
                        }
                        if (JudgeResult.Memoryused[_cur] > _problem.DataSets[_cur].MemoryLimit)
                        {
                            _isfault = true;
                            try
                            {
                                excute.Kill();
                                excute.Close();
                                _isexited = true;
                            }
                            catch
                            {
                                // ignored
                            }
                            JudgeResult.Result[_cur] = "Memory Limit Exceeded";
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Exitcode[_cur] = 0;
                        }
                    }
                    if (_isfault) continue;
                    Thread.Sleep(100);
                    if (!File.Exists(_workingdir + "\\" + _problem.OutputFileName))
                    {
                        JudgeResult.Result[_cur] = "Output File Error";
                        JudgeResult.Score[_cur] = 0;
                        continue;
                    }
                    while (Connection.IsComparing)
                    {
                        Thread.Sleep(100);
                    }
                    Connection.IsComparing = true;
                    if (!string.IsNullOrEmpty(_problem.SpecialJudge))
                    {
                        if (File.Exists(_problem.SpecialJudge))
                        {
                            Thread.Sleep(100);
                            try
                            {
                                try
                                {
                                    File.Delete(_workingdir + "\\hjudge_spj_result.dat");
                                }
                                catch
                                {
                                    // ignored
                                }
                                try
                                {
                                    var xx = new ProcessStartInfo
                                    {
                                        ErrorDialog = false,
                                        UseShellExecute = true,
                                        CreateNoWindow = true,
                                        FileName = _problem.SpecialJudge,
                                        Arguments = Dn(_problem.DataSets[_cur].InputFile) + " " +
                                                    Dn(_problem.DataSets[_cur].OutputFile) + " " +
                                                    Dn(_workingdir + "\\" + _problem.OutputFileName) + " " +
                                                    Dn(_workingdir + "\\hjudge_spj_result.dat"),
                                        WindowStyle = ProcessWindowStyle.Hidden
                                    };
                                    Process.Start(xx)?.WaitForExit();
                                    if (!File.Exists(_workingdir + "\\hjudge_spj_result.dat"))
                                    {
                                        JudgeResult.Result[_cur] = "Special Judger Error";
                                        JudgeResult.Score[_cur] = 0;
                                    }
                                    else
                                    {
                                        var p = File.ReadAllText(_workingdir + "\\hjudge_spj_result.dat");
                                        p = Regex.Replace(p, @"\s", string.Empty);
                                        var gs = Convert.ToSingle(p);
                                        JudgeResult.Score[_cur] = _problem.DataSets[_cur].Score * gs;
                                        if (Math.Abs(gs - 1) > 0.000001)
                                        {
                                            JudgeResult.Result[_cur] = "Wrong Answer";
                                        }
                                        else
                                        {
                                            JudgeResult.Result[_cur] = "Correct";
                                        }
                                    }
                                }
                                catch
                                {
                                    Connection.IsComparing = false;
                                    JudgeResult.Result[_cur] = "Special Judger Error";
                                    JudgeResult.Score[_cur] = 0;
                                }
                            }
                            catch
                            {
                                Connection.IsComparing = false;
                                JudgeResult.Result[_cur] = "Unknown Error";
                                JudgeResult.Score[_cur] = 0;
                            }

                        }
                        else
                        {
                            JudgeResult.Result[_cur] = "Special Judger Error";
                            JudgeResult.Score[_cur] = 0;
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                        try
                        {
                            var fs1 = new FileStream(_problem.DataSets[_cur].OutputFile, FileMode.Open, FileAccess.Read,
                                FileShare.Read);
                            var fs2 = new FileStream(_workingdir + "\\" + _problem.OutputFileName, FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read);
                            var sr1 = new StreamReader(fs1);
                            var sr2 = new StreamReader(fs2);
                            var iswrong = false;
                            do
                            {
                                string s1;
                                try
                                {
                                    s1 = sr1.ReadLine();
                                }
                                catch
                                {
                                    s1 = string.Empty;
                                }
                                string s2;
                                try
                                {
                                    s2 = sr2.ReadLine();
                                }
                                catch
                                {
                                    s2 = string.Empty;
                                }
                                if (s1 == null) s1 = string.Empty;
                                if (s2 == null) s2 = string.Empty;
                                s1 = s1.TrimEnd(' ');
                                s2 = s2.TrimEnd(' ');
                                if (s1 == s2) continue;
                                iswrong = true;
                                JudgeResult.Result[_cur] = "Wrong Answer";
                                JudgeResult.Score[_cur] = 0;
                                if (Regex.Replace(s1, @"\s", string.Empty) == Regex.Replace(s2, @"\s", string.Empty))
                                {
                                    JudgeResult.Result[_cur] = "Presentation Error";
                                }
                                else break;
                            } while (!(sr1.EndOfStream && sr2.EndOfStream));
                            Thread.Sleep(100);
                            sr1.Close();
                            sr2.Close();
                            fs1.Close();
                            fs2.Close();
                            if (iswrong)
                            {
                                Connection.IsComparing = false;
                                continue;
                            }
                            JudgeResult.Result[_cur] = "Correct";
                            JudgeResult.Score[_cur] = _problem.DataSets[_cur].Score;
                        }
                        catch
                        {
                            Connection.IsComparing = false;
                            JudgeResult.Result[_cur] = "Unknown Error";
                            JudgeResult.Score[_cur] = 0;
                        }
                    }
                    Connection.IsComparing = false;
                }
            }
        }

        private void Exithandler(object sender, EventArgs e)
        {
            if (_isexited)
            {
                return;
            }
            var process = sender as Process;
            try
            {
                JudgeResult.Exitcode[_cur] = process?.ExitCode ?? 0;
            }
            catch
            {
                JudgeResult.Exitcode[_cur] = 0;
            }
            if (JudgeResult.Exitcode[_cur] != 0 && !_isfault)
            {
                JudgeResult.Result[_cur] = "Runtime Error";
                JudgeResult.Score[_cur] = 0;
                _isfault = true;
            }
            try
            {
                process?.Kill();
                process?.Close();
            }
            catch
            {
                // ignored
            }
            _isexited = true;
        }

        private void Killwerfault()
        {
            Task.Run(() =>
            {
                while (Connection.CurJudgingCnt != 0)
                {
                    try
                    {
                        var ps = Process.GetProcessesByName("werfault");
                        foreach (var item in ps)
                        {
                            if (item.MainWindowHandle != IntPtr.Zero)
                            {
                                item.WaitForInputIdle();
                                item.CloseMainWindow();
                                item.Kill();
                            }

                        }
                    }
                    catch
                    {
                        // ignored
                    }
                    Thread.Sleep(10);
                }
            });
        }

        private static string Dn(string filename)
        {
            return "\"" + filename + "\"";
        }

        private bool Compile()
        {
            while (Connection.IsCompiling)
            {
                Thread.Sleep(100);
            }
            Connection.IsCompiling = true;
            try
            {
                var a = new ProcessStartInfo
                {
                    FileName = Configuration.Configurations.Compiler,
                    ErrorDialog = false,
                    UseShellExecute = false,
                    Arguments = _problem.CompileCommand,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(a)?.WaitForExit();
                Thread.Sleep(100);
                return File.Exists(_workingdir + "\\test_hjudge.exe");
            }
            catch
            {
                return false;
            }
        }
    }
}