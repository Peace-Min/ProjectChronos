# Replay Receiver Frontier Handoff

This document follows the `frontier-handoff` shape from `Peace-Min/peace-skillbank`.
Its purpose is to give a stronger model a self-contained receiver-side handoff and ask
for an answer that a weak offline local LLM can apply in small steps.

Paste the block below into the frontier model.

```markdown
## Goal
Improve replay receiver rendering in a WPF simulation replay architecture so chart/map receivers stay responsive when replay time resolution is expanded from the old fixed `10ms` assumption to high precision such as `10us`.

The sender already coalesces UI time display updates and throttles outgoing replay time messages. The remaining work is receiver-side architecture: receivers should keep the latest replay state, avoid stale UI work, and render at a bounded cadence without starving the shared WPF UI thread.

## Problem
The observed failure mode was:

> When replay time resolution was expanded from 10ms to 10us, receivers using `DispatcherPriority.Background` sometimes never rendered. Each received message incremented an internal version/invalidation counter, and the queued Background render saw itself as stale before it could run, so it kept skipping forever.

Another observed failure mode was:

> Raising receiver dispatcher priority to Render made receiver rendering happen again, but then sender current-time UI updates and overall replay UI started dropping frames because sender and receiver were competing on the same UI thread.

Expected behavior:

- During `Playback`, receivers should render the latest replay time only at an intentional UI cadence, not once per incoming message.
- During terminal or user-driven changes (`Seek`, `Stopped`, `StoppedByEvent`), receivers should force one final exact render.
- Stale intermediate messages should be cheap to discard, but the latest state must not be starved indefinitely.
- Heavy chart/map UI work must remain bounded because WPF sender and receivers share the same UI thread.

## What I already tried
Sender-side work is already done in the current branch:

- `SimulationReplayViewModel` normalizes replay time from `TimeResolution`.
- Slider `TickFrequency` is bound to `TimeResolution`.
- Manual jump and step input are normalized from `TimeResolution`.
- Replay sender throttles `SimulationTimeChangedMessage` creation to `PlaybackRenderIntervalMs = 100` unless forced.
- Playback path uses async receiver notification only after the sender decides a message is due.
- Sender UI `CurrentTimeDisplay` is throttled while `CurrentTime`/slider progress remains smooth.

Receiver-side ideas discussed:

- Do not treat every message as a render candidate.
- Keep a single `latestMessage` / `latestTime` state.
- Schedule at most one UI render loop at a time.
- Render the latest state when the dispatcher gets time.
- If newer messages arrive while rendering, loop once more or schedule another render; do not queue N dispatcher operations.
- Use cancellation for background data extraction, but avoid a pattern where every message cancels and reschedules UI work forever.
- Use `DispatcherPriority.Background` or `ContextIdle` only if latest-state coalescing prevents starvation; use `Render` only for short UI cursor updates, not heavy chart rebuilds.

## Relevant code
`Messages/SimulationTimeChangedMessage.cs`
```csharp
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
```

`Messages/SimulationTimeChangeKind.cs`
```csharp
namespace ProjectChronos.Messages
{
    public enum SimulationTimeChangeKind
    {
        Playback,
        Stopped,
        StoppedByEvent,
        Seek
    }
}
```

Current sender contract in `ViewModels/SimulationReplayViewModel.cs`:

```csharp
private void PublishTimeChangedAsyncIfDue(double newTime, SimulationTimeChangeKind changeKind, bool forceNotify)
{
    var message = CreateTimeChangedMessageIfDue(newTime, changeKind, forceNotify);
    if (message == null)
    {
        return;
    }

    Task.Run(() => SimulationTimeChanged?.Invoke(message));
}

private void PublishTimeChangedIfDue(double newTime, SimulationTimeChangeKind changeKind, bool forceNotify)
{
    var message = CreateTimeChangedMessageIfDue(newTime, changeKind, forceNotify);
    if (message == null)
    {
        return;
    }

    SimulationTimeChanged?.Invoke(message);
}

