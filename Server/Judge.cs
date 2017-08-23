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
        private readonly Problem _problem;
        private readonly string _workingdir;

        public readonly JudgeInfo JudgeResult = new JudgeInfo();
        private int _cur;
        private bool _isexited;

        private bool _isfault;

        public Judge(int problemId, int userId, string code)
        {
            try
            {
                if (Configuration.Configurations.MutiThreading == 0)
                {
                    var flag = false;
                    while (!flag)
                    {
                        try
                        {
                            lock (Connection.ResourceLoadingLock)
                            {
                                var cpuCounter = new PerformanceCounter
                                {
                                    CategoryName = "Processor",
                                    CounterName = "% Processor Time",
                                    InstanceName = "_Total"
                                };
                                var ramCounter = new PerformanceCounter("Memory", "Available KBytes");
                                var maxMemoryNeeded = _problem.DataSets.Select(i => i.MemoryLimit)
                                    .Concat(new long[] { 0 })
                                    .Max();
                                if (cpuCounter.NextValue() <= 75 && ramCounter.NextValue() > maxMemoryNeeded + 262144 &&
                                    Connection.CurJudgingCnt < 5)
                                    flag = true;
                            }
                        }
                        catch
                        {
                            if (Connection.CurJudgingCnt < 5)
                                flag = true;
                        }
                        Thread.Sleep(100);
                    }
                }
                else
                {
                    while (Connection.CurJudgingCnt >= Configuration.Configurations.MutiThreading)
                        Thread.Sleep(100);
                }
            }
            catch
            {
                //ignored
            }

            if (Connection.CurJudgingCnt == 0)
                Killwerfault();
            lock (Connection.JudgeListCntLock)
            {
                Connection.CurJudgingCnt++;
            }

            try
            {
                _problem = Connection.GetProblem(problemId);
                var id = Guid.NewGuid().ToString().Replace("-", string.Empty);

                JudgeResult.JudgeId = Connection.NewJudge();
                JudgeResult.JudgeDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                JudgeResult.ProblemId = _problem.ProblemId;
                JudgeResult.Code = code;
                JudgeResult.UserId = userId;
                JudgeResult.Exitcode = new int[_problem.DataSets.Length];
                JudgeResult.Result = new string[_problem.DataSets.Length];
                JudgeResult.Score = new float[_problem.DataSets.Length];
                JudgeResult.Timeused = new long[_problem.DataSets.Length];
                JudgeResult.Memoryused = new long[_problem.DataSets.Length];
                Connection.UpdateJudgeInfo(JudgeResult);

                _workingdir = Environment.GetEnvironmentVariable("temp") + "\\Judge_hjudge_" + id;
                if (string.IsNullOrEmpty(_problem.CompileCommand))
                    _problem.CompileCommand = Dn(_workingdir + "\\test.cpp") + " -o " +
                                              Dn(_workingdir + "\\test_hjudge.exe");
                else
                    _problem.CompileCommand = GetRealString(_problem.CompileCommand, 0);
                _problem.SpecialJudge = GetRealString(_problem.SpecialJudge, 0);
                for (var i = 0; i < _problem.ExtraFiles.Length; i++)
                    _problem.ExtraFiles[i] = GetRealString(_problem.ExtraFiles[i], i);
                for (var i = 0; i < _problem.DataSets.Length; i++)
                {
                    _problem.DataSets[i].InputFile = GetRealString(_problem.DataSets[i].InputFile, i);
                    _problem.DataSets[i].OutputFile = GetRealString(_problem.DataSets[i].OutputFile, i);
                }
                _problem.InputFileName = GetRealString(_problem.InputFileName, 0);
                _problem.OutputFileName = GetRealString(_problem.OutputFileName, 0);

                Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 开始评测 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}");

                BeginJudge();

                Connection.UpdateJudgeInfo(JudgeResult);

                Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，结果：{JudgeResult.ResultSummery}");
            }
            catch
            {
                //ignored
            }

            try
            {
                DeleteFiles(_workingdir);
            }
            catch
            {
                //ignored
            }
            lock (Connection.JudgeListCntLock)
            {
                Connection.CurJudgingCnt--;
            }
        }

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

        private void DeleteFiles(string path)
        {
            try
            {
                foreach (var t in Directory.GetDirectories(path))
                    DeleteFiles(t);
                foreach (var t in Directory.GetFiles(path))
                    File.Delete(t);
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
                Directory.CreateDirectory(_workingdir);
                File.WriteAllText(_workingdir + "\\test.cpp", JudgeResult.Code);
                foreach (var t in _problem.ExtraFiles)
                {
                    if (string.IsNullOrEmpty(t))
                        continue;
                    File.Copy(t, _workingdir + "\\" + Path.GetFileName(t), true);
                }
            }
            catch (Exception ex)
            {
                for (_cur = 0; _cur < JudgeResult.Result.Length; _cur++)
                {
                    JudgeResult.Result[_cur] = $"Unknown Error: {ex.Message}";
                    JudgeResult.Exitcode[_cur] = 0;
                    JudgeResult.Score[_cur] = 0;
                    JudgeResult.Timeused[_cur] = 0;
                    JudgeResult.Memoryused[_cur] = 0;
                }
                return;
            }
            if (Compile())
                Judging();
            else
                for (var i = 0; i < JudgeResult.Result.Length; i++)
                {
                    JudgeResult.Result[i] = "Compile Error";
                    JudgeResult.Exitcode[i] = 0;
                    JudgeResult.Score[i] = 0;
                    JudgeResult.Timeused[i] = 0;
                    JudgeResult.Memoryused[i] = 0;
                }
        }

        private void Judging()
        {
            for (_cur = 0; _cur < _problem.DataSets.Length; _cur++)
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
                    try
                    {
                        File.Copy(_problem.DataSets[_cur].InputFile, _workingdir + "\\" + _problem.InputFileName,
                            true);
                    }
                    catch (Exception ex)
                    {
                        JudgeResult.Result[_cur] = $"Unknown Error: {ex.Message}";
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
                    _isfault = false;
                    _isexited = false;
                    var excute = new Process
                    {
                        StartInfo =
                        {
                            FileName = _workingdir + "\\test_hjudge.exe",
                            WorkingDirectory = _workingdir,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            ErrorDialog = false,
                            CreateNoWindow = true,
                            UseShellExecute = true
                        },
                        EnableRaisingEvents = true
                    };
                    excute.Exited += Exithandler;
                    Thread.Sleep(100);
                    try
                    {
                        excute.Start();
                    }
                    catch (Exception ex)
                    {
                        JudgeResult.Result[_cur] = $"Unknown Error: {ex.Message}";
                        JudgeResult.Exitcode[_cur] = 0;
                        JudgeResult.Score[_cur] = 0;
                        JudgeResult.Timeused[_cur] = 0;
                        JudgeResult.Memoryused[_cur] = 0;
                        continue;
                    }
                    long curTime = 0;
                    var noProcessTime = DateTime.Now;
                    while (!_isexited)
                    {
                        long dt = 0;
                        try
                        {
                            excute.Refresh();
                            JudgeResult.Timeused[_cur] = Convert.ToInt64(excute.TotalProcessorTime.TotalMilliseconds);
                            if (curTime != JudgeResult.Timeused[_cur])
                            {
                                noProcessTime = DateTime.Now;
                                curTime = JudgeResult.Timeused[_cur];
                            }
                            else
                            {
                                dt = Convert.ToInt64((DateTime.Now - noProcessTime).TotalMilliseconds);
                            }
                            JudgeResult.Memoryused[_cur] = excute.PeakWorkingSet64 / 1024;
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
                        if (JudgeResult.Timeused[_cur] > _problem.DataSets[_cur].TimeLimit || dt > _problem.DataSets[_cur].TimeLimit * 10)
                        {
                            _isfault = true;
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
                            JudgeResult.Result[_cur] = "Time Limit Exceeded";
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Exitcode[_cur] = 0;
                            if (dt > _problem.DataSets[_cur].TimeLimit * 10 && JudgeResult.Timeused[_cur] < _problem.DataSets[_cur].TimeLimit)
                            {
                                JudgeResult.Result[_cur] = "Output File Error";
                            }
                        }
                        if (JudgeResult.Memoryused[_cur] > _problem.DataSets[_cur].MemoryLimit)
                        {
                            _isfault = true;
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
                            JudgeResult.Result[_cur] = "Memory Limit Exceeded";
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Exitcode[_cur] = 0;
                        }
                    }
                    if (_isfault) continue;
                    Thread.Sleep(100);
                    lock (Connection.ComparingLock)
                    {
                        if (!File.Exists(_workingdir + "\\" + _problem.OutputFileName))
                        {
                            JudgeResult.Result[_cur] = "Output File Error";
                            JudgeResult.Score[_cur] = 0;
                            continue;
                        }
                        if (!string.IsNullOrEmpty(_problem.SpecialJudge))
                        {
                            if (File.Exists(_problem.SpecialJudge))
                            {
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
                                        Thread.Sleep(100);
                                        if (!File.Exists(_workingdir + "\\hjudge_spj_result.dat"))
                                        {
                                            JudgeResult.Result[_cur] = "Special Judge Error";
                                            JudgeResult.Score[_cur] = 0;
                                        }
                                        else
                                        {
                                            var p = File.ReadAllText(_workingdir + "\\hjudge_spj_result.dat");
                                            p = Regex.Replace(p, @"\s", string.Empty);
                                            var gs = Convert.ToSingle(p);
                                            JudgeResult.Score[_cur] = _problem.DataSets[_cur].Score * gs;
                                            if (Math.Abs(gs - 1) > 0.000001)
                                                JudgeResult.Result[_cur] = "Wrong Answer";
                                            else
                                                JudgeResult.Result[_cur] = "Correct";
                                        }
                                    }
                                    catch
                                    {
                                        JudgeResult.Result[_cur] = "Special Judge Error";
                                        JudgeResult.Score[_cur] = 0;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    JudgeResult.Result[_cur] = $"Unknown Error: {ex.Message}";
                                    JudgeResult.Score[_cur] = 0;
                                }
                            }
                            else
                            {
                                JudgeResult.Result[_cur] = "Special Judge Error";
                                JudgeResult.Score[_cur] = 0;
                            }
                        }
                        else
                        {
                            Thread.Sleep(100);
                            try
                            {
                                var fs1 = new FileStream(_problem.DataSets[_cur].OutputFile, FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.Read);
                                var fs2 = new FileStream(_workingdir + "\\" + _problem.OutputFileName,
                                    FileMode.OpenOrCreate,
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
                                    if (Regex.Replace(s1, @"\s", string.Empty) ==
                                        Regex.Replace(s2, @"\s", string.Empty))
                                        JudgeResult.Result[_cur] = "Presentation Error";
                                    else break;
                                } while (!(sr1.EndOfStream && sr2.EndOfStream));
                                sr1.Close();
                                sr2.Close();
                                fs1.Close();
                                fs2.Close();
                                if (iswrong)
                                    continue;
                                JudgeResult.Result[_cur] = "Correct";
                                JudgeResult.Score[_cur] = _problem.DataSets[_cur].Score;
                            }
                            catch (Exception ex)
                            {
                                JudgeResult.Result[_cur] = $"Unknown Error: {ex.Message}";
                                JudgeResult.Score[_cur] = 0;
                            }
                        }
                    }
                }
        }

        private void Exithandler(object sender, EventArgs e)
        {
            if (_isexited)
                return;
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
                            if (item.MainWindowHandle != IntPtr.Zero)
                            {
                                item.WaitForInputIdle();
                                item.CloseMainWindow();
                                item.Kill();
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