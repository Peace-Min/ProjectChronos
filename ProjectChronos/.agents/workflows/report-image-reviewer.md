# Report Image Reviewer

## Purpose
- Review only the PNGs that were flagged as `hardFail` or `warning`.
- Classify each reviewed PNG into one of four categories:
  - `우측 잘림`
  - `카드 겹침`
  - `텍스트 과밀`
  - `시각적으로 수용 가능`
- Write `review.md` in the artifact root.

## Workspace
- `C:\Users\minph\source\repos\ProjectChronos\ProjectChronos`

## Inputs
- `%TEMP%\ProjectChronos\ReportExportStress\latest.txt`
- `%TEMP%\ProjectChronos\ReportExportStress\<latest>\summary.json`
- `%TEMP%\ProjectChronos\ReportExportStress\<latest>\agent_progress.json`
- `%TEMP%\ProjectChronos\ReportExportStress\<latest>\chunks\**\flagged\*.png`
- `%TEMP%\ProjectChronos\ReportExportStress\<latest>\chunks\**\warnings\*.png`

## Steps
1. Read `%TEMP%\ProjectChronos\ReportExportStress\latest.txt`.
2. Open `summary.json` in that artifact root.
3. Open `agent_progress.json` to confirm all chunks are completed.
4. Filter to cases where `hardFail == true` or `warning == true`.
5. Inspect only cases that have `generatedPngPath`.
6. Assign one category per case:
   - `우측 잘림`: right edge clipping or root overflow is visually dominant.
   - `카드 겹침`: detail cards overlap or collide.
   - `텍스트 과밀`: readable bounds are technically valid but the card text is too compressed.
   - `시각적으로 수용 가능`: geometry warning exists but the exported image is still usable.
7. Write `review.md` in the artifact root with:
   - artifact root path
   - minimum safe width confirmation or rejection reason
   - recommended width confirmation or override reason
   - case-by-case review table
   - final recommendation for minimum width and recommended width

## `review.md` Template
```markdown
# Report Image Review

- Artifact root: `<path>`
- Minimum safe width verdict: `<confirmed|rejected>`
- Recommended width verdict: `<confirmed|overridden>`
- Final minimum safe width: `<value>`
- Final recommended width: `<value>`

| Case ID | Severity | Category | Reason | PNG |
| --- | --- | --- | --- | --- |
| det_... | hard-fail | 카드 겹침 | detail card collision | C:\...png |
```
