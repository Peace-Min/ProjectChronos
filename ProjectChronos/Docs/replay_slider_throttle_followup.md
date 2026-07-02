# Replay Slider Throttle Follow-up

## Base Commit

- Base commit: `0939dfa626d0c36550c55a909e77037fe1bc8374`
- Commit summary: time resolution expansion

This document summarizes the discussion after the base commit: replay time resolution,
sender UI thread pressure, Slider Thumb update separation, and the planned test harness.

## State At The Base Commit

The base commit already includes these display consistency fixes:

- `CurrentTimeDisplay` and `TotalTimeDisplay` no longer use
  `TimeSpan.FromSeconds(double)` directly.
  - Purpose: prevent `215.3797` from being displayed as `03:35.3800`.
  - Result: internal time and sent messages keep `215.3797`, and the UI displays
    `03:35.3797`.
- Event marker labels and tooltips use `SimulationEventMarker.DisplayTimestamp`.
  - Purpose: force display precision from the active `TimeResolution`.
  - Example: if the source timestamp is `0.1` and `TimeResolution = 0.0001`,
    the UI displays `0.1000s`.
  - This is a display string, not the raw DB/source value.
- Report timeline interval labels use the report `TimeResolution` format instead
  of fixed `0.##`.
  - Example: `204.3900 -> 215.3797` is displayed as `10.9897`, not `10.99`.

## Current Structure

`SimulationReplayViewModel.CurrentTime` currently has multiple responsibilities:

```text
CurrentTime
 -> internal replay time
 -> Slider.Value / Thumb position
 -> CurrentTimeDisplay source value
 -> receiver message and event matching source value
```

The notification cadence is partially separated:

```text
CurrentTime PropertyChanged
 -> updates Slider Thumb
 -> can occur near frame cadence during high-resolution playback

CurrentTimeDisplay PropertyChanged
 -> updates the visible time text
 -> throttled to 100 ms during playback

SimulationTimeChangedMessage
 -> sent to receivers
 -> throttled to 100 ms during playback
```

Therefore the visible time text is already limited to about 10 Hz, but the Slider
Thumb still follows `CurrentTime` updates and can be updated much more often when
`TimeResolution` is small.

## Difference From The Existing Architecture MD

`Docs/replay_architecture_time_resolution_improvement.md` proposes:

- Keep the internal replay clock high precision.
- Separate UI presentation cadence from internal time resolution.
- Split the visible time into `mm:ss`, fraction, and precision tail.
- Hide, dim, or slowly update the precision tail during playback.
- Force full precision on seek, pause, event-stop, and other rest states.

Current implementation status:

- Implemented:
  - `CurrentTimeDisplay` is throttled to 100 ms during playback.
  - receiver messages are throttled to 100 ms during playback.
  - seek/pause/event-stop paths force immediate display/message updates.
- Not implemented:
  - digit-level display throttling.
  - separate `CurrentTimeMainDisplay`, `CurrentTimeFractionDisplay`,
    `CurrentTimePrecisionDisplay`.
  - separation of Slider Thumb presentation from internal `CurrentTime`.

## Current Assessment

For low-end PCs and for receivers that should still render at
`DispatcherPriority.Background`, the sender should not create more UI thread work just
because `TimeResolution` is smaller.

The Slider Thumb is a pixel-based presentation surface:

```text
TimeResolution = calculation / event / seek accuracy
Slider Thumb   = visual position on screen
```

The Thumb does not need to update for every `10us`, `100us`, or `1us` time change.
Most of those changes are inside the same visual pixel and do not create a meaningful
user-visible difference.

## Recommended Next Implementation

Keep the high-resolution time logic as-is, and separate Slider Thumb presentation.

Recommended structure:

```text
CurrentTime
  internal high-precision time
  receiver message / event matching / seek source

SliderDisplayTime
  bound to Slider.Value
  throttled by fixed presentation cadence first
  later extendable to pixel-significant throttling

CurrentTimeDisplay
  visible time text
  keep current text cadence unless measured otherwise
```

Playback flow:

```text
Tick
 -> update internal CurrentTime at high precision
 -> publish receiver message through existing throttle
 -> update CurrentTimeDisplay through existing text throttle
 -> update SliderDisplayTime only when the Thumb throttle condition allows it
```

User seek flow:

