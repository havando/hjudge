namespace Server
{
    public class Compiler
    {
        public string DisplayName { get; set; }
        public string CompilerPath { get; set; }
        public string DefaultArgs { get; set; }
        public string ExtName { get; set; }
        public string StaticCheck { get; set; }
        public string RunCommand { get; set; }
        public bool Linux { get; set; }
    }
}