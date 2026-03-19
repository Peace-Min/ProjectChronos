using System;
using ProjectChronos.Core;
using ProjectChronos.Services;

namespace ProjectChronos.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly TimelineReportDefinitionService _timelineReportDefinitionService =
            new TimelineReportDefinitionService();

        public MainWindowViewModel()
        {
            SimulationReplayViewModel.Initialize(
                TimelineReportDefinitionService.DefaultScenarioDurationSeconds,
                _timelineReportDefinitionService.CreateScenarioEvents());
        }

        public SimulationReplayViewModel SimulationReplayViewModel { get; } = new SimulationReplayViewModel();
    }
}
