# Replay Architecture Time Resolution Improvement

## Context

`SimulationReplayViewModel` is the replay time publisher. The view calls `Tick()` from
`CompositionTarget.Rendering`, and the view model calculates replay time from
`Stopwatch`, updates bound UI state, and publishes `SimulationTimeChangedMessage` to
chart/map receivers.

The replay time resolution used to be effectively fixed at `10ms`. After
`SetTimeResolution()` allowed much finer resolution such as `10us`, some receivers were
already throttled and others used cancellation/stale-work avoidance. Playback then began
to show starvation or frame-drop symptoms around the sender/receiver Dispatcher
boundary.

## Current Sender Shape

Current playback flow:

```text
CompositionTarget.Rendering
 -> SimulationReplayViewModel.Tick()
 -> Stopwatch-based nextTime calculation
 -> round by _timeResolution
 -> SetCurrentTimeInternal(..., asyncNotify: true)
 -> reserve/publish SimulationTimeChangedMessage only when notify is due or forced
 -> Dispatcher.BeginInvoke(CurrentTime PropertyChanged, DataBind)
 -> CurrentTimeDisplay PropertyChanged only at display cadence or forced state changes
```

Important details:

- `Tick()` frequency does not become `100,000Hz` when resolution becomes `10us`.
- `Tick()` remains tied to WPF rendering, usually around display refresh rate.
- The expensive path is entered whenever rounded `CurrentTime` changes.
- With finer resolution, more rendering ticks become distinct time updates.
- After the sender improvement pass, each async playback update can still create a UI
  Dispatcher `DataBind` update for `CurrentTime`, but notification `Task.Run` is created
  only when the throttled/forced message is actually publishable.
- `CurrentTimeDisplay` is no longer tied to every `CurrentTime` update during playback;
  it uses a lower display cadence and force updates for seek/pause/event-stop.
- `SetProperty(ref _currentTime, value)` was replaced by direct field assignment plus
  explicit property notifications, so the useless `SetCurrentTimeInternal`
  `PropertyChanged` noise is removed.
- In the non-auto-pause event path, one rendering tick can call the async update path
  twice: once for the event timestamp and once for `nextTime`.

So the issue is not a 1000x increase in direct tick count. The issue is that the sender
now spends more frames doing real update work, and it produces high-priority UI binding
work more continuously.

The original async notification path also had a correctness risk: `NotifyTimeChanged()`
could run on a thread-pool thread and touch view-model state such as notification
counters, `_lastNotifyUtc`, `HasInteracted`, and `CurrentEvents`. The improvement pass
now builds the `SimulationTimeChangedMessage` snapshot on the sender side first, then
uses the background task only for event invocation.

## Current Receiver Shape

Receivers such as charts/maps keep their own cached, indexed data and render the value
for the received replay time.

Receiver-side optimization exists in two related forms.

Some receiver designs use version/generation checks:

```text
Receive message
 -> increase version / generation
 -> schedule render through Dispatcher

Render callback
 -> if this callback's version is not latest, skip before occupying UI thread
 -> otherwise render chart/map
```

The checked-in chart examples under `ExternalDrafts/PlaybackRefactorDraft/OriginalSource`
also show a cancellation-token pattern:

```text
Receive message
 -> cancel previous render work
 -> create new CancellationTokenSource
 -> start background preparation
 -> schedule UI apply through Dispatcher
 -> skip/cancel if token is no longer current
```

Both approaches protect the UI thread from stale heavy rendering, but they can starve
under a continuous message stream:

```text
message 1 schedules render(1)
message 2 increments latest version
render(1) runs late and skips
message 3 increments latest version
render(2) runs late and skips
...
```

When receiver rendering is scheduled at `Background`, sender `DataBind` work can keep
winning. When receiver rendering is raised to `Render`, the receiver executes but then
competes with sender time UI updates and causes playback frame drops. The exact priority
differs by receiver implementation, so this document treats `Background` starvation and
`Render` competition as observed scheduling modes rather than a single fixed receiver
implementation.

## Diagnosis

This is a replay architecture cadence problem, not only a local performance problem.

