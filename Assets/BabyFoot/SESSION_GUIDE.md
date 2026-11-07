# Session flow

Project UI, identifiers and documentation are in English.

## Running a session

Enter Play, choose an anonymous participant code (e.g. P001), a difficulty and the trial counts, then select **Start practice session**. Use A/D or left/right arrows to move the goalkeeper. SPACE or **Start next trial** begins an individual trial. After practice, select **Begin measurement block**. Practice results are excluded from the measurement summary.

Default configuration: 5 practice trials and 20 measurement trials. Difficulty speeds: Easy 0.65 m/s, Standard 0.85 m/s and Fast 1.15 m/s. These are provisional pilot settings, not a validated experimental protocol.

Measurement targets are balanced (half left, half right), shuffled using the recorded seed. False starts consume a planned trial and are not replaced. This balances planned target directions, not necessarily the number of physically launched shots if false starts occur. Practice is separately randomized.

During a session, the legacy R shortcut is disabled to avoid discarding trials. **End session** records an active trial as aborted and writes an incomplete summary. Closing Play normally also marks an open session as interrupted. A crash may leave `status: in_progress`; completed rows have already been appended to disk.

## Files

`Application.persistentDataPath/Sessions/<participant>_<session>/`

- `practice.csv`: completed practice trials and any aborted practice trial.
- `measurement.csv`: measurement trials, with explicit result and stimulus-presented flag.
- `session.json`: unique ID, anonymous participant code, configuration, randomization seed, shot spawn, timing definition, status and completed counts.
- `summary.csv`: measurement-only outcomes, response count, mean and median response times. Available after completion or interruption.

Rows are written after every outcome. A blank response field means no post-shot threshold crossing was measured. Response time is the software interval between shot onset and goalkeeper displacement of at least 5 mm; device latency has not been calibrated. False starts are excluded from response-time statistics. No-response counts overlap goal/save outcomes; they are not an additional mutually exclusive result.

All paths and timestamps in records use UTC and decimal points. No participant names are requested. Existing standalone trial CSVs are unchanged.

## Validation

The editor check creates test data only under `Library/SessionChecks`. It verifies 150 balanced reproducible target schedules, validation of participant codes, duplicate-start rejection, separated practice/measurement records, measurement-only summaries and interrupted-session persistence. No Quest Pro validation has been performed yet.
