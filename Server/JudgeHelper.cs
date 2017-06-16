using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
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
    }

    public class Judge
    {
        private readonly string _workingdir;
        private readonly string _command;
        private readonly Problem _problem;

        private bool _isfault;
        private bool _isexited;
        private int _cur;
        private Process _excute;

        private readonly JudgeInfo _judgeResult = new JudgeInfo();
        public Judge(int problemId, string code)
        {
            var toJudge = Connection.GetProblem(problemId);
            var datetime = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            _judgeResult.JudgeId = Connection.NewJudge();
            _judgeResult.JudgeDate = datetime;
            _judgeResult.ProblemId = toJudge.ProblemId;
            _judgeResult.Code = code;
            _judgeResult.Exitcode = new int[_problem.DataSets.Length];
            _judgeResult.Result = new string[_problem.DataSets.Length];
            _judgeResult.Score = new float[_problem.DataSets.Length];
            _judgeResult.Timeused = new long[_problem.DataSets.Length];
            _judgeResult.Memoryused = new long[_problem.DataSets.Length];
            _workingdir = Environment.GetEnvironmentVariable("temp") + "\\Judge_hjudge_" + datetime;
            _command = toJudge.CompileCommand;
            if (string.IsNullOrEmpty(_command))
            {
                _command = Dn(_workingdir + "\\test.cpp") + " -o " + Dn(_workingdir + "\\test_hjudge.exe");
            }
            else
            {
                _command = _command.Replace("${file}", Dn(_workingdir + "\\test.cpp"));
                _command = _command.Replace("${targetfile}", Dn(_workingdir + "\\test_hjudge.exe"));
                _command = _command.Replace("${_workingdir}", _workingdir);
            }
            _problem = toJudge;
            BeginJudge();
            Connection.UpdateJudgeInfo(_judgeResult);
        }

        private void BeginJudge()
        {
            try
            {
                Directory.CreateDirectory(_workingdir);
                File.WriteAllText(_workingdir + "\\test.cpp", _judgeResult.Code);
                foreach (var t in _problem.ExtraFiles)
                {
                    File.Copy(t, _workingdir + "\\" + Path.GetFileName(t), true);
                }
            }
            catch
            {
                for (_cur = 0; _cur < _judgeResult.Result.Length; _cur++)
                {
                    _judgeResult.Result[_cur] = "Unkonwn Error";
                    _judgeResult.Exitcode[_cur] = 0;
                    _judgeResult.Score[_cur] = 0;
                    _judgeResult.Timeused[_cur] = 0;
                    _judgeResult.Memoryused[_cur] = 0;
                }
                return;
            }
            if (Compile(_workingdir + "\\test.cpp"))
            {
                Judging();
            }
            else
            {
                for (var i = 0; i < _judgeResult.Result.Length; i++)
                {
                    _judgeResult.Result[i] = "Compile Error";
                    _judgeResult.Exitcode[i] = 0;
                    _judgeResult.Score[i] = 0;
                    _judgeResult.Timeused[i] = 0;
                    _judgeResult.Memoryused[i] = 0;
                }
            }
        }

        private void Judging()
        {
            for (var i = 0; i < _problem.DataSets.Length; i++)
            {
                if (!File.Exists(_problem.DataSets[i].InputFile) || !File.Exists(_problem.DataSets[i].OutputFile))
                {
                    _judgeResult.Result[i] = "Problem Configuration Error";
                    _judgeResult.Exitcode[i] = 0;
                    _judgeResult.Score[i] = 0;
                    _judgeResult.Timeused[i] = 0;
                    _judgeResult.Memoryused[i] = 0;
                }
                else
                {
                    try
                    {
                        File.Copy(_problem.DataSets[i].InputFile, _workingdir + "\\" + _problem.InputFileName, true);
                    }
                    catch
                    {
                        _judgeResult.Result[i] = "Problem Configuration Error";
                        _judgeResult.Exitcode[i] = 0;
                        _judgeResult.Score[i] = 0;
                        _judgeResult.Timeused[i] = 0;
                        _judgeResult.Memoryused[i] = 0;
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
                        _judgeResult.Result[i] = "Unknown Error";
                        _judgeResult.Exitcode[i] = 0;
                        _judgeResult.Score[i] = 0;
                        _judgeResult.Timeused[i] = 0;
                        _judgeResult.Memoryused[i] = 0;
                        continue;
                    }
                    while (!_isexited)
                    {
                        _judgeResult.Timeused[_cur] = _excute.TotalProcessorTime.Milliseconds;
                        _judgeResult.Memoryused[_cur] = _excute.PeakWorkingSet64 / 1024;
                        if (_judgeResult.Timeused[_cur] > _problem.DataSets[_cur].TimeLimit)
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
                            _judgeResult.Result[_cur] = "Time Limit Excceed";
                            _judgeResult.Score[_cur] = 0;
                        }
                        if (_judgeResult.Memoryused[_cur] > _problem.DataSets[_cur].MemoryLimit)
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
                            _judgeResult.Result[_cur] = "Memory Limit Excceed";
                            _judgeResult.Score[_cur] = 0;
                        }
                        Thread.Sleep(10);
                    }
                    if (_isfault) continue;
                    if (!File.Exists(_workingdir + "\\" + _problem.OutputFileName))
                    {
                        _judgeResult.Result[_cur] = "Output File Error";
                        _judgeResult.Score[_cur] = 0;
                        continue;
                    }

                    if (string.IsNullOrEmpty(_problem.SpecialJudge) && File.Exists(_problem.SpecialJudge))
                    {
                        try
                        {
                            File.Delete(_workingdir + "\\hjudge_spj_result.dat");
                        }
                        catch
                        {
                            // ignored
                        }
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
                        Process.Start(xx).WaitForExit();
                        if (!File.Exists(_workingdir + "\\hjudge_spj_result.dat"))
                        {
                            _judgeResult.Result[_cur] = "Special Judger Error";
                            _judgeResult.Score[_cur] = 0;
                        }
                        else
                        {
                            var x = new FileStream(_workingdir + "\\hjudge_spj_result.dat", FileMode.Open);
                            var y = new StreamReader(x);
                            var p = y.ReadToEnd();
                            y.Close();
                            x.Close();
                            p = Regex.Replace(p, @"\s", "");
                            var gs = Convert.ToSingle(p);
                            _judgeResult.Score[_cur] = _problem.DataSets[_cur].Score * gs;
                            if (Math.Abs(gs - 1) > 0.001)
                            {
                                _judgeResult.Result[_cur] = "Wrong Answer";
                            }
                            else
                            {
                                _judgeResult.Result[_cur] = "Correct";
                            }
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
                            if (s1 != s2)
                            {
                                iswrong = true;
                                _judgeResult.Result[_cur] = "Wrong Answer";
                                _judgeResult.Score[_cur] = 0;
                                if (Regex.Replace(s1, @"\s", "") == Regex.Replace(s2, @"\s", ""))
                                {
                                    _judgeResult.Result[_cur] = "Presentation Answer";
                                }
                                else break;
                            }
                        } while (!(sr1.EndOfStream && sr2.EndOfStream));
                        sr1.Close();
                        sr2.Close();
                        fs1.Close();
                        fs2.Close();
                        if (!iswrong)
                        {
                            _judgeResult.Result[_cur] = "Correct";
                            _judgeResult.Score[_cur] = _problem.DataSets[_cur].Score;
                        }
                    }
                }
            }
        }

        private void Exithandler(object sender, EventArgs e)
        {
            _judgeResult.Exitcode[_cur] = _excute.ExitCode;
            if (_judgeResult.Exitcode[_cur] != 0 && !_isfault)
            {
                _judgeResult.Result[_cur] = "Runtime Error";
                _judgeResult.Score[_cur] = 0;
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

        private bool Compile(string fileName)
        {
            var a = new ProcessStartInfo
            {
                FileName = Configuration.Configurations.Compiler,
                ErrorDialog = false,
                UseShellExecute = true,
                Arguments = _command,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };
            Process.Start(a)?.WaitForExit();
            if (File.Exists(_workingdir + "\\test_hjudge.exe"))
            {
                return true;
            }
            return false;
        }
    }
}
