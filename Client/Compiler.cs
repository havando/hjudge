namespace Client
{
    public class Compiler
    {
        public string DisplayName { get; set; }
        public string CompilerExec { get; set; }
        public bool LinuxComExec { get; set; }
        public string CompilerArgs { get; set; }
        public bool LinuxComArgs { get; set; }
        public string ExtName { get; set; }
        public string StaticCheck { get; set; }
        public bool LinuxStaExec { get; set; }
        public string StaticArgs { get; set; }
        public bool LinuxStaArgs { get; set; }
        public string RunExec { get; set; }
        public bool LinuxRunExec { get; set; }
        public string RunArgs { get; set; }
        public bool LinuxRunArgs { get; set; }
    }
}