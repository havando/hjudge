using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HPSocketCS;

namespace Client
{
    public static class Connection
    {
        private static bool _isUsing;
        private static readonly List<byte> Recv = new List<byte>();
        private static readonly ConcurrentQueue<ObjOperation> Operations = new ConcurrentQueue<ObjOperation>();
        private static readonly TcpPullClient HClient = new TcpPullClient();
        private const string Divtot = "<|h~|split|~j|>";
        private const string Divpar = "<h~|~j>";
        private static bool _isConnecting;
        private static Action<string> _updateMainPage;
        public static bool Init(string ip, ushort port, Action<string> updateMainPage)
        {
            _updateMainPage = updateMainPage;
            HClient.OnConnect += sender =>
            {
                updateMainPage.Invoke($"Connection{Divpar}Connected");
                StayConnection();
                return HandleResult.Ok;
            };
            HClient.OnClose += (sender, operation, code) =>
            {
                updateMainPage.Invoke($"Connection{Divpar}Closed");
                return HandleResult.Ok;
            };
            HClient.OnReceive += HClientOnOnReceive;
            if (!HClient.Connect(ip, port))
            {
                return false;
            }

            DealingBytes();
            DealingOperations();
            return true;
        }

        private static HandleResult HClientOnOnReceive(TcpPullClient tcpPullClient, int length)
        {
            var bufferPtr = IntPtr.Zero;
            try
            {
                bufferPtr = Marshal.AllocHGlobal(length);
                if (tcpPullClient.Fetch(bufferPtr, length) == FetchResult.Ok)
                {
                    var recv = new byte[length];
                    Marshal.Copy(bufferPtr, recv, 0, length);
                    WaitingForUnusing();
                    _isUsing = true;
                    Recv.AddRange(recv);
                    _isUsing = false;
                }
            }
            catch
            {
                return HandleResult.Error;
            }
            finally
            {
                if (bufferPtr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(bufferPtr);
                }
            }
            return HandleResult.Ok;
        }

        #region Network

        private static void WaitingForUnusing()
        {
            while (_isUsing)
            {
                Thread.Sleep(10);
            }
        }

