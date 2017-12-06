using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HPSocketCS;
using Ionic.Zip;
using Newtonsoft.Json;

namespace Client
{
    public static partial class Connection
    {
        public const string Divpar = "<h~|~j>";
        private static readonly ConcurrentQueue<List<byte>> Recv = new ConcurrentQueue<List<byte>>();
        private static readonly ConcurrentQueue<ObjOperation> Operations = new ConcurrentQueue<ObjOperation>();
        private static readonly TcpPullClient HClient = new TcpPullClient();
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
            _ip = ip;
            _port = port;
            UpdateMainPage = updateMainPage;
            HClient.OnConnect += sender =>
            {
                updateMainPage.Invoke($"Connection{Divpar}Connected");
                return HandleResult.Ok;
            };
            HClient.OnReceive += HClientOnOnReceive;
            HClient.OnClose += (sender, enOperation, errorCode) =>
            {
                updateMainPage.Invoke($"Connection{Divpar}Break");
                return HandleResult.Ok;
            };
            Connect(ip, port);
            new Thread(DealingBytes).Start();
            new Thread(DealingOperations).Start();

            return true;
        }

        public static void ReConnect()
        {
            Connect(_ip, _port);
        }

        private static void Connect(string ip, ushort port)
        {
            Task.Run(() =>
            {
                while (!HClient.Connect(ip, port, false))
                {
                    if (IsExited) break;
                    Thread.Sleep(1000);
                }
            });
        }

        public static byte[] CompressBytes(byte[] bytes)
        {
            using (MemoryStream compressStream = new MemoryStream())
            {
                using (var zipStream = new GZipStream(compressStream, CompressionMode.Compress))
                    zipStream.Write(bytes, 0, bytes.Length);
                return compressStream.ToArray();
            }
        }