The sender currently couples multiple cadences:

- internal replay clock
- slider/progress value
- text display
- receiver notification
- event detection/highlighting

The receivers currently couple message arrival to render task creation:

- every accepted message can create a render candidate
- stale render candidates are discarded at execution time
- under continuous updates, the latest render can fail to get an execution window

Dispatcher priority tuning alone creates a trade-off:

```text
Receiver Background priority:
  sender stays smooth, receiver render can starve

Receiver Render priority:
  receiver renders, sender time UI can drop frames
```

## Target Architecture

Use separate cadences for clock, presentation, notification, and heavy rendering.

```text
Internal replay clock:
  high precision, Stopwatch-based, supports 10us

Playback UI:
  slider/progress updates at frame cadence or pixel-significant cadence
  text display updates at a lower human-readable cadence
  pause/seek/event stop forces exact display update

Sender notification:
  publish only at receiver-supported cadence
  coalesce to latest replay time
  avoid per-frame Task.Run when notification will be throttled

Receiver rendering:
  store latest message/state
  keep at most one pending render callback
  when callback executes, render the latest state at that moment
```

## Sender Improvements

### 1. Do not create `Task.Run` per playback frame

Current async playback path creates a task before `NotifyTimeChanged()` performs
throttling.

Preferred direction:

```text
Tick
 -> update latest replay time
 -> check lightweight notification cadence
 -> only schedule/publish when notification is due or forced
```

This avoids creating thread-pool work that is likely to return immediately due to
throttling.

The replacement should also define one clear thread ownership rule:

```text
UI-bound view-model state:
  mutate only on UI thread

Notification throttling counters/timestamps:
  either keep on one thread or protect with synchronization

Receiver event invocation:
  document whether receivers are called from UI thread or background thread
```

### 2. Separate internal time from presentation time

Keep high precision internally, but do not force every presentation surface to update
at that precision.

Recommended conceptual split:

```text
_playbackTime:
  high-precision internal time

CurrentTime:
  UI slider/progress time

CurrentTimeDisplay:
  human-readable text

NotifyTime:
  coalesced receiver notification time
```

Event detection and seek logic should use the high-precision internal time. UI text and
receiver rendering should have their own cadence.

This split has correctness-sensitive edges. Seek, pause, stop, and event-stop must flush
all cadences immediately so the slider, exact text, event state, and receiver state do
not disagree at rest.

### 3. Split time text by semantic unit

Instead of one `CurrentTimeDisplay` string that changes as a whole, split display parts:

```text
CurrentTimeMainDisplay      -> "mm:ss"
CurrentTimeFractionDisplay  -> ".ff" / ".fff"
CurrentTimePrecisionDisplay -> remaining precision, mostly useful when paused
```

Recommended display policy:

```text
During playback:
  slider/progress: frame cadence
  mm:ss: only when second changes
  fraction: 50ms to 100ms cadence
  precision tail: hide, dim, or update slowly

On pause / seek / event stop:
  force exact display update at full configured precision
```

This keeps visible progress smooth while avoiding constant high-precision text churn.

This is a useful UI-cost optimization, but it is not the first fix for receiver
starvation. Apply it after receiver render coalescing and sender notification
coalescing unless profiling shows text layout is the dominant cost.

### 4. Coalesce sender UI updates

If Dispatcher scheduling remains necessary, keep at most one pending UI update:

```text
if uiUpdatePending is false:
    uiUpdatePending = true
    Dispatcher.BeginInvoke(() =>
    {
        uiUpdatePending = false
        publish latest display values
    })
```

This prevents the sender from piling up `DataBind` work during playback.

### 5. Review Dispatcher priority

Because `Tick()` is already called on the UI thread through rendering, avoid adding
high-priority Dispatcher work unless it is required. A continuous stream of `DataBind`
work can starve receiver `Background` rendering.

The slider contract must also follow replay resolution. `TickFrequency` should bind to
`TimeResolution` while `IsSnapToTickEnabled=True`, so thumb dragging and playback use
the same time quantum instead of leaving seek input fixed at the old `10ms` step.

