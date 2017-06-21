using System.Collections.ObjectModel;

namespace Server
{
    internal class ResultTree
    {
        public string Content { get; set; }
        public ObservableCollection<ResultTree> Children { get; set; }
    }
}