private SimulationTimeChangedMessage CreateTimeChangedMessageIfDue(double newTime, SimulationTimeChangeKind changeKind, bool forceNotify)
{
    if (!_isRealtimeRenderingEnabled && !forceNotify) return null;

    bool shouldNotify = forceNotify || (DateTime.UtcNow - _lastNotifyUtc) >= TimeSpan.FromMilliseconds(PlaybackRenderIntervalMs);

    if (!shouldNotify)
    {
        return null;
    }

    _lastNotifyUtc = DateTime.UtcNow;

    if (!IsPlaying || forceNotify)
    {
        double eventMatchEpsilon = GetEventMatchEpsilon();
        var matchedGroup = _sortedGroups?
            .FirstOrDefault(g => Math.Abs(g.Timestamp - newTime) <= eventMatchEpsilon);

        CurrentEvents = matchedGroup?.Events;
    }

    if (!_isInitialize)
    {
        return null;
    }

    if (!HasInteracted && (changeKind == SimulationTimeChangeKind.Seek || changeKind == SimulationTimeChangeKind.Playback))
    {
        HasInteracted = true;
    }

    var currentEvent = CurrentEvents?.FirstOrDefault();
    return new SimulationTimeChangedMessage(newTime, currentEvent, changeKind);
}
```

Current receiver shape from `ExternalDrafts/PlaybackRefactorDraft/OriginalSource/SingleSimUserAnalyChartViewModel.cs`:

```csharp
private void ReceiveSimulationTimeChangedMessage(SimulationTimeChangedMessage message)
{
    if (_chartQueryRequests == null) { return; }

    if (message.ChangeKind == SimulationTimeChangeKind.Playback)
    {
        var now = DateTime.UtcNow;

        if (now - _lastPlaybackRenderAt < _playbackRenderInterval)
        {
            return;
        }

        _lastPlaybackRenderAt = now;
    }
    else
    {
        _lastPlaybackRenderAt = DateTime.UtcNow;
    }

    _renderCts?.Cancel();
    _renderCts = new CancellationTokenSource();

    var token = _renderCts.Token;
    var receiveTime = message.NewTime;

    Task.Run(async () =>
    {
        try
        {
            if (token.IsCancellationRequested) { return; }

            var filteredSeriesPoints = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                var filteredSeriesPointBuffer = new Dictionary<int, List<ChartPoint3D>>();
                foreach (var kvp in _seriesPointBuffer)
                {
                    var seriesIndex = kvp.Key;
                    var points = kvp.Value;
                    var filteredPoints = points.Where(p => p.Time == receiveTime).ToList();

                    filteredSeriesPointBuffer.Add(seriesIndex, filteredPoints);
                }

                return filteredSeriesPointBuffer;
            }, token);

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (token.IsCancellationRequested) { return; }

                foreach (var kvp in filteredSeriesPoints)
                {
                    var seriesIndex = kvp.Key;
                    var points = kvp.Value;

                    if (_is3DViewer)
                    {
                        if (ChartControl is IChartPlayback chartPlayback)
                        {
                            var seriesPoints3D = ChartPointMapper.ToSeriesPoints3D(points);
                            chartPlayback.UpdatePlaybackCursorPosition(new AddSeriesPointDTO(seriesIndex, null, seriesPoints3D));
                        }
                    }
                    else
                    {
                        if (ChartControl is IChartPlayback chartPlayback)
                        {
                            var seriesPoints2D = ChartPointMapper.ToSeriesPoints2D(points);
                            chartPlayback.UpdatePlaybackCursorPosition(new AddSeriesPointDTO(seriesIndex, seriesPoints2D, null));
                        }
                    }
                }
            }, DispatcherPriority.Render, token);
        }
        catch (OperationCanceledException) { }
    });
}
```

Recommended receiver structure to validate/refine:

```csharp
private readonly object _replayRenderGate = new object();
private SimulationTimeChangedMessage _latestReplayMessage;
private bool _renderLoopScheduled;
private bool _renderLoopRunning;
private DateTime _lastPlaybackRenderAt = DateTime.MinValue;
private readonly TimeSpan _playbackRenderInterval = TimeSpan.FromMilliseconds(16);
private CancellationTokenSource _prepareCts;

private void ReceiveSimulationTimeChangedMessage(SimulationTimeChangedMessage message)
{
    if (_chartQueryRequests == null)
    {
        return;
    }

    bool forceRender = message.ChangeKind != SimulationTimeChangeKind.Playback;

    lock (_replayRenderGate)
    {
        _latestReplayMessage = message;

        if (!forceRender && DateTime.UtcNow - _lastPlaybackRenderAt < _playbackRenderInterval)
        {
            return;
        }

        if (_renderLoopScheduled || _renderLoopRunning)
        {
            return;
        }

        _renderLoopScheduled = true;
    }

    Application.Current.Dispatcher.BeginInvoke(
        new Action(RenderLatestReplayMessageAsync),
        DispatcherPriority.Background);
}

private async void RenderLatestReplayMessageAsync()
{
    SimulationTimeChangedMessage message;

    lock (_replayRenderGate)
    {
        _renderLoopScheduled = false;
        _renderLoopRunning = true;
        message = _latestReplayMessage;
        _lastPlaybackRenderAt = DateTime.UtcNow;
    }

    try
    {
        while (message != null)
        {
            var prepared = await PreparePlaybackFrameAsync(message);

            lock (_replayRenderGate)
            {
                if (!ReferenceEquals(message, _latestReplayMessage))
                {
                    message = _latestReplayMessage;
                    continue;
                }
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ApplyPlaybackFrame(prepared);
            }, DispatcherPriority.Render);

            lock (_replayRenderGate)
            {
                if (ReferenceEquals(message, _latestReplayMessage))
                {
                    _renderLoopRunning = false;
                    return;
                }

                message = _latestReplayMessage;
                _lastPlaybackRenderAt = DateTime.UtcNow;
            }
        }
    }
    catch (OperationCanceledException)
    {
        lock (_replayRenderGate)
        {
            _renderLoopRunning = false;
        }
    }
    catch
    {
        lock (_replayRenderGate)
        {
            _renderLoopRunning = false;
        }
        throw;
    }
}

