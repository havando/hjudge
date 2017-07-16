using System.ComponentModel;

namespace Server
{
    public class Problem : INotifyPropertyChanged
    {
        public int ProblemId { get; set; }
        public string ProblemName
        {
            get => _problemName;
            set
            {
                _problemName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ProblemName"));
            }
        }
        public string AddDate { get; set; }
        public int Level { get; set; }
        public Data[] DataSets { get; set; }
        public int Type { get; set; }
        public string SpecialJudge { get; set; }
        public string[] ExtraFiles { get; set; }
        public string InputFileName { get; set; }
        public string OutputFileName { get; set; }
        public string CompileCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
        private string _problemName;
        private bool _isChecked;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }
    }
}
