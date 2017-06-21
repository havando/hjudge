using System;
using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace Client
{
    public static class Configuration
    {
        public static Config Configurations;

        public static void Init()
        {
            Configurations = new Config();
            if (!File.Exists(Environment.CurrentDirectory + "\\AppData\\Config.xml"))
            {
                Configurations.Ip = "";
                Configurations.Port = 0;
                File.WriteAllText(Environment.CurrentDirectory + "\\AppData\\Config.xml", SerializeToXmlString(Configurations), Encoding.UTF8);
            }
            var xmlDeserializer = new XmlSerializer(Configurations.GetType());
            var rdr =
                new StringReader(File.ReadAllText(Environment.CurrentDirectory + "\\AppData\\Config.xml", Encoding.UTF8));
            Configurations = (Config)xmlDeserializer.Deserialize(rdr);
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
            File.WriteAllText(Environment.CurrentDirectory + "\\AppData\\Config.xml", Encoding.UTF8.GetString(Encoding.Default.GetBytes(SerializeToXmlString(Configurations))), Encoding.UTF8);
        }
    }
}
