using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Server
{
    public static partial class Connection
    {
        #region DataBase
        public static string ConnectionString = "Data Source=" + $"{AppDomain.CurrentDomain.BaseDirectory + "\\AppData\\hjudgeData.db"};Initial Catalog=sqlite;Integrated Security=True;";

        private static JudgeInfo GetJudgeInfo(int judgeId)
        {
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
                            if (reader.Read())
                                try
                                {
                                    return new JudgeInfo
                                    {
                                        JudgeId = reader.GetInt32(0),
                                        UserId = reader.GetInt32(1),
                                        JudgeDate = reader.GetString(2),
                                        ProblemId = reader.GetInt32(3),
                                        Code = reader.GetString(4),
                                        Timeused = CastStringArrToLongArr(reader.GetString(5)?.Split(',')),
                                        Memoryused = CastStringArrToLongArr(reader.GetString(6)?.Split(',')),
                                        Exitcode = CastStringArrToIntArr(reader.GetString(7)?.Split(',')),
                                        Result = reader.GetString(8)?.Split(','),
                                        Score = CastStringArrToFloatArr(reader.GetString(9)?.Split(',')),
                                        Type = reader.GetString(10),
                                        Description = reader.GetString(11),
                                        CompetitionId = reader.GetInt32(12),
                                        AdditionInfo = reader.GetString(13)
                                    };
                                }
                                catch
                                {
                                    return new JudgeInfo
                                    {
                                        JudgeId = reader.GetInt32(0),
                                        JudgeDate = reader.GetString(2),
                                        Description = reader.GetString(11)
                                    };
                                }
                    }
                }
            }
            return new JudgeInfo();
        }

        public static List<UserInfo> GetSpecialTypeUser(int userType)
        {
            var a = new List<UserInfo>();
            if (userType <= 0 || userType > 5) return a;
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT * From User Where Type=@1";
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
                            while (reader.Read())
                                a.Add(new UserInfo
                                {
                                    UserId = reader.GetInt32(0),
                                    UserName = reader.GetString(1),
                                    Password = reader.GetString(3),
                                    Type = reader.GetInt32(4)
                                });
                        return a;
                    }
                }
            }
        }

        private static JudgeInfo[] QueryCustomJudgeInfo(int start, int count, string command)
        {
            var ji = new List<JudgeInfo>();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        try
                        {
                            cmd.CommandText = "SELECT * From Judge " + command + " order by JudgeId desc";
                            var reader = cmd.ExecuteReader();
                            if (reader.HasRows)
                                while (reader.Read())
                                {
                                    if (start-- > 0) continue;
                                    if (count-- == 0) break;
                                    try
                                    {
                                        var t = new JudgeInfo
                                        {
                                            JudgeId = reader.GetInt32(0),
                                            UserId = reader.GetInt32(1),
                                            JudgeDate = reader.GetString(2),
                                            ProblemId = reader.GetInt32(3),
                                            Timeused = CastStringArrToLongArr(reader.GetString(5)?.Split(',')),
                                            Memoryused = CastStringArrToLongArr(reader.GetString(6)?.Split(',')),
                                            Result = reader.GetString(8)?.Split(','),
                                            Score = CastStringArrToFloatArr(reader.GetString(9)?.Split(',')),
                                            CompetitionId = reader.GetInt32(12)
                                        };
                                        //if (t.ResultSummary == "Judging...") continue;
                                        ji.Add(t);
                                    }
                                    catch
                                    {
                                        ji.Add(new JudgeInfo
                                        {
                                            JudgeId = reader.GetInt32(0),
                                            JudgeDate = reader.GetString(2),
                                            Description = reader.GetString(11)
                                        });
                                    }
                                }
                        }
                        catch
                        {
                            //ignored
                        }
                    }
                }
            }
            return ji.ToArray();
        }

        private static JudgeInfo[] GetJudgeRecord(int userId, int start, int count)
        {
            var ji = new List<JudgeInfo>();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT * From Judge Where UserId=@1 and CompetitionId=0 order by JudgeId desc";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.Int32)
                        };
                        parameters[0].Value = userId;
                        cmd.Parameters.AddRange(parameters);
                        var reader = cmd.ExecuteReader();
                        if (reader.HasRows)
                            while (reader.Read())
                            {
                                if (start-- > 0) continue;
                                if (count-- == 0) break;
                                try
                                {
                                    var t = new JudgeInfo
                                    {
                                        JudgeId = reader.GetInt32(0),
                                        UserId = reader.GetInt32(1),
                                        JudgeDate = reader.GetString(2),
                                        ProblemId = reader.GetInt32(3),
                                        Code = "-|/|\\|-",
                                        Timeused = CastStringArrToLongArr(reader.GetString(5)?.Split(',')),
                                        Memoryused = CastStringArrToLongArr(reader.GetString(6)?.Split(',')),
                                        Exitcode = CastStringArrToIntArr(reader.GetString(7)?.Split(',')),
                                        Result = reader.GetString(8)?.Split(','),
                                        Score = CastStringArrToFloatArr(reader.GetString(9)?.Split(',')),
                                        Type = reader.GetString(10),
                                        Description = reader.GetString(11),
                                        CompetitionId = reader.GetInt32(12),
                                        AdditionInfo = reader.GetString(13)
                                    };
                                    if (t.ResultSummary == "Judging...") continue;
                                    ji.Add(t);
                                }
                                catch
                                {
                                    ji.Add(new JudgeInfo
                                    {
                                        JudgeId = reader.GetInt32(0),
                                        JudgeDate = reader.GetString(2),
                                        Description = reader.GetString(11)
                                    });
                                }
                            }
                    }
                }
            }
            return ji.ToArray();
        }

        private static bool RegisterUser(string userName, string password, bool requestReview)
        {
            if (CheckUser(userName) != 0)
                return false;
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    try
                    {
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
                            parameters[0].Value = userName;
                            parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            parameters[2].Value = password;
                            parameters[3].Value = requestReview ? 5 : 4;
                            parameters[4].Value = string.Empty;
                            parameters[5].Value = string.Empty;
                            parameters[6].Value = 0;
                            parameters[7].Value = 0;
                            cmd.Parameters.AddRange(parameters);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool RemoteRegister(string userName, string password)
        {
            if (Configuration.Configurations.RegisterMode == 0) return false;

            if (CheckUser(userName) != 0)
                return false;
            SHA256 s = new SHA256CryptoServiceProvider();
            var retVal = s.ComputeHash(Encoding.Unicode.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var t in retVal)
                sb.Append(t.ToString("x2"));
            if (Configuration.Configurations.RegisterMode == 1)
            {
                if (RegisterUser(userName, sb.ToString(), true))
                {
                    UserHelper.GetUserBelongs();
                    UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {userName} 注册待审核");
                }
            }
            else
            {
                if (RegisterUser(userName, sb.ToString(), false))
                {
                    UserHelper.GetUserBelongs();
                    UpdateMainPageState(
                        $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} 用户 {userName} 注册了");
                }
            }
            return true;
        }

        private static bool RemoteChangePassword(string userName, string oldPassword, string newPassword)
        {
            SHA256 s = new SHA256CryptoServiceProvider();
            var retVal = s.ComputeHash(Encoding.Unicode.GetBytes(oldPassword));
            var sb = new StringBuilder();
            foreach (var t in retVal)
                sb.Append(t.ToString("x2"));
            SHA256 s2 = new SHA256CryptoServiceProvider();
            var retVal2 = s2.ComputeHash(Encoding.Unicode.GetBytes(newPassword));
            var sb2 = new StringBuilder();
            foreach (var t in retVal2)
                sb2.Append(t.ToString("x2"));
            using (DataBaseLock.UpgradeableRead())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT Password From User Where userName=@1";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.String)
                        };
                        parameters[0].Value = userName;
                        cmd.Parameters.AddRange(parameters);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            if (sb.ToString() != (string)result) return false;
                            cmd.Parameters.Clear();
                            using (DataBaseLock.Write())
                            {
                                cmd.CommandText = "Update User SET Password=@1 Where UserName=@2";
                                SQLiteParameter[] parameters2 =
                                {
                                    new SQLiteParameter("@1", DbType.String),
                                    new SQLiteParameter("@2", DbType.String)
                                };
                                parameters2[0].Value = sb2.ToString();
                                parameters2[1].Value = userName;
                                cmd.Parameters.AddRange(parameters2);
                                cmd.ExecuteNonQuery();
                            }
                            return true;
                        }
                        return false;
                    }
                }
            }
        }

        private static bool UpdateCoins(int userId, int delta)
        {
            int origin;
            using (DataBaseLock.UpgradeableRead())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT Coins From User Where userId=@1";
                        SQLiteParameter[] parameters1 =
                        {
                            new SQLiteParameter("@1", DbType.Int32)
                        };
                        parameters1[0].Value = userId;
                        cmd.Parameters.AddRange(parameters1);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            origin = (int)result;
                        }
                        else return false;
                        cmd.Parameters.Clear();
                        using (DataBaseLock.Write())
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
                        }
                        return true;
                    }
                }
            }
        }

        private static bool UpdateExperience(int userId, int delta)
        {
            using (DataBaseLock.UpgradeableRead())
            {
                int origin;
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT Experience From User Where userId=@1";
                        SQLiteParameter[] parameters1 =
                        {
                            new SQLiteParameter("@1", DbType.Int32)
                        };
                        parameters1[0].Value = userId;
                        cmd.Parameters.AddRange(parameters1);
                        var result = cmd.ExecuteScalar();
                        if (result != null)
                        {
                            origin = (int)result;
                        }
                        else return false;
                        cmd.Parameters.Clear();
                        using (DataBaseLock.Write())
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
                        }
                        return true;
                    }
                }
            }
        }

        public static int NewCompetition()
        {
            using (DataBaseLock.UpgradeableRead())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        using (DataBaseLock.Write())
                        {
                            cmd.CommandText =
                            "Insert into Competition (CompetitionName, StartTime, EndTime, ProblemSet, Option, Password, Description, SubmitLimit) VALUES (@1, @2, @3, @4, @5, @6, @7, @8)";
                            SQLiteParameter[] parameters =
                            {
                                new SQLiteParameter("@1", DbType.String),
                                new SQLiteParameter("@2", DbType.String),
                                new SQLiteParameter("@3", DbType.String),
                                new SQLiteParameter("@4", DbType.String),
                                new SQLiteParameter("@5", DbType.Int32),
                                new SQLiteParameter("@6", DbType.String),
                                new SQLiteParameter("@7", DbType.String),
                                new SQLiteParameter("@8", DbType.Int32)
                            };
                            parameters[0].Value = string.Empty;
                            parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            parameters[2].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            parameters[3].Value = JsonConvert.SerializeObject(new int[0]);
                            parameters[4].Value = 25;
                            parameters[5].Value = string.Empty;
                            parameters[6].Value = string.Empty;
                            parameters[7].Value = 0;
                            cmd.Parameters.AddRange(parameters);
                            cmd.ExecuteNonQuery();
                        }


                        cmd.CommandText = "select last_insert_rowid() from Competition";
                        return Convert.ToInt32(cmd.ExecuteScalar());

                    }
                }
            }
        }

        public static bool UpdateCompetition(Competition competition)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText =
                            "UPDATE Competition SET CompetitionName=@1, StartTime=@2, EndTime=@3, ProblemSet=@4, Option=@5, Description=@6, SubmitLimit=@7, Password=@8 WHERE CompetitionId=@9";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.String),
                            new SQLiteParameter("@2", DbType.String),
                            new SQLiteParameter("@3", DbType.String),
                            new SQLiteParameter("@4", DbType.String),
                            new SQLiteParameter("@5", DbType.Int32),
                            new SQLiteParameter("@6", DbType.String),
                            new SQLiteParameter("@7", DbType.Int32),
                            new SQLiteParameter("@8", DbType.String),
                            new SQLiteParameter("@9", DbType.Int32)
                        };
                        parameters[0].Value = competition.CompetitionName;
                        parameters[1].Value = competition.StartTime.ToString("yyyy/MM/dd HH:mm:ss");
                        parameters[2].Value = competition.EndTime.ToString("yyyy/MM/dd HH:mm:ss");
                        parameters[3].Value = JsonConvert.SerializeObject(competition.ProblemSet);
                        parameters[4].Value = competition.Option;
                        parameters[5].Value = competition.Description;
                        parameters[6].Value = competition.SubmitLimit;
                        parameters[7].Value = competition.Password;
                        parameters[8].Value = competition.CompetitionId;
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
            }
        }

        private static bool RemoteUpdateProfile(int userId, string userName, string icon)
        {
            var k = CheckUser(userName);
            if (k != userId && k != 0)
                return false;
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
            }
        }

        private static int RemoteLogin(string userName, string password)
        {
            SHA256 s = new SHA256CryptoServiceProvider();
            var retVal = s.ComputeHash(Encoding.Unicode.GetBytes(password));
            var sb = new StringBuilder();
            foreach (var t in retVal)
                sb.Append(t.ToString("x2"));
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT * From User Where userName=@1";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.String)
                        };
                        parameters[0].Value = userName;
                        cmd.Parameters.AddRange(parameters);
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return 2;
                        if (!reader.Read()) return 1;
                        if (sb.ToString() != reader.GetString(3)) return 1;
                        if (reader.GetInt32(4) == 5) return 3;
                        return reader.GetInt32(0) != 1 ? 0 : 1;
                    }
                }
            }
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
                sb.Append(t.ToString("x2"));
            var a = await TryLogin(userName, sb.ToString());
            return a;
        }

        public static void UpdateUserInfo(UserInfo toUpdateInfo)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
            }
        }

        private static Task<int> TryLogin(string userName, string passwordHash)
        {
            return Task.Run(() =>
            {
                using (DataBaseLock.Read())
                {
                    using (var sqLite = new SQLiteConnection(ConnectionString))
                    {
                        sqLite.Open();

                        using (var cmd = new SQLiteCommand(sqLite))
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
                            UserHelper.SetCurrentUser(reader.GetInt32(0), reader.GetString(1), reader.GetString(2),
                                reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.GetString(6));
                            return 0;
                        }
                    }
                }
            });
        }

        public static ObservableCollection<UserInfo> GetUsersBelongs(int userType)
        {
            var a = new ObservableCollection<UserInfo>();
            if (userType <= 0 || userType >= 4) return a;
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
                            while (reader.Read())
                                a.Add(new UserInfo
                                {
                                    UserId = reader.GetInt32(0),
                                    UserName = reader.GetString(1),
                                    Password = reader.GetString(3),
                                    Type = reader.GetInt32(4)
                                });
                        return a;
                    }
                }
            }
        }

        public static Problem GetProblem(int problemId)
        {
            var a = new Problem();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
                                a.Option = reader.GetInt32(11);
                                a.Description = reader.GetString(12);
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
            }
        }

        public static void DeleteUser(IEnumerable<int> toDelete)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();
                    
                    using (var trans = sqLite.BeginTransaction())
                    {
                        using (var cmd = new SQLiteCommand(sqLite))
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
                                cmd.Parameters.Clear();
                            }
                        }
                        try
                        {
                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                        }
                    }
                }
            }
        }

        public static void UpdateUser(IEnumerable<UserInfo> toUpdate)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var trans = sqLite.BeginTransaction())
                    {
                        using (var cmd = new SQLiteCommand(sqLite))
                        {
                            foreach (var t in toUpdate)
                                if (CheckUser(t.UserName) != 0)
                                {
                                    cmd.CommandText = "Update User Set Password=@1, Type=@2 Where UserId=@3";
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
                                    cmd.Parameters.Clear();
                                }
                                else
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
                                    parameters[0].Value = t.UserName;
                                    parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                                    parameters[2].Value = t.Password;
                                    parameters[3].Value = t.Type;
                                    parameters[4].Value = string.Empty;
                                    parameters[5].Value = string.Empty;
                                    parameters[6].Value = 0;
                                    parameters[7].Value = 0;
                                    cmd.Parameters.AddRange(parameters);
                                    cmd.ExecuteNonQuery();
                                    cmd.Parameters.Clear();
                                }
                        }
                        try
                        {
                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                        }
                    }
                }
            }
        }

        public static List<string> SaveUser(IEnumerable<int> toDelete)
        {
            var failed = new List<string>();
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();
                    using (var trans = sqLite.BeginTransaction())
                    {
                        using (var cmd = new SQLiteCommand(sqLite))
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
                                cmd.Parameters.Clear();
                            }
                            foreach (var t in UserHelper.UsersBelongs)
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
                                    cmd.Parameters.Clear();
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
                                    parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                                    parameters[2].Value = t.Password;
                                    parameters[3].Value = t.Type;
                                    parameters[4].Value = string.Empty;
                                    parameters[5].Value = string.Empty;
                                    parameters[6].Value = 0;
                                    parameters[7].Value = 0;
                                    cmd.Parameters.AddRange(parameters);
                                    cmd.ExecuteNonQuery();
                                    cmd.Parameters.Clear();
                                }
                        }
                        try
                        {
                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                        }
                    }
                }
            }
            return failed;
        }

        private static int CheckUser(string userName)
        {
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT UserId From User Where UserName=@1";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.String)
                        };
                        parameters[0].Value = userName;
                        cmd.Parameters.AddRange(parameters);
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return 0;
                        if (reader.Read())
                            return reader.GetInt32(0);
                    }
                }
            }
            return 0;
        }

        public static void ClearJudgeLog()
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "Delete From Judge";
                        cmd.ExecuteNonQuery();
                        cmd.CommandText = "DELETE FROM sqlite_sequence WHERE name = 'Judge'";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static void DeleteCompetition(int competitionId)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = $"Delete From Competition Where CompetitionId={competitionId}";
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static Competition GetCompetition(int competitionId)
        {
            var a = new Competition();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = $"SELECT * From Competition where CompetitionId={competitionId}";
                        var reader = cmd.ExecuteReader();
                        if (reader.HasRows)
                            if (reader.Read())
                            {
                                a.CompetitionId = reader.GetInt32(0);
                                a.CompetitionName = reader.GetString(1);
                                a.StartTime = Convert.ToDateTime(reader.GetString(2));
                                a.EndTime = Convert.ToDateTime(reader.GetString(3));
                                a.ProblemSet = JsonConvert.DeserializeObject<int[]>(reader.GetString(4));
                                a.Option = reader.GetInt32(5);
                                a.Password = reader.GetString(6);
                                a.Description = reader.GetString(7);
                                a.SubmitLimit = reader.GetInt32(8);
                            }
                        return a;
                    }
                }
            }
        }

        public static ObservableCollection<Competition> QueryCompetition(int start = 0, int count = -10)
        {
            var a = new ObservableCollection<Competition>();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT * From Competition";
                        var reader = cmd.ExecuteReader();
                        if (reader.HasRows)
                            while (reader.Read())
                            {
                                if (count != -10)
                                {
                                    if (start-- > 0) continue;
                                    if (count-- == 0) break;
                                }
                                a.Add(new Competition
                                {
                                    CompetitionId = reader.GetInt32(0),
                                    CompetitionName = reader.GetString(1),
                                    StartTime = Convert.ToDateTime(reader.GetString(2)),
                                    EndTime = Convert.ToDateTime(reader.GetString(3)),
                                    ProblemSet = JsonConvert.DeserializeObject<int[]>(reader.GetString(4)),
                                    Option = reader.GetInt32(5),
                                    Password = reader.GetString(6),
                                    Description = reader.GetString(7),
                                    SubmitLimit = reader.GetInt32(8)
                                });
                            }
                        return a;
                    }
                }
            }
        }

        public static List<JudgeInfo> QueryJudgeLogBelongsToCompetition(int competitionId, int userId)
        {
            var a = new List<JudgeInfo>();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        if (userId != 0)
                            cmd.CommandText =
                                $"SELECT * From Judge Where CompetitionId={competitionId} and UserId={userId}";
                        else cmd.CommandText = $"SELECT * From Judge Where CompetitionId={competitionId}";
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return a;
                        while (reader.Read())
                            try
                            {
                                var t = new JudgeInfo
                                {
                                    JudgeId = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    JudgeDate = reader.GetString(2),
                                    ProblemId = reader.GetInt32(3),
                                    Code = reader.GetString(4),
                                    Timeused = CastStringArrToLongArr(reader.GetString(5)?.Split(',')),
                                    Memoryused = CastStringArrToLongArr(reader.GetString(6)?.Split(',')),
                                    Exitcode = CastStringArrToIntArr(reader.GetString(7)?.Split(',')),
                                    Result = reader.GetString(8)?.Split(','),
                                    Score = CastStringArrToFloatArr(reader.GetString(9)?.Split(',')),
                                    Type = reader.GetString(10),
                                    Description = reader.GetString(11),
                                    CompetitionId = reader.GetInt32(12),
                                    AdditionInfo = reader.GetString(13)
                                };
                                if (t.ResultSummary == "Judging...") continue;
                                a.Add(t);
                            }
                            catch
                            {
                                a.Add(new JudgeInfo
                                {
                                    JudgeId = reader.GetInt32(0),
                                    JudgeDate = reader.GetString(2),
                                    Description = reader.GetString(11)
                                });
                            }
                        return a;
                    }
                }
            }
        }

        public static ObservableCollection<JudgeInfo> QueryJudgeLog(bool withCode)
        {
            var curJudgeInfo = new ObservableCollection<JudgeInfo>();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = UserHelper.CurrentUser.Type < 4
                            ? "SELECT * From Judge"
                            : $"SELECT * From Judge Where UserId={UserHelper.CurrentUser.UserId}";
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return curJudgeInfo;
                        while (reader.Read())
                            try
                            {
                                curJudgeInfo.Add(new JudgeInfo
                                {
                                    JudgeId = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    JudgeDate = reader.GetString(2),
                                    ProblemId = reader.GetInt32(3),
                                    Code = withCode ? reader.GetString(4) : string.Empty,
                                    Timeused = CastStringArrToLongArr(reader.GetString(5)?.Split(',')),
                                    Memoryused = CastStringArrToLongArr(reader.GetString(6)?.Split(',')),
                                    Exitcode = CastStringArrToIntArr(reader.GetString(7)?.Split(',')),
                                    Result = reader.GetString(8)?.Split(','),
                                    Score = CastStringArrToFloatArr(reader.GetString(9)?.Split(',')),
                                    Type = reader.GetString(10),
                                    Description = reader.GetString(11),
                                    CompetitionId = reader.GetInt32(12),
                                    AdditionInfo = reader.GetString(13)
                                });
                            }
                            catch
                            {
                                curJudgeInfo.Add(new JudgeInfo
                                {
                                    JudgeId = reader.GetInt32(0),
                                    JudgeDate = reader.GetString(2),
                                    Description = reader.GetString(11)
                                });
                            }
                    }
                }
            }
            return curJudgeInfo;
        }

        public static Message GetMsg(int msgId, int userId)
        {
            var t = new Message();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = $"SELECT * From Message Where MessageId={msgId}";
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return t;
                        if (reader.Read())
                            try
                            {
                                t.MsgId = reader.GetInt32(0);
                                t.User = userId == reader.GetInt32(1) ? GetUserName(reader.GetInt32(2)) : GetUserName(reader.GetInt32(1));
                                t.MessageTime = Convert.ToDateTime(reader.GetString(3));
                                t.Content = reader.GetString(4);
                                t.State = reader.GetInt32(5);
                                t.Direction = userId == reader.GetInt32(1) ? "发送" : "接收";
                            }
                            catch
                            {
                                //ignored
                            }
                    }
                }
            }
            return t;
        }

        public static List<Message> QueryMsg(int userId, bool withContent, int start = 0, int count = -10)
        {
            var t = new List<Message>();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = $"SELECT * From Message Where FromUserId={userId} OR ToUserId={userId}";
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return t;
                        while (reader.Read())
                        {
                            if (count != -10)
                            {
                                if (start-- > 0) continue;
                                if (count-- == 0) break;
                            }
                            try
                            {
                                if (withContent)
                                    t.Add(new Message
                                    {
                                        MsgId = reader.GetInt32(0),
                                        User =
                                            reader.GetInt32(1) == userId
                                                ? GetUserName(reader.GetInt32(2))
                                                : GetUserName(reader.GetInt32(1)),
                                        MessageTime = Convert.ToDateTime(reader.GetString(3)),
                                        Content = reader.GetString(4),
                                        Direction = reader.GetInt32(1) == userId ? "发送" : "接收",
                                        State = reader.GetInt32(5)
                                    });
                                else
                                    t.Add(new Message
                                    {
                                        MsgId = reader.GetInt32(0),
                                        User =
                                            reader.GetInt32(1) == userId
                                                ? GetUserName(reader.GetInt32(2))
                                                : GetUserName(reader.GetInt32(1)),
                                        MessageTime = Convert.ToDateTime(reader.GetString(3)),
                                        Content =
                                            reader.GetString(4).Length > 30
                                                ? reader.GetString(4).Substring(0, 30) + "..."
                                                : reader.GetString(4),
                                        Direction = reader.GetInt32(1) == userId ? "发送" : "接收",
                                        State = reader.GetInt32(5)
                                    });
                            }
                            catch
                            {
                                //ignored
                            }
                        }
                    }
                }
            }
            return t;
        }


        private static int[] CastStringArrToIntArr(IReadOnlyList<string> p)
        {
            if (p == null) return null;
            var f = new int[p.Count];
            for (var i = 0; i < p.Count; i++)
                try
                {
                    f[i] = Convert.ToInt32(p[i]);
                }
                catch
                {
                    f[i] = 0;
                }
            return f;
        }

        private static long[] CastStringArrToLongArr(IReadOnlyList<string> p)
        {
            if (p == null) return null;
            var f = new long[p.Count];
            for (var i = 0; i < p.Count; i++)
                try
                {
                    f[i] = Convert.ToInt64(p[i]);
                }
                catch
                {
                    f[i] = 0;
                }
            return f;
        }

        private static float[] CastStringArrToFloatArr(IReadOnlyList<string> p)
        {
            if (p == null) return null;
            var f = new float[p.Count];
            for (var i = 0; i < p.Count; i++)
                try
                {
                    f[i] = Convert.ToSingle(p[i]);
                }
                catch
                {
                    f[i] = 0;
                }
            return f;
        }

        public static string GetUserName(int userId)
        {
            var userName = string.Empty;
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT UserName From User Where UserId=@1";
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
                            userName = reader.GetString(0);
                            break;
                        }
                    }
                }
            }
            return userName;
        }

        public static int GetUserId(string userName)
        {
            var userId = 0;
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT UserId From User Where UserName=@1";
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
                }
            }
            return userId;
        }

        public static UserInfo GetUser(int userId, bool withoutPassword = false)
        {
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
                            if (reader.Read())
                                return new UserInfo
                                {
                                    UserId = reader.GetInt32(0),
                                    UserName = reader.GetString(1),
                                    RegisterDate = reader.GetString(2),
                                    Password = withoutPassword ? string.Empty : reader.GetString(3),
                                    Type = reader.GetInt32(4),
                                    Icon = reader.GetString(5),
                                    Achievement = reader.GetString(6),
                                    Coins = reader.GetInt32(7),
                                    Experience = reader.GetInt32(8)
                                };
                        return null;
                    }
                }
            }
        }

        private static UserInfo GetUser(string userName, bool withoutPassword = false)
        {
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
                            if (reader.Read())
                                return new UserInfo
                                {
                                    UserId = reader.GetInt32(0),
                                    UserName = reader.GetString(1),
                                    RegisterDate = reader.GetString(2),
                                    Password = withoutPassword ? string.Empty : reader.GetString(3),
                                    Type = reader.GetInt32(4),
                                    Icon = reader.GetString(5),
                                    Achievement = reader.GetString(6),
                                    Coins = reader.GetInt32(7),
                                    Experience = reader.GetInt32(8)
                                };
                        return null;
                    }
                }
            }
        }

        public static string GetProblemName(int problemId)
        {
            var problemName = string.Empty;
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT ProblemName From Problem Where ProblemId=@1";
                        SQLiteParameter[] parameters =
                        {
                            new SQLiteParameter("@1", DbType.Int32)
                        };
                        parameters[0].Value = problemId;
                        cmd.Parameters.AddRange(parameters);
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return problemName;
                        if (reader.Read())
                            problemName = reader.GetString(0);
                    }
                }
            }
            return problemName;
        }

        public static int NewJudge(string description, int competitionId = 0)
        {
            using (DataBaseLock.UpgradeableRead())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        using (DataBaseLock.Write())
                        {
                            cmd.CommandText = "Insert into Judge (Date, Description, CompetitionId) VALUES (@1, @2, @3)";
                            SQLiteParameter[] parameters =
                            {
                                new SQLiteParameter("@1", DbType.String),
                                new SQLiteParameter("@2", DbType.String),
                                new SQLiteParameter("@3", DbType.Int32)
                            };
                            parameters[0].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            parameters[1].Value = description;
                            parameters[2].Value = competitionId;
                            cmd.Parameters.AddRange(parameters);
                            cmd.ExecuteNonQuery();
                        }

                        cmd.CommandText = "select last_insert_rowid() from Judge";
                        return Convert.ToInt32(cmd.ExecuteScalar());

                    }
                }
            }
        }

        public static void UpdateJudgeInfo(JudgeInfo pInfo)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText =
                            "UPDATE Judge SET UserId=@1, ProblemId=@2, Code=@3, Timeused=@4, Memoryused=@5, Exitcode=@6, Result=@7, Score=@8, Type=@10, Description=@11, CompetitionId=@12, AdditionInfo=@13 Where JudgeId=@9";
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
                            new SQLiteParameter("@9", DbType.Int32),
                            new SQLiteParameter("@10", DbType.String),
                            new SQLiteParameter("@11", DbType.String),
                            new SQLiteParameter("@12", DbType.Int32),
                            new SQLiteParameter("@13", DbType.String)
                        };
                        parameters[0].Value = pInfo.UserId;
                        parameters[1].Value = pInfo.ProblemId;
                        parameters[2].Value = pInfo.Code;
                        string timeused = string.Empty,
                            memoryused = string.Empty,
                            exitcode = string.Empty,
                            result = string.Empty,
                            score = string.Empty;
                        for (var i = 0; i < pInfo.Result.Length; i++)
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
                        parameters[3].Value = timeused;
                        parameters[4].Value = memoryused;
                        parameters[5].Value = exitcode;
                        parameters[6].Value = result;
                        parameters[7].Value = score;
                        parameters[8].Value = pInfo.JudgeId;
                        parameters[9].Value = pInfo.Type;
                        parameters[10].Value = pInfo.Description;
                        parameters[11].Value = pInfo.CompetitionId;
                        parameters[12].Value = pInfo.AdditionInfo;
                        cmd.Parameters.AddRange(parameters);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        public static ObservableCollection<Problem> QueryProblems(bool withPrivate, int start = 0, int count = -10)
        {
            var curJudgeInfo = new ObservableCollection<Problem>();
            using (DataBaseLock.Read())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText = "SELECT * From Problem";
                        var reader = cmd.ExecuteReader();
                        if (!reader.HasRows) return curJudgeInfo;
                        while (reader.Read())
                        {
                            if (count != -10)
                            {
                                if (start-- > 0) continue;
                                if (count-- == 0) break;
                            }
                            try
                            {
                                if (!withPrivate)
                                {
                                    if ((reader.GetInt32(11) & 1) != 0)
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
                                            CompileCommand = reader.GetString(10),
                                            Option = reader.GetInt32(11),
                                            Description = reader.GetString(12)
                                        });
                                    else
                                    {
                                        start++;
                                        if (count != -10) count++;
                                    }
                                }
                                else
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
                                        CompileCommand = reader.GetString(10),
                                        Option = reader.GetInt32(11),
                                        Description = reader.GetString(12)
                                    });
                                }
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
                }
            }
            return curJudgeInfo;
        }

        public static int NewProblem()
        {
            using (DataBaseLock.UpgradeableRead())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();
                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        using (DataBaseLock.Write())
                        {
                            cmd.CommandText =
                            "Insert into Problem (ProblemName, AddDate, Level, DataSets, Type, SpecialJudge, ExtraFiles, InputFileName, OutputFileName, CompileCommand, Option, Description) VALUES (@1, @2, @3, @4, @5, @6, @7, @8, @9, @10, @11, @12)";
                            SQLiteParameter[] parameters =
                            {
                                new SQLiteParameter("@1", DbType.String),
                                new SQLiteParameter("@2", DbType.String),
                                new SQLiteParameter("@3", DbType.Int32),
                                new SQLiteParameter("@4", DbType.String),
                                new SQLiteParameter("@5", DbType.Int32),
                                new SQLiteParameter("@6", DbType.String),
                                new SQLiteParameter("@7", DbType.String),
                                new SQLiteParameter("@8", DbType.String),
                                new SQLiteParameter("@9", DbType.String),
                                new SQLiteParameter("@10", DbType.String),
                                new SQLiteParameter("@11", DbType.String),
                                new SQLiteParameter("@12", DbType.String)
                            };
                            parameters[0].Value = string.Empty;
                            parameters[1].Value = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                            parameters[2].Value = 1;
                            parameters[3].Value = JsonConvert.SerializeObject(new Data[0]);
                            parameters[4].Value = 0;
                            parameters[5].Value = string.Empty;
                            parameters[6].Value = JsonConvert.SerializeObject(new string[0]);
                            parameters[7].Value = "${name}.in";
                            parameters[8].Value = "${name}.out";
                            parameters[9].Value = string.Empty;
                            parameters[10].Value = 0;
                            parameters[11].Value = string.Empty;
                            cmd.Parameters.AddRange(parameters);
                            cmd.ExecuteNonQuery();
                        }
                        cmd.CommandText = "select last_insert_rowid() from Problem";
                        return Convert.ToInt32(cmd.ExecuteScalar());
                    }
                }
            }
        }

        public static void DeleteProblem(int problemId)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
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
            }
        }

        public static void UpdateProblem(Problem toUpdateProblem)
        {
            using (DataBaseLock.Write())
            {
                using (var sqLite = new SQLiteConnection(ConnectionString))
                {
                    sqLite.Open();

                    using (var cmd = new SQLiteCommand(sqLite))
                    {
                        cmd.CommandText =
                            "UPDATE Problem SET ProblemName=@1, Level=@2, DataSets=@3, Type=@4, SpecialJudge=@5, ExtraFiles=@6, InputFileName=@7, OutputFileName=@8, CompileCommand=@9, Option=@10, Description=@11 Where ProblemId=@12";
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
                            new SQLiteParameter("@11", DbType.String),
                            new SQLiteParameter("@12", DbType.Int32)
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
                        parameters[9].Value = toUpdateProblem.Option;
                        parameters[10].Value = toUpdateProblem.Description;
                        parameters[11].Value = toUpdateProblem.ProblemId;
                        cmd.Parameters.AddRange(parameters);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        #endregion
    }
}