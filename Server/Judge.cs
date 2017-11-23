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
        private bool _isExited;
        private readonly bool _isFinished;

        private bool _isFault;

        public Judge(int problemId, int userId, string code, string type, bool isStdIO, string description, string defaultTime, int competitionId)
        {
            while (true)
            {
                if (Connection.CurJudgingCnt < (Configuration.Configurations.MutiThreading == 0
                        ? Configuration.ProcessorCount + Connection.IntelligentAdditionWorkingThread
                        : Configuration.Configurations.MutiThreading))
                {
                    lock (Connection.JudgeListCntLock)
                        Connection.CurJudgingCnt++;
                    break;
                }
                Thread.Sleep(100);
            }
            Connection.CanPostJudgTask = true;
            _isFinished = false;
            try
            {
                _problem = Connection.GetProblem(problemId);
                var id = Guid.NewGuid().ToString().Replace("-", string.Empty);

                JudgeResult.JudgeId = Connection.NewJudge(description);
                if (defaultTime == null)
                    JudgeResult.JudgeDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                else JudgeResult.JudgeDate = defaultTime;
                JudgeResult.ProblemId = _problem.ProblemId;
                JudgeResult.Code = code;
                JudgeResult.UserId = userId;
                JudgeResult.Exitcode = new int[_problem.DataSets.Length];
                JudgeResult.Result = new string[_problem.DataSets.Length];
                JudgeResult.Score = new float[_problem.DataSets.Length];
                JudgeResult.Timeused = new long[_problem.DataSets.Length];
                JudgeResult.Memoryused = new long[_problem.DataSets.Length];
                JudgeResult.Type = type;
                JudgeResult.Description = description;
                JudgeResult.CompetitionId = competitionId;
                if (competitionId != 0)
                {
                    var comp = Connection.GetCompetition(competitionId);
                    var judgeLogs = Connection.QueryJudgeLogBelongsToCompetition(competitionId, userId);
                    if ((comp.Option & 1) != 0 && comp.SubmitLimit != 0)
                    {
                        if (judgeLogs.Where(i => i.ProblemId == problemId).Count() >= comp.SubmitLimit)
                        {
                            _isFinished = true;
                            lock (Connection.JudgeListCntLock) Connection.CurJudgingCnt--;
                            return;
                        }
                    }
                }

                var textBlock = Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 准备评测 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}");

                if (Configuration.Configurations.MutiThreading == 0)
                {
                    while (true)
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
                                if (cpuCounter.NextValue() <= 75 &&
                                    ramCounter.NextValue() > maxMemoryNeeded + 262144)
                                {
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            //ignored
                        }
                        Thread.Sleep(100);
                    }
                }

                new Thread(Killwerfault).Start();
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

                    lock (Connection.JudgeListCntLock) Connection.CurJudgingCnt--;
                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummery}", textBlock);
                    _isFinished = true;
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

                    lock (Connection.JudgeListCntLock) Connection.CurJudgingCnt--;
                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummery}", textBlock);
                    _isFinished = true;
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

                _problem.InputFileName = isStdIO ? "stdin" : GetRealString(_problem.InputFileName, 0, extList[0]);
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
            _isFinished = true;
            lock (Connection.JudgeListCntLock) Connection.CurJudgingCnt--;
        }

        public static string GetEngName(string origin)
        {
            var re = new Regex("[A-Z]|[a-z]|[0-9]");
            return re.Matches(origin).Cast<object>().Aggregate(string.Empty, (current, t) => current + t);
        }

        private string GetRealString(string origin, int cur, string extName)
        {
            return origin.Replace("${woringdir}", _workingdir)
                .Replace("${datadir}", AppDomain.CurrentDomain.BaseDirectory + "\\Data")
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
                    for (var i = 0; i < JudgeResult.Result.Length; i++)
                    {
                        JudgeResult.Result[i] = "Compile Error";
                        JudgeResult.Exitcode[i] = 0;
                        JudgeResult.Score[i] = 0;
                        JudgeResult.Timeused[i] = 0;
                        JudgeResult.Memoryused[i] = 0;
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
                        for (var i = 0; i < JudgeResult.Result.Length; i++)
                        {
                            JudgeResult.Result[i] = "Runtime Error";
                            JudgeResult.Exitcode[i] = 0;
                            JudgeResult.Score[i] = 0;
                            JudgeResult.Timeused[i] = 0;
                            JudgeResult.Memoryused[i] = 0;
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
                for (var i = 0; i < JudgeResult.Result.Length; i++)
                {
                    JudgeResult.Result[i] = $"Unknown Error: {ex.Message}";
                    JudgeResult.Exitcode[i] = 0;
                    JudgeResult.Score[i] = 0;
                    JudgeResult.Timeused[i] = 0;
                    JudgeResult.Memoryused[i] = 0;
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
            var cur = -1;
            var noRespondingTime = new int[_problem.DataSets.Length];
            var noRespondingState = false;
            while (cur < _problem.DataSets.Length - 1)
            {
                cur++;
                if (cur != 0)
                {
                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测 #{JudgeResult.JudgeId} 数据点 {cur}/{_problem.DataSets.Length} 完毕，结果：{JudgeResult.Result[cur - 1]}", textBlock);
                }
                if (!File.Exists(_problem.DataSets[cur].InputFile) || !File.Exists(_problem.DataSets[cur].OutputFile))
                {
                    JudgeResult.Result[cur] = "Problem Configuration Error";
                    JudgeResult.Exitcode[cur] = 0;
                    JudgeResult.Score[cur] = 0;
                    JudgeResult.Timeused[cur] = 0;
                    JudgeResult.Memoryused[cur] = 0;
                }
                else
                {
                    if (_problem.InputFileName != "stdin")
                    {
                        try
                        {
                            File.Copy(_problem.DataSets[cur].InputFile, _workingdir + "\\" + _problem.InputFileName,
                                true);
                        }
                        catch
                        {
                            Thread.Sleep(2000);
                            try
                            {
                                File.Copy(_problem.DataSets[cur].InputFile, _workingdir + "\\" + _problem.InputFileName,
                                    true);
                            }
                            catch
                            {
                                Thread.Sleep(3000);
                                try
                                {
                                    File.Copy(_problem.DataSets[cur].InputFile, _workingdir + "\\" + _problem.InputFileName,
                                        true);
                                }
                                catch (Exception ex)
                                {
                                    JudgeResult.Result[cur] = $"Unknown Error: {ex.Message}";
                                    JudgeResult.Exitcode[cur] = 0;
                                    JudgeResult.Score[cur] = 0;
                                    JudgeResult.Timeused[cur] = 0;
                                    JudgeResult.Memoryused[cur] = 0;
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
                    execute.Exited += (sender, e) => _isExited = true;
                    Task<string> res = null;
                    _isFault = false;
                    _isExited = false;
                    try
                    {
                        execute.Start();
                    }
                    catch (Exception ex)
                    {
                        JudgeResult.Result[cur] = $"Unknown Error: {ex.Message}";
                        JudgeResult.Exitcode[cur] = 0;
                        JudgeResult.Score[cur] = 0;
                        JudgeResult.Timeused[cur] = 0;
                        JudgeResult.Memoryused[cur] = 0;
                        continue;
                    }
                    Thread.Sleep(1);
                    var inputStream = execute.StandardInput;
                    var outputStream = execute.StandardOutput;
                    if (_problem.InputFileName == "stdin")
                    {
                        //Thread.Sleep(1);
                        res = outputStream.ReadToEndAsync();
                        inputStream.AutoFlush = true;
                        try
                        {
                            inputStream.WriteAsync(
                                File.ReadAllText(_problem.DataSets[cur].InputFile, Encoding.Default) + "\0").
                                GetAwaiter().OnCompleted(() =>
                                {
                                    inputStream.Close();
                                    inputStream.Dispose();
                                });
                        }
                        catch
                        {
                            //ignored
                        }
                    }
                    else
                    {
                        try
                        {
                            inputStream.Write("\0");
                            inputStream.Close();
                            inputStream.Dispose();
                        }
                        catch
                        {
                            //ignored
                        }
                    }
                    long lastDt = 0;
                    var noChangeTime = DateTime.Now;
                    var isNoResponding = false;
                    while (!_isExited)
                    {
                        try
                        {
                            execute?.Refresh();
                            JudgeResult.Timeused[cur] =
                                Convert.ToInt64(execute.UserProcessorTime.TotalMilliseconds);
                            JudgeResult.Memoryused[cur] = execute.PeakWorkingSet64 / 1024;
                            if (lastDt == Convert.ToInt64(execute.TotalProcessorTime.TotalMilliseconds))
                            {
                                if ((DateTime.Now - noChangeTime).TotalMilliseconds > _problem.DataSets[cur].TimeLimit * (Connection.CurJudgingCnt - Connection.IntelligentAdditionWorkingThread) * 60)
                                {
                                    _isExited = true;
                                    isNoResponding = true;
                                    break;
                                }
                            }
                            else
                            {
                                noChangeTime = DateTime.Now;
                                lastDt = Convert.ToInt64(execute.TotalProcessorTime.TotalMilliseconds);
                            }
                        }
                        catch
                        {
                            //ignored
                        }
                        if (JudgeResult.Timeused[cur] > _problem.DataSets[cur].TimeLimit)
                        {
                            _isFault = true;
                            try
                            {
                                execute?.Kill();
                            }
                            catch
                            {
                                //ignored 
                            }
                            _isExited = true;
                            JudgeResult.Result[cur] = "Time Limit Exceeded";
                            JudgeResult.Score[cur] = 0;
                            JudgeResult.Exitcode[cur] = 0;
                        }
                        if (JudgeResult.Memoryused[cur] > _problem.DataSets[cur].MemoryLimit)
                        {
                            _isFault = true;
                            try
                            {
                                execute?.Kill();
                            }
                            catch
                            {
                                //ignored 
                            }
                            _isExited = true;
                            JudgeResult.Result[cur] = "Memory Limit Exceeded";
                            JudgeResult.Score[cur] = 0;
                            JudgeResult.Exitcode[cur] = 0;
                        }
                        try
                        {
                            execute?.CloseMainWindow();
                        }
                        catch
                        {
                            //ignored
                        }
                        Thread.Sleep(1);
                    }
                    try
                    {
                        execute?.Kill();
                    }
                    catch
                    {
                        //ignored
                    }
                    try
                    {
                        JudgeResult.Exitcode[cur] = execute?.ExitCode ?? 0;
                        if (JudgeResult.Exitcode[cur] != 0)
                        {
                            JudgeResult.Result[cur] = "Runtime Error";
                            JudgeResult.Score[cur] = 0;
                            _isFault = true;
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                    if (isNoResponding)
                    {
                        JudgeResult.Result[cur] = "Unknown Error";
                        JudgeResult.Score[cur] = 0;
                        noRespondingTime[cur]++;
                        if (!noRespondingState)
                        {
                            lock (Connection.AdditionWorkingThreadLock)
                                Connection.IntelligentAdditionWorkingThread++;
                            noRespondingState = true;
                        }
                        try
                        {
                            execute?.Close();
                            execute?.Dispose();
                        }
                        catch
                        {
                            //ignored
                        }
                        if (noRespondingTime[cur] < 3) cur--;
                        continue;
                    }
                    if (_isFault)
                    {
                        try
                        {
                            execute?.Close();
                            execute?.Dispose();
                        }
                        catch
                        {
                            //ignored
                        }
                        continue;
                    }
                    Thread.Sleep(1);
                    if (_problem.InputFileName != "stdin")
                    {
                        if (!File.Exists(_workingdir + "\\" + _problem.OutputFileName))
                        {
                            JudgeResult.Result[cur] = "Output File Error";
                            JudgeResult.Score[cur] = 0;
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
                        execute?.Dispose();
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
                                            Arguments = Dn(_problem.DataSets[cur].InputFile) + " " +
                                                        Dn(_problem.DataSets[cur].OutputFile) + " " +
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
                                            JudgeResult.Result[cur] = "Special Judge Error";
                                            JudgeResult.Score[cur] = 0;
                                        }
                                        else
                                        {
                                            var p = File.ReadAllText(_workingdir + "\\hjudge_spj_result.dat");
                                            p = Regex.Replace(p, @"\s", string.Empty);
                                            var gs = Convert.ToSingle(p);
                                            JudgeResult.Score[cur] = _problem.DataSets[cur].Score * gs;
                                            if (Math.Abs(gs - 1) > 0.000001)
                                                JudgeResult.Result[cur] = "Wrong Answer";
                                            else
                                                JudgeResult.Result[cur] = "Correct";
                                        }
                                    }
                                    catch
                                    {
                                        JudgeResult.Result[cur] = "Special Judge Error";
                                        JudgeResult.Score[cur] = 0;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    JudgeResult.Result[cur] = $"Unknown Error: {ex.Message}";
                                    JudgeResult.Score[cur] = 0;
                                }
                            }
                            else
                            {
                                JudgeResult.Result[cur] = "Special Judge Error";
                                JudgeResult.Score[cur] = 0;
                            }
                        }
                        else
                        {
                            try
                            {
                                var fs1 = new FileStream(_problem.DataSets[cur].OutputFile, FileMode.Open,
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
                                    JudgeResult.Result[cur] = "Wrong Answer";
                                    JudgeResult.Score[cur] = 0;
                                    if (Regex.Replace(s1, @"\s", string.Empty) ==
                                        Regex.Replace(s2, @"\s", string.Empty))
                                        JudgeResult.Result[cur] = "Presentation Error";
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
                                JudgeResult.Result[cur] = "Correct";
                                JudgeResult.Score[cur] = _problem.DataSets[cur].Score;
                            }
                            catch (Exception ex)
                            {
                                JudgeResult.Result[cur] = $"Unknown Error: {ex.Message}";
                                JudgeResult.Score[cur] = 0;
                            }
                        }
                    }
                }
                Thread.Sleep(1);
            }
            if (noRespondingState)
            {
                lock (Connection.AdditionWorkingThreadLock)
                    Connection.IntelligentAdditionWorkingThread--;
            }
            Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测 #{JudgeResult.JudgeId} 数据点 {_problem.DataSets.Length}/{_problem.DataSets.Length} 完毕，结果：{JudgeResult.Result[_problem.DataSets.Length - 1]}", textBlock);
        }

        private void Killwerfault()
        {
            while (!_isFinished)
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
                Thread.Sleep(1);
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
                Thread.Sleep(1);
                return File.Exists(_workingdir + "\\test_hjudge.exe");
            }
            catch
            {
                return false;
            }
        }
    }
}