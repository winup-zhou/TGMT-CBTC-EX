using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using BveTypes.ClassWrappers;
using AtsEx.Extensions.SignalPatch;
using AtsEx.PluginHost;
using AtsEx.PluginHost.Plugins;
using AtsEx.Extensions.PreTrainPatch;

namespace TGMTAts.WCU {
    [PluginType(PluginType.MapPlugin)]
    public class PluginMain : AssemblyPluginBase {
        public double MovementAuthority { get; set; } = 0;
        public int WaySideStatus { get; set; } = 0;
        private SignalPatch SignalPatch;
        private Train Train;
        private PreTrainPatch PreTrainPatch;
        public PluginMain(PluginBuilder builder) : base(builder) {
            Plugins.AllPluginsLoaded += OnAllPluginsLoaded;
            BveHacker.ScenarioCreated += OnScenarioCreated;
        }

        private void OnAllPluginsLoaded(object sender, EventArgs e) {
            MovementAuthority = WaySideStatus = 0;
        }


        private void OnScenarioCreated(ScenarioCreatedEventArgs e) {
            if (!e.Scenario.Trains.ContainsKey("pretrain")) {
                throw new BveFileLoadException("キーが 'PreTrain' の他列車が見つかりませんでした。", "TGMT-CBTC-EX_WCU");
            }

            Train = e.Scenario.Trains["pretrain"];

            SectionManager sectionManager = e.Scenario.SectionManager;
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
                => SourceTrain.TrainInfo.TrackKey == "0" ? PreTrainLocation.FromLocation(SourceTrain.Location, SectionManager) : source;
        }

        public override TickResult Tick(TimeSpan elapsed) {

            MovementAuthority = Train.Location;

            return new MapPluginTickResult();
        }
    }
}
