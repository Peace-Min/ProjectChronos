# Timeline Prototype Handoff (2026-03-24)

## Scope

This document summarizes the prototype-only work done on 2026-03-24 for timeline interval visualization.

Important:

- This work was **PNG/HTML prototype exploration only**.
- No WPF production code was intentionally changed in this pass.
- The user explicitly narrowed the scope to prototype images after code-level feasibility discussion started.

## Repository / Runtime Notes

- Repo root: `C:\Users\82104\source\repos\ProjectChronos\ProjectChronos`
- Current prototype outputs live mostly under `output/playwright/`
- Playwright browser runtime was installed during this work via:
  - `npx playwright install chromium`

## Core Data Assumption

The working event timestamps used in the discussions and prototypes were:

- `200.64`
- `213.13`
- `213.14`
- `221.71`

Derived intervals:

- `12.49`
- `0.01`
- `8.57`

Total span:

- `21.07s`

Critical math used to justify the design problem:

- `0.01 / 21.07 = 0.047%`
- At `912px` axis width, `0.01s ~= 0.43px`
- At `1205px` nearly-full-width axis, `0.01s ~= 0.57px`
- At `1920px` axis, `0.01s ~= 0.91px`

Conclusion:

- The micro interval visibility problem is fundamentally mathematical, not just a prototype mistake.
- A strict single linear scale that makes `0.01s` visually comfortable quickly becomes impractically wide.

## Important Context From Code Inspection

Even though the user later narrowed scope to PNG prototypes, the current WPF export implementation was inspected.

Relevant finding:

- `ViewModels/ReportTimelineExportViewModel.cs` currently spaces slot groups by **equal slot pitch**, not by true timestamp distance.
- See `BuildLayout(...)`, especially the `slotPitch` and `group.CenterX = BaselineStartX + (slotPitch * group.Index)` logic.

Implication:

- Existing WPF export is **not yet a truthful time axis**.
- Prototype work done here should not be mistaken for a direct preview of current production output logic.

## User Feedback That Changed Direction

The user rejected an earlier second prototype because it drifted away from the hand sketch and introduced unnecessary card-style layout.

The correct reference sketch is:

- `C:\Users\82104\Desktop\KakaoTalk_20260324_211849861.jpg`

Important correction:

- The file was initially assumed to be `.png`, but the actual file is `.jpg`.

The hand sketch emphasized:

- One main baseline
- Large intervals shown on the same upper orange lane
- A very short middle interval called out clearly near the center
- Bottom timestamps stacked only where needed
- Minimal structure, no elaborate card layout

## Prototype Sequence

### 1. Truthful ultrawide prototype

Goal:

- Show what happens if strict proportionality is preserved and the axis is widened until `0.01s` becomes visually meaningful.

Files:

- `output/playwright/timeline_truthful_ultrawide.html`
- `output/playwright/timeline_truthful_ultrawide.png`

Characteristics:

- Axis width was expanded to make `0.01s ~= 4px`
- This resulted in a very large image
- Useful for explaining the mathematical limit

### 2. Truthful + helper callout prototype

Goal:

- Keep the main rail truthful at a practical width while making the micro interval readable.

Files:

- `output/playwright/timeline_truthful_microgap.html`
- `output/playwright/timeline_truthful_microgap.png`

Status:

- This prototype was later considered directionally wrong for presentation because it did not align with the hand sketch closely enough.

### 3. Sketch-aligned strict axis-only prototype

Goal:

- Remove extra UI structure and follow the hand sketch more closely.

Files:

- `output/playwright/timeline_truthful_axis_only_sketch.html`
- `output/playwright/timeline_truthful_axis_only_sketch.png`

Characteristics:

- One baseline
- Upper interval arrows
- Bottom timestamps
- Still based on the very wide truthful-axis idea

### 4. Sketch-aligned micro-gap helper prototype

Goal:

- Follow the hand sketch structure while showing a helper treatment for the middle micro interval.

Files:

- `output/playwright/timeline_truthful_microgap_sketch.html`
- `output/playwright/timeline_truthful_microgap_sketch.png`

Characteristics:

- Upper interval arrows
- Middle micro-gap visual treatment
- Bottom stacked timestamps

### 5. Minimum-gap redistribution prototype

Goal:

- Use a fixed output width, force a minimum visible width for the shortest interval, and proportionally take the excess width from surrounding large intervals.

Files:

- `output/playwright/timeline_min_gap_redistributed_sketch.html`
- `output/playwright/timeline_min_gap_redistributed_sketch.png`

Logic:

- Fixed axis width
- `0.01s` forced to a minimum visible width
- Extra width removed proportionally from `12.49s` and `8.57s`

Interpretation:

- This is no longer a perfectly proportional truthful axis
- It is a display-scaled axis with a minimum visible interval width

### 6. Inward-arrow micro-interval prototype

Goal:

