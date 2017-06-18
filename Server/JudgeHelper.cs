﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public class JudgeInfo
    {
        public int JudgeId { get; set; }
        public int UserId { get; set; }
        public int ProblemId { get; set; }
        public string JudgeDate { get; set; }
        public string Code { get; set; }
        public string[] Result { get; set; }
        public float[] Score { get; set; }
        public long[] Timeused { get; set; }
        public long[] Memoryused { get; set; }
        public int[] Exitcode { get; set; }
        public string UserName => Connection.GetUserName(UserId);
        public string ProblemName => Connection.GetProblemName(ProblemId);
        public string ResultSummery
        {
            get
            {
                var error = new int[11];
                var tot = 0;
                foreach (var t in Result)
                {
                    switch (t)
                    {
                        case "Correct": error[0]++; tot++; break;
                        case "Problem Configuration Error": error[1]++; tot++; break;
                        case "Compile Error": error[2]++; tot++; break;
                        case "Wrong Answer": error[3]++; tot++; break;
                        case "Presentation Error": error[4]++; tot++; break;
                        case "Runtime Error": error[5]++; tot++; break;
                        case "Time Limit Excceed": error[6]++; tot++; break;
                        case "Memory Limit Excceed": error[7]++; tot++; break;
                        case "Output File Error": error[8]++; tot++; break;
                        case "Special Judger Error": error[9]++; tot++; break;
                        case "Unknown Error": error[10]++; tot++; break;
                    }
                }
                if (tot == error[0]) { return "Accept"; }
                var max = error[1];
                var j = 1;
                for (var i = 1; i < 11; i++)
                {
                    if (error[i] > max)
                    {
                        j = i;
                    }
                }
                switch (j)
                {
                    case 1: return "Problem Configuration Error";
                    case 2: return "Compile Error";
                    case 3: return "Wrong Answer";
                    case 4: return "Presentation Error";
                    case 5: return "Runtime Error";
                    case 6: return "Time Limit Excceed";
                    case 7: return "Memory Limit Excceed";
                    case 8: return "Output File Error";
                    case 9: return "Special Judger Error";
                    case 10: return "Unknown Error";
                }
                return "Unknown Error";
            }
        }
    }

    public class Judge
    {
        private readonly string _workingdir;
        private readonly Problem _problem;

        private bool _isfault;
        private bool _isexited;
        private int _cur;
        private Process _excute;

        public readonly JudgeInfo JudgeResult = new JudgeInfo();

        public static string GetEngName(string origin)
        {
            var re = new Regex("[A-Z]|[a-z]|[0-9]");
            return re.Matches(origin).Cast<object>().Aggregate("", (current, t) => current + t);
        }

        private string GetRealString(string origin)
        {
            return origin.Replace("${woringdir}", _workingdir)
                .Replace("${datadir}", Environment.CurrentDirectory + "\\Data")
                .Replace("${name}", GetEngName(_problem.ProblemName))
                .Replace("${index0}", _cur.ToString())
                .Replace("${index}", (_cur + 1).ToString())
                .Replace("${file}", _workingdir + "\\test.cpp")
                .Replace("${targetfile}", _workingdir + "\\test_hjudge.exe");
        }

        public Judge(int problemId, int userId, string code)
        {
            _problem = Connection.GetProblem(problemId);
            var datetime = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            JudgeResult.JudgeId = Connection.NewJudge();
            JudgeResult.JudgeDate = datetime;
            JudgeResult.ProblemId = _problem.ProblemId;
            JudgeResult.Code = code;
            JudgeResult.UserId = userId;
            JudgeResult.Exitcode = new int[_problem.DataSets.Length];
            JudgeResult.Result = new string[_problem.DataSets.Length];
            JudgeResult.Score = new float[_problem.DataSets.Length];
            JudgeResult.Timeused = new long[_problem.DataSets.Length];
            JudgeResult.Memoryused = new long[_problem.DataSets.Length];
            _workingdir = Environment.GetEnvironmentVariable("temp") + "\\Judge_hjudge_" + datetime;
            if (string.IsNullOrEmpty(_problem.CompileCommand))
            {
                _problem.CompileCommand = Dn(_workingdir + "\\test.cpp") + " -o " + Dn(_workingdir + "\\test_hjudge.exe");
            }
            else
            {
                _problem.CompileCommand = GetRealString(_problem.CompileCommand);
            }
            _problem.SpecialJudge = GetRealString(_problem.SpecialJudge);
            for (var i = 0; i < _problem.ExtraFiles.Length; i++)
            {
                _problem.ExtraFiles[i] = GetRealString(_problem.ExtraFiles[i]);
            }
            foreach (var t in _problem.DataSets)
            {
                t.InputFile = GetRealString(t.InputFile);
                t.OutputFile = GetRealString(t.OutputFile);
            }
            BeginJudge();
            Connection.UpdateJudgeInfo(JudgeResult);
            try
            {
                Directory.Delete(_workingdir);
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
                    {
                        continue;
                    }
                    File.Copy(t, _workingdir + "\\" + Path.GetFileName(t), true);
                }
            }
            catch
            {
                for (_cur = 0; _cur < JudgeResult.Result.Length; _cur++)
                {
                    JudgeResult.Result[_cur] = "Unkonwn Error";
                    JudgeResult.Exitcode[_cur] = 0;
                    JudgeResult.Score[_cur] = 0;
                    JudgeResult.Timeused[_cur] = 0;
                    JudgeResult.Memoryused[_cur] = 0;
                }
                return;
            }
            if (Compile())
            {
                Judging();
            }
            else
            {
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
                    try
                    {
                        File.Copy(_problem.DataSets[_cur].InputFile, _workingdir + "\\" + _problem.InputFileName, true);
                    }
                    catch
                    {
                        JudgeResult.Result[_cur] = "Problem Configuration Error";
                        JudgeResult.Exitcode[_cur] = 0;
                        JudgeResult.Score[_cur] = 0;
                        JudgeResult.Timeused[_cur] = 0;
                        JudgeResult.Memoryused[_cur] = 0;
                        continue;
                    }
                    _isfault = false;
                    _isexited = false;
                    Killwerfault();
                    _excute = new Process
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
                    _excute.Exited += Exithandler;
                    try
                    {
                        _excute.Start();
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
                        JudgeResult.Timeused[_cur] = _excute.TotalProcessorTime.Milliseconds;
                        JudgeResult.Memoryused[_cur] = _excute.PeakWorkingSet64 / 1024;
                        if (JudgeResult.Timeused[_cur] > _problem.DataSets[_cur].TimeLimit)
                        {
                            _isfault = true;
                            try
                            {
                                _excute.Kill();
                                _excute.Close();
                            }
                            catch
                            {
                                // ignored
                            }
                            JudgeResult.Result[_cur] = "Time Limit Excceed";
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Exitcode[_cur] = 0;
                        }
                        if (JudgeResult.Memoryused[_cur] > _problem.DataSets[_cur].MemoryLimit)
                        {
                            _isfault = true;
                            try
                            {
                                _excute.Kill();
                                _excute.Close();
                            }
                            catch
                            {
                                // ignored
                            }
                            JudgeResult.Result[_cur] = "Memory Limit Excceed";
                            JudgeResult.Score[_cur] = 0;
                            JudgeResult.Exitcode[_cur] = 0;
                        }
                        Thread.Sleep(10);
                    }
                    if (_isfault) continue;
                    if (!File.Exists(_workingdir + "\\" + _problem.OutputFileName))
                    {
                        JudgeResult.Result[_cur] = "Output File Error";
                        JudgeResult.Score[_cur] = 0;
                        continue;
                    }
                    if (string.IsNullOrEmpty(_problem.SpecialJudge))
                    {
                        if (File.Exists(_problem.SpecialJudge))
                        {
                            Thread.Sleep(10);
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
                                    p = Regex.Replace(p, @"\s", "");
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
                                JudgeResult.Result[_cur] = "Special Judger Error";
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
                        Thread.Sleep(10);
                        var fs1 = new FileStream(_problem.DataSets[_cur].OutputFile, FileMode.Open);
                        var fs2 = new FileStream(_workingdir + "\\" + _problem.OutputFileName, FileMode.Open);
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
                                s1 = "";
                            }
                            string s2;
                            try
                            {
                                s2 = sr2.ReadLine();
                            }
                            catch
                            {
                                s2 = "";
                            }
                            if (s1 == null) s1 = "";
                            if (s2 == null) s2 = "";
                            s1 = s1.TrimEnd(' ');
                            s2 = s2.TrimEnd(' ');
                            if (s1 == s2) continue;
                            iswrong = true;
                            JudgeResult.Result[_cur] = "Wrong Answer";
                            JudgeResult.Score[_cur] = 0;
                            if (Regex.Replace(s1, @"\s", "") == Regex.Replace(s2, @"\s", ""))
                            {
                                JudgeResult.Result[_cur] = "Presentation Error";
                            }
                            else break;
                        } while (!(sr1.EndOfStream && sr2.EndOfStream));
                        sr1.Close();
                        sr2.Close();
                        fs1.Close();
                        fs2.Close();
                        if (iswrong) continue;
                        JudgeResult.Result[_cur] = "Correct";
                        JudgeResult.Score[_cur] = _problem.DataSets[_cur].Score;
                    }
                }
            }
        }

        private void Exithandler(object sender, EventArgs e)
        {
            JudgeResult.Exitcode[_cur] = _excute.ExitCode;
            if (JudgeResult.Exitcode[_cur] != 0 && !_isfault)
            {
                JudgeResult.Result[_cur] = "Runtime Error";
                JudgeResult.Score[_cur] = 0;
                _isfault = true;
            }
            try
            {
                _excute.Kill();
                _excute.Close();
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
                while (!_isexited)
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
                return File.Exists(_workingdir + "\\test_hjudge.exe");
            }
            catch
            {
                return false;
            }
        }
    }
}
