using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TGMTAts.WCU {
    public static class Config {

        public const double LessInf = 0x7fffffff;
        public static string PluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);


        public static double TGMTTerrtoryStart = 0;
        public static double TGMTTerrtoryEnd = LessInf;
        public static string PretrainName = "ptrain";
        public static string PretrainTrackkey = "";
        public static double CTCSignalIndex = 255;


        private static void Cfg(this Dictionary<string, string> configDict, string key, ref double param) {
            if (configDict.ContainsKey(key)) {
                var value = configDict[key].ToLowerInvariant();
                if (value == "inf") {
                    param = LessInf;
                } else if (value == "-inf") {
                    param = -LessInf;
                } else {
                    double result;
                    if (!double.TryParse(configDict[key], out result)) return;
                    param = result;
                }
            }
        }

        private static void Cfg(this Dictionary<string, string> configDict, string key, ref bool param) {
            if (configDict.ContainsKey(key)) {
                var str = configDict[key].ToLowerInvariant();
                param = (str == "true" || str == "1");
            }
        }

        private static void Cfg(this Dictionary<string, string> configDict, string key, ref string param) {
            if (configDict.ContainsKey(key)) {
                param = configDict[key];
            }
        }

        private static void Cfg(this Dictionary<string, string> configDict, string key, ref int[] param) {
            if (configDict.ContainsKey(key)) {
                var outputList = new List<int>();
                foreach (var value in configDict[key].Split(',')) {
                    int result;
                    if (!int.TryParse(value.Trim(), out result)) return;
                    outputList.Add(result);
                }
                param = outputList.ToArray();
            }
        }

        public static void Load(string path) {
            if (!File.Exists(path)) return;

            var dict = new Dictionary<string, string>();
            StreamReader configFile = File.OpenText(path);
            string line;
            while ((line = configFile.ReadLine()) != null) {
                line = line.Trim();
                if (line.Length > 0 && line[0] != '#') {
                    string[] commentTokens = line.Split('#');
                    string[] tokens = commentTokens[0].Trim().Split('=');
                    dict.Add(tokens[0].Trim().ToLowerInvariant(), tokens[1].Trim());
                }
            }
            configFile.Close();

            dict.Cfg("tgmtterrtorystart", ref TGMTTerrtoryStart);
            dict.Cfg("tgmtterrtoryend", ref TGMTTerrtoryEnd);
            dict.Cfg("pretrainname", ref PretrainName);
            dict.Cfg("pretraintrackkey", ref PretrainTrackkey);
            dict.Cfg("ctcsignalindex", ref CTCSignalIndex);
        }
    }
}
