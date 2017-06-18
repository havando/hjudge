using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using HPSocketCS;
using System.Runtime.InteropServices;

namespace Server
{
    public static class Connection
    {
        private static SQLiteConnection _sqLite;
        private static List<byte> _recv = new List<byte>();
        private static ConcurrentQueue<ObjOperation> _operations = new ConcurrentQueue<ObjOperation>();
        private static readonly int PkgHeaderSize = Marshal.SizeOf(new PkgHeader());
        public static string Address;
        private static readonly TcpPullServer HServer = new TcpPullServer();

        public static void Init()
        {
            #region DataBase

            if (!File.Exists(Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"))
            {
                SQLiteConnection.CreateFile(Environment.CurrentDirectory + "\\AppData\\hjudgeData.db");
                var sqLite = new SQLiteConnection("Data Source=" +
                                               $"{Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
                sqLite.Open();
                using (var cmd = new SQLiteCommand(sqLite))
                {
                    var sqlTable = new StringBuilder();
                    sqlTable.Append("CREATE TABLE Judge (");
                    sqlTable.Append("JudgeId integer PRIMARY KEY autoincrement,");
                    sqlTable.Append("UserId int,");
                    sqlTable.Append("Date ntext,");
                    sqlTable.Append("ProblemId int,");
                    sqlTable.Append("Code ntext,");
                    sqlTable.Append("Timeused ntext,");
                    sqlTable.Append("Memoryused ntext,");
                    sqlTable.Append("Exitcode ntext,");
                    sqlTable.Append("Result ntext,");
                    sqlTable.Append("Score ntext)");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                    sqlTable.Append("CREATE TABLE User (");
                    sqlTable.Append("UserId integer PRIMARY KEY autoincrement,");
                    sqlTable.Append("UserName ntext,");
                    sqlTable.Append("RegisterDate ntext,");
                    sqlTable.Append("Password ntext,");
                    sqlTable.Append("Type int,");
                    sqlTable.Append("Icon ntext,");
                    sqlTable.Append("Achievement ntext )");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                    sqlTable.Append("CREATE TABLE Problem (");
                    sqlTable.Append("ProblemId integer PRIMARY KEY autoincrement,");
                    sqlTable.Append("ProblemName ntext,");
                    sqlTable.Append("AddDate ntext,");
                    sqlTable.Append("Level int,");
                    sqlTable.Append("DataSets ntext,");
                    sqlTable.Append("Type int,");
                    sqlTable.Append("SpecialJudge ntext,");
                    sqlTable.Append("ExtraFiles ntext,");
                    sqlTable.Append("InputFileName ntext,");
                    sqlTable.Append("OutputFileName ntext,");
                    sqlTable.Append("CompileCommand ntext)");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                } //CreateTable
                using (var cmd = new SQLiteCommand(sqLite))
                {
                    cmd.CommandText = "INSERT INTO User (UserName,RegisterDate,Password,Type,Icon,Achievement) VALUES (@1,@2,@3,@4,@5,@6)";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.String),
                        new SQLiteParameter("@2", DbType.String),
                        new SQLiteParameter("@3", DbType.String),
                        new SQLiteParameter("@4", DbType.Int32),
                        new SQLiteParameter("@5", DbType.String),
                        new SQLiteParameter("@6", DbType.String)
                    };
                    parameters[0].Value = "hjudgeBOSS";
                    parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
                    parameters[2].Value = "cefb1f85346dfbfa4a341e9c41db918ba25bccc4e62c3939390084361126a417";
                    parameters[3].Value = 1;
                    parameters[4].Value = "";
                    parameters[5].Value = "";
                    cmd.Parameters.AddRange(parameters);
                    cmd.ExecuteNonQuery();
                } //InsertBOSSAccount
                sqLite.Close();
            }
            _sqLite = new SQLiteConnection("Data Source=" +
                                          $"{Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
            _sqLite.Open();

            #endregion

            #region Network

            var hostIp = Dns.GetHostAddresses(Dns.GetHostName());
            var flag = false;

            HServer.OnAccept += (id, client) =>
            {
                var ip = "";
                ushort port = 0;
                if (!HServer.GetRemoteAddress(id, ref ip, ref port)) { return HandleResult.Ignore; }
                var clientInfo = new ClientInfo
                {
                    ConnId = id,
                    IpAddress = ip,
                    Port = port,
                    PkgInfo = new PkgInfo
                    {
                        IsHeader = true,
                        Length = PkgHeaderSize
                    }
                };
                HServer.SetExtra(id, clientInfo);
                return HandleResult.Ok;
            };
            HServer.OnClose += (id, operation, code) =>
            {
                HServer.RemoveExtra(id);
                return HandleResult.Ok;
            };
            HServer.OnReceive += HServerOnOnReceive;

            foreach (var t in hostIp)
            {
                if (t.ToString().Contains(":"))
                {
                    continue;
                }
                Address = t + ":23333";
                HServer.IpAddress = t.ToString();
                HServer.Port = 23333;
                if (!HServer.Start()) { continue; }
                flag = true;
                break;
            }
            DealingOperations();
            if (flag) return;
            MessageBox.Show("服务器初始化失败，请检查网络", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);

            #endregion

        }

        #region DataBase



        public static string Logout()
        {
            var a = UserHelper.CurrentUser.UserName;
            UserHelper.SetCurrentUser(0, "", "", "", 0, "", "");
            UserHelper.CurrentUser.IsChanged = false;
            return a;
        }

        public static async Task<int> Login(string userName, string password)
        {
            SHA256 s = new SHA256CryptoServiceProvider();
            var retVal = s.ComputeHash(Encoding.Unicode.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var t in retVal)
            {
                sb.Append(t.ToString("x2"));
            }
            var a = await TryLogin(userName, sb.ToString());
            return a;
        }

        public static void UpdateUserInfo(UserInfo toUpdateInfo)
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "UPDATE User SET UserName=@1, Password=@2, Icon=@3 WHERE UserId=@4";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String),
                    new SQLiteParameter("@2", DbType.String),
                    new SQLiteParameter("@3", DbType.String),
                    new SQLiteParameter("@4", DbType.Int32)
                };
                parameters[0].Value = toUpdateInfo.UserName;
                parameters[1].Value = toUpdateInfo.Password;
                parameters[2].Value = toUpdateInfo.Icon;
                parameters[3].Value = toUpdateInfo.UserId;
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }

