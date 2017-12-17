using System.ComponentModel;
using System.Linq;

namespace Server
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
        public string UserName => Connection.GetUserName(UserId);
        public string ProblemName => Connection.GetProblemName(ProblemId);
        public string Type { get; set; }
        public string Description { get; set; }
        public int CompetitionId { get; set; }
        public string AdditionInfo { get; set; }

        public string ResultSummery
        {
            get
            {
                if (Result == null)
                    return "Unknown Error";
                if (Result.Length == 0 || (Result.Length == 1 && string.IsNullOrEmpty(Result[0])))
                    return "Problem Configuration Error";
                var error = new int[11];
                var tot = 0;
                foreach (var t in Result)
                    switch (t)
                    {
                        case "Correct":
                            error[0]++;
                            tot++;
                            break;
                        case "Problem Configuration Error":
                            error[1]++;
                            tot++;
                            break;
                        case "Compile Error":
                            error[2]++;
                            tot++;
                            break;
                        case "Wrong Answer":
                            error[3]++;
                            tot++;
                            break;
                        case "Presentation Error":
                            error[4]++;
                            tot++;
                            break;
                        case "Runtime Error":
                            error[5]++;
                            tot++;
                            break;
                        case "Time Limit Exceeded":
                            error[6]++;
                            tot++;
                            break;
                        case "Memory Limit Exceeded":
                            error[7]++;
                            tot++;
                            break;
                        case "Output File Error":
                            error[8]++;
                            tot++;
                            break;
                        case "Special Judge Error":
                            error[9]++;
                            tot++;
                            break;
                        default:
                            {
                                if (t?.Contains("Unknown Error") ?? false)
                                {
                                    error[10]++;
                                    tot++;
                                }
                                break;
                            }
                    }
                if (tot == error[0]) return tot != 0 ? "Accepted" : "Judging...";
                var max = error[1];
                var j = 1;
                for (var i = 1; i < 11; i++)
                    if (error[i] > max)
                        j = i;
                switch (j)
                {
                    case 1: return "Problem Configuration Error";
                    case 2: return "Compile Error";
                    case 3: return "Wrong Answer";
                    case 4: return "Presentation Error";
                    case 5: return "Runtime Error";
                    case 6: return "Time Limit Exceeded";
                    case 7: return "Memory Limit Exceeded";
                    case 8: return "Output File Error";
                    case 9: return "Special Judge Error";
                    case 10: return "Unknown Error";
                }
                return "Unknown Error";
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsChecked"));
            }
        }

        public float FullScore => Score?.Sum() ?? 0;
        public event PropertyChangedEventHandler PropertyChanged;
    }
}