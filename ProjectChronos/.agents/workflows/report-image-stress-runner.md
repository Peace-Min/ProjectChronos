# Report Image Stress Runner

## Purpose
- Run the report image stress suite through the chunked test agent.
- Persist partial chunk results even if one chunk fails or the session disconnects.
- Produce final `summary.json`, `summary.md`, chunk-level summaries, and agent progress files.
- Return the final merged exit code without translation.

## Workspace
- `C:\Users\minph\source\repos\ProjectChronos\ProjectChronos`

## Command
```powershell
& 'C:\Users\minph\source\repos\ProjectChronos\ProjectChronos\Diagnostics\run_stress_agent.ps1'
```

## Steps
1. Build `Release` if `bin\Release\ProjectChronos.exe` is missing or stale.
2. Run `Diagnostics\run_stress_agent.ps1`.
3. Wait for the agent script to exit.
4. Read `%TEMP%\ProjectChronos\ReportExportStress\latest.txt` to resolve the latest merged artifact root.
5. Verify that the artifact root contains:
   - `summary.json`
   - `summary.md`
   - `agent_progress.json`
   - `agent_progress.md`
   - `chunks\`
6. Verify that every chunk under `chunks\` has its own `progress.json` while running and `summary.json` when completed.
7. Surface the latest artifact root, exit code, `minimumSafeWidth`, `recommendedWidth`, and the counts of hard failures and warnings.
8. If the agent exits with `1`, report the run as hard-fail.
9. If the agent exits with `2`, report the run as warning-only.
10. If the agent exits with `0`, report the run as clean.

## Expected Outputs
- `%TEMP%\ProjectChronos\ReportExportStress\agent-run-<yyyyMMdd-HHmmss>\summary.json`
- `%TEMP%\ProjectChronos\ReportExportStress\agent-run-<yyyyMMdd-HHmmss>\summary.md`
- `%TEMP%\ProjectChronos\ReportExportStress\agent-run-<yyyyMMdd-HHmmss>\agent_progress.json`
- `%TEMP%\ProjectChronos\ReportExportStress\agent-run-<yyyyMMdd-HHmmss>\agent_progress.md`
- `%TEMP%\ProjectChronos\ReportExportStress\agent-run-<yyyyMMdd-HHmmss>\chunks\<chunk-id>\summary.json`
- `%TEMP%\ProjectChronos\ReportExportStress\agent-run-<yyyyMMdd-HHmmss>\chunks\<chunk-id>\progress.json`
