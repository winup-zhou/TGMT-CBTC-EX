using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Linq;
using System.Reflection;
using AtsEx.PluginHost.Plugins;
using Zbx1425.DXDynamicTexture;
using AtsEx.PluginHost;
using AtsEx.PluginHost.Input.Native;

namespace TGMTAts.OBCU {
    public partial class TGMTAts : AssemblyPluginBase {

        public static int[] panel_ = new int[256];
        public static bool doorOpen;
        public static AtsEx.PluginHost.Native.VehicleSpec vehicleSpec;
        public static double location = -114514;

        public static List<string> debugMessages = new List<string>();

        // 0: RM; 1: SM-I; 2: SM-C; 3: AM-I; 4: AM-C; 5: XAM
        public static int selectedMode = 4;
        // 0: RM; 1: SM; 2: AM; 3: XAM
        public static int driveMode = 1;
        // 0: IXL; 1: ITC; 2: CTC
        public static int signalMode = 2;
        // 1: MM; 2: AM; 3: AA
        public static int doorMode = 1;
        // 无线电
        public static bool RadioAvailable = false;
        public static bool RadioFailed = false;
        // WCU可用性
        public static bool WCUAvailable = false;

        // 暂时的预选速度，-1表示没有在预选
        public static int selectingMode = -1;
        public static double selectModeStartTime = 0;

        public static int ebState = 0;
        public static bool releaseSpeed = false;
        public static int ackMessage = 0;

        public static int TrainNumber = -114514;
        public static int DestinationNumber = -114514;
        public static double ITCNextSectionPos = -114514;

        //定位策略
        public static int BaliseCount = 0;
        public static double SigUpgradePosition = Config.LessInf;
        public static bool Localized = false;

        public static double reverseStartLocation = Config.LessInf;

        public static TrackLimit trackLimit = new TrackLimit();

        public static Form debugWindow;
        public static bool pluginReady = false;

        public static HarmonyLib.Harmony harmony;

        public static TextureHandle hTDTTex;
        public static TextureHandle hHMITex;

        static TGMTAts() {
            Config.Load(Path.Combine(Config.PluginDir, "TGMTConfig.txt"));
            //AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        const string ExpectedHash = "9758E6EA853B042ED49582081371764F43BC8E4DC7955C2B6D949015B984C8E2";

        private void Load() {
            if (Config.Debug) {
                new Thread(() => {
                    debugWindow = new DebugWindow();
                    Application.Run(debugWindow);
                }).Start();
            }
            try {
                //TextureManager.Initialize();
                TGMTPainter.Initialize();
                hHMITex = TextureManager.Register(Config.HMIImageSuffix, 1024, 1024);
                hTDTTex = TextureManager.Register(Config.TDTImageSuffix, 256, 256);
            } catch (Exception ex) {
                MessageBox.Show(ex.ToString());
            }
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args) {
            if (args.Name.Contains("Harmony")) {
                return Assembly.LoadFile(Config.HarmonyPath);
            }
            return null;
        }

        private static void CTCUpgrade() {
            signalMode = 2;
            FixIncompatibleModes();
        }
        static void FixIncompatibleModes() {
            if (BaliseCount >= 2) { Localized = true; BaliseCount = 0; }
            if (WCUAvailable) {
                if (selectedMode == 0 || !Localized) signalMode = 0; // 预选了IXL
                if (signalMode == 0 && driveMode > 0) driveMode = 0; // 没信号就得是RM
                if (Localized) {
                    if (selectedMode == 1 && signalMode > 1) signalMode = 1; // 预选了ITC
                    if (selectedMode == 3 && signalMode > 1) signalMode = 1; // 预选了ITC
                    if (!RadioAvailable && signalMode > 1) signalMode = 1; // 没有无线电信号
                    if (signalMode > 0 && driveMode == 0) driveMode = 1; // 有信号就至少是SM
                }
            } else {
                signalMode = 0;
            }
        }

        public static int ConvertTime(int human) {
            var hrs = human / 10000;
            var min = human / 100 % 100;
            var sec = human % 100;
            return hrs * 3600 + min * 60 + sec;
        }

        public static void SetSignal(int signal) {

        }
        public override void Dispose() {
            if (debugWindow != null) debugWindow.Close();
            TGMTPainter.Dispose();
            hHMITex.Dispose();
            hTDTTex.Dispose();

            ITCNextSectionPos = -114514;
            movementEndpoint = SpeedLimit.inf;
            nextLimit = null;
            selectingMode = -1;
            selectModeStartTime = 0;
            pluginReady = false;
            reverseStartLocation = Config.LessInf;
            releaseSpeed = false;
            ebState = 0;
            ackMessage = 0;

            DestinationNumber = TrainNumber = -114514;

            Ato.ResetCache();
            PreTrainManager.ResetCache();

            RadioAvailable = false;
            RadioFailed = false;
            WCUAvailable = false;
            doorMode = 1;
            signalMode = 2;
            driveMode = 1;
            selectedMode = 4;
            debugMessages.Clear();
            trackLimit = new TrackLimit();

            BaliseCount = 0;
            Localized = false;

            Native.NativeKeys.AtsKeys[NativeAtsKeyName.A1].Pressed -= OnA1Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.B1].Pressed -= OnB1Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.B2].Pressed -= OnB2Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.C1].Pressed -= OnC1Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.C2].Pressed -= OnC2Pressed;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.A1].Released -= OnA1Up;
            Native.NativeKeys.AtsKeys[NativeAtsKeyName.B1].Released -= OnB1Up;

            Native.BeaconPassed -= SetBeaconData;
            Native.DoorClosed -= DoorClose;
            Native.DoorOpened -= DoorOpen;
            Native.Started -= Initialize;

            Plugins.AllPluginsLoaded -= OnAllPluginsLoaded;
            //TextureManager.Dispose();
        }

        public static void Log(string msg) {
            time /= 1000;
            var hrs = time / 3600 % 60;
            var min = time / 60 % 60;
            var sec = time % 60;
            debugMessages.Add(string.Format("{0:D2}:{1:D2}:{2:D2} {3}", Convert.ToInt32(hrs), Convert.ToInt32(min), Convert.ToInt32(sec), msg));
        }
    }
}