using System;
using System.Diagnostics;
using System.IO;

namespace Server
{
    public class JudgeInfo
    {
        public int JudgeId { get; set; }
        public int ProblemId { get; set; }
        public string JudgeDate { get; set; }
        public string Code { get; set; }
        public string[] Result { get; set; }
        public float[] Socre { get; set; }
        public long[] Timeused { get; set; }
        public long[] Memoryused { get; set; }
        public int[] Exitcode { get; set; }
    }

    public class Judge
    {
        private string _workingdir;

        public readonly JudgeInfo JudgeResult = new JudgeInfo();
        public Judge(Problem toJudge, string code)
        {
            var datetime = DateTime.Now.ToString("yyyyMMddHHmmssffff");
            JudgeResult.JudgeDate = datetime;
            JudgeResult.ProblemId = toJudge.ProblemId;
            _workingdir = Environment.GetEnvironmentVariable("temp") + "\\Judge_hjudge_" + datetime;
        }

        public void BeginJudge(string code)
        {
            File.WriteAllText(_workingdir + "\\test.cpp", code);
            if (Compile(_workingdir + "\\test.cpp"))
            {
                //TODO: Judge
            }
        }

        private static string Dn(string filename)
        {
            return "\"" + filename + "\"";
        }

        private bool Compile(string fileName)
        {
            ProcessStartInfo a = new ProcessStartInfo
            {
                FileName = Configuration.Compiler,
                ErrorDialog = false,
                UseShellExecute = true,
                Arguments = Dn(fileName) + " -o " + Dn(_workingdir + "\\test_hjudge.exe"),
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
