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
        public override Version Version { get { return new Version(1, 0, 0, 0); } }

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

            if (!args.Text.StartsWith("/") && !tsplr.mute && existsInDatabase(tsplr.UserID))
            {
                TSPlayer.All.SendMessage(string.Format(TShock.Config.ChatFormat, tsplr.Group.Name, getUserPrefix(tsplr.UserID), tsplr.Name,
                    getUserSuffix(tsplr.UserID), args.Text), getUserColor(tsplr.UserID));

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

            if (args.Parameters[0].ToLower() == "color")
            {
                if (args.Parameters.Count == 5)
                {
                    User user = TShock.Users.GetUserByName(args.Parameters[1]);

                    if (user == null)
                    {
                        args.Player.SendErrorMessage("No users under that name.");
                        return;
                    }

                    int[] values = { 255, 255, 255 };

                    if (!int.TryParse(args.Parameters[2], out values[0]) || !int.TryParse(args.Parameters[3], out values[1]) || !int.TryParse(args.Parameters[4], out values[2]))
                    {
                        args.Player.SendErrorMessage("Invalid color: {0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);
                        return;
                    }

                    setUserColor(user.ID, values);
                    args.Player.SendSuccessMessage("Set {0}'s color to {1} {2} {3}.", user.Name, values[0], values[1], values[2]);
                }
                else
                    args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);

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
            if (hasPrefix(userid))
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

        private Color getUserColor(int userid)
        {
            byte[] color = { (byte)255, (byte)255, (byte)255 };

            if (hasColor(userid))
            {
                using (QueryResult reader = db.QueryReader("SELECT * FROM UserSpecificFunctions WHERE UserID=@0;", userid.ToString()))
                {
                    if (reader.Read())
                    {
                        color[0] = (byte)reader.Get<int>("R");
                        color[1] = (byte)reader.Get<int>("G");
                        color[2] = (byte)reader.Get<int>("B");
                    }
                }
            }

            return new Color(color[0], color[1], color[2]);
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
            if (existsInDatabase(userid))
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
        #endregion
    }
}
