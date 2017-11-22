using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml.Serialization;

namespace ClientConfiguration
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            //if (File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}\\Updater.exe"))
            //{
            //    new Process
            //    {
            //        StartInfo =
            //        {
            //            FileName = $"{AppDomain.CurrentDomain.BaseDirectory}\\Updater.exe",
            //            Arguments =
            //                $"ClientConfiguration {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version} {Process.GetCurrentProcess().Id} \"{AppDomain.CurrentDomain.BaseDirectory}\""
            //        }
            //    }.Start();
            //}
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Generate(IpBox.Text, Convert.ToUInt16(PortBox.Text));
            MessageBox.Show(
                "已成功生成配置文件于 " + AppDomain.CurrentDomain.BaseDirectory + "\\Config.xml\r\n请将该文件放置于 hjudge - Client 的 AppData 目录下",
                "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private static void Generate(string ip, ushort port)
        {
            var configurations = new Config
            {
                Ip = ip,
                Port = port
            };
            File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + "\\Config.xml", SerializeToXmlString(configurations),
                Encoding.UTF8);
        }

        private static string SerializeToXmlString(object objectToSerialize)
        {
            var memoryStream = new MemoryStream();
            var xmlSerializer = new XmlSerializer(objectToSerialize.GetType());
            xmlSerializer.Serialize(memoryStream, objectToSerialize);
            return Encoding.Default.GetString(memoryStream.ToArray());
        }
    }
}