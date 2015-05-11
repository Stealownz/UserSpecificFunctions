using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Data;

using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using TShockAPI.Hooks;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace UserSpecificFunctions
{
    [ApiVersion(1, 17)]
    public class UserSpecificFunctions : TerrariaPlugin
    {
        public override string Name { get { return "UserSpecificFunctions"; } }
        public override string Author { get { return "Professor X"; } }
        public override string Description { get { return "Enables setting a prefix, suffix or a color for a specific player"; } }
        public override Version Version { get { return new Version(1, 1, 3, 0); } }

        private IDbConnection db;

        public UserSpecificFunctions(Main game)
            : base(game)
        {

        }

        #region Initialize/Dispose
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
            }
            base.Dispose(disposing);
        }
        #endregion

        #region Hooks
        private void OnInitialize(EventArgs args)
        {
            SetupDb();

            Commands.ChatCommands.Add(new Command("usf.set", USFCommand, "us"));
        }

        private void OnChat(ServerChatEventArgs args)
        {
            if (args.Handled)
                return;

            TSPlayer tsplr = TShock.Players[args.Who];

            if (!args.Text.StartsWith("/") && !tsplr.mute && tsplr.IsLoggedIn && existsInDatabase(tsplr.UserID))
            {
                TSPlayer.All.SendMessage(string.Format(TShock.Config.ChatFormat, tsplr.Group.Name, (hasPrefix(tsplr.UserID) && getUserPrefix(tsplr.UserID) != null ? getUserPrefix(tsplr.UserID) : tsplr.Group.Prefix), tsplr.Name,
                    (hasSuffix(tsplr.UserID) && getUserSuffix(tsplr.UserID) != null ? getUserSuffix(tsplr.UserID) : tsplr.Group.Suffix), args.Text), tsplr.Group.R, tsplr.Group.G, tsplr.Group.B);

                args.Handled = true;
            }
        }
        #endregion

        #region Commands
        private void USFCommand(CommandArgs args)
        {
            if (args.Parameters.Count < 1)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax:");
                args.Player.SendErrorMessage("{0}us prefix <player name> <prefix>", TShock.Config.CommandSpecifier);
                args.Player.SendErrorMessage("{0}us suffix <player name> <suffix>", TShock.Config.CommandSpecifier);
                args.Player.SendErrorMessage("{0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);
                args.Player.SendErrorMessage("{0}us remove <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
                args.Player.SendErrorMessage("{0}us read <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
                return;
            }

            if (args.Parameters[0].ToLower() == "prefix")
            {
                if (args.Parameters.Count == 3)
                {
                    User user = TShock.Users.GetUserByName(args.Parameters[1]);

                    if (user == null)
                    {
                        args.Player.SendErrorMessage("No users under that name.");
                        return;
                    }

                    string prefix = string.Join(" ", args.Parameters[2]);

                    setUserPrefix(user.ID, prefix);
                    args.Player.SendSuccessMessage("Set {0}'s prefix to {1}.", user.Name, prefix);
                }
                else
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}us prefix <player name> <prefix>", TShock.Config.CommandSpecifier);

                return;
            }

            if (args.Parameters[0].ToLower() == "suffix")
            {
                if (args.Parameters.Count == 3)
                {
                    User user = TShock.Users.GetUserByName(args.Parameters[1]);

                    if (user == null)
                    {
                        args.Player.SendErrorMessage("No users under that name.");
                        return;
                    }

                    string suffix = string.Join(" ", args.Parameters[2]);

                    setUserSuffix(user.ID, suffix);
                    args.Player.SendSuccessMessage("Set {0}'s suffix to {1}.", user.Name, suffix);
                }
                else
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}us suffix <player name> <suffix>", TShock.Config.CommandSpecifier);

                return;
            }

            //if (args.Parameters[0].ToLower() == "color")
            //{
            //    if (args.Parameters.Count == 5)
            //    {
            //        User user = TShock.Users.GetUserByName(args.Parameters[1]);

            //        if (user == null)
            //        {
            //            args.Player.SendErrorMessage("No users under that name.");
            //            return;
            //        }

            //        int[] values = { 255, 255, 255 };

            //        if (!int.TryParse(args.Parameters[2], out values[0]) || !int.TryParse(args.Parameters[3], out values[1]) || !int.TryParse(args.Parameters[4], out values[2]))
            //        {
            //            args.Player.SendErrorMessage("Invalid color: {0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);
            //            return;
            //        }

            //        setUserColor(user.ID, values);
            //        args.Player.SendSuccessMessage("Set {0}'s color to {1} {2} {3}.", user.Name, values[0], values[1], values[2]);
            //    }
            //    else
            //        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);

            //    return;
            //}

            if (args.Parameters[0].ToLower() == "remove")
            {
                if (args.Parameters.Count == 3)
                {
                    User user = TShock.Users.GetUserByName(args.Parameters[1]);

                    if (user == null)
                    {
                        args.Player.SendErrorMessage("No users under that name.");
                        return;
                    }

                    if (args.Parameters[2].ToLower() == "prefix")
                    {
                        if (!hasPrefix(user.ID))
                        {
                            args.Player.SendErrorMessage("This user doesn't have a prefix to remove.");
                            return;
                        }
                        else
                        {
                            removeUserPrefix(user.ID);
                            args.Player.SendSuccessMessage("Removed {0}'s prefix.", user.Name);
                        }
                    }

                    if (args.Parameters[2].ToLower() == "suffix")
                    {
                        if (!hasSuffix(user.ID))
                        {
                            args.Player.SendErrorMessage("This user doesn't have a suffix to remove.");
                            return;
                        }
                        else
                        {
                            removeUserSuffix(user.ID);
                            args.Player.SendSuccessMessage("Removed {0}'s suffix.", user.Name);
                        }
                    }

                    //if (args.Parameters[2].ToLower() == "color")
                    //{
                    //    if (!hasColor(user.ID))
                    //    {
                    //        args.Player.SendErrorMessage("This user doesn't have a color to remove.");
                    //        return;
                    //    }
                    //    else
                    //    {
                    //        removeUserColor(user.ID);
                    //        args.Player.SendSuccessMessage("Removed {0}'s color.", user.Name);
                    //    }
                    //}
                }
                else
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}us remove <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
            }

            if (args.Parameters[0].ToLower() == "read")
            {
                if (args.Parameters.Count == 3)
                {
                    User user = TShock.Users.GetUserByName(args.Parameters[1]);

                    if (user == null)
                    {
                        args.Player.SendErrorMessage("No users under that name.");
                        return;
                    }

                    if (args.Parameters[2].ToLower() == "prefix")
                    {
                        if (!hasPrefix(user.ID))
                        {
                            args.Player.SendErrorMessage("This user doesn't have a prefix to read.");
                            return;
                        }
                        else
                        {
                            args.Player.SendSuccessMessage("{0}'s prefix is: {1}", user.Name, getUserPrefix(user.ID));
                        }
                    }

                    if (args.Parameters[2].ToLower() == "suffix")
                    {
                        if (!hasSuffix(user.ID))
                        {
                            args.Player.SendErrorMessage("This user doesn't have a suffix to read.");
                            return;
                        }
                        else
                        {
                            args.Player.SendSuccessMessage("{0}'s suffix is: {1}", user.Name, getUserSuffix(user.ID));
                        }
                    }

                    //if (args.Parameters[2].ToLower() == "color")
                    //{
                    //    if (!hasColor(user.ID))
                    //    {
                    //        args.Player.SendErrorMessage("This user doesn't have a color to read.");
                    //        return;
                    //    }
                    //    else
                    //    {
                    //        args.Player.SendSuccessMessage("{0}'s color is: {1}", user.Name, getUserColor(user.ID));
                    //    }
                    //}
                }
            }
        }
        #endregion

        #region Database Methods
        private void SetupDb()
        {
            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] dbHost = TShock.Config.MySqlHost.Split(':');
                    db = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            dbHost[0],
                            dbHost.Length == 1 ? "3306" : dbHost[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)

                    };
                    break;

                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "tshock.sqlite");
                    db = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;

            }

            SqlTableCreator sqlcreator = new SqlTableCreator(db, db.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            sqlcreator.EnsureTableStructure(new SqlTable("UserSpecificFunctions",
                new SqlColumn("UserID", MySqlDbType.Int32) { Primary = true, Unique = true, Length = 6 },
                new SqlColumn("Prefix", MySqlDbType.Text) { Length = 25 },
                new SqlColumn("Suffix", MySqlDbType.Text) { Length = 25 },
                new SqlColumn("R", MySqlDbType.Int32),
                new SqlColumn("G", MySqlDbType.Int32),
                new SqlColumn("B", MySqlDbType.Int32)));
        }

        private bool existsInDatabase(int userid)
        {
            using (QueryResult reader = db.QueryReader("SELECT * FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
            {
                if (reader.Read())
                {
                    return true;
                }
                else
                    return false;
            }
        }

        private bool hasPrefix(int userid)
        {
            using (QueryResult reader = db.QueryReader("SELECT Prefix FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
            {
                if (reader.Read())
                {
                    return true;
                }
                else
                    return false;
            }
        }

        private bool hasSuffix(int userid)
        {
            using (QueryResult reader = db.QueryReader("SELECT Suffix FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
            {
                if (reader.Read())
                {
                    return true;
                }
                else
                    return false;
            }
        }

        private bool hasColor(int userid)
        {
            using (QueryResult reader = db.QueryReader("SELECT R, G, B FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
            {
                if (reader.Read())
                {
                    return true;
                }
                else
                    return false;
            }
        }

        private string getUserPrefix(int userid)
        {
            if (hasPrefix(userid))
            {
                using (QueryResult reader = db.QueryReader("SELECT Prefix FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
                {
                    if (reader.Read())
                    {
                        return reader.Get<string>("Prefix");
                    }
                    else
                        return null;
                }
            }
            else
                return null;
        }

        private string getUserSuffix(int userid)
        {
            if (hasSuffix(userid))
            {
                using (QueryResult reader = db.QueryReader("SELECT Suffix FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
                {
                    if (reader.Read())
                    {
                        return reader.Get<string>("Suffix");
                    }
                    else
                        return null;
                }
            }
            else
                return null;
        }

        private void setUserPrefix(int userid, string prefix)
        {
            if (hasPrefix(userid) || existsInDatabase(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET Prefix=@0 WHERE UserID=@1;", prefix, userid.ToString());
            }
            else
            {
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, R, G, B) VALUES (@0, @1, @2, @3, @4, @5);", userid.ToString(), prefix, string.Empty, "", "", "");
            }
        }

        private void setUserSuffix(int userid, string suffix)
        {
            if (hasSuffix(userid) || existsInDatabase(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET Suffix=@0 WHERE UserID=@1;", suffix, userid.ToString());
            }
            else
            {
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, R, G, B) VALUES (@0, @1, @2, @3, @4, @5);", userid.ToString(), string.Empty, suffix, "", "", "");
            }
        }

        private void setUserColor(int userid, int[] color)
        {
            if (hasColor(userid) || existsInDatabase(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET R=@0 WHERE UserID=@1;", color[0], userid.ToString());
                db.Query("UPDATE UserSpecificFunctions SET B=@0 WHERE UserID=@1;", color[1], userid.ToString());
                db.Query("UPDATE UserSpecificFunctions SET G=@0 WHERE UserID=@1;", color[2], userid.ToString());
            }
            else
            {
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, R, G, B) VALUES (@0, @1, @2, @3, @4, @5);", userid.ToString(), string.Empty, string.Empty, color[0], color[1], color[2]);
            }
        }

        private void removeUserPrefix(int userid)
        {
            if (hasPrefix(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET Prefix=null WHERE UserID=@0;", userid.ToString());
            }
        }

        private void removeUserSuffix(int userid)
        {
            if (hasSuffix(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET Suffix=null WHERE UserID=@0;", userid.ToString());
            }
        }

        private void removeUserColor(int userid)
        {
            if (hasColor(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET R=null WHERE UserID=@0;", userid.ToString());
                db.Query("UPDATE UserSpecificFunctions SET G=null WHERE UserID=@0;", userid.ToString());
                db.Query("UPDATE UserSpecificFunctions SET B=null WHERE UserID=@0;", userid.ToString());
            }
        }
        #endregion
    }
}