private async Task<PreparedPlaybackFrame> PreparePlaybackFrameAsync(SimulationTimeChangedMessage message)
{
    var cts = new CancellationTokenSource();
    var previous = Interlocked.Exchange(ref _prepareCts, cts);
    previous?.Cancel();
    previous?.Dispose();

    var token = cts.Token;
    double receiveTime = message.NewTime;

    return await Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();

        var filteredSeriesPointBuffer = new Dictionary<int, List<ChartPoint3D>>();
        foreach (var kvp in _seriesPointBuffer)
        {
            token.ThrowIfCancellationRequested();

            var seriesIndex = kvp.Key;
            var points = kvp.Value;
            var filteredPoints = points.Where(p => p.Time == receiveTime).ToList();
            filteredSeriesPointBuffer.Add(seriesIndex, filteredPoints);
        }

        return new PreparedPlaybackFrame(message, filteredSeriesPointBuffer);
    }, token);
}

private void ApplyPlaybackFrame(PreparedPlaybackFrame frame)
{
    foreach (var kvp in frame.SeriesPoints)
    {
        var seriesIndex = kvp.Key;
        var points = kvp.Value;

        if (_is3DViewer)
        {
            if (ChartControl is IChartPlayback chartPlayback)
            {
                var seriesPoints3D = ChartPointMapper.ToSeriesPoints3D(points);
                chartPlayback.UpdatePlaybackCursorPosition(new AddSeriesPointDTO(seriesIndex, null, seriesPoints3D));
            }
        }
        else
        {
            if (ChartControl is IChartPlayback chartPlayback)
            {
                var seriesPoints2D = ChartPointMapper.ToSeriesPoints2D(points);
                chartPlayback.UpdatePlaybackCursorPosition(new AddSeriesPointDTO(seriesIndex, seriesPoints2D, null));
            }
        }
    }
}

private sealed class PreparedPlaybackFrame
{
    public PreparedPlaybackFrame(
        SimulationTimeChangedMessage message,
        Dictionary<int, List<ChartPoint3D>> seriesPoints)
    {
        Message = message;
        SeriesPoints = seriesPoints;
    }

    public SimulationTimeChangedMessage Message { get; }
    public Dictionary<int, List<ChartPoint3D>> SeriesPoints { get; }
}
```

Important design constraints:

- Do not enqueue one dispatcher render per message.
- Do not keep a stale-skip version counter that can starve every Background render.
- Do not do DB/cache lookup on the UI thread.
- `Seek`, `Stopped`, and `StoppedByEvent` must force a render even if playback throttling would skip.
- UI application should be short and bounded; if chart/map API work is heavy, wrap bulk changes in the chart API's own BeginUpdate/EndUpdate equivalent if available.
- The data lookup should use indexed/cached structures. The example uses `Where(p => p.Time == receiveTime)` because that is the current draft shape, but the target should prefer exact dictionary lookup or nearest-time lookup keyed by normalized replay time.

## Environment & constraints
C# WPF on .NET Framework 4.7.2. The current repo is ProjectChronos. The receiver examples are draft source files under `ExternalDrafts/PlaybackRefactorDraft/OriginalSource`, and the real SW may have the same structure under different file names.

The local implementer may be a weak/offline LLM with no internet. Do not assume new NuGet packages, Reactive Extensions, TPL Dataflow, or external schedulers. Use only standard .NET Framework/WPF APIs and existing project patterns.

The chart/map control is UI-thread-affine. `SimulationTimeChangedMessage` may arrive from a background thread during playback because the sender dispatches async receiver notification after throttle. Any receiver state shared between the message callback, background prepare task, and UI dispatcher must be protected.

The exact chart library version for the real SW is not stated. Avoid version-specific chart APIs unless they already exist in the supplied code, such as `IChartPlayback.UpdatePlaybackCursorPosition`, `ChartPointMapper.ToSeriesPoints2D`, `ChartPointMapper.ToSeriesPoints3D`, and `AddSeriesPointDTO`.

## Ask
Review the receiver-side architecture above and return a concrete, offline-applyable implementation plan for converting existing replay receivers from cancel-every-message rendering to latest-state coalesced rendering, including complete replacement code for `ReceiveSimulationTimeChangedMessage` and the helper methods/classes it needs.

## How to answer (the implementer is a weak offline model)
Your answer will be applied by a WEAK local LLM with no internet, not by me directly. So:
- Open with ONE recommended approach -- decide for me; do not just list options. If you compare
  alternatives, pick a winner and give the reason in one line.
- Then give a numbered plan of SMALL, independently-applyable steps. For each step: the exact
  `file:line` to touch, the precise code to add or replace (a COMPLETE snippet -- never "adjust X"
  or "handle the case"), and a one-line check that it worked.
- Be EXPLICIT over clever: spell out exact names, values, and conditions. Avoid any step that needs
  judgment a weak offline model cannot make.
- Assume an OFFLINE / air-gapped environment: no internet, no new packages or tools, and stay within
  the language / framework / library versions stated above.
```
