using System.ComponentModel;

namespace Client
{
    public class JudgeInfo : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

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
        public string UserName { get; set; }
        public string ProblemName { get; set; }
        public string ResultSummery { get; set; }
        private bool _isChecked { get; set; }
        public bool IsChecked { get; set; }
        public float FullScore { get; set; }
    }
}
