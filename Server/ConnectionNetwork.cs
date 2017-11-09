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
        #region Network

        private static readonly int PkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        private static readonly List<ClientData> Recv = new List<ClientData>();
        private static readonly ConcurrentQueue<ObjOperation> Operations = new ConcurrentQueue<ObjOperation>();
        private static readonly TcpPullServer<ClientInfo> HServer = new TcpPullServer<ClientInfo>();

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
                    i.Data.Clear();
                }
        }

        private static void SendData(string operation, IEnumerable<byte> sendBytes, IntPtr connId)
        {
            var temp = Encoding.Unicode.GetBytes(operation);
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
            temp = temp.Concat(sendBytes).ToArray();
            temp = temp.Concat(Encoding.Unicode.GetBytes(Divtot)).ToArray();
            var final = GetSendBuffer(temp);
            HServer.Send(connId, final, final.Length);
        }

        private static void SendFile(string fileName, IntPtr connId)
        {
            var fileId = Guid.NewGuid().ToString();
            var temp = Encoding.Unicode.GetBytes("File" + Divpar
                                                 + Path.GetFileName(fileName) + Divpar
                                                 + fileId + Divpar
                                                 + new FileInfo(fileName).Length + Divtot);
            var final = GetSendBuffer(temp);
            HServer.Send(connId, final, final.Length);
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long tot = 0;
                while (tot != fs.Length)
                {
                    var bytes = new byte[65536];
                    long cnt = fs.Read(bytes, 0, 65536);
                    var tempc = GetSendBuffer(Encoding.Unicode.GetBytes("File" + Divpar
                                                                        + Path.GetFileName(fileName) + Divpar
                                                                        + fileId + Divpar + tot + Divpar)
                        .Concat(bytes.Take((int)cnt))
                        .Concat(Encoding.Unicode.GetBytes(Divtot)).ToArray());
                    tot += cnt;
                    HServer.Send(connId, tempc, tempc.Length);
                }
                fs.Close();
            }
        }

        private static void SendData(string operation, string sendString, IntPtr connId)
        {
            var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString + Divtot);
            var final = GetSendBuffer(temp);
            HServer.Send(connId, final, final.Length);
        }

        public static void SendMsg(string sendString, IntPtr connId)
        {
            lock (DataBaseLock)
            {
                using (var cmd = new SQLiteCommand(_sqLite))
                {
                    cmd.CommandText = "Insert INTO Message (FromUserId,ToUserId,SendDate,Content) VALUES (@1,@2,@3,@4)";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.Int32),
                        new SQLiteParameter("@2", DbType.Int32),
                        new SQLiteParameter("@3", DbType.String),
                        new SQLiteParameter("@4", DbType.String)
                    };
                    parameters[0].Value = 1;
                    parameters[1].Value = Recv.FirstOrDefault(i => i.Info.ConnId == connId)?.Info.UserId ?? 0;
                    parameters[2].Value = DateTime.Now;
                    parameters[3].Value = sendString;
                    cmd.Parameters.AddRange(parameters);
                    cmd.ExecuteNonQuery();
                }
            }
            SendData("Messaging", sendString, connId);
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
                            lock (BytesLock)
                            {
                                (from c in Recv where c.Info.ConnId == connId select c).FirstOrDefault()?.Data
                                    .AddRange(buffer);
                            }
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
                        if (t.Data.Count == 0)
                            continue;
                        lock (BytesLock)
                        {
                            var temp = Bytespilt(t.Data.ToArray(), Encoding.Unicode.GetBytes(Divtot));
                            if (temp.Count != 0)
                            {
                                t.Data.Clear();
                                t.Data.AddRange(temp[temp.Count - 1]);
                            }
                            temp.RemoveAt(temp.Count - 1);
                            foreach (var i in temp)
                            {
                                var temp2 = Bytespilt(i, Encoding.Unicode.GetBytes(Divpar));
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
                    {
                        var u = Recv.FirstOrDefault(c => c.Info.ConnId == res.Client.ConnId);
                        if (u != null)
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
                                                        lock (BytesLock)
                                                        {
                                                            var uid = GetUserId(
                                                                Encoding.Unicode.GetString(res.Content[0]));
                                                            foreach (var li in Recv.Where(c => c.Info.UserId == uid))
                                                            {
                                                                UpdateMainPageState(
                                                                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {li.Info.UserName} 多终端登陆，已注销其中一个终端的登录状态");
                                                                li.Info.UserId = 0;
                                                                li.Data.Clear();
                                                                SendData("Logout", "Succeed", li.Info.ConnId);
                                                            }
                                                            u.Info.UserId = uid;
                                                        }
                                                        SendData("Login", "Succeed", u.Info.ConnId);
                                                        UpdateMainPageState(
                                                            $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {u.Info.UserName} 登录了");
                                                        break;
                                                    }
                                                case 1:
                                                    {
                                                        SendData("Login", "Incorrect", u.Info.ConnId);
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        SendData("Login", "Unknown", u.Info.ConnId);
                                                        break;
                                                    }
                                            }
                                            break;
                                        }
                                    case "Logout":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;
                                            UpdateMainPageState(
                                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {u.Info.UserName} 注销了");
                                            u.Data.Clear();
                                            u.Info.UserId = 0;
                                            SendData("Logout", "Succeed", u.Info.ConnId);
                                            break;
                                        }
                                    case "Register":
                                        {
                                            if (u.Info.UserId != 0)
                                                break;
                                            Task.Run(() =>
                                            {
                                                RemoteRegister(Encoding.Unicode.GetString(res.Content[0]),
                                                    Encoding.Unicode.GetString(res.Content[1]));
                                            });
                                            break;
                                        }
                                    case "RequestFileList":
                                        {
                                            if (u.Info.UserId == 0)
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
                                                    u.Info.ConnId);
                                            }));
                                            break;
                                        }
                                    case "RequestFile":
                                        {
                                            if (u.Info.UserId == 0)
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
                                            {
                                                UpdateMainPageState(
                                                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {u.Info.UserName} 请求文件：{filePath}");
                                                Task.Run(() => { SendFile(filePath, u.Info.ConnId); });
                                            }
                                            break;
                                        }
                                    case "RequestProblemDataSet":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;
                                            if (!Configuration.Configurations.AllowRequestDataSet)
                                            {
                                                SendData("ProblemDataSet", "Denied", u.Info.ConnId);
                                            }
                                            else
                                            {
                                                UpdateMainPageState(
                                                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {u.Info.UserName} 请求题目 {GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))} 的数据");

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
                                                            , u.Info.ConnId);
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
                                            if (u.Info.UserId == 0)
                                                break;
                                            if (!string.IsNullOrEmpty(Encoding.Unicode.GetString(res.Content[1])))
                                            {
                                                UpdateMainPageState(
                                                    $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {u.Info.UserName} 提交了题目 {GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))} 的代码");

                                                var code = string.Empty;
                                                for (var i = 2; i < res.Content.Count; i++)
                                                    if (i != res.Content.Count - 1)
                                                        code += Encoding.Unicode.GetString(res.Content[i]) + Divpar;
                                                    else
                                                        code += Encoding.Unicode.GetString(res.Content[i]);
                                                Task.Run(() =>
                                                {
                                                    var j = new Judge(
                                                        Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])),
                                                        u.Info.UserId, code,
                                                        Encoding.Unicode.GetString(res.Content[1]), true);
                                                    var jr = JsonConvert.SerializeObject(j.JudgeResult);
                                                    SendData("JudgeResult", jr, u.Info.ConnId);
                                                });
                                            }

                                            break;
                                        }
                                    case "Messaging":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;
                                            UpdateMainPageState(
                                                $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {u.Info.UserName} 发来了消息");
                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                lock (DataBaseLock)
                                                {
                                                    using (var cmd = new SQLiteCommand(_sqLite))
                                                    {
                                                        cmd.CommandText =
                                                            "Insert INTO Message (FromUserId,ToUserId,SendDate,Content) VALUES (@1,@2,@3,@4)";
                                                        SQLiteParameter[] parameters =
                                                        {
                                                        new SQLiteParameter("@1", DbType.Int32),
                                                        new SQLiteParameter("@2", DbType.Int32),
                                                        new SQLiteParameter("@3", DbType.String),
                                                        new SQLiteParameter("@4", DbType.String)
                                                        };
                                                        parameters[0].Value = u.Info.UserId;
                                                        parameters[1].Value = 1;
                                                        parameters[2].Value = DateTime.Now;
                                                        parameters[3].Value = res.Content[0];
                                                        cmd.Parameters.AddRange(parameters);
                                                        cmd.ExecuteNonQuery();
                                                    }
                                                }
                                            }));
                                            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                            {
                                                var x = new Messaging();
                                                x.SetMessage(Encoding.Unicode.GetString(res.Content[0]),
                                                    u.Info.ConnId, u.Info.UserName);
                                                x.Show();
                                            }));

                                            break;
                                        }
                                    case "RequestProblemList":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

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

                                                var pl = QueryProblems();
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
                                                }
                                                var x = JsonConvert.SerializeObject(pl);
                                                SendData("ProblemList", x, u.Info.ConnId);
                                            }));
                                            break;
                                        }
                                    case "RequestProfile":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                var x = JsonConvert.SerializeObject(
                                                    GetUser(Encoding.Unicode.GetString(res.Content[0])));
                                                SendData("Profile", x, u.Info.ConnId);
                                            }));
                                            break;
                                        }
                                    case "RequestCompiler":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            var cmp = Configuration.Configurations.Compiler.Select(t => new Compiler { DisplayName = t.DisplayName }).ToList();
                                            var x = JsonConvert.SerializeObject(cmp);
                                            SendData("Compiler", x, u.Info.ConnId);
                                            break;
                                        }
                                    case "ChangePassword":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                SendData("ChangePassword",
                                                    RemoteChangePassword(u.Info.UserName,
                                                        Encoding.Unicode.GetString(res.Content[0]),
                                                        Encoding.Unicode.GetString(res.Content[1]))
                                                        ? "Succeed"
                                                        : "Failed", u.Info.ConnId);
                                            }));
                                            break;
                                        }
                                    case "UpdateProfile":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                SendData("UpdateProfile",
                                                    RemoteUpdateProfile(
                                                        u.Info.UserId,
                                                        Encoding.Unicode.GetString(res.Content[0]),
                                                        Encoding.Unicode.GetString(res.Content[1]))
                                                        ? "Succeed"
                                                        : "Failed", u.Info.ConnId);
                                            }));
                                            break;
                                        }
                                    case "UpdateCoins":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                SendData("UpdateCoins",
                                                    UpdateCoins(
                                                        u.Info.UserId,
                                                        Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))
                                                        ? "Succeed"
                                                        : "Failed", u.Info.ConnId);
                                            }));
                                            break;
                                        }
                                    case "UpdateExperience":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            SendData("UpdateExperience",
                                                UpdateExperience(
                                                    u.Info.UserId,
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))
                                                    ? "Succeed"
                                                    : "Failed", u.Info.ConnId);
                                            break;
                                        }
                                    case "RequestJudgeRecord":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                var x = GetJudgeRecord(u.Info.UserId,
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])),
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[1])));
                                                SendData("JudgeRecord",
                                                    Encoding.Unicode.GetString(res.Content[0]) + Divpar +
                                                    x.Length + Divpar +
                                                    JsonConvert.SerializeObject(x),
                                                    u.Info.ConnId);
                                            }));
                                            break;
                                        }
                                    case "RequestJudgeCode":
                                        {
                                            if (u.Info.UserId == 0)
                                                break;

                                            ActionList.Enqueue(new Task(() =>
                                            {
                                                SendData("JudgeCode",
                                                    JsonConvert.SerializeObject(GetJudgeInfo(Convert.ToInt32(
                                                        Encoding.Unicode.GetString(res.Content[0])))),
                                                    u.Info.ConnId);
                                            }));
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

        private static void ActionExecuter()
        {
            Task.Run(() =>
            {
                var textBlock = UpdateMainPageState("待投递事项：0，待处理事项：0");
                var cnt = 0;
                var last = 0;
                while (true)
                {
                    if (ActionList.Any())
                    {
                        if (cnt <= 5)
                            if (ActionList.TryDequeue(out var t))
                            {
                                t.ContinueWith(o =>
                                {
                                    lock (ActionCounterLock)
                                    {
                                        cnt--;
                                        UpdateMainPageState($"待投递事项：{ActionList.Count}，待处理事项：{cnt}", textBlock);
                                    }
                                });
                                t.Start();
                                lock (ActionCounterLock)
                                {
                                    cnt++;
                                    UpdateMainPageState($"待投递事项：{ActionList.Count}，待处理事项：{cnt}", textBlock);
                                }
                            }
                        if (last != ActionList.Count)
                        {
                            UpdateMainPageState($"待投递事项：{ActionList.Count}，待处理事项：{cnt}", textBlock);
                            last = ActionList.Count;
                        }
                    }
                    Thread.Sleep(1);
                }
            });
        }

        #endregion
    }
}
