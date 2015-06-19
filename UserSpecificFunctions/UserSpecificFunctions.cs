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
        public override Version Version { get { return new Version(2, 3, 0, 0); } }

        private IDbConnection db;

        private Color userColor;

        private bool[] useChatColor = new bool[255];

        private System.Timers.Timer colorCheckTimer;

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

            colorCheckTimer = new System.Timers.Timer { AutoReset = true, Enabled = true, Interval = 1000 };
            colorCheckTimer.Elapsed += colorCheckTimer_Elapsed;

            Commands.ChatCommands.Add(new Command("usf.set", USFCommand, "us"));
        }

        private void OnChat(ServerChatEventArgs args)
        {
            if (args.Handled)
                return;

            TSPlayer tsplr = TShock.Players[args.Who];

            if (!args.Text.StartsWith("/") && !tsplr.mute && tsplr.IsLoggedIn && existsInDatabase(tsplr.UserID))
            {
                TSPlayer.All.SendMessage(string.Format(TShock.Config.ChatFormat, tsplr.Group.Name, (getUserPrefix(tsplr.UserID) != null ? getUserPrefix(tsplr.UserID) : tsplr.Group.Prefix), tsplr.Name,
                    (getUserSuffix(tsplr.UserID) != null ? getUserSuffix(tsplr.UserID) : tsplr.Group.Suffix), args.Text),
                    useChatColor[tsplr.UserID] ? userColor : new Color(tsplr.Group.R, tsplr.Group.G, tsplr.Group.B));

                args.Handled = true;
            }
        }
        #endregion

        #region colorCheckTimer_Elapsed
        private void colorCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs args)
        {
            foreach (TSPlayer tsplr in TShock.Players)
            {
                if (getUserColor(tsplr.UserID) != null)
                {
                    useChatColor[tsplr.UserID] = true;
                    if (useChatColor[tsplr.UserID])
                    {
                        userColor = getUserColor(tsplr.UserID);
                    }
                }
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

            User user = TShock.Users.GetUserByName(args.Parameters[1]);

            switch (args.Parameters[0].ToLower())
            {
                case "prefix":
                    {
                        if (args.Parameters.Count != 3)
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us prefix <player name> <prefix>", TShock.Config.CommandSpecifier);
                            return;
                        }

                        if (user == null)
                        {
                            args.Player.SendErrorMessage("No users under that name.");
                            return;
                        }

                        string prefix = string.Join(" ", args.Parameters[2]);
                        setUserPrefix(user.ID, prefix);
                        args.Player.SendSuccessMessage("Set \"{0}\"'s prefix to: \"{1}\"", user.Name, prefix);
                    }
                    return;
                case "suffix":
                    {
                        if (args.Parameters.Count != 3)
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us suffix <player name> <suffix>", TShock.Config.CommandSpecifier);
                            return;
                        }

                        if (user == null)
                        {
                            args.Player.SendErrorMessage("No users under that name.");
                            return;
                        }

                        string suffix = string.Join(" ", args.Parameters[2]);
                        setUserSuffix(user.ID, suffix);
                        args.Player.SendSuccessMessage("Set \"{0}\"'s suffix to: \"{1}\"", user.Name, suffix);
                    }
                    return;
                case "color":
                    {
                        if (args.Parameters.Count != 5)
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);
                            return;
                        }

                        if (user == null)
                        {
                            args.Player.SendErrorMessage("No users under that name.");
                            return;
                        }

                        int[] color = { (byte)255, (byte)255, (byte)255 };

                        if (!int.TryParse(args.Parameters[2], out color[0]) || !int.TryParse(args.Parameters[3], out color[1]) || !int.TryParse(args.Parameters[4], out color[2]))
                        {
                            args.Player.SendErrorMessage("Invalid color: {0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);
                            return;
                        }

                        setUserColor(user.ID, color);
                        args.Player.SendSuccessMessage("Set \"{0}\"'s color to: {1},{2},{3}", user.Name, color[0], color[1], color[2]);
                    }
                    return;
                case "remove":
                    {
                        if (args.Parameters.Count != 3)
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us remove <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
                            return;
                        }

                        if (user == null)
                        {
                            args.Player.SendErrorMessage("No users under that name.");
                            return;
                        }

                        switch (args.Parameters[2].ToLower())
                        {
                            case "prefix":
                                {
                                    if (getUserPrefix(user.ID) == null)
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a prefix to remove.");
                                        return;
                                    }

                                    removeUserPrefix(user.ID);
                                    args.Player.SendSuccessMessage("\"{0}\"'s prefix has been removed.", user.Name, getUserPrefix(user.ID));
                                }
                                return;
                            case "suffix":
                                {
                                    if (getUserSuffix(user.ID) == null)
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a suffix to remove.");
                                        return;
                                    }

                                    removeUserSuffix(user.ID);
                                    args.Player.SendSuccessMessage("\"{0}\"'s suffix has been removed.", user.Name);
                                }
                                return;
                            case "color":
                                {
                                    if (!useChatColor[user.ID])
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a color to remove.");
                                        return;
                                    }

                                    removeUserColor(user.ID);
                                    args.Player.SendSuccessMessage("\"{0}\"'s color has been removed.", user.Name);
                                }
                                return;
                        }
                    }
                    return;
                case "read":
                    {
                        if (args.Parameters.Count != 3)
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us read <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
                            return;
                        }

                        switch (args.Parameters[2].ToLower())
                        {
                            case "prefix":
                                {
                                    if (getUserPrefix(user.ID) == null)
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a prefix to read.");
                                        return;
                                    }

                                    args.Player.SendSuccessMessage("\"{0}\"'s prefix is: {1}", user.Name, getUserPrefix(user.ID));
                                }
                                return;
                            case "suffix":
                                {
                                    if (getUserSuffix(user.ID) == null)
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a suffix to read.");
                                        return;
                                    }

                                    args.Player.SendSuccessMessage("\"{0}\"'s suffix is: {1}", user.Name, getUserSuffix(user.ID));
                                }
                                return;
                            case "color":
                                {
                                    if (!useChatColor[user.ID])
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a color to read.");
                                        return;
                                    }

                                    args.Player.SendSuccessMessage("\"{0}\"'s color is: {1}", user.Name, userColor);
                                }
                                return;
                        }
                    }
                    return;
                case "help":
                    {
                        args.Player.SendInfoMessage("{0}us prefix <player name> <prefix>", TShock.Config.CommandSpecifier);
                        args.Player.SendInfoMessage("{0}us suffix <player name> <suffix>", TShock.Config.CommandSpecifier);
                        args.Player.SendInfoMessage("{0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);
                        args.Player.SendInfoMessage("{0}us remove <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
                        args.Player.SendInfoMessage("{0}us read <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
                    }
                    return;
                default:
                    {
                        args.Player.SendErrorMessage("Invalid subcommand. Type {0}us help for a list of valid commands.", TShock.Config.CommandSpecifier);
                    }
                    return;
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

        private string getUserPrefix(int userid)
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

        private string getUserSuffix(int userid)
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

        private Color getUserColor(int userid)
        {
            byte[] color = { (byte)255, (byte)255, (byte)255 };

            using (QueryResult reader = db.QueryReader("SELECT * FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
            {
                if (reader.Read())
                {
                    color[0] = (byte)reader.Get<int>("R");
                    color[1] = (byte)reader.Get<int>("G");
                    color[2] = (byte)reader.Get<int>("B");
                }
            }

            return new Color(color[0], color[1], color[2]);
        }

        private void setUserPrefix(int userid, string prefix)
        {
            if (existsInDatabase(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET Prefix=@0 WHERE UserID=@1;", prefix, userid.ToString());
            }
            else
            {
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, R, G, B) VALUES (@0, @1, @2, @3, @4, @5);", userid.ToString(), prefix, null, "", "", "");
            }
        }

        private void setUserSuffix(int userid, string suffix)
        {
            if (existsInDatabase(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET Suffix=@0 WHERE UserID=@1;", suffix, userid.ToString());
            }
            else
            {
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, R, G, B) VALUES (@0, @1, @2, @3, @4, @5);", userid.ToString(), null, suffix, "", "", "");
            }
        }

        private void setUserColor(int userid, int[] chatcolor)
        {
            if (existsInDatabase(userid))
            {
                db.Query("UPDATE UserSpecificFunctions SET R=@0 WHERE UserID=@1;", chatcolor[0], userid.ToString());
                db.Query("UPDATE UserSpecificFunctions SET G=@0 WHERE UserID=@1;", chatcolor[1], userid.ToString());
                db.Query("UPDATE UserSpecificFunctions SET B=@0 WHERE UserID=@1;", chatcolor[2], userid.ToString());
            }
            else
            {
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, R, G, B) VALUES (@0, @1, @2, @3, @4, @5);", userid.ToString(), null, null, chatcolor[0], chatcolor[1], chatcolor[2]);
            }
        }

        private void removeUserPrefix(int userid)
        {
            db.Query("UPDATE UserSpecificFunctions SET Prefix=null WHERE UserID=@0;", userid.ToString());
        }

        private void removeUserSuffix(int userid)
        {
            db.Query("UPDATE UserSpecificFunctions SET Suffix=null WHERE UserID=@0;", userid.ToString());
        }

        private void removeUserColor(int userid)
        {
            db.Query("UPDATE UserSpecificFunctions SET R=null WHERE UserID=@0;", userid.ToString());
            db.Query("UPDATE UserSpecificFunctions SET G=null WHERE UserID=@0;", userid.ToString());
            db.Query("UPDATE UserSpecificFunctions SET B=null WHERE UserID=@0;", userid.ToString());
        }
        #endregion
    }
}
