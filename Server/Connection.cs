using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using HPSocketCS;
using Ionic.Zip;
using Newtonsoft.Json;

namespace Server
{
    public static class Connection
    {
        private static SQLiteConnection _sqLite;
        private static bool _isUsing;
        private static readonly List<ClientData> Recv = new List<ClientData>();
        private static readonly ConcurrentQueue<ObjOperation> Operations = new ConcurrentQueue<ObjOperation>();
        private static readonly TcpPullServer<ClientInfo> HServer = new TcpPullServer<ClientInfo>();
        private const string Divtot = "<|h~|split|~j|>";
        private const string Divpar = "<h~|~j>";
        public static bool IsExited;
        private static Action<string> _updateMain;
        private static int _id;
        private static readonly int PkgHeaderSize = Marshal.SizeOf(new PkgHeader());

        public static int CurJudgingCnt = 0;

        public static void Init(Action<string> updateMainPage)
        {
            _updateMain = updateMainPage;

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
                    sqlTable.Append("Achievement ntext,");
                    sqlTable.Append("Coins int,");
                    sqlTable.Append("Experience int)");
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
                    cmd.CommandText = "INSERT INTO User (UserName,RegisterDate,Password,Type,Icon,Achievement,Coins,Experience) VALUES (@1,@2,@3,@4,@5,@6,@7,@8)";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.String),
                        new SQLiteParameter("@2", DbType.String),
                        new SQLiteParameter("@3", DbType.String),
                        new SQLiteParameter("@4", DbType.Int32),
                        new SQLiteParameter("@5", DbType.String),
                        new SQLiteParameter("@6", DbType.String),
                        new SQLiteParameter("@7", DbType.Int32),
                        new SQLiteParameter("@8", DbType.Int32)
                    };
                    parameters[0].Value = "hjudgeBOSS";
                    parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
                    parameters[2].Value = "cefb1f85346dfbfa4a341e9c41db918ba25bccc4e62c3939390084361126a417";
                    parameters[3].Value = 1;
                    parameters[4].Value = string.Empty;
                    parameters[5].Value = string.Empty;
                    parameters[6].Value = 0;
                    parameters[7].Value = 0;
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
                var ip = string.Empty;
                ushort port = 0;
                if (!HServer.GetRemoteAddress(id, ref ip, ref port)) { return HandleResult.Ignore; }
                var clientInfo = new ClientInfo
                {
                    UserId = 0,
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
                WaitingForUnusing();
                _isUsing = true;
                Recv.Add(new ClientData { Info = clientInfo });
                _isUsing = false;
                return HandleResult.Ok;
            };
            HServer.OnClose += (id, operation, code) =>
            {
                HServer.RemoveExtra(id);
                var t = (from c in Recv where c.Info.ConnId == id select c).FirstOrDefault();
                WaitingForUnusing();
                _isUsing = true;
                if (t != null) { Recv.Remove(t); }
                _isUsing = false;
                return HandleResult.Ok;
            };
            HServer.OnReceive += HServerOnOnReceive;

            foreach (var t in hostIp)
            {
                HServer.IpAddress = t.ToString();
                HServer.Port = 23333;
                if (!HServer.Start()) { continue; }
                flag = true;
            }
            DealingBytes();
            DealingOperations();
            if (flag) return;
            MessageBox.Show("服务端初始化失败，请检查网络", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);

