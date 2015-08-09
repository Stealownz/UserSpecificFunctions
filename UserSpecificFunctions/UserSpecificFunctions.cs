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

namespace UserSpecificFunctions {
  [ApiVersion(1, 20)]
  public class UserSpecificFunctions : TerrariaPlugin {
    public override string Name { get { return "UserSpecificFunctions"; } }
    public override string Author { get { return "Professor X"; } }
    public override string Description { get { return "Enables setting a prefix, suffix or a color for a specific player"; } }
    public override Version Version { get { return new Version(2, 5, 1, 1); } }

    private IDbConnection db;

    private Dictionary<int, USFPlayer> Players = new Dictionary<int, USFPlayer>();
    public static UserSpecificFunctions LatestInstance;

    public static string permission_usfset = "usf.set";
    public static string permission_usfsetprefix = "usf.set.prefix";
    public static string permission_usfsetsuffix = "usf.set.suffix";
    public static string permission_usfsetcolor = "usf.set.color";
    public static string permission_usfsetother = "usf.set.other";

    public UserSpecificFunctions(Main game)
        : base(game) {
      LatestInstance = this;
    }

    #region Initialize/Dispose
    public override void Initialize() {
      ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
      ServerApi.Hooks.ServerChat.Register(this, OnChat);
      GeneralHooks.ReloadEvent += OnReload;
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
        ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
        GeneralHooks.ReloadEvent -= OnReload;
      }
      base.Dispose(disposing);
    }
    #endregion

    #region Hooks
    private void OnInitialize(EventArgs args) {
      SetupDb();
      loadDatabase();

      Commands.ChatCommands.Add(new Command("usf.set", USFCommand, "us"));
    }

    private void OnReload(ReloadEventArgs args) {
      loadDatabase();
    }

