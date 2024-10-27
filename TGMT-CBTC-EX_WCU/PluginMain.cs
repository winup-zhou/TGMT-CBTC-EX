using System;
using System.Reflection;

using BveTypes.ClassWrappers;
using AtsEx.Extensions.SignalPatch;
using AtsEx.PluginHost;
using AtsEx.PluginHost.Plugins;
using AtsEx.Extensions.PreTrainPatch;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Linq;


namespace TGMTAts.WCU {
    [PluginType(PluginType.MapPlugin)]
    public class TGMTAts : AssemblyPluginBase {
        static TGMTAts() {
            Config.Load(Path.Combine(Config.PluginDir, "TGMT_WCUConfig.txt"));
        }
        //WCU -> OBCU
        private double MA = 0;
        public double MovementAuthority {
            get {
                return MA;
            }
        }
        private double nextBlockLocation = 0;
        public double NextSectionLocation {
            get {
                return nextBlockLocation;
            }
        }
        private bool isTrainHold = false;
        public bool TrainHold {
            get {
                return isTrainHold;
            }
        }

        //OBCU -> WCU
        public int OBCULevel { get; set; } = 0;
        public double SelfTrainLocation { get; set; } = 0;
        public double SelfTrainSpeed { get; set; } = 0;
        public TimeSpan CurrentTime { get; set; } = new TimeSpan();
        public bool AtStation { get; set; } = false;
        public bool StopAtPos { get; set; } = false;
        public bool RadioAvailable { get; set; } = false;

        private List<SignalPatch> SignalPatch = new List<SignalPatch>();
        private Train Train;
        private PreTrainPatch PreTrainPatch;
        private SectionManager sectionManager;
        private StationList staList;
        private bool TrainLoaded = false;
        private Station NextSta;


        public TGMTAts(PluginBuilder builder) : base(builder) {
            Plugins.AllPluginsLoaded += OnAllPluginsLoaded;
            BveHacker.ScenarioCreated += OnScenarioCreated;
        }

        private void OnAllPluginsLoaded(object sender, EventArgs e) {
            MA = OBCULevel = 0;
        }


        private void OnScenarioCreated(ScenarioCreatedEventArgs e) {
            sectionManager = e.Scenario.SectionManager;
            if (!e.Scenario.Trains.ContainsKey(Config.PretrainName)) {
                TrainLoaded = false;
                MessageBox.Show(string.Format("找不到名为 {0} 的列车，请检查Map以及插件设置", Config.PretrainName), "TGMT-CBTC-EX_WCU");
            } else {
                TrainLoaded = true;
                Train = e.Scenario.Trains[Config.PretrainName];
                PreTrainPatch = Extensions.GetExtension<IPreTrainPatchFactory>().Patch(nameof(PreTrainPatch), sectionManager, new PreTrainLocationConverter(Train, sectionManager));
            }

            int pointer = 0;
            while (pointer < sectionManager.Sections.Count - 1) {
                SignalPatch.Add(Extensions.GetExtension<ISignalPatchFactory>().Patch(nameof(SignalPatch), sectionManager.Sections[pointer] as Section, source =>
                (sectionManager.Sections[pointer].Location >= SelfTrainLocation && sectionManager.Sections[pointer].Location >= Config.TGMTTerrtoryStart && sectionManager.Sections[pointer].Location < Config.TGMTTerrtoryEnd)
                    ? (OBCULevel == 2 && RadioAvailable)
                    ? (int)Config.CTCSignalIndex : source : source));
                ++pointer;
            }

            NextSta = e.Scenario.Route.Stations[0] as Station;
        }

        public override void Dispose() {
            for (int i = 0; i < SignalPatch.Count; ++i) SignalPatch[i]?.Dispose();
            PreTrainPatch?.Dispose();
            Plugins.AllPluginsLoaded -= OnAllPluginsLoaded;
            BveHacker.ScenarioCreated -= OnScenarioCreated;
            TrainLoaded = false;
            MA = 0;
            OBCULevel = 0;
            SelfTrainLocation = 0;
            nextBlockLocation = 0;
            isTrainHold = AtStation = false;
            RadioAvailable = false;
            CurrentTime = new TimeSpan();

        }

        private class PreTrainLocationConverter : IPreTrainLocationConverter {
            private readonly Train SourceTrain;
            private readonly SectionManager SectionManager;

            public PreTrainLocationConverter(Train sourceTrain, SectionManager sectionManager) {
                SourceTrain = sourceTrain;
                SectionManager = sectionManager;
            }

            public PreTrainLocation Convert(PreTrainLocation source)
                => SourceTrain.TrainInfo.TrackKey == Config.PretrainTrackkey ? PreTrainLocation.FromLocation(SourceTrain.Location, SectionManager) : source;
        }

        public override TickResult Tick(TimeSpan elapsed) {

            int pointer = 0;
            while (sectionManager.Sections[pointer].Location < SelfTrainLocation)
                pointer++;
            if (pointer >= sectionManager.Sections.Count)
                pointer = sectionManager.Sections.Count - 1;

            var CurrentSection = sectionManager.Sections[pointer == 0 ? 0 : pointer - 1] as Section;
            var NextSection = sectionManager.Sections[pointer] as Section;

            if (AtStation) {
                if (StopAtPos) {
                    int pointer_ = 0;
                    while (BveHacker.Scenario.Route.Stations[pointer_].Location < SelfTrainLocation - 25)
                        pointer_++;
                    NextSta = BveHacker.Scenario.Route.Stations[pointer_] as Station;
                }
                if (CurrentTime.TotalMilliseconds < (NextSta.DepertureTimeMilliseconds - NextSta.StoppageTimeMilliseconds) && NextSta.SignalFlag == true)
                    isTrainHold = true;
                else isTrainHold = false;
            } else {
                int pointer_ = 0;
                while (BveHacker.Scenario.Route.Stations[pointer_].Location < SelfTrainLocation - 25)
                    pointer_++;
                NextSta = BveHacker.Scenario.Route.Stations[pointer_] as Station;
                isTrainHold = false;
            }


            nextBlockLocation = NextSection.Location;
            if (TrainLoaded) MA = Train.Location;

            return new MapPluginTickResult();
        }


    }
}
