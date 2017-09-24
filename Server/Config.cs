﻿using System.Collections.Generic;

namespace Server
{
    public class Config
    {
        public List<Compiler> Compiler { get; set; }
        public string EnvironmentValues { get; set; }
        public bool AllowRequestDataSet { get; set; }
        public int MutiThreading { get; set; }
        public string IpAddress { get; set; }
    }
}