using System;
using System.Reflection;

using BveTypes.ClassWrappers;
using AtsEx.Extensions.SignalPatch;
using AtsEx.PluginHost;
using AtsEx.PluginHost.Plugins;
using AtsEx.Extensions.PreTrainPatch;
using System.IO;

namespace TGMTAts.WCU {
    [PluginType(PluginType.MapPlugin)]
    public class PluginMain : AssemblyPluginBase {
        static PluginMain() {
            Config.Load(Path.Combine(Config.PluginDir, "TGMT_WCUConfig.txt"));
        }

        public static double TGMTTerrtoryStart = 0;
        public static double TGMTTerrtoryEnd = 100000000;

        public double MovementAuthority { get; set; } = 0;
        public int OBCULevel { get; set; } = 0;
        public double SelfTrainLocation { get; set; } = 0;

        private SignalPatch SignalPatch;
        private Train Train;
        private PreTrainPatch PreTrainPatch;
        private SectionManager sectionManager;


        public PluginMain(PluginBuilder builder) : base(builder) {
            Plugins.AllPluginsLoaded += OnAllPluginsLoaded;
            BveHacker.ScenarioCreated += OnScenarioCreated;
        }

        private void OnAllPluginsLoaded(object sender, EventArgs e) {
            MovementAuthority = OBCULevel = 0;
        }


        private void OnScenarioCreated(ScenarioCreatedEventArgs e) {
            if (!e.Scenario.Trains.ContainsKey(Config.PretrainName)) {
                throw new BveFileLoadException(string.Format("キーが {0} の他列車が見つかりませんでした。", Config.PretrainName), "TGMT-CBTC-EX_WCU");
            }

            Train = e.Scenario.Trains[Config.PretrainName];

            sectionManager = e.Scenario.SectionManager;
            PreTrainPatch = Extensions.GetExtension<IPreTrainPatchFactory>().Patch(nameof(PreTrainPatch), sectionManager, new PreTrainLocationConverter(Train, sectionManager));

        }

        public override void Dispose() {
            SignalPatch?.Dispose();
            PreTrainPatch?.Dispose();
            Plugins.AllPluginsLoaded -= OnAllPluginsLoaded;
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
            if(OBCULevel == 2) {
                MovementAuthority = Train.Location;
                for(int i = 0; i < sectionManager.Sections.Count; ++i) {
                    if (sectionManager.Sections[i].Location >= SelfTrainLocation && sectionManager.Sections[i].Location < Train.Location) {
                        SignalPatch = Extensions.GetExtension<ISignalPatchFactory>().Patch(nameof(SignalPatch), sectionManager.Sections[i] as Section, source => 255);
                    }
                } 
            } else {
                for (int i = 0; i < sectionManager.Sections.Count; ++i) {
                    if (sectionManager.Sections[i].Location >= SelfTrainLocation 
                        && sectionManager.Sections[i].Location < Train.Location
                        && sectionManager.Sections[i].Location >= TGMTTerrtoryStart 
                        && sectionManager.Sections[i].Location < TGMTTerrtoryEnd) { 
                        Section section = sectionManager.Sections[i] as Section;
                        int CurrentSignalIndex = section.CurrentSignalIndex;
                        SignalPatch = Extensions.GetExtension<ISignalPatchFactory>().Patch(nameof(SignalPatch), section, source => CurrentSignalIndex);
                    }
                }
            }

            return new MapPluginTickResult();
        }
    }
}
