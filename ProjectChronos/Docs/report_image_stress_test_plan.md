# Report Image Stress Test Plan

## Goal
- Measure minimum safe width and recommended width for timeline report export.
- Record width-specific maximum event capacity.
- Keep long-running execution resumable and observable.

## Why Chunked Execution
- A single end-to-end run renders too many flagged PNGs before any final summary exists.
- When the session disconnects or process tracking is lost, the run looks stalled.
- Chunk execution leaves durable outputs after each segment and allows resume.
- Each chunk now writes `progress.json` and `progress.md` so liveness is visible before the chunk summary exists.

## Chunk Layout
- Deterministic chunks are split per width:
- `det-560`, `det-600`, `det-650`, `det-700`, `det-750`, `det-850`, `det-900`, `det-932`, `det-1000`, `det-1100`, `det-1200`, `det-1500`, `det-1900`
- Random chunks are split into two-seed windows:
- `rand-01-02`, `rand-03-04`, `rand-05-06`, `rand-07-08`, `rand-09-10`, `rand-11-12`, `rand-13-14`, `rand-15-16`, `rand-17-18`, `rand-19-20`

## Artifact Layout
- `agent-run-<timestamp>\summary.json`
- `agent-run-<timestamp>\summary.md`
- `agent-run-<timestamp>\agent_progress.json`
- `agent-run-<timestamp>\agent_progress.md`
- `agent-run-<timestamp>\chunks\<chunk-id>\summary.json`
- `agent-run-<timestamp>\chunks\<chunk-id>\progress.json`
- `agent-run-<timestamp>\chunks\<chunk-id>\progress.md`
- `agent-run-<timestamp>\chunks\<chunk-id>\flagged\`
- `agent-run-<timestamp>\chunks\<chunk-id>\warnings\`

## Execution Flow
1. Run each chunk independently with `run_stress_chunk.ps1`.
2. While a chunk is running, persist `progress.json` and `progress.md` on every case.
3. After each chunk, persist `summary.json` and `run_result.txt`.
4. Update `agent_progress.json` and `agent_progress.md` while polling the active chunk progress.
5. Merge all chunk case results into a final summary when all chunks are complete.
6. Review only the final summary's flagged/warning cases.

## Acceptance
- Every chunk leaves a durable summary.
- Every running chunk leaves a durable progress heartbeat.
- The agent can resume by skipping chunks that already have `summary.json`.
- The final root summary is produced only after all chunks are complete.
- `latest.txt` points to the merged artifact root.
