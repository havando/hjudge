using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using HPSocketCS;

namespace Server
{
    public static partial class Connection
    {
        private static DataTable GetReaderSchema(string tableName, SQLiteConnection connection)
        {
            DataTable schemaTable;
            IDbCommand cmd = new SQLiteCommand
            {
                CommandText = $"select * from [{tableName}]",
                Connection = connection
            };
            try
            {
                using (var reader = cmd.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SchemaOnly))
                {
                    schemaTable = reader.GetSchemaTable();
                }
            }
            catch
            {
                return null;
            }
            return schemaTable;
        }

        private static bool CreateJudgeTable(SQLiteConnection conn)
        {
            var t = GetReaderSchema("Judge", conn);
            if (t != null)
            {
                var tableName = new[]
                {
                    "JudgeId", "UserId", "Date", "ProblemId", "Code", "Timeused", "Memoryused", "Exitcode",
                    "Result", "Score", "Type", "Description", "CompetitionId", "AdditionInfo"
                };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                    if (t.Rows[i].ItemArray[0] as string != tableName[i])
                        return false;
                return true;
            }
            using (var cmd = new SQLiteCommand(conn))
            {
                var sqlTable = new StringBuilder();
                sqlTable.Clear();
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
                sqlTable.Append("Score ntext,");
                sqlTable.Append("Type ntext,");
                sqlTable.Append("Description ntext,");
                sqlTable.Append("CompetitionId int,");
                sqlTable.Append("AdditionInfo ntext)");
                cmd.CommandText = sqlTable.ToString();
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

        private static bool CreateUserTable(SQLiteConnection conn)
        {
            var t = GetReaderSchema("User", conn);
            if (t != null)
            {
                var tableName = new[]
                {
                    "UserId", "UserName", "RegisterDate", "Password", "Type", "Icon", "Achievement", "Coins",
                    "Experience"
                };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                    if (t.Rows[i].ItemArray[0] as string != tableName[i])
                        return false;
                return true;
            }
            using (var cmd = new SQLiteCommand(conn))
            {
                var sqlTable = new StringBuilder();
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
                try
                {
                    cmd.ExecuteNonQuery();
                    InsertBossAccount(conn);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static void InsertBossAccount(SQLiteConnection conn)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
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
                parameters[0].Value = "hjudgeBOSS";
                parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                parameters[2].Value = "cefb1f85346dfbfa4a341e9c41db918ba25bccc4e62c3939390084361126a417";
                parameters[3].Value = 1;
                parameters[4].Value = string.Empty;
                parameters[5].Value = string.Empty;
                parameters[6].Value = 0;
                parameters[7].Value = 0;
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }

        private static bool CreateProblemTable(SQLiteConnection conn)
        {
            var t = GetReaderSchema("Problem", conn);
            if (t != null)
            {
                var tableName = new[]
                {
                    "ProblemId", "ProblemName", "AddDate", "Level", "DataSets", "Type", "SpecialJudge",
                    "ExtraFiles", "InputFileName", "OutputFileName", "CompileCommand", "Option", "Description"
                };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                    if (t.Rows[i].ItemArray[0] as string != tableName[i])
                        return false;
                return true;
            }
            using (var cmd = new SQLiteCommand(conn))
            {
                var sqlTable = new StringBuilder();
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
                sqlTable.Append("CompileCommand ntext,");
                sqlTable.Append("Option int,");
                sqlTable.Append("Description ntext)");
                cmd.CommandText = sqlTable.ToString();
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

        private static bool CreateMessageTable(SQLiteConnection conn)
        {
            var t = GetReaderSchema("Message", conn);
            if (t != null)
            {
                var tableName = new[] { "MessageId", "FromUserId", "ToUserId", "SendDate", "Content", "State" };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                    if (t.Rows[i].ItemArray[0] as string != tableName[i])
                        return false;
                return true;
            }
            using (var cmd = new SQLiteCommand(conn))
            {
                var sqlTable = new StringBuilder();
                sqlTable.Clear();
                sqlTable.Append("CREATE TABLE Message (");
                sqlTable.Append("MessageId integer PRIMARY KEY autoincrement,");
                sqlTable.Append("FromUserId int,");
                sqlTable.Append("ToUserId int,");
                sqlTable.Append("SendDate ntext,");
                sqlTable.Append("Content ntext,");
                sqlTable.Append("State int)");
                cmd.CommandText = sqlTable.ToString();
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

        private static bool CreateCompetitionTable(SQLiteConnection conn)
        {
            var t = GetReaderSchema("Competition", conn);
            if (t != null)
            {
                var tableName = new[]
                {
                    "CompetitionId", "CompetitionName", "StartTime", "EndTime", "ProblemSet", "Option", "Password",
                    "Description", "SubmitLimit"
                };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                    if (t.Rows[i].ItemArray[0] as string != tableName[i])
                        return false;
                return true;
            }
            using (var cmd = new SQLiteCommand(conn))
            {
                var sqlTable = new StringBuilder();
                sqlTable.Clear();
                sqlTable.Append("CREATE TABLE Competition (");
                sqlTable.Append("CompetitionId integer PRIMARY KEY autoincrement,");
                sqlTable.Append("CompetitionName ntext,");
                sqlTable.Append("StartTime ntext,");
                sqlTable.Append("EndTime ntext,");
                sqlTable.Append("ProblemSet ntext,");
                sqlTable.Append("Option int,");
                sqlTable.Append("Password ntext,");
                sqlTable.Append("Description ntext,");
                sqlTable.Append("SubmitLimit int)");
                cmd.CommandText = sqlTable.ToString();
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

        private static void DropTable(SQLiteConnection conn, string tableName)
        {
            using (var cmd = new SQLiteCommand(conn))
            {
                cmd.CommandText = $"DROP TABLE {tableName}";
                cmd.ExecuteNonQuery();
            }
        }

        public static void Init(Func<string, UIElement, bool, UIElement> updateMainPage)
        {
            _updateMain = updateMainPage;

            #region DataBase

            if (!File.Exists(AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\hjudgeData.db"))
                SQLiteConnection.CreateFile(AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\hjudgeData.db");
            //var DbSQLiteConnection = new SQLiteConnection("Data Source=" +
            //                                  $"{AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;");
            if (DbSQLiteConnection.State == ConnectionState.Closed) DbSQLiteConnection.Open();

            if (!CreateJudgeTable(DbSQLiteConnection))
                if (MessageBox.Show("版本升级需要清空所有评测记录，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    DropTable(DbSQLiteConnection, "Judge");
                    CreateJudgeTable(DbSQLiteConnection);
                }
                else
                {
                    Environment.Exit(0);
                }
            if (!CreateUserTable(DbSQLiteConnection))
                if (MessageBox.Show("版本升级需要清空所有用户信息，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    DropTable(DbSQLiteConnection, "User");
                    CreateUserTable(DbSQLiteConnection);
                }
                else
                {
                    Environment.Exit(0);
                }
            if (!CreateProblemTable(DbSQLiteConnection))
                if (MessageBox.Show("版本升级需要清空所有题目信息，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    DropTable(DbSQLiteConnection, "Problem");
                    CreateProblemTable(DbSQLiteConnection);
                }
                else
                {
                    Environment.Exit(0);
                }
            if (!CreateMessageTable(DbSQLiteConnection))
                if (MessageBox.Show("版本升级需要清空所有消息记录，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    DropTable(DbSQLiteConnection, "Message");
                    CreateMessageTable(DbSQLiteConnection);
                }
                else
                {
                    Environment.Exit(0);
                }
            if (!CreateCompetitionTable(DbSQLiteConnection))
                if (MessageBox.Show("版本升级需要清空所有比赛信息，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) ==
                    MessageBoxResult.Yes)
                {
                    DropTable(DbSQLiteConnection, "Competition");
                    CreateCompetitionTable(DbSQLiteConnection);
                }
                else
                {
                    Environment.Exit(0);
                }
            DbSQLiteConnection.Close();
            #endregion

            #region Network

            HServer.OnAccept += (id, client) =>
            {
                var ip = string.Empty;
                ushort port = 0;
                if (!HServer.GetRemoteAddress(id, ref ip, ref port)) return HandleResult.Ignore;
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
                Recv.Add(new ClientData { Info = clientInfo });
                SendData("Version", Assembly.GetExecutingAssembly().GetName().Version.ToString(), id, null);
                return HandleResult.Ok;
            };
            HServer.OnClose += (id, operation, code) =>
            {
                HServer.RemoveExtra(id);
                var t = (from c in Recv where c.Info.ConnId == id select c).FirstOrDefault();
                if (t != null)
                {
                    lock (RemoveClientLock)
                    {
                        var x = new List<ClientData>();
                        while (Recv.TryTake(out var tmp))
                            if (!tmp.Equals(t)) x.Add(tmp);
                            else
                            {
                                if (tmp.Info.UserId != 0)
                                    UpdateMainPageState(
                                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {tmp.Info.UserName} 注销了");
                                break;
                            }
                        foreach (var i in x)
                        {
                            Recv.Add(i);
                        }
                    }
                }
                return HandleResult.Ok;
            };
            HServer.OnReceive += HServerOnOnReceive;

            HServer.IpAddress = Configuration.Configurations.IpAddress;
            HServer.Port = 23333;
            if (!HServer.Start())
                MessageBox.Show("服务端网络初始化失败，请检查系统设置", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            new Thread(DealingBytes)
            {
                Priority = ThreadPriority.Highest
            }.Start();
            for (var i = 0; i < Environment.ProcessorCount; i++)
                new Thread(DealingOperations)
                {
                    Priority = ThreadPriority.Highest
                }.Start();
            #endregion
        }
    }
}