Manual jump input should use the same contract. `JumpTargetTimeText`, step intervals,
`CurrentTime`, event grouping, event matching, and previous/next-event search should be
normalized or compared from `TimeResolution`, not from fixed decimal places such as
`F4`, fixed `100us` tolerances, or `Math.Round(timestamp, 3)`. With `10us`, an input
such as `20.13476` should remain `20.13476`; with `100us`, the same input is expected
to normalize to `20.1348`.

Replay-visible timestamp labels should also use the same precision. Event marker labels
and tooltips should bind to a resolution-formatted timestamp string, and numeric input
boxes should size from the configured precision instead of assuming two decimal places.

## Receiver Improvements

### 1. Replace per-message render candidates with latest-state rendering

Current shape:

```text
message N -> schedule render(N)
render(N) -> skip if N is stale
```

Preferred shape:

```text
message N:
  latestMessage = N
  if renderPending is false:
      renderPending = true
      schedule RenderLatest

RenderLatest:
  renderPending = false
  message = latestMessage
  render message
```

This means:

- many messages collapse into one pending render
- the callback always renders the latest available state
- stale work is avoided before it becomes a queue of abandoned callbacks
- receiver starvation is much less likely

### 2. Add receiver-side render cadence

Even with latest-state rendering, heavy chart/map rendering should have a maximum rate.

Example policy:

```text
Playback messages:
  render at 10Hz / 20Hz / 30Hz depending on chart cost

Seek / stopped / stopped-by-event:
  force immediate render
```

### 3. Keep stale-skip, but move it to scheduling/coalescing

The goal is still valid: stale data should not occupy UI thread. The stronger pattern is
to avoid scheduling many stale callbacks in the first place.

## Recommended Implementation Order

1. Instrument sender counts separately:
   - `Tick()` calls
   - `CurrentTime` changes
   - display updates
   - notification attempts
   - actual notification sends
   - `Task.Run` creations
   - Dispatcher UI update posts
   - Dispatcher post-to-run latency p50/p95/max

2. Update receivers to latest-state rendering with a single pending render callback.

3. Move sender notification throttle before `Task.Run`, or remove per-frame `Task.Run`
   in favor of coalesced publishing.

4. Verify thread ownership for `NotifyTimeChanged()` and `SimulationTimeChanged`
   receivers. UI-bound sender state should not be mutated from arbitrary thread-pool
   callbacks.

5. Split `CurrentTimeDisplay` or throttle text display updates while keeping slider
   progress smooth.

6. Re-test Dispatcher priorities after coalescing. Priority should become a fine-tuning
   choice, not the primary starvation control.

## Verification Checklist

Use the following counters to confirm the cause and measure each fix.

Sender metrics:

- `Tick/sec`
- `CurrentTime changed/sec`
- `Dispatcher DataBind post/sec`
- `CurrentTimeDisplay PropertyChanged/sec`
- `NotifyTimeChanged call/sec`
- `Notify sent/sec`
- `Notify throttled/sec`
- `Task.Run created/sec`
- UI Dispatcher post-to-run latency p50/p95/max

Receiver metrics:

- message received/sec
- render requested/sec
- render actually started/sec
- render completed/sec
- render skipped/canceled/sec
- latest-state overwrite count/sec
- render duration p50/p95/max
- Dispatcher post-to-run latency p50/p95/max

Test scenarios:

```text
Case A: 10ms, 1x, current receiver priority
  establish baseline

Case B: 10us, 1x, receiver Background
  reproduce receiver starvation and compare requested/completed/skipped counts

Case C: 10us, 1x, receiver Render
  confirm sender UI latency or frame-drop regression

Case D: 10us + receiver latest-state single pending render
  verify receiver keeps completing renders without flooding the Dispatcher

Case E: 10us + sender pre-throttle/coalesced notification
  verify Task.Run/sec, Notify call/sec, and Dispatcher latency decrease

Case F: seek / pause / event-stop
  verify all throttles are bypassed or flushed, and exact time/state is visible
```

## Session Validation Harness

