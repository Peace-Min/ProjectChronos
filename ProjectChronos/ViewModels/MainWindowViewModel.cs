using ProjectChronos.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjectChronos.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public SimulationReplayViewModel SimulationReplayViewModel { get; } = new SimulationReplayViewModel();

        public MainWindowViewModel()
        {
            // Example initialization
            var exampleEvents = new List<Models.SimulationEventMarker>
            {
                new Models.SimulationEventMarker(10, Models.EventPriority.High, "Start Event"),
                new Models.SimulationEventMarker(30, Models.EventPriority.Medium, "Mid Event"),
                new Models.SimulationEventMarker(50, Models.EventPriority.Low, "End Event"),
            };
            SimulationReplayViewModel.Initialize(380.0, exampleEvents);
        }
    }
}
