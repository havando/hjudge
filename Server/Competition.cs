using System.ComponentModel;

namespace Server
{
    class Competition : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public int CompetitionId { get; set; }
        public string CompetitionName
        {
            get => _competitionName;
            set
            {
                _competitionName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CompetitionName"));
            }
        }

        public string StartTime
        {
            get => _startTime;
            set
            {
                _startTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StartTime"));
            }
        }

        public string EndTime
        {
            get => _endTime;
            set
            {
                _endTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("EndTime"));
            }
        }

        public string[] ProblemSet
        {
            get => _problemSet;
            set
            {
                _problemSet = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ProblemCount"));
            }
        }
        public int Option { get; set; }
        public string Password { get; set; }
        public string Description { get; set; }
        public int ProblemCount => ProblemSet?.Length ?? 0;

        private string _competitionName;
        private string[] _problemSet { get; set; }
        private string _startTime;
        private string _endTime;

    }
}