During this session, a scratch sender-only harness was used to validate the design.
That harness was intentionally not committed with the production SW changes. The
committed value of this section is the validation shape and observed conclusions, not
an executable test entry point.

The scratch harness built the WPF app with Visual Studio MSBuild, ran
`SimulationReplayViewModel.Tick()` from an STA console process at a configured frame
rate, pumped the WPF Dispatcher, and printed sender/receiver metrics.

Validation scenarios covered:

```text
Baseline:
  compare 10ms and 10us at 1x, 60Hz

Low-speed stress:
  compare 10ms and 10us at 0.1x, 60Hz

Synthetic receiver:
  compare stale-skip, cancel-previous, and latest-state receiver scheduling
  under Background and Render dispatcher priorities
```

Receiver modes:

```text
None:
  sender-only baseline

StaleSkip:
  schedule one receiver render candidate per message and skip stale callbacks

CancelPrevious:
  same scheduling pressure as StaleSkip, but stale callbacks are counted as canceled

LatestState:
  coalesce messages while one receiver render callback is pending
```

Current observed behavior before sender improvements:

```text
1x, 60Hz:
  10ms and 10us are similar because both update near frame cadence.

0.1x, 60Hz:
  10ms produces far fewer real CurrentTime updates.
  10us turns almost every frame into a real update.
  Task.Run/DataBind production rises from roughly 9/s to roughly 52/s in the harness.
```

Observed after first sender improvement pass:

```text
Change:
  Notify throttle/reserve happens before Task.Run.
  Task.Run captures a prepared SimulationTimeChangedMessage payload.
  DataBind UI update posts are protected by a pending flag.

0.1x, 60Hz, 10us:
  Task.Run creation dropped from roughly 52/s to roughly 8/s.

0.1x, 60Hz, 10ms:
  Task.Run creation is roughly 5/s.

Heavy synthetic receiver render:
  large receiver UI work, for example 150ms, pushes Tick interval p95 near that render
  cost. This confirms the shared UI-thread contention model, but it is a synthetic
  Dispatcher pressure test, not a real chart/map visual benchmark.
```

The harness does not validate chart/map rendering. It is intentionally scoped to sender
production pressure and synthetic Dispatcher contention: `Tick`,
`SetCurrentTimeInternal`, `Task.Run`, `DataBind` posts, notification throttling,
messages, `PropertyChanged` counts, receiver skip/cancel/latest-state behavior, and
receiver Dispatcher latency.

Observed after final sender pass:

```text
Additional sender changes:
  CurrentTimeDisplay PropertyChanged is separated from CurrentTime cadence.
  Playback text display updates are capped at 100ms unless force-flushed.
  Seek / pause / event-stop force paths still bypass throttling.
  SetCurrentTimeInternal no longer emits a useless "SetCurrentTimeInternal"
  PropertyChanged notification through SetProperty.
  Sender UI Dispatcher post-to-run latency is measured.

0.1x, 60Hz, 10us, sender-only:
  taskRunCreatedCount ~= 8/s
  uiUpdatePostCount ~= 52/s
  uiDisplayUpdateCount ~= 8/s
  propertyChangedCurrentTime ~= 52/s
  propertyChangedCurrentTimeDisplay ~= 8/s
  propertyChangedSetCurrentTimeInternal = 0

Force scenarios:
  --force-scenario SeekPause emits Seek=1 and Stopped=1 messages.
  --force-scenario EventStop emits StoppedByEvent=1.
  --force-scenario EventPass exercises the one-Tick double SetCurrentTime path and
  records one coalesced UI update in the harness.

Heavy synthetic receiver render:
  ReceiverRenderMs=120 pushes Tick interval p95 near 120ms, confirming that heavy
  receiver UI work still blocks the shared UI thread. Sender-side coalescing reduces
  producer pressure, but it cannot make a long receiver render non-blocking.
```

## Key Principle

High-resolution replay time does not require high-resolution UI rendering.

The clock can be precise at `10us`, while visible presentation and heavy receivers run
at human- and device-appropriate cadences. The architecture should preserve exact time
for seek/event/data selection, but coalesce UI and receiver work to the latest state.
