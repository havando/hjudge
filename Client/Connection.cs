﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HPSocketCS;
using Newtonsoft.Json;

namespace Client
{
    public static partial class Connection
    {
        public const string Divpar = "<h~|~j>";
        private static readonly ConcurrentQueue<List<byte>> Recv = new ConcurrentQueue<List<byte>>();
        private static readonly ConcurrentQueue<ObjOperation> Operations = new ConcurrentQueue<ObjOperation>();
        private static readonly TcpPullClient HClient = new TcpPullClient();
        private static bool _isConnecting;
        private static bool _isConnected;
        private static bool _isReceivingData;
        private static bool _isSendingData;
        public static bool IsExited;
        public static Action<string> UpdateMainPage;
        private static readonly int PkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        private static readonly List<FileRecvInfo> FrInfo = new List<FileRecvInfo>();
        private static readonly PkgInfo PkgInfo = new PkgInfo();
        private static int _id;
        private static string _ip;
        private static ushort _port;
        public static bool CanSwitch = true;

        public static bool Init(string ip, ushort port, Action<string> updateMainPage)
        {
            UpdateMainPage = updateMainPage;
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
                    Thread.Sleep(5000);
                } while (!_isConnected);
                new Thread(StayConnection).Start();
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
            _isReceivingData = true;
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
                            _isReceivingData = false;
                            UpdateMainPage.Invoke($"ReceivingFile{Divpar}Done");
                            Recv.Enqueue(buffer.ToList());
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
                    if (bufferPtr != IntPtr.Zero)
                        Marshal.FreeHGlobal(bufferPtr);
                }
            }
            return HandleResult.Ok;
        }

        #region Network


        public static void SendFile(string fileName, string title)
        {
            _isSendingData = true;
            var fileId = Guid.NewGuid().ToString();
            var temp = Encoding.Unicode.GetBytes(title + Divpar
                                                 + Path.GetFileName(fileName) + Divpar
                                                 + fileId + Divpar
                                                 + new FileInfo(fileName).Length);
            var final = GetSendBuffer(temp);
            HClient.Send(final, final.Length);
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long tot = 0;
                while (tot != fs.Length)
                {
                    var bytes = new byte[131072];
                    long cnt = fs.Read(bytes, 0, 131072);
                    var tempc = GetSendBuffer(Encoding.Unicode.GetBytes(title + Divpar
                                                                        + Path.GetFileName(fileName) + Divpar
                                                                        + fileId + Divpar + tot + Divpar)
                        .Concat(bytes.Take((int)cnt)).ToArray());
                    tot += cnt;
                    HClient.Send(tempc, tempc.Length);
                }
                fs.Close();
            }
            _isSendingData = false;
        }

        public static void SendData(string operation, IEnumerable<byte> sendBytes)
        {
            _isSendingData = true;
            var temp = Encoding.Unicode.GetBytes(operation);
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
            temp = temp.Concat(sendBytes).ToArray();
            var final = GetSendBuffer(temp);
            HClient.Send(final, final.Length);
            _isSendingData = false;
        }

        public static void SendData(string operation, string sendString)
        {
            _isSendingData = true;
            var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString);
            var final = GetSendBuffer(temp);
            HClient.Send(final, final.Length);
            _isSendingData = false;
        }

        public static void SendMsg(string sendString, string targetUser)
        {
            var t = new Message { Content = sendString, Direction = "发送", MessageTime = DateTime.Now, User = targetUser };
            SendData("Messaging", JsonConvert.SerializeObject(t));
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
                    if (Recv.TryDequeue(out var temp))
                    {
                        if (IsExited) break;
                        var temp2 = Bytespilt(temp, Encoding.Unicode.GetBytes(Divpar));
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
                    Thread.Sleep(1);
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
                            try
                            {
                                if (Encoding.Unicode.GetString(res.Content[0]) == "ActionFailed")
                                {
                                    MessageBox.Show($"抱歉，程序异常，请重新启动本客户端。\n因为 {Encoding.Unicode.GetString(res.Content[1])}\n操作：{res.Operation}\n堆栈跟踪：\n{Encoding.Unicode.GetString(res.Content[2])}", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                                    continue;
                                }
                            }
                            catch
                            {
                                //ignored
                            }
                            var content = string.Empty;
                            try
                            {
                                for (var i = 0; i < res.Content.Count; i++)
                                    if (i != res.Content.Count - 1)
                                        content += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                    else
                                        content += Encoding.Unicode.GetString(res.Content[i]);
                            }
                            catch
                            {
                                //ignored
                            }
                            switch (res.Operation)
                            {
                                case "Logout":
                                    {
                                        while (Recv.TryDequeue(out var temp)) { temp.Clear(); }
                                        UpdateMainPage.Invoke($"Logout{Divpar}Succeed");
                                        break;
                                    }
                                case "ProblemDataSet":
                                    {
                                        if (Encoding.Unicode.GetString(res.Content[0]) == "Denied")
                                        {
                                            UpdateMainPage.Invoke($"ProblemDataSet{Divpar}Denied");
                                            break;
                                        }
                                        UpdateMainPage.Invoke($"ProblemDataSet{Divpar}Accepted");
                                        var problemId = Encoding.Unicode.GetString(res.Content[0]);
                                        var fileName = $"{problemId}_{DateTime.Now:yyyyMMddHHmmssffff}.zip";
                                        File.WriteAllBytes(
                                            $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}",
                                            res.Content[1]);
                                        Process.Start("explorer.exe",
                                            $"/select,\"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}\"");
                                        break;
                                    }
                                case "File":
                                    {
                                        var fileName = Encoding.Unicode.GetString(res.Content[0]);
                                        if (fileName == "NotFound")
                                        {
                                            UpdateMainPage($"FileReceived{Divpar}Error");
                                            continue;
                                        }
                                        var fileId = Encoding.Unicode.GetString(res.Content[1]);
                                        var length = Convert.ToInt64(Encoding.Unicode.GetString(res.Content[2]));
                                        if (FrInfo.Any(i => i.FileId == fileId))
                                        {
                                            var fs = FrInfo.FirstOrDefault(i => i.FileId == fileId);
                                            var x = new List<byte>();
                                            for (var i = 3; i < res.Content.Count; i++)
                                                if (i != res.Content.Count - 1)
                                                {
                                                    x.AddRange(res.Content[i]);
                                                    x.AddRange(Encoding.Unicode.GetBytes(Divpar));
                                                }
                                                else
                                                {
                                                    x.AddRange(res.Content[i]);
                                                }
                                            fs.Fs.Position = length;
                                            fs.Fs.Write(x.ToArray(), 0, x.Count);
                                            fs.CurrentLength += x.Count;
                                            if (fs.CurrentLength >= fs.TotLength)
                                            {
                                                fs.Fs.Close();
                                                fs.Fs.Dispose();
                                                FrInfo.Remove(fs);
                                                Process.Start("explorer.exe",
                                                    $"/select,\"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}\"");
                                                UpdateMainPage($"FileReceived{Divpar}Done");
                                            }
                                            else
                                            {
                                                UpdateMainPage(
                                                    $"FileReceiving{Divpar}{Math.Round((double)fs.CurrentLength * 100 / fs.TotLength, 1)} %");
                                            }
                                        }
                                        else
                                        {
                                            FrInfo.Add(new FileRecvInfo
                                            {
                                                CurrentLength = 0,
                                                FileId = fileId,
                                                FileName = fileName,
                                                Fs = new FileStream(
                                                    $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}",
                                                    FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite),
                                                TotLength = length
                                            });
                                        }
                                        break;
                                    }
                                case "ChangePassword":
                                    {
                                        UpdateMainPage.Invoke(
                                            $"ChangePassword{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");

                                        break;
                                    }
                                case "UpdateProfile":
                                    {
                                        UpdateMainPage.Invoke(
                                            $"UpdateProfile{Divpar}{Encoding.Unicode.GetString(res.Content[0])}");
                                        break;
                                    }
                                case "AddProblem":
                                    {
                                        _addProblemResult = JsonConvert.DeserializeObject<Problem>(content);
                                        _addProblemState = true;
                                        break;
                                    }
                                case "DeleteProblem":
                                    {
                                        _deleteProblemResult = true;
                                        break;
                                    }
                                case "UpdateProblem":
                                    {
                                        _updateProblemResult = true;
                                        break;
                                    }
                                case "QueryProblems":
                                    {
                                        _queryProblemsResult = JsonConvert.DeserializeObject<ObservableCollection<Problem>>(content);
                                        _queryProblemsResultState = true;
                                        break;
                                    }
                                case "QueryJudgeLogs":
                                    {
                                        _queryJudgeLogResult = JsonConvert.DeserializeObject<ObservableCollection<JudgeInfo>>(content);
                                        _queryJudgeLogResultState = true;
                                        break;
                                    }
                                case "DataFile":
                                case "PublicFile":
                                    {
                                        switch (Encoding.Unicode.GetString(res.Content[0]))
                                        {
                                            case "Succeeded":
                                                {
                                                    MessageBox.Show("上传成功", "提示", MessageBoxButton.OK,
                                                        MessageBoxImage.Information);
                                                    break;
                                                }
                                            default:
                                                {
                                                    MessageBox.Show("上传失败，可能因为已有同名文件存在", "提示", MessageBoxButton.OK,
                                                        MessageBoxImage.Error);
                                                    break;
                                                }
                                        }
                                        UploadFileResult = true;
                                        break;
                                    }
                                case "RequestCode":
                                    {
                                        _getJudgeCodeResult = JsonConvert.DeserializeObject<JudgeInfo>(content);
                                        _getJudgeCodeState = true;
                                        break;
                                    }
                                case "GetProblemDescription":
                                    {
                                        _detailsProblemResult = JsonConvert.DeserializeObject<Problem>(content)?.Description ?? string.Empty;
                                        _detailsProblemState = true;
                                        break;
                                    }
                                case "QueryJudgeLogBelongsToCompetition":
                                    {
                                        foreach (var i in JsonConvert.DeserializeObject<List<JudgeInfo>>(content))
                                        {
                                            _queryJudgeLogBelongsToCompetitionResult.Add(i);
                                        }
                                        _queryJudgeLogBelongsToCompetitionState = true;
                                        break;
                                    }
                                case "QueryProblemsForCompetition":
                                    {
                                        foreach (var i in JsonConvert.DeserializeObject<List<Problem>>(content))
                                        {
                                            _queryProblemsForCompetitionResult.Add(i);
                                        }
                                        _queryProblemsForCompetitionState = true;
                                        break;
                                    }
                                case "QueryLanguagesForCompetition":
                                    {
                                        _queryLanguagesForCompetitionResult = JsonConvert.DeserializeObject<List<Compiler>>(content);
                                        _queryLanguagesForCompetitionState = true;
                                        break;
                                    }
                                case "GetCurrentDateTime":
                                    {
                                        _getCurrentDateTimeResult = Convert.ToDateTime(content);
                                        _getCurrentDateTimeState = true;
                                        break;
                                    }
                                case "QueryCompetitionClient":
                                    {
                                        _queryCompetitionResult = JsonConvert.DeserializeObject<List<Competition>>(content);
                                        _queryCompetitionState = true;
                                        break;
                                    }
                                case "NewCompetitionClient":
                                    {
                                        _newCompetitionResult = JsonConvert.DeserializeObject<Competition>(content);
                                        _newCompetitionState = true;
                                        break;
                                    }
                                case "DeleteCompetitionClient":
                                    {
                                        _deleteCompetitionState = true;
                                        break;
                                    }
                                case "GetProblemClient":
                                    {
                                        _getProblemResult = JsonConvert.DeserializeObject<Problem>(content);
                                        _getProblemState = true;
                                        break;
                                    }
                                case "UpdateCompetitionClient":
                                    {
                                        _updateCompetitionState = true;
                                        break;
                                    }
                                default:
                                    {
                                        UpdateMainPage.Invoke(
                                             $"{res.Operation}{Divpar}{content}");
                                        break;
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                if (Encoding.Unicode.GetString(res.Content[0]) == "ActionFailed")
                                {
                                    MessageBox.Show($"抱歉，命令解析出现错误，请重新启动本客户端。\n因为 {ex.Message}\n操作：{res.Operation}\n堆栈跟踪：\n{ex.StackTrace}", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            catch
                            {
                                //ignored
                            }
                        }
                    Thread.Sleep(1);
                }
            });
        }

        private static bool _hasNotify;

        private static void StayConnection()
        {
            var cnt = 0;
            while (!IsExited)
            {
                SendData("@", string.Empty);
                Thread.Sleep(10000);
                while (_isReceivingData || _isSendingData)
                    Thread.Sleep(5000);
                if (_isConnecting)
                {
                    cnt = 0;
                    _isConnecting = false;
                    _hasNotify = false;
                    continue;
                }
                else
                {
                    cnt++;
                    if (cnt <= 3) continue;
                    else cnt = 0;
                }
                UpdateMainPage.Invoke($"Connection{Divpar}Break");
                if (!_hasNotify)
                {
                    MessageBox.Show("与服务端的连接已断开", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                    _hasNotify = true;
                }
                break;
            }
            Thread.Sleep(1000);
            Connect(_ip, _port);
        }

        #endregion
    }
}