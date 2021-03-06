﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Server
{
    public static class Configuration
    {
        public static Config Configurations;
        public static bool IsHidden;
        public static readonly int ProcessorCount = Environment.ProcessorCount;

        static Configuration()
        {
            if (ProcessorCount <= 0) ProcessorCount = 2;
        }

        public static void Init()
        {
            Configurations = new Config();
            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\Config.xml"))
            {
                Configurations.Compiler = new List<Compiler>();
                Configurations.EnvironmentValues = string.Empty;
                Configurations.AllowRequestDataSet = true;
                Configurations.MutiThreading = 0;
                Configurations.IpAddress = "::";
                Configurations.AllowCompetitorMessaging = true;
                Configurations.RegisterMode = 0;
                File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\Config.xml",
                    SerializeToXmlString(Configurations), Encoding.UTF8);
            }
            var xmlDeserializer = new XmlSerializer(Configurations.GetType());
            var rdr =
                new StringReader(
                    File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\Config.xml", Encoding.UTF8));
            Configurations = (Config) xmlDeserializer.Deserialize(rdr);
            var pathlist = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PATH", Configurations.EnvironmentValues + ";" + pathlist,
                EnvironmentVariableTarget.Process);
            if (Configurations.Compiler == null)
                Configurations.Compiler = new List<Compiler>();
        }

        private static string SerializeToXmlString(object objectToSerialize)
        {
            var memoryStream = new MemoryStream();
            var xmlSerializer = new XmlSerializer(objectToSerialize.GetType());
            xmlSerializer.Serialize(memoryStream, objectToSerialize);
            return Encoding.Default.GetString(memoryStream.ToArray());
        }

        public static void Save()
        {
            var pathlist = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PATH", Configurations.EnvironmentValues + ";" + pathlist,
                EnvironmentVariableTarget.Process);
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\Config.xml",
                Encoding.UTF8.GetString(Encoding.Default.GetBytes(SerializeToXmlString(Configurations))),
                Encoding.UTF8);
        }
    }
}