        private static Task<int> TryLogin(string userName, string passwordHash)
        {
            return Task.Run(() =>
            {
                using (var cmd = new SQLiteCommand(_sqLite))
                {
                    cmd.CommandText = "SELECT * From User Where userName=@1";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.String)
                    };
                    parameters[0].Value = userName;
                    cmd.Parameters.AddRange(parameters);
                    var reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (passwordHash == reader.GetString(3))
                            {
                                Console.Write(reader.GetString(2));
                                UserHelper.SetCurrentUser(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.GetString(6));
                                return 0;
                            }
                            return 1;
                        }
                    }
                    else
                    {
                        return 1;
                    }

                }
                return 2;
            });
        }

        public static ObservableCollection<UserInfo> GetUsersBelongs(int userType)
        {
            var a = new ObservableCollection<UserInfo>();
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From User Where Type>@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32)
                };
                switch (userType)
                {
                    case 1:
                        parameters[0].Value = 1;
                        break;
                    case 2:
                        parameters[0].Value = 2;
                        break;
                    case 3:
                        parameters[0].Value = 3;
                        break;
                }
                cmd.Parameters.AddRange(parameters);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        a.Add(new UserInfo
                        {
                            UserId = reader.GetInt32(0),
                            UserName = reader.GetString(1),
                            Password = reader.GetString(3),
                            Type = reader.GetInt32(4)
                        });
                    }
                }
                return a;
            }
        }

        public static Problem GetProblem(int problemId)
        {
            var a = new Problem();
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From Problem Where ProblemId=@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String)
                };
                parameters[0].Value = problemId;
                cmd.Parameters.AddRange(parameters);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    if (!reader.Read()) return a;
                    a.ProblemId = reader.GetInt32(0);
                    a.AddDate = reader.GetString(2);
                    try
                    {
                        a.ProblemName = reader.GetString(1);
                        a.Level = reader.GetInt32(3);
                        a.DataSets = CastJsonArrToDataArr((JArray)JsonConvert.DeserializeObject(reader.GetString(4)));
                        a.Type = reader.GetInt32(5);
                        a.SpecialJudge = reader.GetString(6);
                        a.ExtraFiles = CastJsonArrToStringArr((JArray)JsonConvert.DeserializeObject(reader.GetString(7)));
                        a.InputFileName = reader.GetString(8);
                        a.OutputFileName = reader.GetString(9);
                        a.CompileCommand = reader.GetString(10);
                    }
                    catch
                    {
                        //ignored
                    }
                    return a;
                }
                a.ProblemId = 0;
                return a;
            }
        }

        public static List<string> SaveUser(IEnumerable<int> toDelete)
        {
            var failed = new List<string>();
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                foreach (var t in toDelete)
                {
                    cmd.CommandText = "DELETE From User Where UserId=@1";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.Int32)
                    };
                    parameters[0].Value = t;
                    cmd.Parameters.AddRange(parameters);
                    cmd.ExecuteNonQuery();
                }
                foreach (var t in UserHelper.UsersBelongs)
                {
                    if (t.UserId != 0)
                    {
                        if (!(t.IsChanged ?? false)) continue;
                        cmd.CommandText = "UPDATE User SET Password=@1, Type=@2 WHERE UserId=@3";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.String),
                            new SQLiteParameter("@2", DbType.Int32),
                            new SQLiteParameter("@3", DbType.Int32)
                        };
                        parameters[0].Value = t.Password;
                        parameters[1].Value = t.Type;
                        parameters[2].Value = t.UserId;
                        cmd.Parameters.AddRange(parameters);
                        cmd.ExecuteNonQuery();
                    }
                    else
                    {
                        if (CheckUser(t.UserName) != 0)
                        {
                            failed.Add(t.UserName);
                            continue;
                        }
                        cmd.CommandText =
                            "INSERT INTO User (UserName,RegisterDate,Password,Type,Icon,Achievement) VALUES (@1,@2,@3,@4,@5,@6)";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.String),
                            new SQLiteParameter("@2", DbType.String),
                            new SQLiteParameter("@3", DbType.String),
                            new SQLiteParameter("@4", DbType.Int32),
                            new SQLiteParameter("@5", DbType.String),
                            new SQLiteParameter("@6", DbType.String)
                        };
                        parameters[0].Value = t.UserName;
                        parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
                        parameters[2].Value = t.Password;
                        parameters[3].Value = t.Type;
                        parameters[4].Value = "";
                        parameters[5].Value = "";
                        cmd.Parameters.AddRange(parameters);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            return failed;
        }

        private static int CheckUser(string userName)
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From User Where UserName=@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String)
                };
                parameters[0].Value = userName;
                cmd.Parameters.AddRange(parameters);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    if (reader.Read())
                    {
                        return reader.GetInt32(0);
                    }
                }
            }
            return 0;
        }

        public static void ClearJudgeLog()
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "Delete From Judge";
                cmd.ExecuteNonQuery();
                cmd.CommandText = "DELETE FROM sqlite_sequence WHERE name = 'Judge'";
                cmd.ExecuteNonQuery();
            }
        }

        public static ObservableCollection<JudgeInfo> QueryJudgeLog()
        {
            var curJudgeInfo = new ObservableCollection<JudgeInfo>();
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                if (UserHelper.CurrentUser.Type != 4) { cmd.CommandText = "SELECT * From Judge"; }
                else { cmd.CommandText = $"SELECT * From Judge Where UserId={UserHelper.CurrentUser.UserId}"; }
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        try
                        {
                            curJudgeInfo.Add(new JudgeInfo
                            {
                                JudgeId = reader.GetInt32(0),
                                UserId = reader.GetInt32(1),
                                JudgeDate = reader.GetString(2),
                                ProblemId = reader.GetInt32(3),
                                Code = reader.GetString(4),
                                Timeused = CastStringArrToLongArr(reader.GetString(5).Split(',')),
                                Memoryused = CastStringArrToLongArr(reader.GetString(6).Split(',')),
                                Exitcode = CastStringArrToIntArr(reader.GetString(7).Split(',')),
                                Result = reader.GetString(8).Split(','),
                                Score = CastStringArrToFloatArr(reader.GetString(9).Split(','))
                            });
                        }
                        catch
                        {
                            curJudgeInfo.Add(new JudgeInfo()
                            {
                                JudgeId = reader.GetInt32(0),
                                JudgeDate = reader.GetString(2)
                            });
                        }
                    }
                }
            }
            return curJudgeInfo;
        }

        private static int[] CastStringArrToIntArr(IReadOnlyList<string> p)
        {
            if (p == null) return null;
            var f = new int[p.Count];
            for (var i = 0; i < p.Count; i++)
            {
                f[i] = Convert.ToInt32(p[i]);
            }
            return f;
        }

        private static long[] CastStringArrToLongArr(IReadOnlyList<string> p)
        {
            if (p == null) return null;
            var f = new long[p.Count];
            for (var i = 0; i < p.Count; i++)
            {
                f[i] = Convert.ToInt64(p[i]);
            }
            return f;
        }

        private static float[] CastStringArrToFloatArr(IReadOnlyList<string> p)
        {
            if (p == null) return null;
            var f = new float[p.Count];
            for (var i = 0; i < p.Count; i++)
            {
                f[i] = Convert.ToSingle(p[i]);
            }
            return f;
        }

        public static string GetUserName(int userId)
        {
            var userName = "";
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From User Where UserId=@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32)
                };
                parameters[0].Value = userId;
                cmd.Parameters.AddRange(parameters);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        userName = reader.GetString(1);
                        break;
                    }
                }
            }
            return userName;
        }

        public static string GetProblemName(int problemId)
        {
            var problemName = "";
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From Problem Where ProblemId=@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32)
                };
                parameters[0].Value = problemId;
                cmd.Parameters.AddRange(parameters);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        problemName = reader.GetString(1);
                        break;
                    }
                }
            }
            return problemName;
        }

        public static int NewJudge()
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "Insert into Judge (Date) VALUES (@1)";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String)
                };
                parameters[0].Value = DateTime.Now.ToString("yyyyMMddHHmmssffff");
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
                cmd.CommandText = "select last_insert_rowid() from Judge";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public static void UpdateJudgeInfo(JudgeInfo pInfo)
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "UPDATE Judge SET UserId=@1, ProblemId=@2, Code=@3, Timeused=@4, Memoryused=@5, Exitcode=@6, Result=@7, Score=@8 Where JudgeId=@9";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32),
                    new SQLiteParameter("@2", DbType.Int32),
                    new SQLiteParameter("@3", DbType.String),
                    new SQLiteParameter("@4", DbType.String),
                    new SQLiteParameter("@5", DbType.String),
                    new SQLiteParameter("@6", DbType.String),
                    new SQLiteParameter("@7", DbType.String),
                    new SQLiteParameter("@8", DbType.String),
                    new SQLiteParameter("@9", DbType.Int32)
                };
                parameters[0].Value = pInfo.UserId;
                parameters[1].Value = pInfo.ProblemId;
                parameters[2].Value = pInfo.Code;
                string timeused = "", memoryused = "", exitcode = "", result = "", score = "";
                for (var i = 0; i < pInfo.Result.Length; i++)
                {
                    if (i != pInfo.Timeused.Length - 1)
                    {
                        timeused += pInfo.Timeused[i] + ",";
                        memoryused += pInfo.Memoryused[i] + ",";
                        exitcode += pInfo.Exitcode[i] + ",";
                        result += pInfo.Result[i] + ",";
                        score += pInfo.Score[i] + ",";
                    }
                    else
                    {
                        timeused += pInfo.Timeused[i];
                        memoryused += pInfo.Memoryused[i];
                        exitcode += pInfo.Exitcode[i];
                        result += pInfo.Result[i];
                        score += pInfo.Score[i];
                    }
                }
                parameters[3].Value = timeused;
                parameters[4].Value = memoryused;
                parameters[5].Value = exitcode;
                parameters[6].Value = result;
                parameters[7].Value = score;
                parameters[8].Value = pInfo.JudgeId;
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }

        public static ObservableCollection<Problem> QueryProblems()
        {
            var curJudgeInfo = new ObservableCollection<Problem>();
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From Problem";
                var reader = cmd.ExecuteReader();
                if (!reader.HasRows) return curJudgeInfo;
                while (reader.Read())
                {
                    try
                    {
                        curJudgeInfo.Add(new Problem
                        {
                            ProblemId = reader.GetInt32(0),
                            ProblemName = reader.GetString(1),
                            AddDate = reader.GetString(2),
                            Level = reader.GetInt32(3),
                            DataSets =
                                CastJsonArrToDataArr((JArray)JsonConvert.DeserializeObject(reader.GetString(4))),
                            Type = reader.GetInt32(5),
                            SpecialJudge = reader.GetString(6),
                            ExtraFiles =
                                CastJsonArrToStringArr((JArray)JsonConvert.DeserializeObject(reader.GetString(7))),
                            InputFileName = reader.GetString(8),
                            OutputFileName = reader.GetString(9),
                            CompileCommand = reader.GetString(10),
                        });
                    }
                    catch
                    {
                        curJudgeInfo.Add(new Problem()
                        {
                            AddDate = reader.GetString(2),
                            ProblemId = reader.GetInt32(0)
                        });
                    }
                }
            }
            return curJudgeInfo;
        }

        private static string[] CastJsonArrToStringArr(JArray p)
        {
            var res = new string[p.Count];
            for (var i = 0; i < p.Count; i++)
            {
                res[i] = p[i].ToString();
            }
            return res;
        }
        private static Data[] CastJsonArrToDataArr(JArray p)
        {
            var res = new Data[p.Count];
            for (var i = 0; i < p.Count; i++)
            {
                res[i] = new Data()
                {
                    InputFile = p[i]["InputFile"].ToString(),
                    OutputFile = p[i]["OutputFile"].ToString(),
                    MemoryLimit = Convert.ToInt64(p[i]["MemoryLimit"].ToString()),
                    TimeLimit = Convert.ToInt64(p[i]["TimeLimit"].ToString()),
                    Score = Convert.ToSingle(p[i]["Score"].ToString())
                };
            }
            return res;
        }

        public static int NewProblem()
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "Insert into Problem (AddDate, DataSets, ExtraFiles) VALUES (@1, @2, @3)";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String),
                    new SQLiteParameter("@2", DbType.String),
                    new SQLiteParameter("@3", DbType.String)

                };
                parameters[0].Value = DateTime.Now.ToString("yyyyMMddHHmmssffff");
                parameters[1].Value = JsonConvert.SerializeObject(new Data[0]);
                parameters[2].Value = JsonConvert.SerializeObject(new string[0]);
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
                cmd.CommandText = "select last_insert_rowid() from Problem";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        public static void DeleteProblem(int problemId)
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "Delete from Problem Where ProblemId=@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32)
                };
                parameters[0].Value = problemId;
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }

        public static void UpdateProblem(Problem toUpdateProblem)
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText =
                    "UPDATE Problem SET ProblemName=@1, Level=@2, DataSets=@3, Type=@4, SpecialJudge=@5, ExtraFiles=@6, InputFileName=@7, OutputFileName=@8, CompileCommand=@9 Where ProblemId=@10";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String),
                    new SQLiteParameter("@2", DbType.Int32),
                    new SQLiteParameter("@3", DbType.String),
                    new SQLiteParameter("@4", DbType.Int32),
                    new SQLiteParameter("@5", DbType.String),
                    new SQLiteParameter("@6", DbType.String),
                    new SQLiteParameter("@7", DbType.String),
                    new SQLiteParameter("@8", DbType.String),
                    new SQLiteParameter("@9", DbType.String),
                    new SQLiteParameter("@10", DbType.Int32),
                };
                parameters[0].Value = toUpdateProblem.ProblemName;
                parameters[1].Value = toUpdateProblem.Level;
                parameters[2].Value = JsonConvert.SerializeObject(toUpdateProblem.DataSets);
                parameters[3].Value = toUpdateProblem.Type;
                parameters[4].Value = toUpdateProblem.SpecialJudge;
                parameters[5].Value = JsonConvert.SerializeObject(toUpdateProblem.ExtraFiles);
                parameters[6].Value = toUpdateProblem.InputFileName;
                parameters[7].Value = toUpdateProblem.OutputFileName;
                parameters[8].Value = toUpdateProblem.CompileCommand;
                parameters[9].Value = toUpdateProblem.ProblemId;
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }

        #endregion

        #region Network

        private static HandleResult HServerOnOnReceive(IntPtr connId, int length)
        {
            var clientInfo = (ClientInfo)HServer.GetExtra(connId);
            if (clientInfo == null) { return HandleResult.Error; }
            var pkgInfo = clientInfo.PkgInfo;
            var required = pkgInfo.Length;
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
                        if (pkgInfo.IsHeader)
                        {
                            var header = (PkgHeader) Marshal.PtrToStructure(bufferPtr, typeof(PkgHeader));
                            required = header.BodySize;
                        }
                        else
                        {
                            var recv = new byte[required];
                            Marshal.Copy(bufferPtr, recv, 0, required);
                            _recv.AddRange(recv);
                        }
                        pkgInfo.IsHeader = !pkgInfo.IsHeader;
                        pkgInfo.Length = required;
                        if (!HServer.SetExtra(connId, clientInfo))
                        {
                            return HandleResult.Error;
                        }
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
            }
            return HandleResult.Ok;
        }

        private static void DealingOperations()
        {
            Task.Run(async () =>
            {

            });
        }

        #endregion
    }

    public class ClientInfo
    {
        public IntPtr ConnId { get; set; }
        public string IpAddress { get; set; }
        public ushort Port { get; set; }
        public PkgInfo PkgInfo { get; set; }
    }
    public class PkgHeader
    {
        public int Id;
        public int BodySize;
    }
    public class PkgInfo
    {
        public bool IsHeader;
        public int Length;
    }
    public class ObjOperation
    {
        public byte[] Content { get; set; }
        public ClientInfo Client { get; set; }
    }
}
