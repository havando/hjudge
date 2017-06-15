using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Server
{
    public static class Connection
    {
        private static SQLiteConnection _sqLite;
        public static string Address;
        public static void Init()
        {
            if (!File.Exists(Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"))
            {
                SQLiteConnection.CreateFile(Environment.CurrentDirectory + "\\AppData\\hjudgeData.db");
                SQLiteConnection sqLite = new SQLiteConnection("Data Source=" +
                                               $"{Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
                sqLite.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(sqLite))
                {
                    StringBuilder sqlTable = new StringBuilder();
                    sqlTable.Append("CREATE TABLE Judge (");
                    sqlTable.Append("Id int identity(1,1) primary key,");
                    sqlTable.Append("UserId int,");
                    sqlTable.Append("Date datetime2,");
                    sqlTable.Append("ProblemId int,");
                    sqlTable.Append("Code ntext,");
                    sqlTable.Append("Result ntext,");
                    sqlTable.Append("Score int,");
                    sqlTable.Append("Details ntext )");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                    sqlTable.Append("CREATE TABLE User (");
                    sqlTable.Append("UserId int identity(1,1) primary key,");
                    sqlTable.Append("UserName ntext,");
                    sqlTable.Append("RegisterDate datetime2,");
                    sqlTable.Append("Password ntext,");
                    sqlTable.Append("Type int,");
                    sqlTable.Append("Icon ntext,");
                    sqlTable.Append("Achievement ntext )");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                    sqlTable.Append("CREATE TABLE Problem (");
                    sqlTable.Append("ProblemId int identity(1,1) primary key,");
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
                using (SQLiteCommand cmd = new SQLiteCommand(sqLite))
                {
                    cmd.CommandText = "INSERT INTO User VALUES (@1,@2,@3,@4,@5,@6,@7)";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.Int32),
                        new SQLiteParameter("@2", DbType.String),
                        new SQLiteParameter("@3", DbType.String),
                        new SQLiteParameter("@4", DbType.String),
                        new SQLiteParameter("@5", DbType.Int32),
                        new SQLiteParameter("@6", DbType.String),
                        new SQLiteParameter("@7", DbType.String),
                    };
                    parameters[0].Value = 1;
                    parameters[1].Value = "hjudgeBOSS";
                    parameters[2].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss:ffff");
                    parameters[3].Value = "cefb1f85346dfbfa4a341e9c41db918ba25bccc4e62c3939390084361126a417";
                    parameters[4].Value = 1;
                    parameters[5].Value = "";
                    parameters[6].Value = "";
                    cmd.Parameters.AddRange(parameters);
                    cmd.ExecuteNonQuery();
                } //InsertBOSSAccount
                sqLite.Close();
            }
            _sqLite = new SQLiteConnection("Data Source=" +
                                          $"{Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
            _sqLite.Open();
            Address = "127.0.0.1:23333";
        }

        public static async Task<int> Login(string userName, string password)
        {
            SHA256 s = new SHA256CryptoServiceProvider();
            byte[] retVal = s.ComputeHash(Encoding.Unicode.GetBytes(password));
            StringBuilder sb = new StringBuilder();
            foreach (var t in retVal)
            {
                sb.Append(t.ToString("x2"));
            }
            int a = await TryLogin(userName, sb.ToString());
            return a;
        }

        public static bool UpdateUserInfo(UserInfo toUpdateInfo)
        {
            using (SQLiteCommand cmd = new SQLiteCommand(_sqLite))
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

        private static Task<int> TryLogin(string userName, string passwordHash)
        {
            return Task.Run(() =>
            {
                using (SQLiteCommand cmd = new SQLiteCommand(_sqLite))
                {
                    cmd.CommandText = "SELECT * From User Where userName=@1";
                    SQLiteParameter[] parameters =
                    {
                        new SQLiteParameter("@1", DbType.String)
                    };
                    parameters[0].Value = userName;
                    cmd.Parameters.AddRange(parameters);
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (passwordHash == reader.GetString(3))
                            {
                                Console.Write(reader.GetString(2));
                                UserHelper.SetCurrentUser(reader.GetInt32(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetInt32(4),reader.GetString(5),reader.GetString(6));
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

        public static Problem GetProblem(int problemId)
        {
            Problem a = new Problem();
            using (SQLiteCommand cmd = new SQLiteCommand(_sqLite))
            {
                cmd.CommandText = "SELECT * From Problem Where ProblemId=@1";
                SQLiteParameter[] parameters =
                {
                    new SQLiteParameter("@1", DbType.String)
                };
                parameters[0].Value = problemId;
                cmd.Parameters.AddRange(parameters);
                SQLiteDataReader reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {
                    a.ProblemName = reader.GetString(1);
                    a.AddDate = reader.GetString(2);
                    a.Level = reader.GetInt32(3);
                    a.DataSets = (Data[]) JsonConvert.DeserializeObject(reader.GetString(4));
                    a.Type = reader.GetInt32(5);
                    a.SpecialJudge = reader.GetString(6);
                    a.ExtraFiles = (string[])JsonConvert.DeserializeObject(reader.GetString(7));
                    a.InputFileName = reader.GetString(8);
                    a.OutputFileName = reader.GetString(9);
                    a.CompileCommand = reader.GetString(10);
                    return a;
                }
                a.ProblemId = 0;
                return a;
            }
        }
    }
}
