using System.Linq;
using System.Text.RegularExpressions;

namespace Server
{
    public class QueryCommand
    {
        private string GetEngName(string origin)
        {
            var re = new Regex("[A-Z]|[a-z]|[0-9]");
            return re.Matches(origin).Cast<object>().Aggregate(string.Empty, (current, t) => current + t);
        }
        public string Name;
        public int Operator;
        public string Value;
        public int NextRelation;
        public string Command => $"{GetEngName(Name)} {(Operator == 1 ? "=" : Operator == 2 ? "<=" : Operator == 3 ? ">=" : Operator == 4 ? "<" : Operator == 5 ? ">" : "!=")} {GetEngName(Value)}{(NextRelation == 1 ? " and " : NextRelation == 2 ? " or " : " ")}";
    }
}
