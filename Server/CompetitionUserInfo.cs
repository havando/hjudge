using System;

namespace Server
{
    internal class CompetitionUserInfo
    {
        public int Rank { get; set; }
        public string UserName { get; set; }
        public float Score { get; set; }
        public string TimeCost => $"{TotTime.Days * 24 + TotTime.Hours}:{TotTime.Minutes}:{TotTime.Seconds}";
        public TimeSpan TotTime { get; set; }
        public CompetitionProblemInfo[] ProblemInfo { get; set; }
    }
}