    private void OnChat(ServerChatEventArgs args) {
      if (args.Handled)
        return;

      TSPlayer tsplr = TShock.Players[args.Who];

      if (!args.Text.StartsWith("/") && !tsplr.mute && tsplr.IsLoggedIn && Players.ContainsKey(tsplr.User.ID)) {
        string prefix = Players[tsplr.User.ID].Prefix != null ? Players[tsplr.User.ID].Prefix : tsplr.Group.Prefix;
        string suffix = Players[tsplr.User.ID].Suffix != null ? Players[tsplr.User.ID].Suffix : tsplr.Group.Suffix;
        Color color = Players[tsplr.User.ID].ChatColor != string.Format("000,000,000") ?
          new Color(Players[tsplr.User.ID].R, Players[tsplr.User.ID].G, Players[tsplr.User.ID].B) :
          new Color(tsplr.Group.R, tsplr.Group.G, tsplr.Group.B);
        
        if (!TShock.Config.EnableChatAboveHeads) {
          TShock.Utils.Broadcast(string.Format(TShock.Config.ChatFormat, tsplr.Group.Name, prefix, tsplr.Name, suffix, args.Text), color);
        }
        else {
          Player ply = Main.player[args.Who];
          string name = ply.name;
          ply.name = String.Format(TShock.Config.ChatAboveHeadsFormat, tsplr.Group.Name, prefix, tsplr.Name, suffix);
          NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, ply.name, args.Who, 0, 0, 0, 0);
          ply.name = name;
          var text = args.Text;
          NetMessage.SendData((int)PacketTypes.ChatText, -1, args.Who, text, args.Who, color.R, color.G, color.B);
          NetMessage.SendData((int)PacketTypes.PlayerInfo, -1, -1, name, args.Who, 0, 0, 0, 0);

          string msg = String.Format("<{0}> {1}",
            String.Format(TShock.Config.ChatAboveHeadsFormat, tsplr.Group.Name, prefix, tsplr.Name, suffix),
            text);

          tsplr.SendMessage(msg, color.R, color.G, color.B);
          TSPlayer.Server.SendMessage(msg, color.R, color.G, color.B);
          TShock.Log.Info("Broadcast: {0}", msg);
          args.Handled = true;
        }

        args.Handled = true;
      }
    }
    #endregion

    #region Commands
    private void USFCommand(CommandArgs args) {
      if (args.Parameters.Count < 2) {
        USFCommandHelp(args);
        return;
      }

      User user = TShock.Users.GetUserByName(args.Parameters[1]);
      if (user == null) {
        args.Player.SendErrorMessage("No users under that name.");
        return;
      }
      if (user.Name != args.Player.User.Name && !args.Player.Group.HasPermission(permission_usfsetother)) {
        args.Player.SendErrorMessage("You cannot modify other players' stats.");
        return;
      }

      switch (args.Parameters[0].ToLower()) {
        case "prefix":
          {
            if (!args.Player.Group.HasPermission(permission_usfsetprefix)) {
              args.Player.SendErrorMessage("You do not have access to this command.");
              return;
            }

            if (args.Parameters.Count == 3) {
              string prefix = string.Join(" ", args.Parameters[2]);
              setUserPrefix(user.ID, prefix);
              args.Player.SendSuccessMessage("Set \"{0}\"'s prefix to: \"{1}\"", user.Name, prefix);
            }
            else if (args.Parameters.Count == 2) {
              if (!Players.ContainsKey(user.ID) || Players[user.ID].Prefix == null) {
                args.Player.SendErrorMessage("\"{0}\" has no prefix to display.", user.Name);
              }
              else {
                args.Player.SendSuccessMessage("\"{0}\"'s prefix is: \"{1}\"", user.Name, Players[user.ID].Prefix);
              }
            }
            else {
              args.Player.SendErrorMessage("Invalid syntax: {0}us prefix <player name> [prefix]", TShock.Config.CommandSpecifier);
              return;
            }
          }
          return;
        case "suffix":
          {
            if (!args.Player.Group.HasPermission(permission_usfsetsuffix)) {
              args.Player.SendErrorMessage("You do not have access to this command.");
              return;
            }

            if (args.Parameters.Count == 3) {
              string suffix = string.Join(" ", args.Parameters[2]);
              setUserSuffix(user.ID, suffix);
              args.Player.SendSuccessMessage("Set \"{0}\"'s suffix to: \"{1}\"", user.Name, suffix);
            }
            else if (args.Parameters.Count == 2) {
              if (!Players.ContainsKey(user.ID) || Players[user.ID].Suffix == null) {
                args.Player.SendErrorMessage("\"{0}\" has no suffix to display.", user.Name);
              }
              else {
                args.Player.SendSuccessMessage("\"{0}\"'s suffix is: \"{1}\"", user.Name, Players[user.ID].Suffix);
              }
            }
            else {
              args.Player.SendErrorMessage("Invalid syntax: {0}us suffix <player name> [suffix]", TShock.Config.CommandSpecifier);
              return;
            }
          }
          return;
        case "color":
          {
            if (!args.Player.Group.HasPermission(permission_usfsetcolor)) {
              args.Player.SendErrorMessage("You do not have access to this command.");
              return;
            }

            if (args.Parameters.Count == 3) {
              string color = args.Parameters[2];
              string[] parts = color.Split(',');
              byte r;
              byte g;
              byte b;
              if (parts.Length == 3 && byte.TryParse(parts[0], out r) && byte.TryParse(parts[1], out g) && byte.TryParse(parts[2], out b)) {
                try {
                  setUserColor(user.ID, color);
                  args.Player.SendSuccessMessage("Set \"{0}\"'s color to: \"{1}\"", user.Name, color);
                }
                catch (Exception ex) {
                  args.Player.SendErrorMessage(ex.ToString());
                }
              }
            }
            else if (args.Parameters.Count == 2) {
              if (!Players.ContainsKey(user.ID) || Players[user.ID].ChatColor == string.Format("000,000,000")) {
                args.Player.SendErrorMessage("\"{0}\" has no chat color to display.", user.Name);
              }
              else {
                args.Player.SendSuccessMessage("\"{0}\"'s chat color is: \"{1}\"", user.Name, Players[user.ID].ChatColor);
              }
            }
            else
              args.Player.SendErrorMessage("Invalid color: {0}us color <player name> [rrr,ggg,bbb]", TShock.Config.CommandSpecifier);
          }
          return;
        case "remove":
          {
            if (!args.Player.Group.HasPermission(permission_usfset)) {
              args.Player.SendErrorMessage("You do not have access to this command.");
              return;
            }

            if (args.Parameters.Count != 3) {
              args.Player.SendErrorMessage("Invalid syntax: {0}us remove <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
              return;
            }

            switch (args.Parameters[2].ToLower()) {
              case "prefix":
                {
                  if (!Players.ContainsKey(user.ID) || Players[user.ID].Prefix == null) {
                    args.Player.SendErrorMessage("This user doesn't have a prefix to remove.");
                    return;
                  }

                  removeUserPrefix(user.ID);
                  args.Player.SendSuccessMessage("Removed {0}'s prefix.", user.Name);
                }
                return;
              case "suffix":
                {
                  if (!Players.ContainsKey(user.ID) || Players[user.ID].Suffix == null) {
                    args.Player.SendErrorMessage("This user doesn't have a suffix to remove.");
                    return;
                  }

                  removeUserSuffix(user.ID);
                  args.Player.SendSuccessMessage("Removed {0}'s suffix.", user.Name);
                }
                return;
              case "color":
                {
                  if (!Players.ContainsKey(user.ID) || Players[user.ID].ChatColor == string.Format("000,000,000")) {
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
            USFCommandHelp(args);
          }
          return;
        default:
          {
            args.Player.SendErrorMessage("Invalid subcommand. Type {0}us help for a list of valid commands.", TShock.Config.CommandSpecifier);
          }
          return;
      }
    }

    private void USFCommandHelp(CommandArgs args) {
      args.Player.SendErrorMessage("Invalid syntax! Proper syntax:");
      if (args.Player.Group.HasPermission(permission_usfsetprefix))
        args.Player.SendErrorMessage("{0}us prefix <player name> <prefix>", TShock.Config.CommandSpecifier);
      if (args.Player.Group.HasPermission(permission_usfsetsuffix))
        args.Player.SendErrorMessage("{0}us suffix <player name> <suffix>", TShock.Config.CommandSpecifier);
      if (args.Player.Group.HasPermission(permission_usfsetcolor))
        args.Player.SendErrorMessage("{0}us color <player name> <r g b>", TShock.Config.CommandSpecifier);
      if (args.Player.Group.HasPermission(permission_usfset))
        args.Player.SendErrorMessage("{0}us remove <player name> <prefix/suffix/color>", TShock.Config.CommandSpecifier);
    }
    #endregion

    #region Database Methods
    private void SetupDb() {
      switch (TShock.Config.StorageType.ToLower()) {
        case "mysql":
          string[] dbHost = TShock.Config.MySqlHost.Split(':');
          db = new MySqlConnection() {
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

    private void loadDatabase() {
      Players.Clear();

      using (QueryResult reader = db.QueryReader("SELECT * FROM UserSpecificFunctions")) {
        while (reader.Read()) {
          int userid = reader.Get<int>("UserID");
          string prefix = reader.Get<string>("Prefix");
          string suffix = reader.Get<string>("Suffix");
          string color = reader.Get<string>("ChatColor");

          Players.Add(userid, new USFPlayer(userid, prefix, suffix, color));
        }
      }
    }

    public void setUserPrefix(int userid, string prefix) {
      if (!Players.ContainsKey(userid)) {
        Players.Add(userid, new USFPlayer(userid, prefix, null, string.Format("000,000,000")));
        db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, ChatColor) VALUES (@0, @1, @2, @3);", userid.ToString(), prefix, null, string.Format("000,000,000"));
      }
      else {
        Players[userid].Prefix = prefix;
        db.Query("UPDATE UserSpecificFunctions SET Prefix=@0 WHERE UserID=@1;", prefix, userid.ToString());
      }
    }

    public void setUserSuffix(int userid, string suffix) {
      if (!Players.ContainsKey(userid)) {
        Players.Add(userid, new USFPlayer(userid, null, suffix, string.Format("000,000,000")));
        db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, ChatColor) VALUES (@0, @1, @2, @3);", userid.ToString(), null, suffix, string.Format("000,000,000"));
      }
      else {
        Players[userid].Suffix = suffix;
        db.Query("UPDATE UserSpecificFunctions SET Suffix=@0 WHERE UserID=@1;", suffix, userid.ToString());
      }
    }

    public void setUserColor(int userid, string chatcolor) {
      if (!Players.ContainsKey(userid)) {
        Players.Add(userid, new USFPlayer(userid, null, null, chatcolor));
        db.Query("INSERT INTO UserSpecificFunctions (UserID, Prefix, Suffix, ChatColor) VALUES (@0, @1, @2, @3);", userid.ToString(), null, null, chatcolor);
      }
      else {
        Players[userid].ChatColor = chatcolor;
        db.Query("UPDATE UserSpecificFunctions SET ChatColor=@0 WHERE UserID=@1;", chatcolor, userid.ToString());
      }
    }

    public void removeUserPrefix(int userid) {
      Players[userid].Prefix = null;
      db.Query("UPDATE UserSpecificFunctions SET Prefix=null WHERE UserID=@0;", userid.ToString());
    }

    public void removeUserSuffix(int userid) {
      Players[userid].Suffix = null;
      db.Query("UPDATE UserSpecificFunctions SET Suffix=null WHERE UserID=@0;", userid.ToString());
    }

    public void removeUserColor(int userid) {
      Players[userid].ChatColor = string.Format("000,000,000");
      db.Query("UPDATE UserSpecificFunctions SET ChatColor=@0 WHERE UserID=@1;", string.Format("000,000,000"), userid.ToString());
    }
    #endregion
  }
}
