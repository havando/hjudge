using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HPSocketCS;
using Newtonsoft.Json;

namespace Server
{
    public static partial class Connection
    {
        private static readonly int PkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        private static readonly ConcurrentBag<ClientData> Recv = new ConcurrentBag<ClientData>();
        private static readonly ConcurrentQueue<(ObjOperation obj, string token)> Operations = new ConcurrentQueue<(ObjOperation obj, string token)>();
        private static readonly TcpPullServer<ClientInfo> HServer = new TcpPullServer<ClientInfo>();
        private static readonly List<FileRecvInfo> FrInfo = new List<FileRecvInfo>();

        private static List<string> SearchFiles(string path)
        {
            var a = Directory.GetDirectories(path).Select(Path.GetFileName).ToList();
            a.Add("|");
            a.AddRange(Directory.GetFiles(path).Where(i => new FileInfo(i).Length <= 512 * 1048576)
                .Select(Path.GetFileName));
            return a;
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

        private static string CharArrayToString(char[] array)
        {
            var res = new StringBuilder();
            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] != '\0')
                {
                    res.Append(array[i]);
                }
            }
            return res.ToString();
        }

        private static byte[] GetSendBuffer(byte[] bodyBytes, string token)
        {
            var header = new PkgHeader
            {
                Id = ++_id,
                BodySize = bodyBytes.Length
            };
            var p = token?.ToCharArray() ?? new char[0];
            header.Token = new char[32];
            var j = 0;
            for (var i = 0; i < p.Length; i++)
            {
                if (p[i] != '-')
                    header.Token[j++] = p[i];
            }
            var headerBytes = HServer.StructureToByte(header);
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

        public static void LogoutAll()
        {
            foreach (var i in Recv)
                if (i.Info.UserId != 0)
                {
                    i.Info.UserId = 0;
                    while (i.Data.TryDequeue(out var temp)) temp.Content.Clear();
                    HServer.Disconnect(i.Info.ConnId);
                }
        }

        private static void SendData(string operation, IEnumerable<byte> sendBytes, IntPtr connId, string token)
        {
            var temp = Encoding.Unicode.GetBytes(operation);
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
            temp = temp.Concat(sendBytes).ToArray();
            var final = GetSendBuffer(CompressBytes(temp), token);
            HServer.Send(connId, final, final.Length);
        }

        private static void SendFile(string fileName, IntPtr connId, string title, string token)
        {
            if (!File.Exists(fileName))
                SendData(title, "NotFound", connId, token);
            var fileId = Guid.NewGuid().ToString();
            var temp = Encoding.Unicode.GetBytes(title + Divpar
                                                 + Path.GetFileName(fileName) + Divpar
                                                 + fileId + Divpar
                                                 + new FileInfo(fileName).Length);
            var final = GetSendBuffer(CompressBytes(temp), token);
            HServer.Send(connId, final, final.Length);
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long tot = 0;
                while (tot != fs.Length)
                {
                    var bytes = new byte[131072];
                    long cnt = fs.Read(bytes, 0, 131072);
                    var tempc = GetSendBuffer(CompressBytes((Encoding.Unicode.GetBytes(title + Divpar
                                                                        + Path.GetFileName(fileName) + Divpar
                                                                        + fileId + Divpar + tot + Divpar)
                        .Concat(bytes.Take((int)cnt)).ToArray())), token);
                    tot += cnt;
                    HServer.Send(connId, tempc, tempc.Length);
                }
                fs.Close();
            }
        }

        private static void SendData(string operation, string sendString, IntPtr connId, string token)
        {
            var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString);
            var final = GetSendBuffer(CompressBytes(temp), token);
            HServer.Send(connId, final, final.Length);
        }

        private static void SetMsgState(int msgId, int state)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();
                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "UPDATE Message SET State=@1 Where MessageId=@2";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.Int32),
                            new SQLiteParameter("@2", DbType.Int32)
                        };
                        parameters[0].Value = state;
                        parameters[1].Value = msgId;
                        cmd.Parameters.AddRange(parameters);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void SendMsg(string sendString, int fromUserId, int toUserId, string token)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();
                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText =
                            "Insert INTO Message (FromUserId,ToUserId,SendDate,Content,State) VALUES (@1,@2,@3,@4,@5)";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.Int32),
                            new SQLiteParameter("@2", DbType.Int32),
                            new SQLiteParameter("@3", DbType.String),
                            new SQLiteParameter("@4", DbType.String),
                            new SQLiteParameter("@5", DbType.Int32)
                        };
                        parameters[0].Value = fromUserId;
                        parameters[1].Value = toUserId;
                        parameters[2].Value = DateTime.Now;
                        parameters[3].Value = sendString;
                        parameters[4].Value = 0;
                        cmd.Parameters.AddRange(parameters);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            var t = new Message
            {
                Content = sendString,
                MessageTime = DateTime.Now,
                Direction = "接收",
                User = GetUserName(fromUserId),
                State = 0
            };
            SendData("Messaging", JsonConvert.SerializeObject(t),
                Recv.Where(i => i.Info.UserId == toUserId).Select(p => p.Info.ConnId).FirstOrDefault(), token);
        }

        private static string _token = string.Empty;
        private static HandleResult HServerOnOnReceive(IntPtr connId, int length)
        {
            var clientInfo = HServer.GetExtra(connId);
            if (clientInfo == null)
                return HandleResult.Error;
            var myPkgInfo = clientInfo.PkgInfo;
            var required = myPkgInfo.Length;
            var remain = length;
            while (remain >= required)
            {
                var bufferPtr = IntPtr.Zero;
                try
                {
                    remain -= required;
                    bufferPtr = Marshal.AllocHGlobal(required);
                    if (HServer.Fetch(connId, bufferPtr, required) == FetchResult.Ok)
                    {
                        if (myPkgInfo.IsHeader)
                        {
                            var header = (PkgHeader)Marshal.PtrToStructure(bufferPtr, typeof(PkgHeader));
                            required = header.BodySize;
                            _token = CharArrayToString(header.Token);
                        }
                        else
                        {
                            var buffer = new byte[required];
                            Marshal.Copy(bufferPtr, buffer, 0, required);
                            required = PkgHeaderSize;
                            (from c in Recv where c.Info.ConnId == connId select c).FirstOrDefault()?.Data
                                .Enqueue((Decompress(buffer).ToList(), _token));
                        }
                        myPkgInfo.IsHeader = !myPkgInfo.IsHeader;
                        myPkgInfo.Length = required;
                        if (HServer.SetExtra(connId, clientInfo) == false)
                            return HandleResult.Error;
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
                Parallel.ForEach(Recv, (t, state) =>
                {
                    if (IsExited) state.Stop();
                    try
                    {
                        while (t.Data.TryDequeue(out var temp))
                        {
                            var temp2 = Bytespilt(temp.Content, Encoding.Unicode.GetBytes(Divpar));
                            if (temp2.Count == 0)
                                continue;
                            var operation = Encoding.Unicode.GetString(temp2[0]);
                            temp2.RemoveAt(0);
                            Operations.Enqueue((new ObjOperation
                            {
                                Operation = operation,
                                Client = t.Info,
                                Content = temp2
                            }, temp.Token));
                        }
                    }
                    catch
                    {
                        //ignored
                    }
                });
                Thread.Sleep(1);
            }
        }

        private static void DealingOperations()
        {
            while (!IsExited)
            {
                if (Operations.TryDequeue(out var res))
                {
                    var u = Recv.FirstOrDefault(c => c.Info.ConnId == res.obj.Client.ConnId);
                    if (u == null) continue;
                    try
                    {
                        switch (res.obj.Operation)
                        {
                            case "Login":
                                {
                                    var x = RemoteLogin(Encoding.Unicode.GetString(res.obj.Content[0]),
                                        Encoding.Unicode.GetString(res.obj.Content[1]));
                                    switch (x)
                                    {
                                        case 0:
                                            {
                                                var uid = GetUserId(
                                                    Encoding.Unicode.GetString(res.obj.Content[0]));
                                                //foreach (var li in Recv.Where(c => c.Info.UserId == uid))
                                                //{
                                                //    UpdateMainPageState(
                                                //        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {li.Info.UserName} 多终端登陆，已注销其中一个终端的登录状态");
                                                //    li.Info.UserId = 0;
                                                //    while (li.Data.TryDequeue(out var temp)) temp.Content.Clear();
                                                //    SendData("Logout", "Succeed", li.Info.ConnId, res.token);
                                                //}
                                                res.obj.Client.UserId = uid;
                                                SendData("Login", "Succeed", res.obj.Client.ConnId, res.token);
                                                UpdateMainPageState(
                                                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.obj.Client.UserName} 登录了");
                                                break;
                                            }
                                        case 1:
                                            {
                                                SendData("Login", "Incorrect", res.obj.Client.ConnId, res.token);
                                                break;
                                            }
                                        case 3:
                                            {
                                                SendData("Login", "NeedReview", res.obj.Client.ConnId, res.token);
                                                break;
                                            }
                                        default:
                                            {
                                                SendData("Login", "Unknown", res.obj.Client.ConnId, res.token);
                                                break;
                                            }
                                    }
                                    break;
                                }
                            case "Logout":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    UpdateMainPageState(
                                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.obj.Client.UserName} 注销了");
                                    while (u.Data.TryDequeue(out var temp)) temp.Content.Clear();
                                    res.obj.Client.UserId = 0;
                                    SendData("Logout", "Succeed", res.obj.Client.ConnId, res.token);
                                    break;
                                }
                            case "Register":
                                {
                                    if (res.obj.Client.UserId != 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    Task.Run(() =>
                                    {
                                        if (RemoteRegister(Encoding.Unicode.GetString(res.obj.Content[0]),
                                            Encoding.Unicode.GetString(res.obj.Content[1])))
                                            SendData("Register",
                                                Configuration.Configurations.RegisterMode == 2
                                                    ? "Succeeded"
                                                    : "NeedReview", res.obj.Client.ConnId, res.token);
                                        else
                                            SendData("Register",
                                                Configuration.Configurations.RegisterMode == 0
                                                    ? "Failed"
                                                    : "Duplicate", res.obj.Client.ConnId, res.token);
                                    });
                                    break;
                                }
                            case "RequestFileList":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var filePath = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    if (filePath.Length > 1)
                                    {
                                        if (filePath.Substring(0, 1) == "\\")
                                            filePath = filePath.Substring(1);
                                        if (filePath.Substring(filePath.Length - 1) == "\\")
                                            filePath = filePath.Substring(filePath.Length - 1);
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = SearchFiles(
                                            AppDomain.CurrentDomain.BaseDirectory + "\\Files" +
                                            (string.IsNullOrEmpty(filePath) ? string.Empty : $"\\{filePath}")
                                        );
                                        var y = string.Empty;
                                        for (var i = 0; i < x.Count; i++)
                                            if (i != x.Count - 1)
                                                y += x[i] + Divpar;
                                            else
                                                y += x[i];
                                        SendData("FileList",
                                            filePath + Divpar + y,
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestFile":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var filePath = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    if (filePath.Length > 1)
                                    {
                                        if (filePath.Substring(0, 1) == "\\")
                                            filePath = filePath.Substring(1);
                                        if (filePath.Substring(filePath.Length - 1) == "\\")
                                            filePath = filePath.Substring(0, filePath.Length - 1);
                                    }
                                    filePath = AppDomain.CurrentDomain.BaseDirectory + "\\Files\\" + filePath;
                                    if (File.Exists(filePath))
                                        UpdateMainPageState(
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.obj.Client.UserName} 请求文件：{filePath}");
                                    Task.Run(() => { SendFile(filePath, res.obj.Client.ConnId, "File", res.token); });
                                    break;
                                }
                            case "RequestProblemDataSet":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    if (!Configuration.Configurations.AllowRequestDataSet)
                                    {
                                        SendData("ProblemDataSet", "Denied", res.obj.Client.ConnId, res.token);
                                    }
                                    else
                                    {
                                        UpdateMainPageState(
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.obj.Client.UserName} 请求题目 {GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])))} 的数据");

                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            try
                                            {
                                                var problem =
                                                    GetProblem(Convert.ToInt32(
                                                        Encoding.Unicode.GetString(res.obj.Content[0])));

                                                string GetEngName(string origin)
                                                {
                                                    var re = new Regex("[A-Z]|[a-z]|[0-9]");
                                                    return re.Matches(origin).Cast<object>().Aggregate(string.Empty,
                                                        (current, t) => current + t);
                                                }

                                                string GetRealString(string origin, string problemName, int cur)
                                                {
                                                    return origin
                                                        .Replace("${datadir}",
                                                            AppDomain.CurrentDomain.BaseDirectory + "\\Data")
                                                        .Replace("${name}", GetEngName(problemName))
                                                        .Replace("${index0}", cur.ToString())
                                                        .Replace("${index}", (cur + 1).ToString());
                                                }

                                                var ms = new MemoryStream();
                                                using (var zip = new Ionic.Zip.ZipFile())
                                                {
                                                    for (var i = 0; i < problem.DataSets.Length; i++)
                                                    {
                                                        var inputName =
                                                            GetRealString(problem.DataSets[i].InputFile,
                                                                problem.ProblemName, i);
                                                        var outputName =
                                                            GetRealString(problem.DataSets[i].OutputFile,
                                                                problem.ProblemName, i);
                                                        var inputFilePath = inputName.Replace(
                                                            AppDomain.CurrentDomain.BaseDirectory,
                                                            string.Empty);
                                                        inputFilePath = inputFilePath.Substring(0,
                                                                            inputFilePath.LastIndexOf("\\",
                                                                                StringComparison.Ordinal)) + "\\" +
                                                                        (i + 1);
                                                        var outputFilePath = outputName.Replace(
                                                            AppDomain.CurrentDomain.BaseDirectory,
                                                            string.Empty);
                                                        outputFilePath = outputFilePath.Substring(0,
                                                                             outputFilePath.LastIndexOf("\\",
                                                                                 StringComparison.Ordinal)) + "\\" +
                                                                         (i + 1);
                                                        if (File.Exists(inputName))
                                                            zip.AddFile(inputName,
                                                                inputFilePath);
                                                        if (File.Exists(outputName))
                                                            zip.AddFile(outputName,
                                                                outputFilePath);
                                                    }
                                                    zip.Save(ms);
                                                }
                                                var x = new List<byte>();
                                                x.AddRange(Encoding.Unicode.GetBytes(
                                                    problem.ProblemId + Divpar));
                                                x.AddRange(ms.ToArray());
                                                SendData("ProblemDataSet", x
                                                    , res.obj.Client.ConnId, res.token);
                                            }
                                            catch
                                            {
                                                //ignored
                                            }
                                        }));
                                    }
                                    break;
                                }
                            case "SubmitCode":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    if (!string.IsNullOrEmpty(Encoding.Unicode.GetString(res.obj.Content[2])))
                                    {
                                        UpdateMainPageState(
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.obj.Client.UserName} 提交了题目 {GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])))} 的代码");

                                        var code = string.Empty;
                                        for (var i = 2; i < res.obj.Content.Count; i++)
                                            if (i != res.obj.Content.Count - 1)
                                                code += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                            else
                                                code += Encoding.Unicode.GetString(res.obj.Content[i]);
                                        var problemId = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                        var type = Encoding.Unicode.GetString(res.obj.Content[1]);
                                        var userId = res.obj.Client.UserId;
                                        ActionList.Enqueue(new Task(() =>
                                            new Thread(() =>
                                            {
                                                var j = new Judge(problemId, userId, code, type, true, "在线评测", null, 0, (jid) => { SendData("JudgeId", JsonConvert.SerializeObject(new JudgeInfo { JudgeId = jid, ProblemId = problemId, UserId = res.obj.Client.UserId, Code = code, CompetitionId = 0 }), res.obj.Client.ConnId, res.token); });
                                                var jr = JsonConvert.SerializeObject(j.JudgeResult);
                                                SendData("JudgeResult", jr, res.obj.Client.ConnId, res.token);
                                            }).Start()));
                                    }
                                    break;
                                }
                            case "Messaging":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var x = string.Empty;
                                    for (var i = 0; i < res.obj.Content.Count; i++)
                                        if (i != res.obj.Content.Count - 1)
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                        else
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]);
                                    var t = JsonConvert.DeserializeObject<Message>(x);
                                    UpdateMainPageState(
                                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.obj.Client.UserName} 向 {t.User} 发送了消息");
                                    if (t.User == GetUserName(1))
                                    {
                                        using (DataBaseLock.Write())
                                        {
                                            using (var sqLite = new SQLiteConnection(ConnectionString))
                                            {
                                                sqLite.Open();
                                                using (var cmd = new SQLiteCommand(sqLite))
                                                {
                                                    cmd.CommandText =
                                                        "Insert INTO Message (FromUserId,ToUserId,SendDate,Content,State) VALUES (@1,@2,@3,@4,@5)";
                                                    SQLiteParameter[] parameters =
                                                    {
                                                        new SQLiteParameter("@1", DbType.Int32),
                                                        new SQLiteParameter("@2", DbType.Int32),
                                                        new SQLiteParameter("@3", DbType.String),
                                                        new SQLiteParameter("@4", DbType.String),
                                                        new SQLiteParameter("@5", DbType.Int32)
                                                    };
                                                    parameters[0].Value = res.obj.Client.UserId;
                                                    parameters[1].Value = GetUserId(t.User);
                                                    parameters[2].Value = DateTime.Now;
                                                    parameters[3].Value = t.Content;
                                                    parameters[4].Value = 1;
                                                    cmd.Parameters.AddRange(parameters);
                                                    cmd.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            var y = new Messaging();
                                            y.SetMessage(new Message
                                            {
                                                Content = t.Content,
                                                User = res.obj.Client.UserName,
                                                State = 1,
                                                MessageTime = DateTime.Now
                                            });
                                            y.Show();
                                        });
                                    }
                                    else
                                    {
                                        SendMsg(t.Content, res.obj.Client.UserId, GetUserId(t.User), res.token);
                                    }
                                    break;
                                }
                            case "RequestProblem":
                                {
                                    var pid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var p = GetProblem(pid);
                                        p.CompileCommand = string.Empty;
                                        foreach (var i in p.DataSets)
                                        {
                                            i.InputFile = i.OutputFile = string.Empty;
                                        }
                                        for (var i = 0; i < p.ExtraFiles.Length; i++)
                                        {
                                            p.ExtraFiles[i] = string.Empty;
                                        }
                                        p.SpecialJudge = string.Empty;
                                        SendData("RequestProblem", JsonConvert.SerializeObject(p), res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestProblemListGrouped":
                                {
                                    var start = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    var count = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[1]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        string GetEngName(string origin)
                                        {
                                            var re = new Regex("[A-Z]|[a-z]|[0-9]");
                                            return re.Matches(origin).Cast<object>().Aggregate(string.Empty,
                                                (current, t) => current + t);
                                        }

                                        string GetRealString(string origin, string problemName, int cur)
                                        {
                                            return origin
                                                .Replace("${datadir}",
                                                    AppDomain.CurrentDomain.BaseDirectory + "\\Data")
                                                .Replace("${name}", GetEngName(problemName))
                                                .Replace("${index0}", cur.ToString())
                                                .Replace("${index}", (cur + 1).ToString());
                                        }

                                        var pl = QueryProblems(false, start, count);
                                        foreach (var problem in pl)
                                        {
                                            problem.InputFileName = GetRealString(problem.InputFileName,
                                                problem.ProblemName, 0);
                                            problem.OutputFileName = GetRealString(problem.OutputFileName,
                                                problem.ProblemName, 0);
                                            problem.ExtraFiles = new[] { string.Empty };
                                            problem.AddDate = string.Empty;
                                            problem.CompileCommand = string.Empty;
                                            foreach (var problemDataSet in problem.DataSets)
                                                problemDataSet.InputFile =
                                                    problemDataSet.OutputFile = string.Empty;
                                            problem.SpecialJudge = string.Empty;
                                            problem.Description = string.Empty;
                                            problem.Option = 0;
                                        }
                                        SendData("ProblemListGrouped", JsonConvert.SerializeObject(pl),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestProblemList":
                                {
                                    var id = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        string GetEngName(string origin)
                                        {
                                            var re = new Regex("[A-Z]|[a-z]|[0-9]");
                                            return re.Matches(origin).Cast<object>().Aggregate(string.Empty,
                                                (current, t) => current + t);
                                        }

                                        string GetRealString(string origin, string problemName, int cur)
                                        {
                                            return origin
                                                .Replace("${datadir}",
                                                    AppDomain.CurrentDomain.BaseDirectory + "\\Data")
                                                .Replace("${name}", GetEngName(problemName))
                                                .Replace("${index0}", cur.ToString())
                                                .Replace("${index}", (cur + 1).ToString());
                                        }

                                        var pl = QueryProblems(false);
                                        foreach (var problem in pl)
                                        {
                                            problem.InputFileName = GetRealString(problem.InputFileName,
                                                problem.ProblemName, 0);
                                            problem.OutputFileName = GetRealString(problem.OutputFileName,
                                                problem.ProblemName, 0);
                                            problem.ExtraFiles = new[] { string.Empty };
                                            problem.AddDate = string.Empty;
                                            problem.CompileCommand = string.Empty;
                                            foreach (var problemDataSet in problem.DataSets)
                                                problemDataSet.InputFile =
                                                    problemDataSet.OutputFile = string.Empty;
                                            problem.SpecialJudge = string.Empty;
                                            problem.Description = string.Empty;
                                            problem.Option = 0;
                                        }
                                        foreach (var i in pl)
                                            SendData("ProblemList", id + Divpar + JsonConvert.SerializeObject(i),
                                                res.obj.Client.ConnId, res.token);
                                        if (pl.Count() == 0)
                                            SendData("ProblemList",
                                                id + Divpar + JsonConvert.SerializeObject(null),
                                                res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestProfile":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }

                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = JsonConvert.SerializeObject(
                                            GetUser(res.obj.Client.UserName, true));
                                        SendData("Profile", x, res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestCompiler":
                                {
                                    var cmp = Configuration.Configurations.Compiler
                                        .Select(t => new Compiler { DisplayName = t.DisplayName }).ToList();
                                    var x = JsonConvert.SerializeObject(cmp);
                                    SendData("Compiler", x, res.obj.Client.ConnId, res.token);
                                    break;
                                }
                            case "QueryLanguagesForCompetition":
                                {
                                    var cmp = Configuration.Configurations.Compiler
                                        .Select(t => new Compiler { DisplayName = t.DisplayName }).ToList();
                                    var x = JsonConvert.SerializeObject(cmp);
                                    SendData("QueryLanguagesForCompetition", x, res.obj.Client.ConnId, res.token);
                                    break;
                                }
                            case "ChangePassword":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }

                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("ChangePassword",
                                            RemoteChangePassword(res.obj.Client.UserName,
                                                Encoding.Unicode.GetString(res.obj.Content[0]),
                                                Encoding.Unicode.GetString(res.obj.Content[1]))
                                                ? "Succeed"
                                                : "Failed", res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "UpdateProfile":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }

                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("UpdateProfile",
                                            RemoteUpdateProfile(
                                                res.obj.Client.UserId,
                                                Encoding.Unicode.GetString(res.obj.Content[0]),
                                                Encoding.Unicode.GetString(res.obj.Content[1]))
                                                ? "Succeed"
                                                : "Failed", res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "UpdateCoins":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }

                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("UpdateCoins",
                                            UpdateCoins(
                                                res.obj.Client.UserId,
                                                Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])))
                                                ? "Succeed"
                                                : "Failed", res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "UpdateExperience":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }

                                    SendData("UpdateExperience",
                                        UpdateExperience(
                                            res.obj.Client.UserId,
                                            Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])))
                                            ? "Succeed"
                                            : "Failed", res.obj.Client.ConnId, res.token);
                                    break;
                                }
                            case "RequestJudgeRecord":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }

                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = GetJudgeRecord(res.obj.Client.UserId,
                                            Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])),
                                            Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[1])));
                                        SendData("JudgeRecord",
                                            Encoding.Unicode.GetString(res.obj.Content[0]) + Divpar +
                                            x.Length + Divpar +
                                            JsonConvert.SerializeObject(x),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestJudgeCode":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }

                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("JudgeCode",
                                            JsonConvert.SerializeObject(GetJudgeInfo(Convert.ToInt32(
                                                Encoding.Unicode.GetString(res.obj.Content[0])))),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "AddProblem":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("AddProblem", JsonConvert.SerializeObject(GetProblem(NewProblem())),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "DeleteProblem":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        DeleteProblem(Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])));
                                        SendData("DeleteProblem", string.Empty, res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "UpdateProblem":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var x = string.Empty;
                                    for (var i = 0; i < res.obj.Content.Count; i++)
                                        if (i != res.obj.Content.Count - 1)
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                        else
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]);
                                    var p = JsonConvert.DeserializeObject<Problem>(x);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        UpdateProblem(p);
                                        SendData("UpdateProblem", string.Empty, res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "QueryProblems":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = QueryProblems(true);
                                        foreach (var i in x)
                                            i.Description = string.Empty;
                                        SendData("QueryProblems", JsonConvert.SerializeObject(x), res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "GetProblemDescription":
                                {
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = GetProblem(Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])));
                                        SendData("GetProblemDescription",
                                            JsonConvert.SerializeObject(new Problem
                                            {
                                                Description = x?.Description ?? string.Empty,
                                                Option = x?.Option ?? 0
                                            }), res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "QueryJudgeLogs":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("QueryJudgeLogs", JsonConvert.SerializeObject(QueryJudgeLog(false)),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestCode":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = GetJudgeInfo(Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])));
                                        SendData("RequestCode",
                                            JsonConvert.SerializeObject(new JudgeInfo { Code = x?.Code ?? string.Empty }),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "ClearJudgingLogs":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(ClearJudgeLog));
                                    break;
                                }
                            case "DataFile":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var fileName = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    var fileId = Encoding.Unicode.GetString(res.obj.Content[1]);
                                    var length = Convert.ToInt64(Encoding.Unicode.GetString(res.obj.Content[2]));
                                    if (FrInfo.Any(i => i.FileId == fileId))
                                    {
                                        var fs = FrInfo.FirstOrDefault(i => i.FileId == fileId);
                                        var x = new List<byte>();
                                        for (var i = 3; i < res.obj.Content.Count; i++)
                                            if (i != res.obj.Content.Count - 1)
                                            {
                                                x.AddRange(res.obj.Content[i]);
                                                x.AddRange(Encoding.Unicode.GetBytes(Divpar));
                                            }
                                            else
                                            {
                                                x.AddRange(res.obj.Content[i]);
                                            }
                                        fs.Fs.Position = length;
                                        fs.Fs.Write(x.ToArray(), 0, x.Count);
                                        fs.CurrentLength += x.Count;
                                        if (fs.CurrentLength >= fs.TotLength)
                                        {
                                            var filePath =
                                                $"{Environment.GetEnvironmentVariable("temp")}\\{fileId}\\{fileName}";
                                            fs.Fs.Close();
                                            fs.Fs.Dispose();
                                            FrInfo.Remove(fs);
                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                try
                                                {
                                                    System.IO.Compression.ZipFile.ExtractToDirectory(filePath,
                                                        $"{AppDomain.CurrentDomain.BaseDirectory}\\Data");
                                                }
                                                catch
                                                {
                                                    SendData("DataFile", "Failed", res.obj.Client.ConnId, res.token);
                                                    return;
                                                }
                                                finally
                                                {
                                                    try
                                                    {
                                                        File.Delete(filePath);
                                                    }
                                                    catch
                                                    {
                                                        //ignored
                                                    }
                                                }
                                                SendData("DataFile", "Succeeded", res.obj.Client.ConnId, res.token);
                                            }));
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            if (!Directory.Exists($"{Environment.GetEnvironmentVariable("temp")}\\{fileId}")
                                            )
                                                Directory.CreateDirectory(
                                                    $"{Environment.GetEnvironmentVariable("temp")}\\{fileId}");
                                            FrInfo.Add(new FileRecvInfo
                                            {
                                                CurrentLength = 0,
                                                FileId = fileId,
                                                FileName = fileName,
                                                Fs = new FileStream(
                                                    $"{Environment.GetEnvironmentVariable("temp")}\\{fileId}\\{fileName}",
                                                    FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite),
                                                TotLength = length
                                            });
                                        }
                                        catch
                                        {
                                            SendData("DataFile", "Failed", res.obj.Client.ConnId, res.token);
                                        }
                                    }
                                    break;
                                }
                            case "PublicFile":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var fileName = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    var fileId = Encoding.Unicode.GetString(res.obj.Content[1]);
                                    var length = Convert.ToInt64(Encoding.Unicode.GetString(res.obj.Content[2]));
                                    if (FrInfo.Any(i => i.FileId == fileId))
                                    {
                                        var fs = FrInfo.FirstOrDefault(i => i.FileId == fileId);
                                        var x = new List<byte>();
                                        for (var i = 3; i < res.obj.Content.Count; i++)
                                            if (i != res.obj.Content.Count - 1)
                                            {
                                                x.AddRange(res.obj.Content[i]);
                                                x.AddRange(Encoding.Unicode.GetBytes(Divpar));
                                            }
                                            else
                                            {
                                                x.AddRange(res.obj.Content[i]);
                                            }
                                        fs.Fs.Position = length;
                                        fs.Fs.Write(x.ToArray(), 0, x.Count);
                                        fs.CurrentLength += x.Count;
                                        if (fs.CurrentLength >= fs.TotLength)
                                        {
                                            var filePath =
                                                $"{Environment.GetEnvironmentVariable("temp")}\\{fileId}\\{fileName}";
                                            fs.Fs.Close();
                                            fs.Fs.Dispose();
                                            FrInfo.Remove(fs);
                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                try
                                                {
                                                    System.IO.Compression.ZipFile.ExtractToDirectory(filePath,
                                                        $"{AppDomain.CurrentDomain.BaseDirectory}\\Files");
                                                }
                                                catch
                                                {
                                                    SendData("PublicFile", "Failed", res.obj.Client.ConnId, res.token);
                                                    return;
                                                }
                                                finally
                                                {
                                                    try
                                                    {
                                                        File.Delete(filePath);
                                                    }
                                                    catch
                                                    {
                                                        //ignored
                                                    }
                                                }
                                                SendData("PublicFile", "Succeeded", res.obj.Client.ConnId, res.token);
                                            }));
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            if (!Directory.Exists($"{Environment.GetEnvironmentVariable("temp")}\\{fileId}")
                                            )
                                                Directory.CreateDirectory(
                                                    $"{Environment.GetEnvironmentVariable("temp")}\\{fileId}");
                                            FrInfo.Add(new FileRecvInfo
                                            {
                                                CurrentLength = 0,
                                                FileId = fileId,
                                                FileName = fileName,
                                                Fs = new FileStream(
                                                    $"{Environment.GetEnvironmentVariable("temp")}\\{fileId}\\{fileName}",
                                                    FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite),
                                                TotLength = length
                                            });
                                        }
                                        catch
                                        {
                                            SendData("PublicFile", "Failed", res.obj.Client.ConnId, res.token);
                                        }
                                    }
                                    break;
                                }
                            case "ClearData":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var tid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        string GetEngName(string origin)
                                        {
                                            var re = new Regex("[A-Z]|[a-z]|[0-9]");
                                            return re.Matches(origin).Cast<object>().Aggregate(string.Empty,
                                                (current, ti) => current + ti);
                                        }

                                        string GetRealString(string origin, string problemName, int cur)
                                        {
                                            return origin
                                                .Replace("${datadir}",
                                                    AppDomain.CurrentDomain.BaseDirectory + "\\Data")
                                                .Replace("${name}", GetEngName(problemName))
                                                .Replace("${index0}", cur.ToString())
                                                .Replace("${index}", (cur + 1).ToString());
                                        }

                                        var p = GetProblem(tid);
                                        if (p.ProblemId == 0) return;
                                        for (var cnt = 0; cnt < p.DataSets.Length; cnt++)
                                        {
                                            var fin = GetRealString(p.DataSets[cnt].InputFile, p.ProblemName, cnt);
                                            var fout = GetRealString(p.DataSets[cnt].OutputFile, p.ProblemName, cnt);
                                            if (!string.IsNullOrEmpty(fin))
                                                try
                                                {
                                                    File.Delete(fin);
                                                }
                                                catch
                                                {
                                                    //ignored
                                                }
                                            if (!string.IsNullOrEmpty(fout))
                                                try
                                                {
                                                    File.Delete(fout);
                                                }
                                                catch
                                                {
                                                    //ignored
                                                }
                                        }
                                    }));
                                    break;
                                }
                            case "DeleteExtra":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var tid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        string GetEngName(string origin)
                                        {
                                            var re = new Regex("[A-Z]|[a-z]|[0-9]");
                                            return re.Matches(origin).Cast<object>().Aggregate(string.Empty,
                                                (current, ti) => current + ti);
                                        }

                                        string GetRealString(string origin, string problemName)
                                        {
                                            return origin
                                                .Replace("${datadir}",
                                                    AppDomain.CurrentDomain.BaseDirectory + "\\Data")
                                                .Replace("${name}", GetEngName(problemName));
                                        }

                                        var p = GetProblem(tid);
                                        if (p.ProblemId == 0) return;
                                        foreach (var f in p.ExtraFiles)
                                        {
                                            var fr = GetRealString(f, p.ProblemName);
                                            if (!string.IsNullOrEmpty(fr))
                                                try
                                                {
                                                    File.Delete(fr);
                                                }
                                                catch
                                                {
                                                    //ignored
                                                }
                                        }
                                    }));
                                    break;
                                }
                            case "DeleteJudge":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var tid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        string GetEngName(string origin)
                                        {
                                            var re = new Regex("[A-Z]|[a-z]|[0-9]");
                                            return re.Matches(origin).Cast<object>().Aggregate(string.Empty,
                                                (current, ti) => current + ti);
                                        }

                                        string GetRealString(string origin, string problemName)
                                        {
                                            return origin
                                                .Replace("${datadir}",
                                                    AppDomain.CurrentDomain.BaseDirectory + "\\Data")
                                                .Replace("${name}", GetEngName(problemName));
                                        }

                                        var p = GetProblem(tid);
                                        if (p.ProblemId == 0) return;
                                        var f = GetRealString(p.SpecialJudge, p.ProblemName);
                                        if (!string.IsNullOrEmpty(f))
                                            try
                                            {
                                                File.Delete(f);
                                            }
                                            catch
                                            {
                                                //ignored
                                            }
                                    }));
                                    break;
                                }
                            case "RequestMsgListGrouped":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var start = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    var count = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[1]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var t = QueryMsg(res.obj.Client.UserId, false, start, count);
                                        t.Reverse();
                                        SendData("RequestMsgListGrouped", JsonConvert.SerializeObject(t),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestMsgList":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var id = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var t = QueryMsg(res.obj.Client.UserId, false);
                                        t.Reverse();
                                        foreach (var i in t)
                                            SendData("RequestMsgList", id + Divpar + JsonConvert.SerializeObject(i),
                                                res.obj.Client.ConnId, res.token);
                                        if (t.Count() == 0)
                                            SendData("RequestMsgList",
                                                id + Divpar + JsonConvert.SerializeObject(null),
                                                res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestMsg":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetMsg(Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])), res.obj.Client.UserId);
                                    ActionList.Enqueue(new Task(() =>
                                        SendData("RequestMsg", JsonConvert.SerializeObject(t), res.obj.Client.ConnId, res.token)));
                                    break;
                                }
                            case "RequestMsgTargetUserGrouped":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var id = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = GetUser(res.obj.Client.UserId);
                                        var t = GetSpecialTypeUser(1);
                                        if (x.Type >= 4)
                                        {
                                            t.AddRange(GetSpecialTypeUser(2));
                                            t.AddRange(GetSpecialTypeUser(3));
                                            if (Configuration.Configurations.AllowCompetitorMessaging)
                                                t.AddRange(GetSpecialTypeUser(4));
                                        }
                                        else
                                        {
                                            t.AddRange(GetUsersBelongs(1));
                                        }
                                        var p = new List<string>();
                                        p.AddRange(t.Where(i => i.UserId != res.obj.Client.UserId).Select(i => i.UserName));
                                        SendData("RequestMsgTargetUserGrouped", id + Divpar + JsonConvert.SerializeObject(p),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestMsgTargetUser":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var id = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = GetUser(res.obj.Client.UserId);
                                        var t = GetSpecialTypeUser(1);
                                        if (x.Type >= 4)
                                        {
                                            t.AddRange(GetSpecialTypeUser(2));
                                            t.AddRange(GetSpecialTypeUser(3));
                                            if (Configuration.Configurations.AllowCompetitorMessaging)
                                                t.AddRange(GetSpecialTypeUser(4));
                                        }
                                        else
                                        {
                                            t.AddRange(GetUsersBelongs(1));
                                        }
                                        var p = new List<string>();
                                        p.AddRange(t.Where(i => i.UserId != res.obj.Client.UserId).Select(i => i.UserName));
                                        foreach (var i in p)
                                            SendData("RequestMsgTargetUser", id + Divpar + JsonConvert.SerializeObject(i),
                                                res.obj.Client.ConnId, res.token);
                                        if (p.Count() == 0)
                                            SendData("RequestMsgTargetUser",
                                                id + Divpar + JsonConvert.SerializeObject(null),
                                                res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "SetMsgState":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var msgId = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    var state = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[1]));
                                    ActionList.Enqueue(new Task(() => SetMsgState(msgId, state)));
                                    break;
                                }
                            case "RequestCompetition":
                                {
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("RequestCompetition",
                                            JsonConvert.SerializeObject(GetCompetition(
                                                Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0])))),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestCompetitionListGrouped":
                                {
                                    var start = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    var count = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[1]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var t = QueryCompetition(start, count, false)?.Reverse();
                                        if (t == null) t = new List<Competition>();
                                        SendData("RequestCompetitionListGrouped",
                                            JsonConvert.SerializeObject(t),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestCompetitionList":
                                {
                                    var id = Encoding.Unicode.GetString(res.obj.Content[0]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var t = QueryCompetition(0, -10, false)?.Reverse();
                                        if (t == null) return;
                                        foreach (var i in t)
                                            SendData("RequestCompetitionList",
                                                id + Divpar + JsonConvert.SerializeObject(i),
                                                res.obj.Client.ConnId, res.token);
                                        if (t.Count() == 0)
                                            SendData("RequestCompetitionList",
                                                id + Divpar + JsonConvert.SerializeObject(null),
                                                res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "QueryJudgeLogBelongsToCompetition":
                                {
                                    var cid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var t = GetCompetition(cid);
                                        var withRank = (t?.Option ?? 0) & 16;
                                        var x = QueryJudgeLogBelongsToCompetition(cid,
                                            withRank != 0 ? res.obj.Client.UserId : 0);
                                        foreach (var i in x)
                                            if (i.UserId != res.obj.Client.UserId) i.Code = string.Empty;
                                        SendData("QueryJudgeLogBelongsToCompetition", JsonConvert.SerializeObject(x),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "QueryProblemsForCompetition":
                                {
                                    var cid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var competition = GetCompetition(cid);
                                        var pList = competition.ProblemSet.Select(GetProblem).ToList();
                                        SendData("QueryProblemsForCompetition", JsonConvert.SerializeObject(pList),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "SubmitCodeForCompetition":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var pid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    var cid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[1]));
                                    if (!string.IsNullOrEmpty(Encoding.Unicode.GetString(res.obj.Content[3])))
                                    {
                                        var t = GetCompetition(cid);
                                        if (DateTime.Now > t.EndTime || DateTime.Now < t.StartTime)
                                        {
                                            SendData("JudgeIdForCompetition", "Failed", res.obj.Client.ConnId, res.token);
                                        }
                                        var code = string.Empty;
                                        for (var i = 3; i < res.obj.Content.Count; i++)
                                            if (i != res.obj.Content.Count - 1)
                                                code += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                            else
                                                code += Encoding.Unicode.GetString(res.obj.Content[i]);
                                        var type = Encoding.Unicode.GetString(res.obj.Content[2]);
                                        var userId = res.obj.Client.UserId;
                                        UpdateMainPageState(
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.obj.Client.UserName} 提交了题目 {GetProblemName(pid)} 的代码");
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            new Thread(() =>
                                            {
                                                var j = new Judge(pid, userId, code, type, true, "在线评测",
                                                    DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), cid, (jid) => { SendData("JudgeIdForCompetition", JsonConvert.SerializeObject(new JudgeInfo { JudgeId = jid, ProblemId = pid, UserId = res.obj.Client.UserId, Code = code, CompetitionId = cid }), res.obj.Client.ConnId, res.token); });
                                                if (j.Cancelled)
                                                {
                                                    SendData("JudgeIdForCompetition", "Failed", res.obj.Client.ConnId, res.token);
                                                    return;
                                                }
                                                var jr = JsonConvert.SerializeObject(j.JudgeResult);
                                                if ((t.Option & 8) != 0)
                                                    SendData("JudgeResultForCompetition", jr, res.obj.Client.ConnId, res.token);
                                            }).Start();
                                        }));
                                    }
                                    break;
                                }
                            case "GetCurrentDateTime":
                                {
                                    SendData("GetCurrentDateTime", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"),
                                        res.obj.Client.ConnId, res.token);
                                    break;
                                }
                            case "QueryCompetitionClient":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("QueryCompetitionClient",
                                            JsonConvert.SerializeObject(QueryCompetition()), res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "NewCompetitionClient":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("NewCompetitionClient",
                                            JsonConvert.SerializeObject(GetCompetition(NewCompetition())),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "DeleteCompetitionClient":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var cid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        DeleteCompetition(cid);
                                        SendData("DeleteCompetitionClient", string.Empty, res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "UpdateCompetitionClient":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var x = string.Empty;
                                    for (var i = 0; i < res.obj.Content.Count; i++)
                                        if (i != res.obj.Content.Count - 1)
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                        else
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        UpdateCompetition(JsonConvert.DeserializeObject<Competition>(x));
                                        SendData("UpdateCompetitionClient", string.Empty, res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "GetProblem":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var pid = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        SendData("GetProblem", JsonConvert.SerializeObject(GetProblem(pid)),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "GetServerConfig":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 3)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = new ServerConfig
                                        {
                                            AllowCompetitorMessaging =
                                                Configuration.Configurations.AllowCompetitorMessaging,
                                            AllowRequestDataSet = Configuration.Configurations.AllowRequestDataSet,
                                            MutiThreading = Configuration.Configurations.MutiThreading,
                                            RegisterMode = Configuration.Configurations.RegisterMode
                                        };
                                        SendData("GetServerConfig", JsonConvert.SerializeObject(x), res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "UpdateServerConfig":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 3)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var x = string.Empty;
                                    for (var i = 0; i < res.obj.Content.Count; i++)
                                        if (i != res.obj.Content.Count - 1)
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                        else
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var config = JsonConvert.DeserializeObject<ServerConfig>(x);
                                        Configuration.Configurations.AllowCompetitorMessaging =
                                            config.AllowCompetitorMessaging;
                                        Configuration.Configurations.AllowRequestDataSet = config.AllowRequestDataSet;
                                        Configuration.Configurations.MutiThreading = config.MutiThreading;
                                        Configuration.Configurations.RegisterMode = config.RegisterMode;
                                        Configuration.Save();
                                        SendData("UpdateServerConfig", string.Empty, res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "GetUserBelongings":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var x = GetUsersBelongs(t.Type);
                                        foreach (var i in x)
                                        {
                                            i.Achievement = string.Empty;
                                            i.Icon = string.Empty;
                                            i.RegisterDate = string.Empty;
                                        }
                                        SendData("GetUserBelongings", t.Type + Divpar + JsonConvert.SerializeObject(x),
                                            res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "UpdateUserBelongings":
                                {
                                    if (res.obj.Client.UserId == 0)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var t = GetUser(res.obj.Client.UserId);
                                    if (t.Type <= 0 || t.Type >= 4)
                                    {
                                        SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                        break;
                                    }
                                    var x = string.Empty;
                                    for (var i = 0; i < res.obj.Content.Count; i++)
                                        if (i != res.obj.Content.Count - 1)
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                        else
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var users = JsonConvert.DeserializeObject<List<List<UserInfo>>>(x);
                                        if (users.Count != 2) return;
                                        DeleteUser(users[0]?.Select(i => i.UserId));
                                        UpdateUser(users[1]);
                                        SendData("UpdateUserBelongings", string.Empty, res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "QuerySpecialJudgeLogs":
                                {
                                    var start = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[0]));
                                    var count = Convert.ToInt32(Encoding.Unicode.GetString(res.obj.Content[1]));
                                    var x = string.Empty;
                                    for (var i = 2; i < res.obj.Content.Count; i++)
                                        if (i != res.obj.Content.Count - 1)
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]) + Divpar;
                                        else
                                            x += Encoding.Unicode.GetString(res.obj.Content[i]);
                                    ActionList.Enqueue(new Task(() =>
                                    {
                                        var commandSet = JsonConvert.DeserializeObject<List<QueryCommand>>(x);
                                        var command = string.Empty;
                                        foreach (var c in commandSet)
                                        {
                                            command += c.Command;
                                        }
                                        if (commandSet.Count == 0) command = string.Empty;
                                        else command = "where " + command;
                                        SendData("QuerySpecialJudgeLogs", JsonConvert.SerializeObject(QueryCustomJudgeInfo(start, count, command)), res.obj.Client.ConnId, res.token);
                                    }));
                                    break;
                                }
                            case "RequestClient":
                                {
                                    SendFile(AppDomain.CurrentDomain.BaseDirectory + "\\ClientPkg.zip", res.obj.Client.ConnId,
                                        "RequestClient", res.token);
                                    break;
                                }
                            default:
                                {
                                    SendData(res.obj.Operation, "OperationDenied", res.obj.Client.ConnId, res.token);
                                    break;
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        new Thread(() => MessageBox.Show($"错误信息\n--------\n时间：{DateTime.Now:yyyy/MM/dd HH:mm:ss}\n摘要：\n{ex.Message}\n堆栈：\n{ex.StackTrace}\n\n请求信息\n--------\n操作：{res.obj.Operation}\n内容：\n{(res.obj.Content.Count != 0 ? res.obj.Content.ConvertAll(Encoding.Unicode.GetString).Aggregate((last, next) => last + "\n" + next) : string.Empty)}\n来源：({res.obj.Client.ConnId}) {res.obj.Client.Address}\n用户：({res.obj.Client.UserId}) {res.obj.Client.UserName}", "错误报告", MessageBoxButton.OK, MessageBoxImage.Error)).Start();
                        SendData(res.obj.Operation, "ActionFailed" + Divpar + ex.Message + Divpar + ex.StackTrace,
                            res.obj.Client.ConnId, res.token);
                    }
                }
                else Thread.Sleep(1);
            }
        }

        public static ObservableCollection<ClientInfo> GetAllConnectedClient()
        {
            var a = new ObservableCollection<ClientInfo>();
            foreach (var i in Recv)
            {
                if (i.Info.UserId == 0) continue;
                i.Info.IsChecked = false;
                a.Add(i.Info);
            }
            return a;
        }

        public static void ActionExecuter(UIElement textBlock)
        {
            new Thread(() =>
            {
                var cnt = 0;
                long tot = 0;
                var last = 0;
                var addition = 0;
                var limit = Environment.ProcessorCount * 2;
                var dealingTime = DateTime.Now;
                var withAddition = false;
                while (true)
                {
                    if (ActionList.Any())
                    {
                        if (cnt < limit + addition)
                        {
                            dealingTime = DateTime.Now;
                            if (ActionList.TryDequeue(out var t))
                            {
                                if (withAddition)
                                {
                                    withAddition = false;
                                    t.ContinueWith(o =>
                                    {
                                        lock (ActionCounterLock)
                                        {
                                            tot++;
                                            cnt--;
                                            addition--;
                                            UpdateMainPageState(
                                                $"当前负荷：待投递任务：{ActionList.Count}，待处理任务：{cnt}。已完成任务：{tot}", textBlock);
                                        }
                                    });
                                }
                                else
                                {
                                    t.ContinueWith(o =>
                                    {
                                        lock (ActionCounterLock)
                                        {
                                            tot++;
                                            cnt--;
                                            UpdateMainPageState(
                                                $"当前负荷：待投递任务：{ActionList.Count}，待处理任务：{cnt}。已完成任务：{tot}", textBlock);
                                        }
                                    });
                                }
                                t.Start();
                                lock (ActionCounterLock)
                                {
                                    cnt++;
                                    UpdateMainPageState($"当前负荷：待投递任务：{ActionList.Count}，待处理任务：{cnt}。已完成任务：{tot}",
                                        textBlock);
                                }
                            }
                        }
                        else
                        {
                            if ((DateTime.Now - dealingTime).TotalSeconds > 3)
                            {
                                withAddition = true;
                                lock (ActionCounterLock)
                                {
                                    addition++;
                                }
                            }
                        }
                        if (last != ActionList.Count)
                        {
                            UpdateMainPageState($"当前负荷：待投递任务：{ActionList.Count}，待处理任务：{cnt}。已完成任务：{tot}", textBlock);
                            last = ActionList.Count;
                        }
                    }
                    else Thread.Sleep(1);
                }
            }).Start();
        }
    }
}