        public static void SendData(string operation, IEnumerable<byte> sendBytes)
        {
            var temp = Encoding.Unicode.GetBytes(operation);
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
            temp = temp.Concat(sendBytes).ToArray();
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divtot)).ToArray();
            HClient.Send(temp, temp.Length);
        }

        public static void SendData(string operation, string sendString)
        {
            var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString + Divtot);
            HClient.Send(temp, temp.Length);
        }

        public static void SendMsg(string sendString, IntPtr connId)
        {
            SendData("Messaging", sendString);
        }


        private static int Searchbytes(IReadOnlyList<byte> srcBytes, IReadOnlyList<byte> searchBytes, int start)
        {
            if (srcBytes == null) { return -1; }
            if (searchBytes == null) { return -1; }
            if (srcBytes.Count == 0) { return -1; }
            if (searchBytes.Count == 0) { return -1; }
            if (srcBytes.Count < searchBytes.Count) { return -1; }
            if (start >= srcBytes.Count) { return -1; }
            for (var i = start; i < srcBytes.Count - searchBytes.Count + 1; i++)
            {
                if (srcBytes[i] != searchBytes[0]) continue;
                if (searchBytes.Count == 1) { return i; }
                var flag = true;
                for (var j = 1; j < searchBytes.Count; j++)
                {
                    if (srcBytes[i + j] == searchBytes[j]) continue;
                    flag = false;
                    break;
                }
                if (flag) { return i; }
            }
            return -1;
        }

        private static List<byte[]> Bytespilt(IReadOnlyList<byte> ori, IReadOnlyList<byte> spi)
        {
            var pp = new List<byte[]>();
            var idx = 0;
            var idxx = 0;
            while (idxx != -1)
            {
                var tmp = new List<byte>();
                idxx = Searchbytes(ori, spi, idx + 1);
                if (idxx != -1)
                {
                    for (var i = idx; i < idxx; i++)
                    {
                        tmp.Add(ori[i]);
                    }
                }
                else
                {
                    for (var i = idx; i < ori.Count; i++)
                    {
                        tmp.Add(ori[i]);
                    }
                }
                idx = idxx + spi.Count;
                pp.Add(tmp.ToArray());
            }
            return pp;
        }

        private static void DealingBytes()
        {
            Task.Run(() =>
            {
                while (!Environment.HasShutdownStarted)
                {
                    WaitingForUnusing();
                    _isUsing = true;
                    var temp = Bytespilt(Recv.ToArray(), Encoding.Unicode.GetBytes(Divtot));
                    if (temp.Count != 0)
                    {
                        Recv.Clear();
                        Recv.AddRange(temp[temp.Count - 1]);
                    }
                    _isUsing = false;
                    temp.RemoveAt(temp.Count - 1);
                    if (temp.Count == 0)
                    {
                        continue;
                    }
                    foreach (var i in temp)
                    {
                        var temp2 = Bytespilt(i, Encoding.Unicode.GetBytes(Divpar));
                        if (temp2.Count == 0)
                        {
                            continue;
                        }
                        var operation = Encoding.Unicode.GetString(temp2[0]);
                        switch (operation)
                        {
                            case "&":
                                {
                                    _isConnecting = true;
                                    break;
                                }
                            default:
                                {
                                    temp2.RemoveAt(0);
                                    Operations.Enqueue(new ObjOperation
                                    {
                                        Operation = operation,
                                        Content = temp2
                                    });
                                    break;
                                }
                        }
                    }
                    Thread.Sleep(10);
                }
            });
        }

        private static void DealingOperations()
        {
            Task.Run(() =>
            {
                while (!Environment.HasShutdownStarted)
                {
                    if (Operations.TryDequeue(out var res))
                    {
                        try
                        {
                            switch (res.Operation)
                            {
                                case "Login":
                                    {
                                        var x = Encoding.Unicode.GetString(res.Content[0]);
                                        switch (x)
                                        {
                                            case "Succeed":
                                                {
                                                    _updateMainPage.Invoke($"Login{Divpar}Succeed");
                                                    break;
                                                }
                                            case "Incorrect":
                                                {
                                                    _updateMainPage.Invoke($"Login{Divpar}Incorrect");
                                                    break;
                                                }
                                            case "Unknown":
                                                {
                                                    _updateMainPage.Invoke($"Login{Divpar}Unknown");
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                case "Logout":
                                    {
                                        _updateMainPage.Invoke($"Logout{Divpar}Succeed");
                                        break;
                                    }
                                case "Messaging":
                                    {
                                        _updateMainPage.Invoke($"Messaging{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                                case "FileList":
                                    {
                                        _updateMainPage.Invoke($"FileList{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                                case "File":
                                    {
                                        var fileName = Encoding.Unicode.GetString(res.Content[0]);
                                        fileName = fileName.Substring(
                                            fileName.Length -
                                            (fileName.Length - fileName.LastIndexOf("\\", StringComparison.Ordinal)),
                                            fileName.Length - fileName.LastIndexOf("\\", StringComparison.Ordinal));
                                        File.WriteAllBytes($"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}", res.Content[1]);
                                        Process.Start("explorer.exe",
                                            $"/select, {Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}");
                                        break;
                                    }
                                case "JudgeResult":
                                    {
                                        _updateMainPage.Invoke($"JudgeResult{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                                case "ProblemList":
                                    {
                                        _updateMainPage.Invoke($"ProblemList{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                                case "Profile":
                                    {
                                        _updateMainPage.Invoke($"Profile{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                                case "ChangePassword":
                                    {
                                        _updateMainPage.Invoke(
                                            $"ChangePassword{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                                case "UpdateProfile":
                                    {
                                        _updateMainPage.Invoke(
                                            $"UpdateProfile{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    Thread.Sleep(10);
                }
            });
        }

        private static void StayConnection()
        {
            Task.Run(() =>
            {
                while (Environment.HasShutdownStarted)
                {
                    _isConnecting = false;
                    var x = Encoding.Unicode.GetBytes("@");
                    HClient.Send(x, x.Length);
                    Thread.Sleep(10000);
                    if (_isConnecting) { continue; }
                    MessageBox.Show("与服务端的连接已断开", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(1);
                }
            });
        }

        #endregion
    }
}
