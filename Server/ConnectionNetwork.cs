using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HPSocketCS;
using Ionic.Zip;
using Newtonsoft.Json;
using System.Collections.Concurrent;

namespace Server
{
    public static partial class Connection
    {
        private static readonly int PkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        private static readonly List<ClientData> Recv = new List<ClientData>();
        private static readonly ConcurrentQueue<ObjOperation> Operations = new ConcurrentQueue<ObjOperation>();
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

        private static byte[] GetSendBuffer(byte[] bodyBytes)
        {
            var header = new PkgHeader
            {
                Id = ++_id,
                BodySize = bodyBytes.Length
            };
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
                    SendData("Logout", "Succeed", i.Info.ConnId);
                    i.Info.UserId = 0;
                    while (i.Data.TryDequeue(out var temp)) { temp.Clear(); }
                }
        }

        private static void SendData(string operation, IEnumerable<byte> sendBytes, IntPtr connId)
        {
            var temp = Encoding.Unicode.GetBytes(operation);
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
            temp = temp.Concat(sendBytes).ToArray();
            var final = GetSendBuffer(temp);
            HServer.Send(connId, final, final.Length);
        }

        private static void SendFile(string fileName, IntPtr connId)
        {
            if (!File.Exists(fileName))
            {
                SendData("File", "NotFound", connId);
            }
            var fileId = Guid.NewGuid().ToString();
            var temp = Encoding.Unicode.GetBytes("File" + Divpar
                                                 + Path.GetFileName(fileName) + Divpar
                                                 + fileId + Divpar
                                                 + new FileInfo(fileName).Length);
            var final = GetSendBuffer(temp);
            HServer.Send(connId, final, final.Length);
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long tot = 0;
                while (tot != fs.Length)
                {
                    var bytes = new byte[131072];
                    long cnt = fs.Read(bytes, 0, 131072);
                    var tempc = GetSendBuffer(Encoding.Unicode.GetBytes("File" + Divpar
                                                                        + Path.GetFileName(fileName) + Divpar
                                                                        + fileId + Divpar + tot + Divpar)
                        .Concat(bytes.Take((int)cnt)).ToArray());
                    tot += cnt;
                    HServer.Send(connId, tempc, tempc.Length);
                }
                fs.Close();
            }
        }

        private static void SendData(string operation, string sendString, IntPtr connId)
        {
            var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString);
            var final = GetSendBuffer(temp);
            HServer.Send(connId, final, final.Length);
        }

        public static void SetMsgState(int msgId, int state)
        {
            lock (DataBaseLock)
            {
                using (var cmd = new SQLiteCommand(_sqLite))
                {
                    cmd.CommandText = "UPDATE Message SET State=@1 Where MessageId=@2";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.Int32),
                        new SQLiteParameter("@2", DbType.Int32),
                    };
                    parameters[0].Value = state;
                    parameters[1].Value = msgId;
                    cmd.Parameters.AddRange(parameters);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static void SendMsg(string sendString, int fromUserId, int toUserId)
        {
            lock (DataBaseLock)
            {
                using (var cmd = new SQLiteCommand(_sqLite))
                {
                    cmd.CommandText = "Insert INTO Message (FromUserId,ToUserId,SendDate,Content,State) VALUES (@1,@2,@3,@4,@5)";
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
            var t = new Message { Content = sendString, MessageTime = DateTime.Now, Direction = "接收", User = GetUserName(fromUserId), State = 0 };
            SendData("Messaging", JsonConvert.SerializeObject(t), Recv.Where(i => i.Info.UserId == toUserId)?.Select(p => p.Info.ConnId)?.FirstOrDefault() ?? IntPtr.Zero);
        }

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
                        }
                        else
                        {
                            var buffer = new byte[required];
                            Marshal.Copy(bufferPtr, buffer, 0, required);
                            required = PkgHeaderSize;
                            (from c in Recv where c.Info.ConnId == connId select c).FirstOrDefault()?.Data.Enqueue(buffer.ToList());
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
            Task.Run(() =>
            {
                while (!IsExited)
                {
                    foreach (var t in Recv)
                    {
                        if (IsExited) break;
                        while (t.Data.TryDequeue(out var temp))
                        {
                            var temp2 = Bytespilt(temp, Encoding.Unicode.GetBytes(Divpar));
                            if (temp2.Count == 0)
                                continue;
                            var operation = Encoding.Unicode.GetString(temp2[0]);
                            switch (operation)
                            {
                                case "@":
                                    {
                                        SendData("&", string.Empty, t.Info.ConnId);
                                        break;
                                    }
                                default:
                                    {
                                        temp2.RemoveAt(0);
                                        Operations.Enqueue(new ObjOperation
                                        {
                                            Operation = operation,
                                            Client = t.Info,
                                            Content = temp2
                                        });
                                        break;
                                    }
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
                    {
                        var u = Recv.FirstOrDefault(c => c.Info.ConnId == res.Client.ConnId);
                        if (u == null) continue;
                        try
                        {
                            switch (res.Operation)
                            {
                                case "Login":
                                    {
                                        var x = RemoteLogin(Encoding.Unicode.GetString(res.Content[0]),
                                            Encoding.Unicode.GetString(res.Content[1]));
                                        switch (x)
                                        {
                                            case 0:
                                                {
                                                    var uid = GetUserId(
                                                        Encoding.Unicode.GetString(res.Content[0]));
                                                    foreach (var li in Recv.Where(c => c.Info.UserId == uid))
                                                    {
                                                        UpdateMainPageState(
                                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {li.Info.UserName} 多终端登陆，已注销其中一个终端的登录状态");
                                                        li.Info.UserId = 0;
                                                        while (li.Data.TryDequeue(out var temp)) { temp.Clear(); }
                                                        SendData("Logout", "Succeed", li.Info.ConnId);
                                                    }
                                                    res.Client.UserId = uid;
                                                    SendData("Login", "Succeed", res.Client.ConnId);
                                                    UpdateMainPageState(
                                                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.Client.UserName} 登录了");
                                                    break;
                                                }
                                            case 1:
                                                {
                                                    SendData("Login", "Incorrect", res.Client.ConnId);
                                                    break;
                                                }
                                            case 3:
                                                {
                                                    SendData("Login", "NeedReview", res.Client.ConnId);
                                                    break;
                                                }
                                            default:
                                                {
                                                    SendData("Login", "Unknown", res.Client.ConnId);
                                                    break;
                                                }
                                        }
                                        break;
                                    }
                                case "Logout":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;
                                        UpdateMainPageState(
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.Client.UserName} 注销了");
                                        while (u.Data.TryDequeue(out var temp)) { temp.Clear(); }
                                        res.Client.UserId = 0;
                                        SendData("Logout", "Succeed", res.Client.ConnId);
                                        break;
                                    }
                                case "Register":
                                    {
                                        if (res.Client.UserId != 0)
                                            break;
                                        Task.Run(() =>
                                        {
                                            if (RemoteRegister(Encoding.Unicode.GetString(res.Content[0]),
                                                Encoding.Unicode.GetString(res.Content[1])))
                                            {
                                                SendData("Register",
                                                    Configuration.Configurations.RegisterMode == 2
                                                        ? "Succeeded"
                                                        : "NeedReview", res.Client.ConnId);
                                            }
                                            else
                                            {
                                                SendData("Register",
                                                    Configuration.Configurations.RegisterMode == 0
                                                        ? "Failed"
                                                        : "Duplicate", res.Client.ConnId);
                                            }
                                        });
                                        break;
                                    }
                                case "RequestFileList":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;
                                        var filePath = Encoding.Unicode.GetString(res.Content[0]);
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
                                                Environment.CurrentDirectory + "\\Files" +
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
                                                res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "RequestFile":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;
                                        var filePath = Encoding.Unicode.GetString(res.Content[0]);
                                        if (filePath.Length > 1)
                                        {
                                            if (filePath.Substring(0, 1) == "\\")
                                                filePath = filePath.Substring(1);
                                            if (filePath.Substring(filePath.Length - 1) == "\\")
                                                filePath = filePath.Substring(filePath.Length - 1);
                                        }
                                        filePath = Environment.CurrentDirectory + "\\Files\\" + filePath;
                                        if (File.Exists(filePath))
                                            UpdateMainPageState(
                                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.Client.UserName} 请求文件：{filePath}");
                                        Task.Run(() => { SendFile(filePath, res.Client.ConnId); });
                                        break;
                                    }
                                case "RequestProblemDataSet":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;
                                        if (!Configuration.Configurations.AllowRequestDataSet)
                                        {
                                            SendData("ProblemDataSet", "Denied", res.Client.ConnId);
                                        }
                                        else
                                        {
                                            UpdateMainPageState(
                                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.Client.UserName} 请求题目 {GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))} 的数据");

                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                try
                                                {
                                                    var problem =
                                                        GetProblem(Convert.ToInt32(
                                                            Encoding.Unicode.GetString(res.Content[0])));

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
                                                                Environment.CurrentDirectory + "\\Data")
                                                            .Replace("${name}", GetEngName(problemName))
                                                            .Replace("${index0}", cur.ToString())
                                                            .Replace("${index}", (cur + 1).ToString());
                                                    }

                                                    var ms = new MemoryStream();
                                                    using (var zip = new ZipFile())
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
                                                                Environment.CurrentDirectory,
                                                                string.Empty);
                                                            inputFilePath = inputFilePath.Substring(0,
                                                                                inputFilePath.LastIndexOf("\\",
                                                                                    StringComparison.Ordinal)) + "\\" +
                                                                            (i + 1);
                                                            var outputFilePath = outputName.Replace(
                                                                Environment.CurrentDirectory,
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
                                                        , res.Client.ConnId);
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
                                        if (res.Client.UserId == 0)
                                            break;
                                        if (!string.IsNullOrEmpty(Encoding.Unicode.GetString(res.Content[1])))
                                        {
                                            UpdateMainPageState(
                                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.Client.UserName} 提交了题目 {GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))} 的代码");

                                            var code = string.Empty;
                                            for (var i = 2; i < res.Content.Count; i++)
                                                if (i != res.Content.Count - 1)
                                                    code += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                                else
                                                    code += Encoding.Unicode.GetString(res.Content[i]);
                                            new Thread(() =>
                                            {
                                                var j = new Judge(
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])),
                                                    res.Client.UserId, code,
                                                    Encoding.Unicode.GetString(res.Content[1]), true, "在线评测");
                                                var jr = JsonConvert.SerializeObject(j.JudgeResult);
                                                SendData("JudgeResult", jr, res.Client.ConnId);
                                            }).Start();
                                        }

                                        break;
                                    }
                                case "Messaging":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        var t = JsonConvert.DeserializeObject<Message>(x);
                                        UpdateMainPageState(
                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {res.Client.UserName} 向 {t.User} 发送了消息");
                                        if (t.User == GetUserName(1) || (UserHelper.CurrentUser.UserId != 0 && t.User == UserHelper.CurrentUser.UserName))
                                        {
                                            lock (DataBaseLock)
                                            {
                                                using (var cmd = new SQLiteCommand(_sqLite))
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
                                                    parameters[0].Value = res.Client.UserId;
                                                    parameters[1].Value = GetUserId(t.User);
                                                    parameters[2].Value = DateTime.Now;
                                                    parameters[3].Value = t.Content;
                                                    parameters[4].Value = 1;
                                                    cmd.Parameters.AddRange(parameters);
                                                    cmd.ExecuteNonQuery();
                                                }
                                            }
                                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                            {
                                                var y = new Messaging();
                                                y.SetMessage(new Message { Content = t.Content, User = res.Client.UserName, State = 1, MessageTime = DateTime.Now });
                                                y.Show();
                                            }));
                                        }
                                        else SendMsg(t.Content, res.Client.UserId, GetUserId(t.User));
                                        break;
                                    }
                                case "RequestProblemList":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;
                                        var id = Encoding.Unicode.GetString(res.Content[0]);
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
                                                        Environment.CurrentDirectory + "\\Data")
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
                                            {
                                                SendData("ProblemList", id + Divpar + JsonConvert.SerializeObject(i), res.Client.ConnId);
                                            }
                                        }));
                                        break;
                                    }
                                case "RequestProfile":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            var x = JsonConvert.SerializeObject(
                                                GetUser(Encoding.Unicode.GetString(res.Content[0])));
                                            SendData("Profile", x, res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "RequestCompiler":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        var cmp = Configuration.Configurations.Compiler.Select(t => new Compiler { DisplayName = t.DisplayName }).ToList();
                                        var x = JsonConvert.SerializeObject(cmp);
                                        SendData("Compiler", x, res.Client.ConnId);
                                        break;
                                    }
                                case "ChangePassword":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            SendData("ChangePassword",
                                                RemoteChangePassword(res.Client.UserName,
                                                    Encoding.Unicode.GetString(res.Content[0]),
                                                    Encoding.Unicode.GetString(res.Content[1]))
                                                    ? "Succeed"
                                                    : "Failed", res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "UpdateProfile":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            SendData("UpdateProfile",
                                                RemoteUpdateProfile(
                                                    res.Client.UserId,
                                                    Encoding.Unicode.GetString(res.Content[0]),
                                                    Encoding.Unicode.GetString(res.Content[1]))
                                                    ? "Succeed"
                                                    : "Failed", res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "UpdateCoins":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            SendData("UpdateCoins",
                                                UpdateCoins(
                                                    res.Client.UserId,
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))
                                                    ? "Succeed"
                                                    : "Failed", res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "UpdateExperience":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        SendData("UpdateExperience",
                                            UpdateExperience(
                                                res.Client.UserId,
                                                Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))
                                                ? "Succeed"
                                                : "Failed", res.Client.ConnId);
                                        break;
                                    }
                                case "RequestJudgeRecord":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            var x = GetJudgeRecord(res.Client.UserId,
                                                Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])),
                                                Convert.ToInt32(Encoding.Unicode.GetString(res.Content[1])));
                                            SendData("JudgeRecord",
                                                Encoding.Unicode.GetString(res.Content[0]) + Divpar +
                                                x.Length + Divpar +
                                                JsonConvert.SerializeObject(x),
                                                res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "RequestJudgeCode":
                                    {
                                        if (res.Client.UserId == 0)
                                            break;

                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            SendData("JudgeCode",
                                                JsonConvert.SerializeObject(GetJudgeInfo(Convert.ToInt32(
                                                    Encoding.Unicode.GetString(res.Content[0])))),
                                                res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "AddProblem":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            SendData("AddProblem", JsonConvert.SerializeObject(GetProblem(NewProblem())), res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "DeleteProblem":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            DeleteProblem(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])));
                                            SendData("DeleteProblem", string.Empty, res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "UpdateProblem":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        var x = string.Empty;
                                        for (var i = 0; i < res.Content.Count; i++)
                                            if (i != res.Content.Count - 1)
                                                x += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                            else
                                                x += Encoding.Unicode.GetString(res.Content[i]);
                                        var p = JsonConvert.DeserializeObject<Problem>(x);
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            UpdateProblem(p);
                                            SendData("UpdateProblem", string.Empty, res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "QueryProblems":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            var x = QueryProblems(true);
                                            foreach (var i in x)
                                            {
                                                i.Description = string.Empty;
                                            }
                                            SendData("QueryProblems", JsonConvert.SerializeObject(x), res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "GetProblemDescription":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            var x = GetProblem(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])));
                                            SendData("GetProblemDescription", JsonConvert.SerializeObject(new Problem { Description = x?.Description ?? string.Empty }), res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "QueryJudgeLogs":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            SendData("QueryJudgeLogs", JsonConvert.SerializeObject(QueryJudgeLog(false)), res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "RequestCode":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            var x = GetJudgeInfo(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])));
                                            SendData("RequestCode", JsonConvert.SerializeObject(new JudgeInfo { Code = x?.Code ?? string.Empty }), res.Client.ConnId);
                                        }));
                                        break;
                                    }
                                case "ClearJudgingLogs":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        ActionList.Enqueue(new Task(() => ClearJudgeLog()));
                                        break;
                                    }
                                case "DataFile":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        var fileName = Encoding.Unicode.GetString(res.Content[0]);
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
                                                            $"{Environment.CurrentDirectory}\\Data");
                                                    }
                                                    catch
                                                    {
                                                        SendData("DataFile", "Failed", res.Client.ConnId);
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
                                                    SendData("DataFile", "Succeeded", res.Client.ConnId);
                                                }));
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                if (!Directory.Exists($"{Environment.GetEnvironmentVariable("temp")}\\{fileId}"))
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
                                                SendData("DataFile", "Failed", res.Client.ConnId);
                                            }
                                        }
                                        break;
                                    }
                                case "PublicFile":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        var fileName = Encoding.Unicode.GetString(res.Content[0]);
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
                                                            $"{Environment.CurrentDirectory}\\Files");
                                                    }
                                                    catch
                                                    {
                                                        SendData("PublicFile", "Failed", res.Client.ConnId);
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
                                                    SendData("PublicFile", "Succeeded", res.Client.ConnId);
                                                }));
                                            }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                if (!Directory.Exists($"{Environment.GetEnvironmentVariable("temp")}\\{fileId}"))
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
                                                SendData("PublicFile", "Failed", res.Client.ConnId);
                                            }
                                        }
                                        break;
                                    }
                                case "ClearData":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        var tid = Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0]));
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
                                                        Environment.CurrentDirectory + "\\Data")
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
                                                {
                                                    try
                                                    {
                                                        File.Delete(fin);
                                                    }
                                                    catch
                                                    {
                                                        //ignored
                                                    }
                                                }
                                                if (!string.IsNullOrEmpty(fout))
                                                {
                                                    try
                                                    {
                                                        File.Delete(fout);
                                                    }
                                                    catch
                                                    {
                                                        //ignored
                                                    }
                                                }
                                            }
                                        }));
                                        break;
                                    }
                                case "DeleteExtra":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        var tid = Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0]));
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
                                                        Environment.CurrentDirectory + "\\Data")
                                                    .Replace("${name}", GetEngName(problemName));
                                            }

                                            var p = GetProblem(tid);
                                            if (p.ProblemId == 0) return;
                                            foreach (var f in p.ExtraFiles)
                                            {
                                                var fr = GetRealString(f, p.ProblemName);
                                                if (!string.IsNullOrEmpty(fr))
                                                {
                                                    try
                                                    {
                                                        File.Delete(fr);
                                                    }
                                                    catch
                                                    {
                                                        //ignored
                                                    }
                                                }
                                            }
                                        }));
                                        break;
                                    }
                                case "DeleteJudge":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetUser(res.Client.UserId);
                                        if (t.Type <= 0 || t.Type >= 4) break;
                                        var tid = Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0]));
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
                                                        Environment.CurrentDirectory + "\\Data")
                                                    .Replace("${name}", GetEngName(problemName));
                                            }

                                            var p = GetProblem(tid);
                                            if (p.ProblemId == 0) return;
                                            var f = GetRealString(p.SpecialJudge, p.ProblemName);
                                            if (!string.IsNullOrEmpty(f))
                                            {
                                                try
                                                {
                                                    File.Delete(f);
                                                }
                                                catch
                                                {
                                                    //ignored
                                                }
                                            }
                                        }));
                                        break;
                                    }
                                case "RequestMsgList":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var id = Encoding.Unicode.GetString(res.Content[0]);
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            var t = QueryMsg(res.Client.UserId, false);
                                            t.Reverse();
                                            foreach (var i in t)
                                            {
                                                SendData("RequestMsgList", id + Divpar + JsonConvert.SerializeObject(i), res.Client.ConnId);
                                            }
                                        }));
                                        break;
                                    }
                                case "RequestMsg":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var t = GetMsg(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])));
                                        ActionList.Enqueue(new Task(() => SendData("RequestMsg", JsonConvert.SerializeObject(t), res.Client.ConnId)));
                                        break;
                                    }
                                case "RequestMsgTargetUser":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var id = Encoding.Unicode.GetString(res.Content[0]);
                                        ActionList.Enqueue(new Task(() =>
                                        {
                                            var x = GetUser(res.Client.UserId);
                                            var t = GetSpecialTypeUser(1);
                                            if (x.Type >= 4)
                                            {
                                                t.AddRange(GetSpecialTypeUser(2));
                                                t.AddRange(GetSpecialTypeUser(3));
                                                if (Configuration.Configurations.AllowCompetitorMessaging)
                                                {
                                                    t.AddRange(GetSpecialTypeUser(4));
                                                }
                                            }
                                            else
                                            {
                                                t.AddRange(GetUsersBelongs(1));
                                            }
                                            var p = new List<string>();
                                            p.AddRange(t.Where(i => i.UserId != res.Client.UserId).Select(i => i.UserName));
                                            foreach (var i in p)
                                            {
                                                SendData("RequestMsgTargetUser", id + Divpar + JsonConvert.SerializeObject(i), res.Client.ConnId);
                                            }
                                        }));
                                        break;
                                    }
                                case "SetMsgState":
                                    {
                                        if (res.Client.UserId == 0) break;
                                        var msgId = Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0]));
                                        var state = Convert.ToInt32(Encoding.Unicode.GetString(res.Content[1]));
                                        ActionList.Enqueue(new Task(() => SetMsgState(msgId, state)));
                                        break;
                                    }
                            }
                        }
                        catch (Exception ex)
                        {
                            SendData(res.Operation, "ActionFailed" + Divpar + ex.Message + Divpar + ex.StackTrace, res.Client.ConnId);
                        }
                    }
                    Thread.Sleep(1);
                }
            });
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
            Task.Run(() =>
            {
                var cnt = 0;
                long tot = 0;
                var last = 0;
                var addition = 0;
                var limit = Environment.ProcessorCount * 2;
                var dealingTime = DateTime.Now;
                while (true)
                {
                    if (ActionList.Any())
                    {
                        if (cnt < limit + addition)
                        {
                            if (addition != 0) addition--;
                            dealingTime = DateTime.Now;
                            if (ActionList.TryDequeue(out var t))
                            {
                                t.ContinueWith(o =>
                                {
                                    lock (ActionCounterLock)
                                    {
                                        tot++;
                                        cnt--;
                                        UpdateMainPageState($"当前负荷：待投递任务：{ActionList.Count}，待处理任务：{cnt}。已完成任务：{tot}", textBlock);
                                    }
                                });
                                t.Start();
                                lock (ActionCounterLock)
                                {
                                    cnt++;
                                    UpdateMainPageState($"当前负荷：待投递任务：{ActionList.Count}，待处理任务：{cnt}。已完成任务：{tot}", textBlock);
                                }
                            }
                        }
                        else
                        {
                            if ((DateTime.Now - dealingTime).TotalSeconds > 5)
                            {
                                addition++;
                            }
                        }
                        if (last != ActionList.Count)
                        {
                            UpdateMainPageState($"当前负荷：待投递任务：{ActionList.Count}，待处理任务：{cnt}。已完成任务：{tot}", textBlock);
                            last = ActionList.Count;
                        }
                    }
                    Thread.Sleep(1);
                }
            });
        }
    }
}
