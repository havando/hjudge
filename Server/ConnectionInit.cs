using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using HPSocketCS;
using System.Threading;

namespace Server
{
    public static partial class Connection
    {
        private static DataTable GetReaderSchema(string tableName, SQLiteConnection connection)
        {
            DataTable schemaTable = null;
            IDbCommand cmd = new SQLiteCommand
            {
                CommandText = string.Format("select * from [{0}]", tableName),
                Connection = connection
            };
            try
            {
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SchemaOnly))
                {
                    schemaTable = reader.GetSchemaTable();
                }
            }
            catch { return null; }
            return schemaTable;
        }
        private static bool CreateJudgeTable(SQLiteConnection conn)
        {
            var t = GetReaderSchema("Judge", conn);
            if (t != null)
            {
                var tableName = new[] { "JudgeId", "UserId", "Date", "ProblemId", "Code", "Timeused", "Memoryused", "Exitcode",
                    "Result", "Score", "Type", "Description", "CompetitionId" };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                {
                    if ((t.Rows[i].ItemArray[0] as string) != tableName[i])
                    {
                        return false;
                    }
                }
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
                sqlTable.Append("CompetitionId int)");
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
                var tableName = new[] { "UserId", "UserName", "RegisterDate", "Password", "Type", "Icon", "Achievement", "Coins", "Experience" };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                {
                    if ((t.Rows[i].ItemArray[0] as string) != tableName[i])
                    {
                        return false;
                    }
                }
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
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        private static bool CreateProblemTable(SQLiteConnection conn)
        {
            var t = GetReaderSchema("Problem", conn);
            if (t != null)
            {
                var tableName = new[] { "ProblemId", "ProblemName", "AddDate", "Level", "DataSets", "Type", "SpecialJudge",
                    "ExtraFiles", "InputFileName", "OutputFileName", "CompileCommand", "Option", "Description" };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                {
                    if ((t.Rows[i].ItemArray[0] as string) != tableName[i])
                    {
                        return false;
                    }
                }
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
                {
                    if ((t.Rows[i].ItemArray[0] as string) != tableName[i])
                    {
                        return false;
                    }
                }
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
                var tableName = new[] { "CompetitionId", "CompetitionName", "StartTime", "EndTime", "ProblemSet", "Option", "Password", "Description", "SubmitLimit" };
                if (tableName.Length != t.Rows.Count) return false;
                for (var i = 0; i < t.Rows.Count; i++)
                {
                    if ((t.Rows[i].ItemArray[0] as string) != tableName[i])
                    {
                        return false;
                    }
                }
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
            var sqLite = new SQLiteConnection("Data Source=" +
                                              $"{AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
            sqLite.Open();

            if (!CreateJudgeTable(sqLite))
            {
                if (MessageBox.Show("版本升级需要清空所有评测记录，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DropTable(sqLite, "Judge");
                    CreateJudgeTable(sqLite);
                }
                else Environment.Exit(0);
            }
            if (!CreateUserTable(sqLite))
            {
                if (MessageBox.Show("版本升级需要清空所有用户信息，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DropTable(sqLite, "User");
                    CreateUserTable(sqLite);
                }
                else Environment.Exit(0);
            }
            if (!CreateProblemTable(sqLite))
            {
                if (MessageBox.Show("版本升级需要清空所有题目信息，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DropTable(sqLite, "Problem");
                    CreateProblemTable(sqLite);
                }
                else Environment.Exit(0);
            }
            if (!CreateMessageTable(sqLite))
            {
                if (MessageBox.Show("版本升级需要清空所有消息记录，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DropTable(sqLite, "Message");
                    CreateMessageTable(sqLite);
                }
                else Environment.Exit(0);
            }
            if (!CreateCompetitionTable(sqLite))
            {
                if (MessageBox.Show("版本升级需要清空所有比赛信息，是否继续？", "提示", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DropTable(sqLite, "Competition");
                    CreateCompetitionTable(sqLite);
                }
                else Environment.Exit(0);
            }
            _sqLite = new SQLiteConnection("Data Source=" +
                                           $"{AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
            _sqLite.Open();

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
                SendData("Version", Assembly.GetExecutingAssembly().GetName().Version.ToString(), id);
                return HandleResult.Ok;
            };
            HServer.OnClose += (id, operation, code) =>
            {
                HServer.RemoveExtra(id);
                var t = (from c in Recv where c.Info.ConnId == id select c).FirstOrDefault();
                if (t != null)
                    Recv.Remove(t);
                return HandleResult.Ok;
            };
            HServer.OnReceive += HServerOnOnReceive;

            HServer.IpAddress = Configuration.Configurations.IpAddress;
            HServer.Port = 23333;
            if (!HServer.Start())
                MessageBox.Show("服务端网络初始化失败，请检查系统设置", "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            new Thread(DealingBytes).Start();
            new Thread(DealingOperations).Start();

            #endregion
        }
    }
}
