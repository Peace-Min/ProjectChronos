# Report Break Prototype Review

## Purpose
- Compare the current exported report image against the user-provided prototype image.
- Review only the timeline break symbol.
- Use the local skill `report-break-review` as the acceptance rubric.
- Reject any result that still differs in break language from the prototype.

## Inputs
- Canonical prototype reference:
  - `C:\Users\CEO\source\repos\ProjectChronos\ProjectChronos\Docs\prototype_reference.md`
- Prototype image from the active Codex thread, using the canonical reference note above as the source of truth
- Current export PNG:
  - `C:\Users\CEO\source\repos\ProjectChronos\ProjectChronos\output\report_timeline_realdata_compare.png`
- Review rubric:
  - `C:\Users\CEO\source\repos\ProjectChronos\ProjectChronos\.agents\skills\report-break-review\SKILL.md`

## Review Checklist
The image fails if any one item fails:

1. `S readability`
2. `Double-stroke separation`
3. `Axis-break meaning`
4. `Baseline relationship`
5. `Prototype likeness`

## Steps
1. Open the local skill rubric.
2. Open the canonical prototype reference note.
3. Inspect the user-provided prototype image in the active thread that matches that note.
4. Inspect the current export PNG.
5. Compare only the break symbol, not the rest of the layout.
6. Write remaining deltas in plain language.
7. If any delta remains, mark the result as `FAIL`.
8. Only when all five checklist items pass, mark the result as `PASS`.

## Output Format
Write a short review note with:
- verdict: `PASS` or `FAIL`
- failed checklist items
- visual delta notes
- next geometry/rendering-only action
