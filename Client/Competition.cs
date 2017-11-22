using System;
using System.Threading;

namespace Client
{
    public class Competition
    {
        public int CompetitionId { get; set; }
        public string CompetitionName { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int[] ProblemSet { get; set; }
        public int Option { get; set; }
        public string Password { get; set; }
        public string Description { get; set; }
        public int ProblemCount { get; set; }
        public int SubmitLimit { get; set; }

        public int ProblemSolved => Connection.GetProblemSolvedCount(CompetitionId);
    }

    public static partial class Connection
    {
        private static int _getProblemSolvedCountResult;
        public static int GetProblemSolvedCount(int competitionId)
        {
            _getProblemSolvedCountResult = -1;
            SendData("GetProblemSolvedCount", $"{competitionId}");
            while (_getProblemSolvedCountResult == -1)
            {
                Thread.Sleep(1);
            }
            return _getProblemSolvedCountResult;
        }

    }

}