        public static byte[] Decompress(byte[] bytes)
        {
            using (var compressStream = new MemoryStream(bytes))
            {
                using (var zipStream = new GZipStream(compressStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        zipStream.CopyTo(resultStream);
                        return resultStream.ToArray();
                    }
                }
            }
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
                            Recv.Enqueue(Decompress(buffer).ToList());
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
            var fileId = Guid.NewGuid().ToString();
            var temp = Encoding.Unicode.GetBytes(title + Divpar
                                                 + Path.GetFileName(fileName) + Divpar
                                                 + fileId + Divpar
                                                 + new FileInfo(fileName).Length);
            var final = GetSendBuffer(CompressBytes(temp));
            HClient.Send(final, final.Length);
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long tot = 0;
                while (tot != fs.Length)
                {
                    var bytes = new byte[131072];
                    long cnt = fs.Read(bytes, 0, 131072);
                    var tempc = GetSendBuffer(CompressBytes(Encoding.Unicode.GetBytes(title + Divpar
                                                                        + Path.GetFileName(fileName) + Divpar
                                                                        + fileId + Divpar + tot + Divpar)
                        .Concat(bytes.Take((int)cnt)).ToArray()));
                    tot += cnt;
                    HClient.Send(tempc, tempc.Length);
                }
                fs.Close();
            }
        }

        public static void SendData(string operation, IEnumerable<byte> sendBytes)
        {
            var temp = Encoding.Unicode.GetBytes(operation);
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
            temp = temp.Concat(sendBytes).ToArray();
            var final = GetSendBuffer(CompressBytes(temp));
            HClient.Send(final, final.Length);
        }

        public static void SendData(string operation, string sendString)
        {
            var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString);
            var final = GetSendBuffer(CompressBytes(temp));
            HClient.Send(final, final.Length);
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
            while (!IsExited)
            {
                if (Recv.TryDequeue(out var temp))
                {
                    if (IsExited) break;
                    try
                    {
                        var temp2 = Bytespilt(temp, Encoding.Unicode.GetBytes(Divpar));
                        if (temp2.Count == 0)
                            continue;
                        var operation = Encoding.Unicode.GetString(temp2[0]);
                        temp2.RemoveAt(0);
                        Operations.Enqueue(new ObjOperation
                        {
                            Operation = operation,
                            Content = temp2
                        });
                    }
                    catch
                    {
                        //ignored
                    }
                }
                Thread.Sleep(1);
            }
        }

        private static void DealingOperations()
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
                                MessageBox.Show(
                                    $"抱歉，程序异常，请重新启动本客户端。\n因为 {Encoding.Unicode.GetString(res.Content[1])}\n操作：{res.Operation}\n堆栈跟踪：\n{Encoding.Unicode.GetString(res.Content[2])}",
                                    "提示", MessageBoxButton.OK, MessageBoxImage.Error);
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
                                    while (Recv.TryDequeue(out var temp)) temp.Clear();
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
                            case "RequestClient":
                            case "File":
                                {
                                    var fileName = Encoding.Unicode.GetString(res.Content[0]);
                                    if (fileName == "NotFound" && res.Operation == "File")
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
                                            if (res.Operation == "File")
                                            {
                                                Process.Start("explorer.exe",
                                                    $"/select,\"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}\"");
                                                UpdateMainPage($"FileReceived{Divpar}Done");
                                            }
                                            if (res.Operation == "RequestClient")
                                            {
                                                using (var zFile = new ZipFile(
                                                    $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\{fileName}")
                                                )
                                                {
                                                    if (!Directory.Exists(Environment.GetEnvironmentVariable("temp") +
                                                                          "\\hjudgeClientUpdates"))
                                                    {
                                                        Directory.CreateDirectory(
                                                            Environment.GetEnvironmentVariable("temp") +
                                                            "\\hjudgeClientUpdates");
                                                    }
                                                    else
                                                    {
                                                        Directory.Delete(Environment.GetEnvironmentVariable("temp") +
                                                                         "\\hjudgeClientUpdates", true);
                                                        Directory.CreateDirectory(
                                                            Environment.GetEnvironmentVariable("temp") +
                                                            "\\hjudgeClientUpdates");
                                                    }
                                                    zFile.ExtractAll(
                                                        Environment.GetEnvironmentVariable("temp") +
                                                        "\\hjudgeClientUpdates",
                                                        ExtractExistingFileAction.OverwriteSilently);
                                                    var batchCode = $@"@echo off
title hjudge Updater
color 3F
cls
echo.
echo  Updating hjudge Client, please wait...
echo.
echo    [DO NOT CLOSE THIS WINDOW]
ping /n 10 0.0.0.0>nul
xcopy /y /h /r /e /i ""{Environment.GetEnvironmentVariable("temp") + "\\hjudgeClientUpdates"}"" ""{AppDomain.CurrentDomain.BaseDirectory}"" 1>nul 2>nul
rd /s /q ""{Environment.GetEnvironmentVariable("temp") + "\\hjudgeClientUpdates"}"" 1>nul 2>nul
cls
echo.
echo  Finish.
ping /n 2 0.0.0.0>nul
start """" ""{AppDomain.CurrentDomain.BaseDirectory}\Client.exe""
exit
";
                                                    File.WriteAllText(Environment.GetEnvironmentVariable("temp") + "\\hjudgeClientUpdater.bat", batchCode, Encoding.Default);
                                                    Process.Start(
                                                        Environment.GetEnvironmentVariable("temp") +
                                                        "\\hjudgeClientUpdater.bat");
                                                    IsExited = true;
                                                    Environment.Exit(0);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            if (res.Operation == "File")
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
                                    _queryProblemsResult =
                                        JsonConvert.DeserializeObject<ObservableCollection<Problem>>(content);
                                    _queryProblemsResultState = true;
                                    break;
                                }
                            case "QueryJudgeLogs":
                                {
                                    _queryJudgeLogResult =
                                        JsonConvert.DeserializeObject<ObservableCollection<JudgeInfo>>(content);
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
                                    _detailsProblemResult =
                                        JsonConvert.DeserializeObject<Problem>(content)?.Description ?? string.Empty;
                                    _detailsProblemState = true;
                                    break;
                                }
                            case "QueryJudgeLogBelongsToCompetition":
                                {
                                    foreach (var i in JsonConvert.DeserializeObject<List<JudgeInfo>>(content))
                                        QueryJudgeLogBelongsToCompetitionResult.Add(i);
                                    _queryJudgeLogBelongsToCompetitionState = true;
                                    break;
                                }
                            case "QueryProblemsForCompetition":
                                {
                                    foreach (var i in JsonConvert.DeserializeObject<List<Problem>>(content))
                                        QueryProblemsForCompetitionResult.Add(i);
                                    _queryProblemsForCompetitionState = true;
                                    break;
                                }
                            case "QueryLanguagesForCompetition":
                                {
                                    _queryLanguagesForCompetitionResult =
                                        JsonConvert.DeserializeObject<List<Compiler>>(content);
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
                            case "GetProblem":
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
                            case "GetServerConfig":
                                {
                                    _getServerConfigResult = JsonConvert.DeserializeObject<ServerConfig>(content);
                                    _getServerConfigState = true;
                                    break;
                                }
                            case "UpdateServerConfig":
                                {
                                    _updateServerConfigState = true;
                                    break;
                                }
                            case "GetUserBelongings":
                                {
                                    _getUserBelongingsType = Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0]));
                                    var x = string.Empty;
                                    for (var i = 1; i < res.Content.Count; i++)
                                        if (i != res.Content.Count - 1)
                                            x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                        else
                                            x += Encoding.Unicode.GetString(res.Content[i]);
                                    _getUserBelongingsResult = JsonConvert.DeserializeObject<List<UserInfo>>(x);
                                    _getUserBelongingsState = true;
                                    break;
                                }
                            case "UpdateUserBelongings":
                                {
                                    _updateUserBelongingsState = true;
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
                                MessageBox.Show(
                                    $"抱歉，命令解析出现错误，请重新启动本客户端。\n因为 {ex.Message}\n操作：{res.Operation}\n堆栈跟踪：\n{ex.StackTrace}",
                                    "提示", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        catch
                        {
                            //ignored
                        }
                    }
                Thread.Sleep(1);
            }
        }
        #endregion
    }
}