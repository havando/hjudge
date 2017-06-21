using System.Collections.Generic;
using System.Linq;

namespace Server
{
    public class JudgeResult
    {
        public string MemberName { get; set; }
        public List<JudgeInfo> Result { get; set; }

        public float FullScore => Result?.Sum(i => i.FullScore) ?? 0;
    }
}