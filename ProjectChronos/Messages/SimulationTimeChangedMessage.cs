using ProjectChronos.Models;

namespace ProjectChronos.Messages
{
    public sealed class SimulationTimeChangedMessage
    {
        public SimulationTimeChangedMessage(
            double newTime,
            SimulationEventMarker currentEvent,
            SimulationTimeChangeKind changeKind)
        {
            NewTime = newTime;
            CurrentEvent = currentEvent;
            ChangeKind = changeKind;
        }

        public double NewTime { get; }

        public SimulationEventMarker CurrentEvent { get; }

        public SimulationTimeChangeKind ChangeKind { get; }
    }
}