- Reduce distortion further by **not** reserving full label-fit width for the micro interval.
- Keep only the minimum cursor separation, and render the short interval as compact notation:
  - `-> <- (0.01)`

Files:

- `output/playwright/timeline_micro_interval_inward_arrows.html`
- `output/playwright/timeline_micro_interval_inward_arrows.png`

This is the **latest direction** reached in the conversation.

## Latest Agreed Direction

At the end of the conversation, the user proposed this refinement and accepted the logic behind it:

- Do **not** force enough width to fit the full short-interval label inside the micro span
- Instead, for very short intervals:
  - keep only a minimum visible cursor separation
  - render the upper orange lane as compact inward-arrow notation
  - example: `-> <- (n.xx)`
- Keep bottom stacking only for the overlapping timestamps

This direction is important because it reduces distortion compared with a label-fit minimum width.

### Latest rule set in plain terms

- Large intervals remain on the same top orange lane
- Extremely short intervals switch to compact notation instead of consuming full label-fit span
- Minimum forced width should be tied to **cursor separation only**, not to text-fit width
- Bottom timestamps can stack vertically when x positions are too close

## Prototype / Rule Distinctions

The following distinctions were repeatedly discussed and should remain explicit:

### A. Strict truthful axis

- One global linear scale
- No local widening
- Honest, but micro intervals become nearly invisible unless the image is extremely wide

### B. Truthful axis + helper layer

- Main axis stays truthful
- Micro interval readability is improved by helper ticks / leader lines / callouts
- Better for explanation than for unified visual simplicity

### C. Minimum-gap redistribution axis

- Not strictly proportional anymore
- A minimum visible width is assigned to the shortest interval
- Neighboring larger intervals absorb the difference
- Practical for presentation

### D. Minimum cursor gap + compact short-interval notation

- Also not strictly proportional
- Causes less distortion than text-fit minimum width
- This became the most promising presentation direction during the latest exchange

## Existing Earlier Prototype Files Outside `output/playwright`

The following artifacts were already present and are relevant to the same line of exploration:

- `prototype_actual_interval_review.html`
- `output/prototype_actual_interval_review.png`
- `output/current_branch_prototype.png`
- `output/current_branch_realdata.png`

These should be treated as earlier prototype artifacts from the same overall problem space.

## Files Modified / Added During This Work

Prototype outputs created in `output/playwright/`:

- `timeline_truthful_ultrawide.html`
- `timeline_truthful_ultrawide.png`
- `timeline_truthful_microgap.html`
- `timeline_truthful_microgap.png`
- `timeline_truthful_axis_only_sketch.html`
- `timeline_truthful_axis_only_sketch.png`
- `timeline_truthful_microgap_sketch.html`
- `timeline_truthful_microgap_sketch.png`
- `timeline_min_gap_redistributed_sketch.html`
- `timeline_min_gap_redistributed_sketch.png`
- `timeline_micro_interval_inward_arrows.html`
- `timeline_micro_interval_inward_arrows.png`

This handoff document:

- `Docs/UI_Stacking/prototype_handoff_2026-03-24.md`

## Intentionally Not Part Of This Work

The following items were present in the working tree but were **not** part of the requested prototype documentation effort:

- `ui_capture.png`
- `ui_tree_dump.json`

Treat them as unrelated dirty-worktree items unless the user explicitly asks otherwise.

## Recommended Continuation Point

If another agent continues from here, the safest next step is:

1. Use `output/playwright/timeline_micro_interval_inward_arrows.png` as the current leading direction.
2. Confirm whether the minimum cursor gap should be tuned (for example `10px`, `12px`, `14px`).
3. Confirm whether the compact short-interval notation should remain:
   - `-> <- (0.01)`
   - or be centered differently while preserving the same idea.
4. Only after visual agreement, decide whether to:
   - keep the result as a reporting-only prototype convention
   - or translate the chosen rule into WPF production code

## Regeneration Commands

If another agent needs to regenerate the latest PNG prototype:

```powershell
npx playwright screenshot --browser chromium --viewport-size "1920,1040" --full-page --wait-for-timeout 500 "file:///C:/Users/82104/source/repos/ProjectChronos/ProjectChronos/output/playwright/timeline_micro_interval_inward_arrows.html" "C:/Users/82104/source/repos/ProjectChronos/ProjectChronos/output/playwright/timeline_micro_interval_inward_arrows.png"
```

If Playwright browsers are missing:

```powershell
npx playwright install chromium
```

## Summary

The conversation converged on this practical understanding:

- Perfect proportionality makes `0.01s` almost invisible at normal report widths.
- The user accepts that complete proportionality will likely be broken for presentation purposes.
- The most promising compromise is:
  - minimum visible cursor separation for the shortest interval
  - compact upper-lane notation for the short interval
  - bottom stacking only where timestamps collide

At the end of this handoff, the latest prototype matching that direction is:

- `output/playwright/timeline_micro_interval_inward_arrows.png`
