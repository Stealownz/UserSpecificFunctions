using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UserSpecificFunctions {
  public class USFPlayer {
    public int UserID;
    public string Prefix;
    public string Suffix;
    public byte R = 0;
    public byte G = 0;
    public byte B = 0;

    public string ChatColor {
      get { return string.Format("{0},{1},{2}", R.ToString("D3"), G.ToString("D3"), B.ToString("D3")); }
      set {
        if (null != value) {
          string[] parts = value.Split(',');
          if (3 == parts.Length) {
            byte r, g, b;
            if (byte.TryParse(parts[0], out r) && byte.TryParse(parts[1], out g) && byte.TryParse(parts[2], out b)) {
              R = r;
              G = g;
              B = b;
              return;
            }
          }
        }
      }
    }

    public USFPlayer(int userid, string prefix, string suffix, string chatcolor) {
      UserID = userid;
      Prefix = prefix;
      Suffix = suffix;
      ChatColor = chatcolor;
    }
  }
}
