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
    [ApiVersion(1, 20)]
    public class UserSpecificFunctions : TerrariaPlugin
    {
        public override string Name { get { return "UserSpecificFunctions"; } }
        public override string Author { get { return "Professor X"; } }
        public override string Description { get { return "Enables setting a prefix, suffix or a color for a specific player"; } }
        public override Version Version { get { return new Version(2, 5, 1, 1); } }

        private IDbConnection db;

        private Dictionary<int, USFPlayer> Players = new Dictionary<int, USFPlayer>();

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
            loadDatabase();

            Commands.ChatCommands.Add(new Command("usf.set", USFCommand, "us"));
        }

        private void OnChat(ServerChatEventArgs args)
        {
            if (args.Handled)
                return;

            TSPlayer tsplr = TShock.Players[args.Who];

            if (!args.Text.StartsWith("/") && !tsplr.mute && tsplr.IsLoggedIn && Players.ContainsKey(tsplr.User.ID))
            {
                TShock.Utils.Broadcast(string.Format(TShock.Config.ChatFormat, tsplr.Group.Name, Players[tsplr.User.ID].Prefix != null
                    ? Players[tsplr.User.ID].Prefix : tsplr.Group.Prefix, tsplr.Name, Players[tsplr.User.ID].Suffix != null ?
                    Players[tsplr.User.ID].Suffix : tsplr.Group.Suffix, args.Text), Players[tsplr.User.ID].ChatColor != string.Format("000,000,000") ?
                    new Color(Players[tsplr.User.ID].R, Players[tsplr.User.ID].G, Players[tsplr.User.ID].B) : new Color(tsplr.Group.R, tsplr.Group.G, tsplr.Group.B));

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

            User user = TShock.Users.GetUserByName(args.Parameters[1]);
            if (user == null)
            {
                args.Player.SendErrorMessage("No users under that name.");
                return;
            }
            if (user.Name != args.Player.User.Name && !args.Player.Group.HasPermission("usf.set.other"))
            {
                args.Player.SendErrorMessage("You cannot modify other players' stats.");
                return;
            }

            switch (args.Parameters[0].ToLower())
            {
                case "prefix":
                    {
                        if (args.Parameters.Count == 3)
                        {
                            string prefix = string.Join(" ", args.Parameters[2]);
                            setUserPrefix(user.ID, prefix);
                            args.Player.SendSuccessMessage("Set \"{0}\"'s prefix to: \"{1}\"", user.Name, prefix);
                        }
                        else if (args.Parameters.Count == 2)
                        {
                            if (!Players.ContainsKey(user.ID) || Players[user.ID].Prefix == null)
                            {
                                args.Player.SendErrorMessage("\"{0}\" has no prefix to display.", user.Name);
                            }
                            else
                            {
                                args.Player.SendSuccessMessage("\"{0}\"'s prefix is: \"{1}\"", user.Name, Players[user.ID].Prefix);
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us prefix <player name> [prefix]", TShock.Config.CommandSpecifier);
                            return;
                        }
                    }
                    return;
                case "suffix":
                    {
                        if (args.Parameters.Count == 3)
                        {
                            string suffix = string.Join(" ", args.Parameters[2]);
                            setUserSuffix(user.ID, suffix);
                            args.Player.SendSuccessMessage("Set \"{0}\"'s suffix to: \"{1}\"", user.Name, suffix);
                        }
                        else if (args.Parameters.Count == 2)
                        {
                            if (!Players.ContainsKey(user.ID) || Players[user.ID].Suffix == null)
                            {
                                args.Player.SendErrorMessage("\"{0}\" has no suffix to display.", user.Name);
                            }
                            else
                            {
                                args.Player.SendSuccessMessage("\"{0}\"'s suffix is: \"{1}\"", user.Name, Players[user.ID].Suffix);
                            }
                        }
                        else
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us suffix <player name> [suffix]", TShock.Config.CommandSpecifier);
                            return;
                        }
                    }
                    return;
                case "color":
                    {
                        if (args.Parameters.Count == 3)
                        {
                            string color = args.Parameters[2];
                            string[] parts = color.Split(',');
                            byte r;
                            byte g;
                            byte b;
                            if (parts.Length == 3 && byte.TryParse(parts[0], out r) && byte.TryParse(parts[1], out g) && byte.TryParse(parts[2], out b))
                            {
                                try
                                {
                                    setUserColor(user.ID, color);
                                    args.Player.SendSuccessMessage("Set \"{0}\"'s color to: \"{1}\"", user.Name, color);
                                }
                                catch (Exception ex)
                                {
                                    args.Player.SendErrorMessage(ex.ToString());
                                }
                            }
                        }
                        else if (args.Parameters.Count == 2)
                        {
                            if (!Players.ContainsKey(user.ID) || Players[user.ID].ChatColor == string.Format("000,000,000"))
                            {
                                args.Player.SendErrorMessage("\"{0}\" has no chat color to display.", user.Name);
                            }
                            else
                            {
                                args.Player.SendSuccessMessage("\"{0}\"'s chat color is: \"{1}\"", user.Name, Players[user.ID].ChatColor);
                            }
                        }
                        else
                            args.Player.SendErrorMessage("Invalid color: {0}us color <player name> [rrr,ggg,bbb]", TShock.Config.CommandSpecifier);
                    }
                    return;
                case "remove":
                    {
                        if (args.Parameters.Count != 3)
                        {
                            args.Player.SendErrorMessage("Invalid syntax: {0}us remove <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
                            return;
                        }

                        switch (args.Parameters[2].ToLower())
                        {
                            case "prefix":
                                {
                                    if (!Players.ContainsKey(user.ID) || Players[user.ID].Prefix == null)
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a prefix to remove.");
                                        return;
                                    }

                                    removeUserPrefix(user.ID);
                                    args.Player.SendSuccessMessage("Removed {0}'s prefix.", user.Name);
                                }
                                return;
                            case "suffix":
                                {
                                    if (!Players.ContainsKey(user.ID) || Players[user.ID].Suffix == null)
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a suffix to remove.");
                                        return;
                                    }

                                    removeUserSuffix(user.ID);
                                    args.Player.SendSuccessMessage("Removed {0}'s suffix.", user.Name);
                                }
                                return;
                            case "color":
                                {
                                    if (!Players.ContainsKey(user.ID) || Players[user.ID].ChatColor == string.Format("000,000,000"))
                                    {
                                        args.Player.SendErrorMessage("This user doesn't have a color to remove.");
                                        return;
                                    }

                                    removeUserColor(user.ID);
                                    args.Player.SendSuccessMessage("Removed {0}'s color.", user.Name);
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
                new SqlColumn("ChatColor", MySqlDbType.Text)));
        }

        private void loadDatabase()
        {
            Players.Clear();

            using (QueryResult reader = db.QueryReader("SELECT * FROM UserSpecificFunctions"))
            {
                while (reader.Read())
                {
                    int userid = reader.Get<int>("UserID");
                    string prefix = reader.Get<string>("Prefix");
                    string suffix = reader.Get<string>("Suffix");
                    string color = reader.Get<string>("ChatColor");

                    Players.Add(userid, new USFPlayer(userid, prefix, suffix, color));
                }
            }
        }

        private void setUserPrefix(int userid, string prefix)
        {
            if (!Players.ContainsKey(userid))
            {
                Players.Add(userid, new USFPlayer(userid, prefix, null, string.Format("000,000,000")));
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, ChatColor) VALUES (@0, @1, @2, @3);", userid.ToString(), prefix, null, string.Format("000,000,000"));
            }
            else
            {
                Players[userid].Prefix = prefix;
                db.Query("UPDATE UserSpecificFunctions SET Prefix=@0 WHERE UserID=@1;", prefix, userid.ToString());
            }
        }

        private void setUserSuffix(int userid, string suffix)
        {
            if (!Players.ContainsKey(userid))
            {
                Players.Add(userid, new USFPlayer(userid, null, suffix, string.Format("000,000,000")));
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, ChatColor) VALUES (@0, @1, @2, @3);", userid.ToString(), null, suffix, string.Format("000,000,000"));
            }
            else
            {
                Players[userid].Suffix = suffix;
                db.Query("UPDATE UserSpecificFunctions SET Suffix=@0 WHERE UserID=@1;", suffix, userid.ToString());
            }
        }

        private void setUserColor(int userid, string chatcolor)
        {
            if (!Players.ContainsKey(userid))
            {
                Players.Add(userid, new USFPlayer(userid, null, null, chatcolor));
                db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, ChatColor) VALUES (@0, @1, @2, @3);", userid.ToString(), null, null, chatcolor);
            }
            else
            {
                Players[userid].ChatColor = chatcolor;
                db.Query("UPDATE UserSpecificFunctions SET ChatColor=@0 WHERE UserID=@1;", chatcolor, userid.ToString());
            }
        }

        private void removeUserPrefix(int userid)
        {
            Players[userid].Prefix = null;
            db.Query("UPDATE UserSpecificFunctions SET Prefix=null WHERE UserID=@0;", userid.ToString());
        }

        private void removeUserSuffix(int userid)
        {
            Players[userid].Suffix = null;
            db.Query("UPDATE UserSpecificFunctions SET Suffix=null WHERE UserID=@0;", userid.ToString());
        }

        private void removeUserColor(int userid)
        {
            Players[userid].ChatColor = string.Format("000,000,000");
            db.Query("UPDATE UserSpecificFunctions SET ChatColor=@0 WHERE UserID=@1;", string.Format("000,000,000"), userid.ToString());
        }
        #endregion
    }
}
