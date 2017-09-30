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
        private const string Divtot = "<|h~|split|~j|>";
        private const string Divpar = "<h~|~j>";
        private static readonly object BytesLock = new object();
        private static readonly List<byte> Recv = new List<byte>();
        private static readonly ConcurrentQueue<ObjOperation> Operations = new ConcurrentQueue<ObjOperation>();
        private static readonly TcpPullClient HClient = new TcpPullClient();
        private static bool _isConnecting;
        private static bool _isConnected;
        private static bool _isReceiving;
        public static bool IsExited;
        private static Action<string> _updateMainPage;
        private static readonly int PkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        private static readonly PkgInfo PkgInfo = new PkgInfo();
        private static int _id;
        private static string _ip;
        private static ushort _port;

        public static bool Init(string ip, ushort port, Action<string> updateMainPage)
        {
            _updateMainPage = updateMainPage;
            HClient.OnConnect += sender =>
            {
                updateMainPage.Invoke($"Connection{Divpar}Connected");
                _isConnected = true;
                return HandleResult.Ok;
            };
            HClient.OnReceive += HClientOnOnReceive;
            _ip = ip;
            _port = port;
            Connect(ip, port);
            DealingBytes();
            DealingOperations();

            return true;
        }

        private static void Connect(string ip, ushort port)
        {
            Task.Run(() =>
            {
                do
                {
                    if (IsExited) break;
                    HClient.Connect(ip, port);
                    Thread.Sleep(1000);
                } while (!_isConnected);
                StayConnection();
            });
        }

        private static byte[] GetSendBuffer(byte[] bodyBytes)
        {
            var header = new PkgHeader
            {
                Id = ++_id,
                BodySize = bodyBytes.Length
            };
            var headerBytes = HClient.StructureToByte(header);
            var ptr = IntPtr.Zero;
            try
            {
                var bufferSize = headerBytes.Length + bodyBytes.Length;
                ptr = Marshal.AllocHGlobal(bufferSize);
                Marshal.Copy(headerBytes, 0, ptr, headerBytes.Length);
                Marshal.Copy(bodyBytes, 0, ptr + headerBytes.Length, bodyBytes.Length);
                var bytes = new byte[bufferSize];
                Marshal.Copy(ptr, bytes, 0, bufferSize);
                return bytes;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
            }
        }

        private static HandleResult HClientOnOnReceive(TcpPullClient sender, int length)
        {
            _isReceiving = true;
            var required = PkgInfo.Length;
            var remain = length;
            while (remain >= required)
            {
                var bufferPtr = IntPtr.Zero;
                try
                {
                    remain -= required;
                    bufferPtr = Marshal.AllocHGlobal(required);
                    if (sender.Fetch(bufferPtr, required) == FetchResult.Ok)
                    {
                        if (PkgInfo.IsHeader)
                        {
                            var header = (PkgHeader)Marshal.PtrToStructure(bufferPtr, typeof(PkgHeader));
                            required = header.BodySize;
                        }
                        else
                        {
                            var buffer = new byte[required];
                            Marshal.Copy(bufferPtr, buffer, 0, required);
                            lock (BytesLock)
                            {
                                Recv.AddRange(buffer);
                            }
                            required = PkgHeaderSize;
                        }
                        PkgInfo.IsHeader = !PkgInfo.IsHeader;
                        PkgInfo.Length = required;
                    }
                }
                catch
                {
                    return HandleResult.Error;
                }
                finally
                {
                    _isReceiving = false;
                    _updateMainPage.Invoke($"ReceivingFile{Divpar}Done");
                    if (bufferPtr != IntPtr.Zero)
                        Marshal.FreeHGlobal(bufferPtr);
                }
            }
            return HandleResult.Ok;
        }

        #region Network

        public static void SendData(string operation, IEnumerable<byte> sendBytes)
        {
            Task.Run(() =>
            {
                var temp = Encoding.Unicode.GetBytes(operation);
                temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
                temp = temp.Concat(sendBytes).ToArray();
                temp = temp.Concat(Encoding.Unicode.GetBytes(Divtot)).ToArray();
                var final = GetSendBuffer(temp);
                HClient.Send(final, final.Length);
            });
        }

        public static void SendData(string operation, string sendString)
        {
            Task.Run(() =>
            {
                var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString + Divtot);
                var final = GetSendBuffer(temp);
                HClient.Send(final, final.Length);
            });
        }

        public static void SendMsg(string sendString)
        {
            SendData("Messaging", sendString);
        }


        private static int Searchbytes(IReadOnlyList<byte> srcBytes, IReadOnlyList<byte> searchBytes, int start)
        {
            if (srcBytes == null) return -1;
            if (searchBytes == null) return -1;
            if (srcBytes.Count == 0) return -1;
            if (searchBytes.Count == 0) return -1;
            if (srcBytes.Count < searchBytes.Count) return -1;
            if (start >= srcBytes.Count) return -1;
            for (var i = start; i < srcBytes.Count - searchBytes.Count + 1; i++)
            {
                if (srcBytes[i] != searchBytes[0]) continue;
                if (searchBytes.Count == 1) return i;
                var flag = true;
                for (var j = 1; j < searchBytes.Count; j++)
                {
                    if (srcBytes[i + j] == searchBytes[j]) continue;
                    flag = false;
                    break;
                }
                if (flag) return i;
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
                    for (var i = idx; i < idxx; i++)
                        tmp.Add(ori[i]);
                else
                    for (var i = idx; i < ori.Count; i++)
                        tmp.Add(ori[i]);
                idx = idxx + spi.Count;
                pp.Add(tmp.ToArray());
            }
            return pp;
        }

        private static void DealingBytes()
        {
            Task.Run(() =>
            {
                while (!IsExited)
                {
                    lock (BytesLock)
                    {
                        if (Recv.Count == 0)
                        {
                            Thread.Sleep(10);
                            continue;
                        }
                        var temp = Bytespilt(Recv.ToArray(), Encoding.Unicode.GetBytes(Divtot));
                        if (temp.Count != 0)
                        {
                            Recv.Clear();
                            Recv.AddRange(temp[temp.Count - 1]);
                        }
                        temp.RemoveAt(temp.Count - 1);
                        foreach (var i in temp)
                        {
                            if (IsExited) break;
                            var temp2 = Bytespilt(i, Encoding.Unicode.GetBytes(Divpar));
                            if (temp2.Count == 0)
                                continue;
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
                                        Task.Run(() =>
                                        {
                                            temp2.RemoveAt(0);
                                            Operations.Enqueue(new ObjOperation
                                            {
                                                Operation = operation,
                                                Content = temp2
                                            });
                                        });
                                        break;
                                    }
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
                while (!IsExited)
                {
                    if (Operations.TryDequeue(out var res))
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
                                        lock (BytesLock)
                                        {
                                            Recv.Clear();
                                        }
                                        _updateMainPage.Invoke($"Logout{Divpar}Succeed");
                                        break;
                                    }
                                case "ProblemDataSet":
                                    {
                                        if (Encoding.Unicode.GetString(res.Content[0]) == "Denied")
                                        {
                                            _updateMainPage.Invoke($"ProblemDataSet{Divpar}Denied");
                                            break;
                                        }
                                        var problemId = Encoding.Unicode.GetString(res.Content[0]);
                                        var fileName = $"{problemId}_{DateTime.Now:yyyyMMddHHmmssffff}.zip";
                                        File.WriteAllBytes(
                                            $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}",
                                            res.Content[1]);
                                        Process.Start("explorer.exe",
                                            $"/select,\"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}\"");
                                        break;
                                    }
                                case "Messaging":
                                    {
                                        _updateMainPage.Invoke(
                                            $"Messaging{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");

                                        break;
                                    }
                                case "FileList":
                                    {
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        _updateMainPage.Invoke($"FileList{Divpar}{x}");

                                        break;
                                    }
                                case "File":
                                    {
                                        var fileName = Encoding.Unicode.GetString(res.Content[0]);
                                        var x = new List<byte>();
                                        for (var i = 1; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                            {
                                                x.AddRange(res.Content[i]);
                                                x.AddRange(Encoding.Unicode.GetBytes(Divpar));
                                            }
                                            else
                                            {
                                                x.AddRange(res.Content[i]);
                                            }
                                        File.WriteAllBytes(
                                            $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}",
                                            x.ToArray());
                                        x.Clear();
                                        GC.Collect();
                                        Process.Start("explorer.exe",
                                            $"/select,\"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}\"");
                                        _updateMainPage($"FileReceived{Divpar}Done");
                                        break;
                                    }
                                case "JudgeResult":
                                    {
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        _updateMainPage.Invoke(
                                            $"JudgeResult{Divpar}{x}");
                                        break;
                                    }
                                case "ProblemList":
                                    {
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        _updateMainPage.Invoke(
                                            $"ProblemList{Divpar}{x}");
                                        break;
                                    }
                                case "Profile":
                                    {
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        _updateMainPage.Invoke(
                                            $"Profile{Divpar}{x}");
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
                                case "JudgeRecord":
                                    {
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        _updateMainPage.Invoke($"JudgeRecord{Divpar}{x}");
                                        break;
                                    }
                                case "Compiler":
                                    {
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        _updateMainPage.Invoke(
                                            $"Compiler{Divpar}{x}");
                                        break;
                                    }
                                case "JudgeCode":
                                    {
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        _updateMainPage.Invoke($"JudgeCode{Divpar}{x}");
                                        break;
                                    }
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    Thread.Sleep(10);
                }
            });
        }

        private static bool _hasNotify;

        private static void StayConnection()
        {
            Task.Run(() =>
            {
                while (!IsExited)
                {
                    SendData("@", string.Empty);
                    Thread.Sleep(30000);
                    while (_isReceiving)
                        Thread.Sleep(5000);
                    if (_isConnecting)
                    {
                        _isConnecting = false;
                        _hasNotify = false;
                        continue;
                    }
                    _updateMainPage.Invoke($"Connection{Divpar}Break");
                    if (!_hasNotify)
                    {
                        MessageBox.Show("与服务端的连接已断开", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        _hasNotify = true;
                    }
                    break;
                }
                Thread.Sleep(3000);
                Connect(_ip, _port);
            });
        }

        #endregion
    }
}