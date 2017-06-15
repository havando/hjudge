﻿using System;

namespace Server
{
    public class Problem
    {
        public int ProblemId { get; set; }
        public string ProblemName { get; set; }
        public DateTime AddDate { get; set; }
        public int Level { get; set; }
        public Data[] DataSets { get; set; }
        public int Type { get; set; }
        public string SpecialJudge { get; set; }
        public string[] ExtraFiles { get; set; }
        public string InputFileName { get; set; }
        public string OutputFileName { get; set; }
        public string CompileCommand { get; set; }
    }
}
