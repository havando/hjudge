﻿using System;
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
        private readonly string _id;
        private readonly Problem _problem;
        private readonly string _workingdir;
        public readonly bool Cancelled;
        public int DeltaCoins, DeltaExperience;

        public readonly JudgeInfo JudgeResult = new JudgeInfo();
        private bool _isExited;

        private bool _isFault;

        public Judge(int problemId, int userId, string code, string type, bool isStdIO, string description,
            string defaultTime, int competitionId, Action<int> idCallBack = null)
        {
            Cancelled = false;
            if (competitionId != 0)
            {
                var comp = Connection.GetCompetition(competitionId);
                var judgeLogs = Connection.QueryJudgeLogBelongsToCompetition(competitionId, userId);
                if ((comp.Option & 1) != 0 && comp.SubmitLimit != 0)
                    if (judgeLogs.Count(i => i.ProblemId == problemId) >= comp.SubmitLimit)
                    {
                        Connection.CanPostJudgTask = true;
                        Cancelled = true;
                        return;
                    }
            }

            JudgeResult.JudgeId = Connection.NewJudge(description);
            idCallBack?.Invoke(JudgeResult.JudgeId);
            _problem = Connection.GetProblem(problemId);
            _id = Guid.NewGuid().ToString().Replace("-", string.Empty);
            JudgeResult.JudgeDate = defaultTime ?? DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
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
            JudgeResult.AdditionInfo = string.Empty;
            Connection.UpdateJudgeInfo(JudgeResult);

            var textBlock = Connection.UpdateMainPageState(
                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 准备评测 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}");

            while (true)
            {
                if (Connection.CurJudgingCnt < (Configuration.Configurations.MutiThreading == 0
                        ? Configuration.ProcessorCount + Connection.IntelligentAdditionWorkingThread
                        : Configuration.Configurations.MutiThreading))
                {
                    lock (Connection.JudgeListCntLock)
                    {
                        Connection.CurJudgingCnt++;
                    }
                    break;
                }
                Thread.Sleep(100);
            }
            Connection.CanPostJudgTask = true;

            JudgeResult.JudgeDate = defaultTime ?? DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
            try
            {

                if (Configuration.Configurations.MutiThreading == 0)
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
                                if (cpuCounter.NextValue() <= 70 &&
                                    ramCounter.NextValue() > maxMemoryNeeded + 262144)
                                    break;
                            }
                        }
                        catch
                        {
                            //ignored
                        }
                        Thread.Sleep(1000);
                    }

                Connection.UpdateJudgeInfo(JudgeResult);
                _workingdir = Environment.GetEnvironmentVariable("temp") + "\\hjudgeWorkingDir\\Judge_hjudge_" + _id;

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

                    lock (Connection.JudgeListCntLock)
                    {
                        Connection.CurJudgingCnt--;
                    }
                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummary}",
                        textBlock);
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

                    lock (Connection.JudgeListCntLock)
                    {
                        Connection.CurJudgingCnt--;
                    }
                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummary}",
                        textBlock);
                    return;
                }

                if (string.IsNullOrEmpty(_problem.CompileCommand))
                {
                    _problem.CompileCommand = t.LinuxComArgs
                        ? GetRealStringWSL(t.CompilerArgs, 0, extList[0])
                        : GetRealString(t.CompilerArgs, 0, extList[0]);
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
                            _problem.CompileCommand = t.LinuxComArgs
                                ? GetRealStringWSL(comm[1], 0, extList[0])
                                : GetRealString(comm[1], 0, extList[0]);
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
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 开始评测 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}",
                    textBlock);

                BeginJudge(
                    t.LinuxComExec
                        ? GetRealStringWSL(t.CompilerExec, 0, extList[0])
                        : GetRealString(t.CompilerExec, 0, extList[0]),
                    t.LinuxStaExec
                        ? GetRealStringWSL(t.StaticCheck, 0, extList[0])
                        : GetRealString(t.StaticCheck, 0, extList[0]),
                    t.LinuxStaArgs
                        ? GetRealStringWSL(t.StaticArgs, 0, extList[0])
                        : GetRealString(t.StaticArgs, 0, extList[0]),
                    t.LinuxRunExec
                        ? GetRealStringWSL(t.RunExec, 0, extList[0])
                        : GetRealString(t.RunExec, 0, extList[0]),
                    t.LinuxRunArgs
                        ? GetRealStringWSL(t.RunArgs, 0, extList[0])
                        : GetRealString(t.RunArgs, 0, extList[0]), textBlock);

                Connection.UpdateJudgeInfo(JudgeResult);

                Connection.UpdateMainPageState(
                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测完毕 #{JudgeResult.JudgeId}，题目：{JudgeResult.ProblemName}，用户：{JudgeResult.UserName}，结果：{JudgeResult.ResultSummary}",
                    textBlock);

                if (userId > 1)
                {
                    if (JudgeResult.ResultSummary.Contains("Exceeded"))
                    {
                        DeltaCoins = Connection.RandomNum.Next(4, 16);
                        DeltaExperience = Connection.RandomNum.Next(8, 16);
                    }
                    else if (JudgeResult.ResultSummary.Contains("Accepted"))
                    {
                        DeltaCoins = Connection.RandomNum.Next(16, 64);
                        DeltaExperience = Connection.RandomNum.Next(32, 64);
                    }
                    else
                    {
                        DeltaExperience = Connection.RandomNum.Next(2, 4);
                    }

                    Connection.UpdateCoins(userId, DeltaCoins);
                    Connection.UpdateExperience(userId, DeltaExperience);
                }
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
                .Replace("${datadir}", AppDomain.CurrentDomain.BaseDirectory + "\\Data")
                .Replace("${name}", GetEngName(_problem.ProblemName))
                .Replace("${index0}", cur.ToString())
                .Replace("${index}", (cur + 1).ToString())
                .Replace("${file}", _workingdir + $"\\test{extName}")
                .Replace("${targetfile}", _workingdir + $"\\test_hjudge_{_id}.exe");
        }

        private string GetFileNameWSL(string fileName)
        {
            return "/mnt/" + (fileName.Substring(0, 1).ToLower() + fileName.Substring(1)).Replace('\\', '/')
                   .Replace(":", string.Empty);
        }

        private string GetRealStringWSL(string origin, int cur, string extName)
        {
            return origin.Replace("${woringdir}", GetFileNameWSL(_workingdir))
                .Replace("${datadir}", GetFileNameWSL(AppDomain.CurrentDomain.BaseDirectory + "\\Data"))
                .Replace("${name}", GetEngName(_problem.ProblemName))
                .Replace("${index0}", cur.ToString())
                .Replace("${index}", (cur + 1).ToString())
                .Replace("${file}", GetFileNameWSL(_workingdir + $"\\test{extName}"))
                .Replace("${targetfile}", GetFileNameWSL(_workingdir + $"\\test_hjudge_{_id}.exe"));
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

        private void BeginJudge(string compiler, string staticCheck, string staticArgs, string runExec, string runArgs, UIElement textBlock)
        {
            var additionInfo = string.Empty;
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
                if (!string.IsNullOrEmpty(staticCheck))
                {
                    var a = new Process
                    {
                        StartInfo =
                        {
                            FileName = staticCheck,
                            Arguments = staticArgs,
                            ErrorDialog = false,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        }
                    };
                    a.Start();
                    var staticRes1 = a.StandardOutput.ReadToEndAsync();
                    var staticRes2 = a.StandardError.ReadToEndAsync();
                    if (!a.WaitForExit(1000 * 60))
                    {
                        try
                        {
                            a.Kill();
                        }
                        finally
                        {
                            a.Close();
                        }
                    }
                    additionInfo += "静态检查：\n" + staticRes1.Result.Replace(_workingdir, "...").Replace(GetFileNameWSL(_workingdir), "...") + "\n" + staticRes2.Result.Replace(_workingdir, "...").Replace(GetFileNameWSL(_workingdir), "...") + "\n";
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
            var (isSucceeded, compileLog) = Compile(compiler);
            additionInfo += compileLog;
            JudgeResult.AdditionInfo = additionInfo;
            if (isSucceeded)
                Judging(textBlock, runExec, runArgs);
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

        private void Judging(UIElement textBlock, string runExec, string runArgs)
        {
            var cur = -1;
            var noRespondingState = false;
            var noRespondingTime = new int[_problem.DataSets.Length];
            var failToCatchProcessTime = new int[_problem.DataSets.Length];
            while (cur < _problem.DataSets.Length - 1)
            {
                var failToCatchProcess = false;
                cur++;
                if (cur != 0)
                    Connection.UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 评测 #{JudgeResult.JudgeId} 数据点 {cur}/{_problem.DataSets.Length} 完毕，结果：{JudgeResult.Result[cur - 1]}",
                        textBlock);
                if ((!string.IsNullOrWhiteSpace(_problem.DataSets[cur].InputFile) && !File.Exists(_problem.DataSets[cur].InputFile)) || !File.Exists(_problem.DataSets[cur].OutputFile))
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
                        if (!string.IsNullOrWhiteSpace(_problem.DataSets[cur].InputFile))
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
                                        File.Copy(_problem.DataSets[cur].InputFile,
                                            _workingdir + "\\" + _problem.InputFileName,
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

                    void exit(object sender, EventArgs args)
                    {
                        _isExited = true;
                    }

                    var testId = $"test_hjudge_{_id}";
                    var execute = new Process
                    {
                        StartInfo =
                        {
                            FileName = string.IsNullOrEmpty(runExec)
                                ? _workingdir + $"\\test_hjudge_{_id}.exe"
                                : runExec,
                            Arguments = string.IsNullOrEmpty(runArgs) ? string.Empty : runArgs,
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

                    execute.Exited += exit;
                    Task<string> res = null;
                    _isFault = false;
                    _isExited = false;
                    var startCatch = DateTime.Now;
                    JudgeHelper.Subscribe(testId);
                    long lastDt = 0;
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
                        JudgeHelper.Desubscribe(testId);
                        continue;
                    }
                    var inputStream = execute.StandardInput;
                    var outputStream = execute.StandardOutput;
                    while (JudgeHelper.Processes[testId] == null)
                    {
                        if (_isExited || (DateTime.Now - startCatch).TotalSeconds > 10)
                        {
                            failToCatchProcess = true;
                            break;
                        }
                        Thread.Sleep(1);
                    }
                    if (failToCatchProcess)
                    {
                        JudgeHelper.Desubscribe(testId);
                        JudgeResult.Result[cur] = "Unknown Error";
                        JudgeResult.Score[cur] = 0;
                        failToCatchProcessTime[cur]++;
                        try
                        {
                            execute.Kill();
                        }
                        catch
                        {
                            //ignored
                        }
                        execute.Close();
                        if (failToCatchProcessTime[cur] < 3) cur--;
                        continue;
                    }

                    var process = JudgeHelper.Processes[testId];
                    var noChangeTime = DateTime.Now; //Prevent from process suspending / IO block causing no response

                    JudgeHelper.Desubscribe(testId);

                    try
                    {
                        JudgeResult.Timeused[cur] = Math.Max(JudgeResult.Timeused[cur], Convert.ToInt64(process.UserProcessorTime.TotalMilliseconds));
                        JudgeResult.Memoryused[cur] = Math.Max(JudgeResult.Memoryused[cur], process.PeakWorkingSet64 >> 10);
                    }
                    catch
                    {
                        //ignored
                    }
                    var resume = false;

                    var isNoResponding = false;
                    do
                    {
                        var taskCount = 0;
                        try
                        {
                            while (!_isExited)
                            {
                                taskCount = 0;
                                if (!resume)
                                {
                                    resume = true;

                                    new Thread(() =>
                                    {
                                        if (_problem.InputFileName == "stdin")
                                            try
                                            {
                                                res = outputStream.ReadToEndAsync();
                                                inputStream.AutoFlush = true;
                                                if (!string.IsNullOrWhiteSpace(_problem.DataSets[cur].InputFile))
                                                    lock (Connection.StdinWriterLock)
                                                        inputStream.WriteAsync(
                                                                File.ReadAllText(_problem.DataSets[cur].InputFile, Encoding.Default) + "\0")
                                                            .ContinueWith(o =>
                                                            {
                                                                inputStream.Close();
                                                            });
                                                else
                                                {
                                                    inputStream.WriteAsync("\0")
                                                        .ContinueWith(o =>
                                                        {
                                                            inputStream.Close();
                                                        });
                                                }
                                            }
                                            catch
                                            {
                                                //ignored
                                            }
                                        else
                                            try
                                            {
                                                inputStream.Write("\0");
                                                inputStream.Close();
                                            }
                                            catch
                                            {
                                                //ignored
                                            }
                                    }).Start();

                                    JudgeHelper.ResumeProcess(process.Id);
                                }
                                JudgeResult.Timeused[cur] = Math.Max(JudgeResult.Timeused[cur],
                                    Convert.ToInt64(process.UserProcessorTime.TotalMilliseconds));
                                if (JudgeResult.Timeused[cur] > _problem.DataSets[cur].TimeLimit)
                                {
                                    _isFault = true;
                                    _isExited = true;
                                    JudgeResult.Result[cur] = "Time Limit Exceeded";
                                    JudgeResult.Score[cur] = 0;
                                    JudgeResult.Exitcode[cur] = 0;
                                }

                                taskCount++;
                                JudgeResult.Memoryused[cur] = Math.Max(JudgeResult.Memoryused[cur],
                                    process.PeakWorkingSet64 >> 10);
                                if (JudgeResult.Memoryused[cur] > _problem.DataSets[cur].MemoryLimit)
                                {
                                    _isFault = true;
                                    _isExited = true;
                                    JudgeResult.Result[cur] = "Memory Limit Exceeded";
                                    JudgeResult.Score[cur] = 0;
                                    JudgeResult.Exitcode[cur] = 0;
                                }

                                taskCount++;

                                if (lastDt == Convert.ToInt64(process.TotalProcessorTime.TotalMilliseconds))
                                {
                                    if ((DateTime.Now - noChangeTime).TotalMilliseconds >
                                        _problem.DataSets[cur].TimeLimit *
                                        (Connection.CurJudgingCnt - Connection.IntelligentAdditionWorkingThread) * 10)
                                    {
                                        _isExited = true;
                                        isNoResponding = true;
                                        break;
                                    }

                                    try
                                    {
                                        process.CloseMainWindow();
                                    }
                                    catch
                                    {
                                        //ignored
                                    }
                                }
                                else
                                {
                                    noChangeTime = DateTime.Now;
                                    lastDt = Convert.ToInt64(process.TotalProcessorTime.TotalMilliseconds);
                                }

                                process.Refresh();
                            }
                        }
                        catch
                        {
                            if (!_isExited)
                            {
                                if (taskCount != 2)
                                {
                                    process.Refresh();
                                }
                            }
                        }
                    } while (!_isExited);

                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        //ignored
                    }
                    try
                    {
                        execute.Kill();
                    }
                    catch
                    {
                        //ignored
                    }
                    try
                    {
                        JudgeResult.Exitcode[cur] = execute.ExitCode;
                        if (JudgeResult.Exitcode[cur] == 0)
                            JudgeResult.Exitcode[cur] = process.ExitCode;
                        if (JudgeResult.Exitcode[cur] != 0 && !_isFault)
                        {
                            JudgeResult.Result[cur] = "Runtime Error";
                            JudgeResult.Score[cur] = 0;
                            _isFault = true;
                            isNoResponding = false;
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                    JudgeHelper.TerminateProcess(process.Id);
                    if (isNoResponding)
                    {
                        JudgeResult.Result[cur] = "Time Limit Exceeded";
                        JudgeResult.Score[cur] = 0;
                        noRespondingTime[cur]++;
                        if (!noRespondingState)
                        {
                            lock (Connection.AdditionWorkingThreadLock)
                            {
                                Connection.IntelligentAdditionWorkingThread++;
                            }
                            noRespondingState = true;
                        }
                        process.Close();
                        execute.Close();
                        if (noRespondingTime[cur] < 3) cur--;
                        continue;
                    }
                    if (_isFault)
                    {
                        process.Close();
                        execute.Close();
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
                        File.WriteAllText(_workingdir + "\\" + _problem.OutputFileName + ".htmp",
                            res?.Result ?? string.Empty, Encoding.Default);
                    }
                    outputStream.Close();
                    process.Close();
                    execute.Close();
                    Thread.Sleep(1);
                    lock (Connection.ComparingLock)
                    {
                        if (!string.IsNullOrEmpty(_problem.SpecialJudge))
                            if (File.Exists(_problem.SpecialJudge))
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
                                    var pcs = new Process { StartInfo = xx };
                                    pcs.Start();
                                    if (!pcs.WaitForExit(1000 * 60))
                                    {
                                        try
                                        {
                                            pcs.Kill();
                                        }
                                        catch
                                        {
                                            pcs.Close();
                                        }
                                    }
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
                                        if (Math.Abs(gs - 1) > 0.00001F)
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
                            else
                            {
                                JudgeResult.Result[cur] = "Special Judge Error";
                                JudgeResult.Score[cur] = 0;
                            }
                        else
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
                                sr2.Close();
                                fs1.Close();
                                fs2.Close();
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
                Thread.Sleep(1);
            }
            if (noRespondingState)
                lock (Connection.AdditionWorkingThreadLock)
                {
                    Connection.IntelligentAdditionWorkingThread--;
                }
        }

        private static string Dn(string filename)
        {
            return "\"" + filename + "\"";
        }

        private (bool isSucceeded, string compileLog) Compile(string compiler)
        {
            try
            {
                var a = new ProcessStartInfo
                {
                    FileName = compiler,
                    ErrorDialog = false,
                    UseShellExecute = false,
                    Arguments = _problem.CompileCommand,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                var b = new Process { StartInfo = a };
                b.Start();
                var stdErr = b.StandardError.ReadToEndAsync();
                var stdOut = b.StandardOutput.ReadToEndAsync();
                if (!b.WaitForExit(1000 * 60))
                {
                    try
                    {
                        b.Kill();
                    }
                    finally
                    {
                        b.Close();
                    }
                }
                var log = "编译日志：\n" + stdOut.Result + "\n" + stdErr.Result;
                log = log.Replace(_workingdir, "...").Replace(GetFileNameWSL(_workingdir), "...");
                Thread.Sleep(1);
                return (File.Exists(_workingdir + $"\\test_hjudge_{_id}.exe"), log);
            }
            catch
            {
                return (false, string.Empty);
            }
        }
    }
}