```text
User starts Slider Thumb drag or track seek
 -> IsPlaying = false
 -> bypass Slider throttle
 -> seek CurrentTime immediately from SliderDisplayTime
 -> update CurrentTimeDisplay immediately
 -> send receiver message with forceNotify
```

Stopping playback when the user starts dragging is consistent with existing manual
actions such as `Step`, `JumpToTime`, and `StepEvent`.

## Side Effects To Watch

- Changing Slider binding from `CurrentTime` to `SliderDisplayTime` changes the seek
  path and must be tested carefully.
- Playback Tick must not overwrite the user-controlled Thumb value while the user is
  dragging.
- Drag and seek paths must bypass playback throttling.
- Seek, pause, event-stop, and end-of-playback must flush all presentation values:
  `CurrentTime`, `SliderDisplayTime`, `CurrentTimeDisplay`, and receiver message state.
- `IsSnapToTickEnabled=True` and `TickFrequency=TimeResolution` must be checked with
  the new `SliderDisplayTime` binding.
- During playback, `SliderDisplayTime` may intentionally lag behind `CurrentTime` as
  presentation lag. Receiver messages must continue to use internal `CurrentTime`.

## Test Harness Plan

The test plan should combine a ViewModel-level sender harness with a WPF Dispatcher
synthetic receiver.

### 1. Sender Metrics

Record these counters per scenario:

```text
Tick/sec
CurrentTime changed/sec
SliderDisplayTime changed/sec
CurrentTimeDisplay changed/sec
Dispatcher UI update post/sec
SimulationTimeChangedMessage sent/sec
SimulationTimeChangedMessage throttled/sec
Dispatcher post-to-run latency p50/p95/max
```

Comparison axes:

```text
TimeResolution: 10ms / 100us / 10us / 1us
PlaybackSpeed: 0.1x / 1x
Slider throttle: off / fixed cadence / pixel-significant
```

Success criteria:

```text
SliderDisplayTime changed/sec does not explode when TimeResolution becomes 10us or 1us.
CurrentTime remains high precision.
Seek/pause/event-stop flush presentation state exactly.
```

### 2. Synthetic Receiver

Reproduce receiver rendering pressure through WPF Dispatcher:

```text
Receiver priority: Background / Normal
Receiver mode: latest-state single pending render
Synthetic render cost: 0ms / 30ms / 60ms / 120ms
```

Record:

```text
message received/sec
render requested/sec
render started/sec
render completed/sec
render skipped/coalesced/sec
receiver Dispatcher latency p50/p95/max
```

Success criteria:

```text
Background-priority render completed/sec does not fall to zero.
Receivers can render latest state periodically without raising priority to Normal.
Sender DataBind pressure decreases compared with baseline.
```

### 3. Slider User Interaction

Test these cases:

```text
Playback + Thumb drag start -> IsPlaying = false
Drag updates SliderDisplayTime immediately
Tick does not overwrite SliderDisplayTime during drag
Drag end aligns CurrentTime, SliderDisplayTime, and CurrentTimeDisplay
Track click / page move / keyboard move are treated as seek input
```

Automation coverage:

- ViewModel state and method tests.
- WPF Dispatcher pump based binding smoke tests.
- If UI Automation is not reliable for WPF Slider drag, use a code-behind or behavior
  event simulation harness.

Manual confirmation still needed:

- Real low-end PC frame-drop perception.
- Slider Thumb drag feel.
- Final UX with the real heavy chart/map receivers.

## Step-by-step Work Order

1. Add measurement harness.
2. Measure current baseline.
3. Add `SliderDisplayTime` and move Slider binding to it.
4. Apply fixed-cadence Slider throttle during playback.
5. Bypass throttle for drag/seek.
6. Verify flush behavior for seek, pause, event-stop, and end-of-playback.
7. Compare synthetic Background receiver behavior before/after.
8. If fixed cadence is not enough, pass usable Slider pixel width from View to
   ViewModel and extend to pixel-significant throttling.

## Current Conclusion

- The most likely remaining sender-side UI pressure is Slider Thumb / `CurrentTime`
  binding updates, not visible time text.
- Thumb presentation should be tied to visual cadence or pixel movement, not directly
  to `TimeResolution`.
- The next safest implementation step is to separate `SliderDisplayTime` from
  internal `CurrentTime`.
- Digit-level time text throttling remains a later optimization candidate, not the
  primary fix for the current receiver starvation problem.
