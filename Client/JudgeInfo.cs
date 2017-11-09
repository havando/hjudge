using System.ComponentModel;
using System.Linq;

namespace Client
{
    public class JudgeInfo : INotifyPropertyChanged
    {
        private bool _isChecked;

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
        public string Type { get; set; }

        public string ResultSummery { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }

        public float FullScore;
        public event PropertyChangedEventHandler PropertyChanged;
    }
}