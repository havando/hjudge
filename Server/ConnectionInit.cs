using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using HPSocketCS;

namespace Server
{
    public static partial class Connection
    {
        public static void Init(Func<string, UIElement, bool, UIElement> updateMainPage)
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
                    sqlTable.Append("Score ntext,");
                    sqlTable.Append("Type ntext,");
                    sqlTable.Append("Description ntext)");
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
                    sqlTable.Append("CompileCommand ntext,");
                    sqlTable.Append("Option int,");
                    sqlTable.Append("Description ntext)");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                    sqlTable.Append("CREATE TABLE Message (");
                    sqlTable.Append("MessageId integer PRIMARY KEY autoincrement,");
                    sqlTable.Append("FromUserId int,");
                    sqlTable.Append("ToUserId int,");
                    sqlTable.Append("SendDate ntext,");
                    sqlTable.Append("Content ntext)");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                    sqlTable.Append("CREATE TABLE Competition (");
                    sqlTable.Append("CompetitionId integer PRIMARY KEY autoincrement,");
                    sqlTable.Append("CompetitionName ntext,");
                    sqlTable.Append("StartTime ntext,");
                    sqlTable.Append("EndTime ntext,");
                    sqlTable.Append("ProblemSet ntext,");
                    sqlTable.Append("Option int,");
                    sqlTable.Append("Password ntext,");
                    sqlTable.Append("Description ntext)");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                } //CreateTable
                using (var cmd = new SQLiteCommand(sqLite))
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
                } //InsertBOSSAccount
                sqLite.Close();
            }
            _sqLite = new SQLiteConnection("Data Source=" +
                                           $"{Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
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
            DealingBytes();
            DealingOperations();

            #endregion
        }
    }
}
