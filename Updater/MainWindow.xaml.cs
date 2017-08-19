using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Path = System.IO.Path;

namespace Updater
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            switch (UpdateInfo.Product)
            {
                case "Server":
                case "Client":
                case "ClientConfiguration":
                    break;
                default:
                    Environment.Exit(0);
                    break;
            }
            var version = CheckUpdate();
            if (version.Version == UpdateInfo.CurrentVersion)
            {
                Environment.Exit(0);
            }
            InitializeComponent();
            Product.Content = string.Format(Product.Content.ToString(), UpdateInfo.Product, version.Version);
            Task.Run(() =>
            {
                var request =
                    WebRequest.Create(version.Uri) as HttpWebRequest;
                var response = request?.GetResponse() as HttpWebResponse;
                var responseStream = response?.GetResponseStream();
                if (responseStream != null)
                {
                    Stream stream =
                        new FileStream(
                            Environment.GetEnvironmentVariable("temp") + $"\\hjudge_{UpdateInfo.Product}_update.zip",
                            FileMode.Create);
                    var bArr = new byte[1024];
                    var dsize = responseStream.Read(bArr, 0, bArr.Length);
                    while (dsize > 0)
                    {
                        stream.Write(bArr, 0, dsize);
                        dsize = responseStream.Read(bArr, 0, bArr.Length);
                    }
                    stream.Close();
                    responseStream.Close();
                    DeleteDir(Environment.GetEnvironmentVariable("temp") + $"\\hjudge_{UpdateInfo.Product}_update");
                    ZipFile.ExtractToDirectory(Environment.GetEnvironmentVariable("temp") +
                                               $"\\hjudge_{UpdateInfo.Product}_update.zip",
                        Environment.GetEnvironmentVariable("temp") + $"\\hjudge_{UpdateInfo.Product}_update");
                    var a = Process.GetProcessById(UpdateInfo.ProcessId);
                    try
                    {
                        a.Kill();
                        a.Close();
                    }
                    catch
                    {
                        //ignored
                    }
                    Thread.Sleep(5000);
                    Copy(Environment.GetEnvironmentVariable("temp") + $"\\hjudge_{UpdateInfo.Product}_update",
                        UpdateInfo.RootDirectory, true);
                    Thread.Sleep(1000);
                    var p = new Process { StartInfo = { FileName = UpdateInfo.RootDirectory + $"\\{version.Program}" } };
                    p.Start();
                    Environment.Exit(0);
                }
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private NewVersionInfo CheckUpdate()
        {
            NewVersionInfo x = null;
            try
            {
                var request =
                    WebRequest.Create($"http://www.hez2010.vip/hjudge/Update_{UpdateInfo.Product}.json") as
                        HttpWebRequest;
                var response = request?.GetResponse() as HttpWebResponse;
                var responseStream = response?.GetResponseStream();
                if (responseStream != null)
                {
                    var bytes = new byte[1024];
                    var allInfo = new List<byte>();
                    while (responseStream.Read(bytes, 0, 1024) > 0)
                    {
                        allInfo.AddRange(bytes);
                    }
                    var updateinfo = Encoding.Default.GetString(allInfo.ToArray());
                    if (updateinfo.IndexOf("{", StringComparison.Ordinal) >= 0)
                    {
                        x = JsonConvert.DeserializeObject<NewVersionInfo>(updateinfo);
                    }
                    responseStream.Close();
                }
            }
            catch
            {
                Environment.Exit(0);
            }
            return x ?? new NewVersionInfo();
        }

        public static void DeleteDir(string srcPath)
        {
            try
            {
                var dir = new DirectoryInfo(srcPath);
                var fileinfo = dir.GetFileSystemInfos();
                foreach (var i in fileinfo)
                {
                    if (i is DirectoryInfo)
                    {
                        var subdir = new DirectoryInfo(i.FullName);
                        subdir.Delete(true);
                    }
                    else
                    {
                        File.Delete(i.FullName);
                    }
                }
            }
            catch
            {
                //ignored
            }
        }

        private static void Copy(string sourceFolderName, string destFolderName, bool overwrite)
        {
            var sourceFilesPath = Directory.GetFileSystemEntries(sourceFolderName);

            foreach (var sourceFilePath in sourceFilesPath)
            {
                var directoryName = Path.GetDirectoryName(sourceFilePath);
                var forlders = directoryName?.Split('\\');
                var lastDirectory = forlders?[forlders.Length - 1];
                if (lastDirectory != null)
                {
                    var dest = Path.Combine(destFolderName, lastDirectory);

                    if (File.Exists(sourceFilePath))
                    {
                        var sourceFileName = Path.GetFileName(sourceFilePath);
                        if (!Directory.Exists(dest))
                        {
                            Directory.CreateDirectory(dest);
                        }
                        File.Copy(sourceFilePath, Path.Combine(dest, sourceFileName), overwrite);
                    }
                    else
                    {
                        Copy(sourceFilePath, dest, overwrite);
                    }
                }
            }
        }
    }
}
