---
name: report-break-review
description: Use when reviewing or implementing the analysis report timeline break symbol against a provided prototype image. Treat the break as an axis-break symbol, not a decorative curve, and use strict prototype-first visual acceptance.
---

# Report Break Review

Use this skill for the report export timeline when the user is judging whether the break symbol matches a prototype image.

## Non-negotiable meaning

The break is an `Axis Break Symbol`.
It is not:

- a timeline path curve
- a decorative flourish
- a marker icon
- a pin, diamond, chevron, or bracket

The intended reading is:

- two parallel vertical S-curve strokes
- inserted on the baseline
- indicating compressed or omitted time on the axis

## Acceptance source

Use the user-provided prototype image as the only acceptance reference.

For this project, the current canonical prototype reference is documented in:

- `C:\Users\CEO\source\repos\ProjectChronos\ProjectChronos\Docs\prototype_reference.md`

If that note says a local `prototype` PNG is deprecated, do not use that PNG for acceptance.

Do not treat these as acceptance:

- successful build
- successful PNG export
- "close enough"
- "same logic"
- "same intent"

## Hard review checklist

The result fails if any one item below fails.

1. `S readability`
The symbol must read as an S-curve at export scale, not as a tiny glyph or bracket.

2. `Double-stroke separation`
The two S strokes must read as two distinct parallel strokes, not as a merged icon.

3. `Axis-break meaning`
The symbol must read as an inserted axis-break sign, not as a marker attached to the rail.

4. `Baseline relationship`
The cutout must be tight enough that the baseline still reads continuously, but clear enough that the symbol is not swallowed by the line.

5. `Prototype likeness`
The overall break language must match the prototype's visual impression before calling it done.

## Workflow

1. Inspect the current exported image.
2. Compare only the break symbol against the prototype.
3. Write the remaining visual deltas in plain language.
4. If any delta remains, treat the result as failed.
5. Change geometry or immediate rendering only.
6. Re-export and re-check.

## Response rules

- Never say the work is done while any visual delta remains.
- Never use "directionally correct" as a pass.
- If the result is still wrong, say it is wrong directly.
- Prefer naming exactly what is wrong:
  `too merged`, `too thin`, `too decorative`, `not S enough`, `cutout too wide`, `cutout too narrow`, `reads like brackets`, `reads like marker`.

## Scope guardrail

When this skill is active, do not broaden the task into general spacing or layout cleanup unless the user explicitly asks for that too.
Primary focus is the break symbol's shape and reading.