            #endregion
        }

        #region DataBase

        private static JudgeInfo GetJudgeInfo(int judgeId)
        {
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From Judge Where JudgeId=@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32)
                };
                parameters[0].Value = judgeId;
                cmd.Parameters.AddRange(parameters);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    if (reader.Read())
                    {
                        return new JudgeInfo
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
                        };
                    }
                }
            }
            return new JudgeInfo();
        }

        private static JudgeInfo[] GetJudgeRecord(int userId, int start, int count)
        {
            var ji = new List<JudgeInfo>();
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From Judge Where UserId=@1 order by JudgeId desc";
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
                        if (start-- > 0) continue;
                        if (count-- == 0) break;
                        ji.Add(new JudgeInfo
                        {
                            JudgeId = reader.GetInt32(0),
                            UserId = reader.GetInt32(1),
                            JudgeDate = reader.GetString(2),
                            ProblemId = reader.GetInt32(3),
                            Code = "-|/|\\|-",
                            Timeused = CastStringArrToLongArr(reader.GetString(5).Split(',')),
                            Memoryused = CastStringArrToLongArr(reader.GetString(6).Split(',')),
                            Exitcode = CastStringArrToIntArr(reader.GetString(7).Split(',')),
                            Result = reader.GetString(8).Split(','),
                            Score = CastStringArrToFloatArr(reader.GetString(9).Split(','))
                        });
                    }
                }
            }
            return ji.ToArray();
        }

        private static bool RemoteChangePassword(string userName, string oldPassword, string newPassword)
        {
            SHA256 s = new SHA256CryptoServiceProvider();
            var retVal = s.ComputeHash(Encoding.Unicode.GetBytes(oldPassword));
            var sb = new StringBuilder();
            foreach (var t in retVal)
            {
                sb.Append(t.ToString("x2"));
            }
            SHA256 s2 = new SHA256CryptoServiceProvider();
            var retVal2 = s2.ComputeHash(Encoding.Unicode.GetBytes(newPassword));
            var sb2 = new StringBuilder();
            foreach (var t in retVal2)
            {
                sb2.Append(t.ToString("x2"));
            }
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
                    if (!reader.Read()) return false;
                    if (sb.ToString() != reader.GetString(3)) return false;
                    using (var cmd2 = new SQLiteCommand(_sqLite))
                    {
                        cmd2.CommandText = "Update User SET Password=@1 Where UserName=@2";
                        cmd2.Parameters.Clear();
                        SQLiteParameter[] parameters2 =
                        {
                            new SQLiteParameter("@1", DbType.String),
                            new SQLiteParameter("@2", DbType.String)
                        };
                        parameters2[0].Value = sb2.ToString();
                        parameters2[1].Value = userName;
                        cmd2.Parameters.AddRange(parameters2);
                        cmd2.ExecuteNonQuery();
                    }
                    return true;
                }
                return false;
            }
        }

        private static bool UpdateCoins(int userId, int delta)
        {
            int origin;
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From User Where userId=@1";
                SQLiteParameter[] parameters1 =
                {
                    new SQLiteParameter("@1", DbType.Int32)
                };
                parameters1[0].Value = userId;
                cmd.Parameters.AddRange(parameters1);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    if (!reader.Read()) return false;
                    origin = reader.GetInt32(7);
                }
                else
                {
                    return false;
                }
            }
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "UPDATE User SET Coins=@1 WHERE UserId=@2";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32),
                    new SQLiteParameter("@2", DbType.Int32)
                };
                parameters[0].Value = delta + origin;
                parameters[1].Value = userId;
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
                return true;
            }
        }

        private static bool UpdateExperience(int userId, int delta)
        {
            int origin;
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From User Where userId=@1";
                SQLiteParameter[] parameters1 =
                {
                    new SQLiteParameter("@1", DbType.Int32)
                };
                parameters1[0].Value = userId;
                cmd.Parameters.AddRange(parameters1);
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    if (!reader.Read()) return false;
                    origin = reader.GetInt32(8);
                }
                else
                {
                    return false;
                }
            }
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "UPDATE User SET Experience=@1 WHERE UserId=@2";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.Int32),
                    new SQLiteParameter("@2", DbType.Int32)
                };
                parameters[0].Value = delta + origin;
                parameters[1].Value = userId;
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
                return true;
            }
        }
        private static bool RemoteUpdateProfile(int userId, string userName, string icon)
        {
            var k = CheckUser(userName);
            if (k != userId && k != 0)
            {
                return false;
            }
            using (var cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "UPDATE User SET UserName=@1, Icon=@2 WHERE UserId=@3";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String),
                    new SQLiteParameter("@2", DbType.String),
                    new SQLiteParameter("@3", DbType.Int32)
                };
                parameters[0].Value = userName;
                parameters[1].Value = icon;
                parameters[2].Value = userId;
                cmd.Parameters.AddRange(parameters);
                try
                {
                    cmd.ExecuteNonQuery();
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static int RemoteLogin(string userName, string password)
        {
            SHA256 s = new SHA256CryptoServiceProvider();
            var retVal = s.ComputeHash(Encoding.Unicode.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var t in retVal)
            {
                sb.Append(t.ToString("x2"));
            }
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
                        return sb.ToString() == reader.GetString(3) && reader.GetInt32(0) != 1 ? 0 : 1;
                    }
                }
                else
                {
                    return 1;
                }

            }
            return 2;
        }

        public static string Logout()
        {
            var a = UserHelper.CurrentUser.UserName;
            UserHelper.SetCurrentUser(0, string.Empty, string.Empty, string.Empty, 0, string.Empty, string.Empty);
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
                    if (!reader.HasRows) return 1;
                    if (!reader.Read()) return 2;
                    if (passwordHash != reader.GetString(3)) return 1;
                    UserHelper.SetCurrentUser(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.GetString(6));
                    return 0;
                }
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
                        a.DataSets = JsonConvert.DeserializeObject<Data[]>(reader.GetString(4));
                        a.Type = reader.GetInt32(5);
                        a.SpecialJudge = reader.GetString(6);
                        a.ExtraFiles = JsonConvert.DeserializeObject<string[]>(reader.GetString(7));
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
                            "INSERT INTO User (UserName,RegisterDate,Password,Type,Icon,Achievement,Coins,Experience) VALUES (@1,@2,@3,@4,@5,@6,@7,@8)";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.String),
                            new SQLiteParameter("@2", DbType.String),
                            new SQLiteParameter("@3", DbType.String),
                            new SQLiteParameter("@4", DbType.Int32),
                            new SQLiteParameter("@5", DbType.String),
                            new SQLiteParameter("@6", DbType.String),
                            new SQLiteParameter("@7", DbType.Int32),
                            new SQLiteParameter("@8", DbType.Int32)
                        };
                        parameters[0].Value = t.UserName;
                        parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
                        parameters[2].Value = t.Password;
                        parameters[3].Value = t.Type;
                        parameters[4].Value = string.Empty;
                        parameters[5].Value = string.Empty;
                        parameters[6].Value = 0;
                        parameters[7].Value = 0;
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
                if (!reader.HasRows) return 0;
                if (reader.Read())
                {
                    return reader.GetInt32(0);
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
                cmd.CommandText = UserHelper.CurrentUser.Type != 4 ? "SELECT * From Judge" : $"SELECT * From Judge Where UserId={UserHelper.CurrentUser.UserId}";
                var reader = cmd.ExecuteReader();
                if (!reader.HasRows) return curJudgeInfo;
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
                        curJudgeInfo.Add(new JudgeInfo
                        {
                            JudgeId = reader.GetInt32(0),
                            JudgeDate = reader.GetString(2)
                        });
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
            var userName = string.Empty;
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
                if (!reader.HasRows) return userName;
                while (reader.Read())
                {
                    userName = reader.GetString(1);
                    break;
                }
            }
            return userName;
        }

        private static int GetUserId(string userName)
        {
            var userId = 0;
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
                if (!reader.HasRows) return userId;
                while (reader.Read())
                {
                    userId = reader.GetInt32(0);
                    break;
                }
            }
            return userId;
        }

        public static UserInfo GetUser(int userId)
        {
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
                    if (reader.Read())
                    {
                        return new UserInfo
                        {
                            UserId = reader.GetInt32(0),
                            UserName = reader.GetString(1),
                            RegisterDate = reader.GetString(2),
                            Password = reader.GetString(3),
                            Type = reader.GetInt32(4),
                            Icon = reader.GetString(5),
                            Achievement = reader.GetString(6),
                            Coins = reader.GetInt32(7),
                            Experience = reader.GetInt32(8)
                        };
                    }
                }
                return null;
            }
        }

        private static UserInfo GetUser(string userName)
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
                        return new UserInfo
                        {
                            UserId = reader.GetInt32(0),
                            UserName = reader.GetString(1),
                            RegisterDate = reader.GetString(2),
                            Password = reader.GetString(3),
                            Type = reader.GetInt32(4),
                            Icon = reader.GetString(5),
                            Achievement = reader.GetString(6),
                            Coins = reader.GetInt32(7),
                            Experience = reader.GetInt32(8)
                        };
                    }
                }
                return null;
            }
        }

        public static string GetProblemName(int problemId)
        {
            var problemName = string.Empty;
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
                if (!reader.HasRows) return problemName;
                while (reader.Read())
                {
                    problemName = reader.GetString(1);
                    break;
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
                parameters[0].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
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
                string timeused = string.Empty, memoryused = string.Empty, exitcode = string.Empty, result = string.Empty, score = string.Empty;
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
                            DataSets = JsonConvert.DeserializeObject<Data[]>(reader.GetString(4)),
                            Type = reader.GetInt32(5),
                            SpecialJudge = reader.GetString(6),
                            ExtraFiles = JsonConvert.DeserializeObject<string[]>(reader.GetString(7)),
                            InputFileName = reader.GetString(8),
                            OutputFileName = reader.GetString(9),
                            CompileCommand = reader.GetString(10)
                        });
                    }
                    catch
                    {
                        curJudgeInfo.Add(new Problem
                        {
                            AddDate = reader.GetString(2),
                            ProblemId = reader.GetInt32(0)
                        });
                    }
                }
            }
            return curJudgeInfo;
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
                parameters[0].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
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
                    new SQLiteParameter("@10", DbType.Int32)
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

        private static List<string> SearchFiles(string path)
        {
            var a = Directory.GetDirectories(path).Select(Path.GetFileName).ToList();
            a.Add("|");
            a.AddRange(Directory.GetFiles(path).Select(Path.GetFileName).ToList());
            return a;
        }

        private static void WaitingForUnusing()
        {
            while (_isUsing)
            {
                if (IsExited) break;
                Thread.Sleep(10);
            }
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
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
        private static void SendData(string operation, IEnumerable<byte> sendBytes, IntPtr connId)
        {
            Task.Run(() =>
            {
                var temp = Encoding.Unicode.GetBytes(operation);
                temp = temp.Concat(Encoding.Unicode.GetBytes(Divpar)).ToArray();
                temp = temp.Concat(sendBytes).ToArray();
                temp = temp.Concat(Encoding.Unicode.GetBytes(Divtot)).ToArray();
                var final = GetSendBuffer(temp);
                HServer.Send(connId, final, final.Length);
            });
        }

        private static void SendData(string operation, string sendString, IntPtr connId)
        {
            Task.Run(() =>
            {
                var temp = Encoding.Unicode.GetBytes(operation + Divpar + sendString + Divtot);
                var final = GetSendBuffer(temp);
                HServer.Send(connId, final, final.Length);
            });
        }

        public static void SendMsg(string sendString, IntPtr connId)
        {
            SendData("Messaging", sendString, connId);
        }

        private static HandleResult HServerOnOnReceive(IntPtr connId, int length)
        {
            var clientInfo = HServer.GetExtra(connId);
            if (clientInfo == null)
            {
                return HandleResult.Error;
            }
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
                            WaitingForUnusing();
                            _isUsing = true;
                            (from c in Recv where c.Info.ConnId == connId select c).FirstOrDefault()?.Data.AddRange(buffer);
                            _isUsing = false;
                        }
                        myPkgInfo.IsHeader = !myPkgInfo.IsHeader;
                        myPkgInfo.Length = required;
                        if (HServer.SetExtra(connId, clientInfo) == false)
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
                while (!IsExited)
                {
                    foreach (var t in Recv)
                    {
                        if (IsExited) break;
                        if (t.Data.Count == 0)
                        {
                            continue;
                        }
                        WaitingForUnusing();
                        _isUsing = true;
                        var temp = Bytespilt(t.Data.ToArray(), Encoding.Unicode.GetBytes(Divtot));
                        if (temp.Count != 0)
                        {
                            t.Data.Clear();
                            t.Data.AddRange(temp[temp.Count - 1]);
                        }
                        temp.RemoveAt(temp.Count - 1);
                        _isUsing = false;
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
                        var u = (from c in Recv where c.Info.ConnId == res.Client.ConnId select c)
                            .FirstOrDefault();
                        if (u != null)
                        {
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
                                                        WaitingForUnusing();
                                                        _isUsing = true;
                                                        u.Info.UserId =
                                                            GetUserId(Encoding.Unicode.GetString(res.Content[0]));
                                                        _isUsing = false;
                                                        SendData("Login", "Succeed", u.Info.ConnId);
                                                        UpdateMainPageState(
                                                            $"{DateTime.Now} 用户 {u.Info.UserName} 登录了");
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
                                            {
                                                break;
                                            }
                                            UpdateMainPageState(
                                                $"{DateTime.Now} 用户 {u.Info.UserName} 注销了");
                                            u.Data.Clear();
                                            u.Info.UserId = 0;
                                            SendData("Logout", "Succeed", u.Info.ConnId);
                                            break;
                                        }
                                    case "RequestFileList":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                var filePath = Encoding.Unicode.GetString(res.Content[0]);
                                                if (filePath.Length > 1)
                                                {
                                                    if (filePath.Substring(0, 1) == "\\")
                                                    {
                                                        filePath = filePath.Substring(1);
                                                    }
                                                    if (filePath.Substring(filePath.Length - 1) == "\\")
                                                    {
                                                        filePath = filePath.Substring(filePath.Length - 1);
                                                    }
                                                }
                                                var x = SearchFiles(
                                                    Environment.CurrentDirectory + "\\Files" +
                                                    (string.IsNullOrEmpty(filePath) ? string.Empty : $"\\{filePath}")
                                                );
                                                var y = string.Empty;
                                                for (var i = 0; i < x.Count; i++)
                                                {
                                                    if (i != x.Count - 1)
                                                    {
                                                        y += x[i] + Divpar;
                                                    }
                                                    else
                                                    {
                                                        y += x[i];
                                                    }
                                                }
                                                SendData("FileList",
                                                    filePath + Divpar + y,
                                                    u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "RequestFile":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                var filePath = Encoding.Unicode.GetString(res.Content[0]);
                                                if (filePath.Length > 1)
                                                {
                                                    if (filePath.Substring(0, 1) == "\\")
                                                    {
                                                        filePath = filePath.Substring(1);
                                                    }
                                                    if (filePath.Substring(filePath.Length - 1) == "\\")
                                                    {
                                                        filePath = filePath.Substring(filePath.Length - 1);
                                                    }
                                                }
                                                filePath = Environment.CurrentDirectory + "\\Files\\" + filePath;
                                                var fileName = Path.GetFileName(filePath);
                                                if (File.Exists(filePath))
                                                {
                                                    UpdateMainPageState(
                                                        $"{DateTime.Now} 用户 {u.Info.UserName} 请求文件：{filePath}");
                                                    SendData("File",
                                                        Encoding.Unicode.GetBytes(fileName).ToList()
                                                            .Concat(Encoding.Unicode.GetBytes(Divpar)
                                                                .Concat(File.ReadAllBytes(
                                                                    filePath))),
                                                        u.Info.ConnId);
                                                }
                                            });
                                            break;
                                        }
                                    case "RequestProblemDataSet":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                if (!Configuration.Configurations.AllowRequestDataSet)
                                                {
                                                    SendData("ProblemDataSet", "Denied", u.Info.ConnId);

                                                }
                                                else
                                                {
                                                    UpdateMainPageState(
                                                    $"{DateTime.Now} 用户 {u.Info.UserName} 请求题目数据：{GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))}");
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
                                                                if (File.Exists(inputName))
                                                                {
                                                                    zip.AddFile(inputName);
                                                                }
                                                                if (File.Exists(outputName))
                                                                {
                                                                    zip.AddFile(outputName);
                                                                }
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
                                                }

                                            });
                                            break;
                                        }
                                    case "SubmitCode":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                if (!string.IsNullOrEmpty(Encoding.Unicode.GetString(res.Content[1])))
                                                {
                                                    UpdateMainPageState(
                                                        $"{DateTime.Now} 用户 {u.Info.UserName} 提交了题目 {GetProblemName(Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))} 的代码");
                                                    Task.Run(() =>
                                                    {
                                                        var j = new Judge(
                                                            Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])),
                                                            u.Info.UserId, Encoding.Unicode.GetString(res.Content[1]));
                                                        var x = JsonConvert.SerializeObject(j.JudgeResult);
                                                        SendData("JudgeResult", x, u.Info.ConnId);
                                                    });
                                                }
                                            });

                                            break;
                                        }
                                    case "Messaging":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                UpdateMainPageState(
                                    $"{DateTime.Now} 用户 {u.Info.UserName} 发来了消息");
                                                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                                                {
                                                    var x = new Messaging();
                                                    x.SetMessage(Encoding.Unicode.GetString(res.Content[0]),
                                                        u.Info.ConnId, u.Info.UserName);
                                                    x.Show();
                                                }));
                                            });

                                            break;
                                        }
                                    case "RequestProblemList":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
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
                                                }
                                                var x = JsonConvert.SerializeObject(pl);
                                                SendData("ProblemList", x, u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "RequestProfile":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                var x = JsonConvert.SerializeObject(
                                                    GetUser(Encoding.Unicode.GetString(res.Content[0])));
                                                SendData("Profile", x, u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "ChangePassword":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                SendData("ChangePassword",
                                                    RemoteChangePassword(u.Info.UserName,
                                                        Encoding.Unicode.GetString(res.Content[0]),
                                                        Encoding.Unicode.GetString(res.Content[1]))
                                                        ? "Succeed"
                                                        : "Failed", u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "UpdateProfile":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                SendData("UpdateProfile",
                                                    RemoteUpdateProfile(
                                                        u.Info.UserId,
                                                        Encoding.Unicode.GetString(res.Content[0]),
                                                        Encoding.Unicode.GetString(res.Content[1]))
                                                        ? "Succeed"
                                                        : "Failed", u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "UpdateCoins":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                SendData("UpdateCoins",
                                                    UpdateCoins(
                                                        u.Info.UserId,
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))
                                                    ? "Succeed"
                                                    : "Failed", u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "UpdateExperience":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                SendData("UpdateExperience",
                                                    UpdateExperience(
                                                        u.Info.UserId,
                                                        Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])))
                                                        ? "Succeed"
                                                        : "Failed", u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "RequestJudgeRecord":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                var x = GetJudgeRecord(u.Info.UserId,
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[0])),
                                                    Convert.ToInt32(Encoding.Unicode.GetString(res.Content[1])));
                                                SendData("JudgeRecord",
                                                    Encoding.Unicode.GetString(res.Content[0]) + Divpar +
                                                     x.Length + Divpar +
                                                    JsonConvert.SerializeObject(x),
                                                    u.Info.ConnId);
                                            });
                                            break;
                                        }
                                    case "RequestJudgeCode":
                                        {
                                            if (u.Info.UserId == 0)
                                            {
                                                break;
                                            }
                                            Task.Run(() =>
                                            {
                                                SendData("JudgeCode",
                                                    JsonConvert.SerializeObject(GetJudgeInfo(Convert.ToInt32(
                                                        Encoding.Unicode.GetString(res.Content[0])))),
                                                    u.Info.ConnId);
                                            });
                                            break;
                                        }
                                }
                            }
                            catch
                            {
                                continue;
                            }
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

        #endregion

        public static void UpdateMainPageState(string content)
        {
            _updateMain.Invoke(content);
        }
    }
}
