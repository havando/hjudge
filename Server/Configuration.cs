using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Server
{
    public class Config
    {
        public string Compiler { get; set; }
        public string EnvironmentValues { get; set; }
    }
    public static class Configuration
    {
        public static Config Configurations;

        public static void Init()
        {
            Configurations = new Config();
            if (!File.Exists(Environment.CurrentDirectory + "\\AppData\\Config.xml"))
            {
                Configurations.Compiler = "";
                Configurations.EnvironmentValues = "";
                File.WriteAllText(Environment.CurrentDirectory + "\\AppData\\Config.xml", SerializeToXmlString(Configurations));
            }
            var xmlDeserializer = new XmlSerializer(Configurations.GetType());
            var rdr =
                new StringReader(File.ReadAllText(Environment.CurrentDirectory + "\\AppData\\Config.xml"));
            Configurations = (Config)xmlDeserializer.Deserialize(rdr);
            var pathlist = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("PATH", Configurations.EnvironmentValues + ";" + pathlist, EnvironmentVariableTarget.Process);
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
            Environment.SetEnvironmentVariable("PATH", Configurations.EnvironmentValues + ";" + pathlist, EnvironmentVariableTarget.Process);
            File.WriteAllText(Environment.CurrentDirectory + "\\AppData\\Config.xml", SerializeToXmlString(Configurations), Encoding.Default);
        }
    }
}
