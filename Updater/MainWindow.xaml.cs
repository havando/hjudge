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
            try
            {
                if (version.Version == UpdateInfo.CurrentVersion)
                {
                    Environment.Exit(0);
                }
                InitializeComponent();
                TextBox.Text = version.Content;
                Product.Content = string.Format(Product.Content.ToString(), UpdateInfo.Product, version.Version);
                Task.Run(() =>
                {
                    var request =
                        WebRequest.Create(version.Uri) as HttpWebRequest;
                    var response = request?.GetResponse() as HttpWebResponse;
                    var responseStream = response?.GetResponseStream();
                    var x = Guid.NewGuid().ToString().Replace("-", string.Empty);
                    if (responseStream != null)
                    {
                        Stream stream =
                            new FileStream(
                                Environment.GetEnvironmentVariable("temp") +
                                $"\\hjudge_{UpdateInfo.Product}_update_{x}.zip",
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
                        ZipFile.ExtractToDirectory(Environment.GetEnvironmentVariable("temp") +
                                                   $"\\hjudge_{UpdateInfo.Product}_update_{x}.zip",
                            Environment.GetEnvironmentVariable("temp") + $"\\hjudge_{UpdateInfo.Product}_update_{x}");
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
                        try
                        {
                            Copy(
                                Environment.GetEnvironmentVariable("temp") +
                                $"\\hjudge_{UpdateInfo.Product}_update_{x}",
                                UpdateInfo.RootDirectory, true);
                        }
                        catch
                        {
                            MessageBox.Show("更新失败, 请手动下载更新", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                            Process.Start(version.Uri);
                            Environment.Exit(0);
                        }
                        Thread.Sleep(1000);
                        var batprogram = $@"@echo off
setlocal enabledelayedexpansion
title hjudge - Updater
cls
echo.
echo  正在更新... 请勿关闭本窗口
echo.
ping /n 5 0.0.0.0>nul
taskkill /f /im {Process.GetCurrentProcess().ProcessName}.exe >nul 2>nul
ping /n 3 0.0.0.0>nul
for /f ""tokens=* delims="" %%a in ('dir /s /a-d /b /q ""{Environment.CurrentDirectory}\*.replace""') do (
del /f /q ""%%~dpa%%~na""
ren ""%%a"" ""%%~na""
)
start """" ""{Environment.CurrentDirectory}\{version.Program}""
";
                        if (File.Exists(Environment.GetEnvironmentVariable("temp") + "\\update_hjudge_bat.bat"))
                        {
                            File.Delete(Environment.GetEnvironmentVariable("temp") + "\\update_hjudge_bat.bat");
                        }
                        File.WriteAllBytes(Environment.GetEnvironmentVariable("temp") + "\\update_hjudge_bat.bat",
                            Encoding.GetEncoding("GBK").GetBytes(batprogram));
                        Process.Start(Environment.GetEnvironmentVariable("temp") + "\\update_hjudge_bat.bat");
                        Environment.Exit(0);
                    }
                });
            }
            catch
            {
                MessageBox.Show("更新失败, 请手动下载更新", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                Process.Start(version.Uri);
                Environment.Exit(0);
            }
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
                    WebRequest.Create($"http://www.hez2010.vip/hjudge/Update_{UpdateInfo.Product}.txt") as
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
                        try
                        {
                            File.Copy(sourceFilePath, Path.Combine(dest, sourceFileName), overwrite);
                        }
                        catch
                        {
                            File.Copy(sourceFilePath, Path.Combine(dest, sourceFileName + ".replace"), overwrite);
                        }
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
