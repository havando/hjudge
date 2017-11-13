using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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

        public Judge(int problemId, int userId, string code, string type, bool isStdio)
        {
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
                JudgeResult.Type = type;

                var textBlock = Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 准备评测 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}");

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

                lock (Connection.JudgeListCntLock)
                {
                    if (Connection.CurJudgingCnt == 0)
                        new Thread(Killwerfault).Start();
                    Connection.CurJudgingCnt++;
                }

                Connection.UpdateJudgeInfo(JudgeResult);
                _workingdir = Environment.GetEnvironmentVariable("temp") + "\\Judge_hjudge_" + id;

                var t = Configuration.Configurations.Compiler.FirstOrDefault(i => i.DisplayName == type);
                if (t == null)
                {
                    for (var i = 0; i < JudgeResult.Result.Length; i++)
                    {
                        JudgeResult.Result[i] = "Compile Error";
                        JudgeResult.Exitcode[i] = 0;
                        JudgeResult.Score[i] = 0;
                        JudgeResult.Timeused[i] = 0;
                        JudgeResult.Memoryused[i] = 0;
                    }
                    Connection.UpdateJudgeInfo(JudgeResult);

                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummery}", textBlock);
                    return;
                }
                var extList = t.ExtName.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries);
                if (extList.Length == 0)
                {
                    for (var i = 0; i < JudgeResult.Result.Length; i++)
                    {
                        JudgeResult.Result[i] = "Compile Error";
                        JudgeResult.Exitcode[i] = 0;
                        JudgeResult.Score[i] = 0;
                        JudgeResult.Timeused[i] = 0;
                        JudgeResult.Memoryused[i] = 0;
                    }
                    Connection.UpdateJudgeInfo(JudgeResult);

                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummery}", textBlock);
                    return;
                }

                if (string.IsNullOrEmpty(_problem.CompileCommand))
                {
                    _problem.CompileCommand = GetRealString(t.DefaultArgs, 0, extList[0]);
                }
                else
                {
                    var commList = _problem.CompileCommand.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var s in commList)
                    {
                        var comm = s.Split(':');
                        if (comm.Length != 2) continue;
                        if (comm[0] == type)
                        {
                            _problem.CompileCommand = GetRealString(comm[1], 0, extList[0]);
                            break;
                        }
                    }
                }

                _problem.SpecialJudge = GetRealString(_problem.SpecialJudge, 0, extList[0]);

                for (var i = 0; i < _problem.ExtraFiles.Length; i++)
                    _problem.ExtraFiles[i] = GetRealString(_problem.ExtraFiles[i], i, extList[0]);
                for (var i = 0; i < _problem.DataSets.Length; i++)
                {
                    _problem.DataSets[i].InputFile = GetRealString(_problem.DataSets[i].InputFile, i, extList[0]);
                    _problem.DataSets[i].OutputFile = GetRealString(_problem.DataSets[i].OutputFile, i, extList[0]);
                }

                _problem.InputFileName = isStdio ? "stdin" : GetRealString(_problem.InputFileName, 0, extList[0]);
                _problem.OutputFileName = GetRealString(_problem.OutputFileName, 0, extList[0]);

                Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 开始评测 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}", textBlock);

                BeginJudge(t.CompilerPath, t.SafeCheck, textBlock);

                Connection.UpdateJudgeInfo(JudgeResult);

                Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummery}", textBlock);
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

        private string GetRealString(string origin, int cur, string extName)
        {
            return origin.Replace("${woringdir}", _workingdir)
                .Replace("${datadir}", Environment.CurrentDirectory + "\\Data")
                .Replace("${name}", GetEngName(_problem.ProblemName))
                .Replace("${index0}", cur.ToString())
                .Replace("${index}", (cur + 1).ToString())
                .Replace("${file}", _workingdir + $"\\test{extName}")
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

        private void BeginJudge(string compiler, string safeCheck, UIElement textBlock)
        {
            try
            {
                Directory.CreateDirectory(_workingdir);
                var extList = Configuration.Configurations.Compiler
                    .FirstOrDefault(i => i.DisplayName == JudgeResult.Type)?.ExtName.Split(' ');
                if (extList == null || extList.Length == 0)
                {
                    for (_cur = 0; _cur < JudgeResult.Result.Length; _cur++)
                    {
                        JudgeResult.Result[_cur] = "Compile Error";
                        JudgeResult.Exitcode[_cur] = 0;
                        JudgeResult.Score[_cur] = 0;
                        JudgeResult.Timeused[_cur] = 0;
                        JudgeResult.Memoryused[_cur] = 0;
                    }
                    return;
                }
                File.WriteAllText(_workingdir + $"\\test{extList[0]}", JudgeResult.Code, Encoding.Default);
                foreach (var t in _problem.ExtraFiles)
                {
                    if (string.IsNullOrEmpty(t))
                        continue;
                    File.Copy(t, _workingdir + "\\" + Path.GetFileName(t), true);
                }
                if (!string.IsNullOrEmpty(safeCheck))
                {
                    var a = new ProcessStartInfo
                    {
                        FileName = safeCheck,
                        Arguments = Dn(_workingdir + $"\\test{extList[0]}") + " " +
                                    Dn(_workingdir + "\\security_check.res"),
                        ErrorDialog = false,
                        UseShellExecute = true,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    Process.Start(a)?.WaitForExit();
                    var fs = File.OpenRead(_workingdir + "\\security_check.res");
                    var sr = new StreamReader(fs);
                    if ((sr.ReadLine()?.Trim() ?? string.Empty) != "ok")
                    {
                        for (_cur = 0; _cur < JudgeResult.Result.Length; _cur++)
                        {
                            JudgeResult.Result[_cur] = "Runtime Error";
                            JudgeResult.Exitcode[_cur] = 0;
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Timeused[_cur] = 0;
                            JudgeResult.Memoryused[_cur] = 0;
                        }
                        sr.Close();
                        fs.Close();
                        return;
                    }
                    sr.Close();
                    fs.Close();
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

            if (Compile(compiler))
                Judging(textBlock);
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

        private void Judging(UIElement textBlock)
        {
            for (_cur = 0; _cur < _problem.DataSets.Length; _cur++)
            {
                if (_cur != 0)
                {
                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测 #{JudgeResult.JudgeId} 数据点 {_cur}/{_problem.DataSets.Length} 完毕，结果：{JudgeResult.Result[_cur - 1]}", textBlock);
                }
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
                    if (_problem.InputFileName != "stdin")
                    {
                        try
                        {
                            File.Copy(_problem.DataSets[_cur].InputFile, _workingdir + "\\" + _problem.InputFileName,
                                true);
                        }
                        catch
                        {
                            Thread.Sleep(1000);
                            try
                            {
                                File.Copy(_problem.DataSets[_cur].InputFile, _workingdir + "\\" + _problem.InputFileName,
                                    true);
                            }
                            catch
                            {
                                Thread.Sleep(1000);
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
                            }
                        }
                    }
                    if (_problem.InputFileName == "stdin")
                        try
                        {
                            File.Delete(_workingdir + "\\" + _problem.OutputFileName + ".htmp");
                        }
                        catch
                        {
                            //ignored
                        }
                    else
                        try
                        {
                            File.Delete(_workingdir + "\\" + _problem.OutputFileName);
                        }
                        catch
                        {
                            //ignored
                        }
                    var execute = new Process
                    {
                        StartInfo =
                        {
                            FileName = _workingdir + "\\test_hjudge.exe",
                            WorkingDirectory = _workingdir,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            ErrorDialog = false,
                            CreateNoWindow = true,
                            UseShellExecute = false,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true
                        },
                        EnableRaisingEvents = true
                    };
                    execute.Exited += Exithandler;

                    Task<string> res = null;
                    _isfault = false;
                    _isexited = false;
                    try
                    {
                        execute.Start();
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

                    var inputStream = execute.StandardInput;
                    var outputStream = execute.StandardOutput;
                    if (_problem.InputFileName == "stdin")
                    {
                        //Thread.Sleep(10);
                        res = outputStream.ReadToEndAsync();
                        inputStream.AutoFlush = true;
                        inputStream.WriteAsync(
                                File.ReadAllText(_problem.DataSets[_cur].InputFile, Encoding.Default) + "\0")
                            .ContinueWith(o =>
                            {
                                try
                                {
                                    inputStream.Close();
                                    inputStream.Dispose();
                                }
                                catch
                                {
                                        //ignored
                                    }
                            });
                    }
                    else
                    {
                        try
                        {
                            inputStream.Close();
                            inputStream.Dispose();
                        }
                        catch
                        {
                            //ignored
                        }
                    }
                    while (!_isexited)
                    {
                        try
                        {
                            execute?.Refresh();
                            JudgeResult.Timeused[_cur] =
                                Convert.ToInt64(execute.TotalProcessorTime.TotalMilliseconds);
                            JudgeResult.Memoryused[_cur] = execute.PeakWorkingSet64 / 1024;
                        }
                        catch
                        {
                            //ignored
                        }
                        if (JudgeResult.Timeused[_cur] > _problem.DataSets[_cur].TimeLimit)
                        {
                            _isfault = true;
                            try
                            {
                                execute?.Kill();
                            }
                            catch
                            {
                                //ignored 
                            }
                            _isexited = true;
                            JudgeResult.Result[_cur] = "Time Limit Exceeded";
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Exitcode[_cur] = 0;
                        }
                        if (JudgeResult.Memoryused[_cur] > _problem.DataSets[_cur].MemoryLimit)
                        {
                            _isfault = true;
                            try
                            {
                                execute?.Kill();
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
                    if (_isfault)
                    {
                        try
                        {
                            execute?.Close();
                        }
                        catch
                        {
                            //ignored
                        }
                        continue;
                    }
                    try
                    {
                        execute?.Kill();
                    }
                    catch
                    {
                        //ignored 
                    }
                    Thread.Sleep(1);
                    if (_problem.InputFileName != "stdin")
                    {
                        if (!File.Exists(_workingdir + "\\" + _problem.OutputFileName))
                        {
                            JudgeResult.Result[_cur] = "Output File Error";
                            JudgeResult.Score[_cur] = 0;
                            continue;
                        }
                    }
                    else
                    {
                        File.WriteAllText(_workingdir + "\\" + _problem.OutputFileName + ".htmp", res?.Result ?? string.Empty, Encoding.Default);
                    }
                    try
                    {
                        outputStream?.Close();
                        outputStream?.Dispose();
                    }
                    catch
                    {
                        //ignored
                    }
                    try
                    {
                        execute?.Close();
                    }
                    catch
                    {
                        //ignored
                    }
                    Thread.Sleep(1);
                    lock (Connection.ComparingLock)
                    {
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
                                                        (_problem.InputFileName != "stdin"
                                                            ? Dn(_workingdir + "\\" + _problem.OutputFileName)
                                                            : Dn(_workingdir + "\\" + _problem.OutputFileName + ".htmp")
                                                        ) + " " +
                                                        Dn(_workingdir + "\\hjudge_spj_result.dat"),
                                            WindowStyle = ProcessWindowStyle.Hidden
                                        };
                                        Process.Start(xx)?.WaitForExit();
                                        Thread.Sleep(1);
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
                            try
                            {
                                var fs1 = new FileStream(_problem.DataSets[_cur].OutputFile, FileMode.Open,
                                    FileAccess.Read,
                                    FileShare.ReadWrite);
                                var fs2 = new FileStream(
                                    _problem.InputFileName != "stdin"
                                        ? _workingdir + "\\" + _problem.OutputFileName
                                        : _workingdir + "\\" + _problem.OutputFileName + ".htmp",
                                    FileMode.OpenOrCreate,
                                    FileAccess.Read,
                                    FileShare.ReadWrite);
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
                                sr1.Dispose();
                                sr2.Close();
                                sr2.Dispose();
                                fs1.Close();
                                fs1.Dispose();
                                fs2.Close();
                                fs2.Dispose();
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
            Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测 #{JudgeResult.JudgeId} 数据点 {_problem.DataSets.Length}/{_problem.DataSets.Length} 完毕，结果：{JudgeResult.Result[_problem.DataSets.Length - 1]}", textBlock);
        }

        private void Exithandler(object sender, EventArgs e)
        {
            if (sender is Process process)
            {
                try
                {
                    JudgeResult.Exitcode[_cur] = process.ExitCode;
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
                    process.Kill();
                }
                catch
                {
                    // ignored
                }
                _isexited = true;
            }
        }

        private void Killwerfault()
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
                            item.Close();
                        }
                }
                catch
                {
                    // ignored
                }
                Thread.Sleep(10);
            }
        }

        private static string Dn(string filename)
        {
            return "\"" + filename + "\"";
        }

        private bool Compile(string compiler)
        {
            try
            {
                var a = new ProcessStartInfo
                {
                    FileName = compiler,
                    ErrorDialog = false,
                    UseShellExecute = true,
                    Arguments = _problem.CompileCommand,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(a)?.WaitForExit();
                //Thread.Sleep(10);
                return File.Exists(_workingdir + "\\test_hjudge.exe");
            }
            catch
            {
                return false;
            }
        }
    }
}