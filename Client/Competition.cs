using System;
using System.ComponentModel;

namespace Client
{
    public class Competition : INotifyPropertyChanged
    {
        private string _competitionName;
        private DateTime _endTime;
        private DateTime _startTime;
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

        public DateTime StartTime
        {
            get => _startTime;
            set
            {
                _startTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("StartTime"));
            }
        }

        public DateTime EndTime
        {
            get => _endTime;
            set
            {
                _endTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("EndTime"));
            }
        }

        public int[] ProblemSet
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
        public int SubmitLimit { get; set; }
        private int[] _problemSet { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}