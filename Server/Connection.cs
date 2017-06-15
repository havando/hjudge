using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Server
{
    public static class Connection
    {
        private static SQLiteConnection _sqLite;

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
                    sqlTable.Append("Icon image,");
                    sqlTable.Append("Achievement ntext )");
                    cmd.CommandText = sqlTable.ToString();
                    cmd.ExecuteNonQuery();
                    sqlTable.Clear();
                    sqlTable.Append("CREATE TABLE Problem (");
                    sqlTable.Append("ProblemId int identity(1,1) primary key,");
                    sqlTable.Append("ProblemName ntext,");
                    sqlTable.Append("AddDate datetime2,");
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
                sqLite.Close();
            }
            _sqLite = new SQLiteConnection("Data Source=" +
                                          $"{Environment.CurrentDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;Max Pool Size=10");
            _sqLite.Open();
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
                    foreach (var t in parameters)
                    {
                        cmd.Parameters.Add(t);
                    }
                    SQLiteDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (passwordHash == reader.GetString(3))
                            {
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
    }
}
