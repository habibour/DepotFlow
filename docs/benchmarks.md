# Report performance: benchmark results

Every number in this document was produced by `dotnet run --project src/DepotFlow.Seeder -- --bench` against the data set described below. Nothing is estimated. The tables are generated from the harness output; the raw output of the "before" and the final run is at the end.

## 1. Setup

- Client machine: macOS 26.5.1, Arm64, 10 logical CPUs, 16 GB RAM
- SQL Server (as seen from inside its container): Microsoft SQL Server 2022 (RTM-CU14) (KB5038325) - 16.0.4135.4 (X64); 10 CPUs and 6.2 GB visible; database compatibility level 160
- Database: DepotFlowBench; data as of 2026-10-01
- Visits: 1,500,710 rows
- Invoices: 1,499,510 rows
- Containers: 348,837 rows
- ShippingLines: 25 rows
- YardSlots: 1,600 rows
- Data: `--visits 1500000 --seed 42 --reset`, loaded in 50 seconds; about 36 months of history, 1,200 visits still in the yard, 86.4% of invoices paid. `InvoiceLines` and `AuditLogs` are not seeded (bulk loading bypasses the audit interceptor), so the benchmark touches `Visits`, `Invoices`, `YardSlots`, `Containers` and `ShippingLines` only.
- Docker: Docker Desktop on an Apple M4 laptop, 8 GB memory limit, 10 CPUs; SQL Server 2022 is an amd64 image, so it runs under emulation on this arm64 machine. Absolute times are therefore pessimistic compared with a native x64 server; the ratios between versions are what carry over.
- Server settings: MAXDOP 0, cost threshold for parallelism 5. `Visits` was 650 MB and `Invoices` 422 MB including all indexes (measured before the optimization indexes were added), so the whole data set fits in the buffer cache: these are warm-cache measurements.

## 2. Method

For every report and parameter set, both versions of the stored procedure are run: `usp_X_Baseline` (the straightforward query, written the way someone would write it first, never changed) and `usp_X` (the version the API calls, changed by the optimizations below).

1. One run, discarded: it warms the buffer cache and the plan cache.
2. Ten timed runs on one connection; wall-clock milliseconds around `ExecuteReader` and reading every row. Median and 95th percentile (nearest rank, so with ten runs the p95 is the slowest run).
3. One run with `SET STATISTICS IO ON`: logical reads of the largest real table the procedure touches (worktables are excluded).
4. The same run with `SET STATISTICS XML ON`: the main operators of the actual execution plan, summarised in words.
5. A SHA-256 of all result rows from the baseline and the current procedure is compared; they must be identical, and every row below says whether they were.

Parameter sets are relative to the data's "as of" date, 2026-10-01: *last 30 days* is 30 local days ending on that date, *last 12 months* is 2025-10-01 to 2026-10-01 (366 days), *all 36 months* is 2023-10-01 to 2026-10-01. "One line" is shipping line 1, the busiest.

**"Before"** below is the *first* harness run, taken before any optimization: baseline procedures on the Day 2 schema. **"After"** is the current procedures on the final schema, from the final harness run; the last column gives the range seen across the two complete final runs, because this laptop was busy with other work and timings moved (section 6).

## 3. Results

Median and p95 are milliseconds. Reads are logical reads on the largest table the procedure touches. The target from the specification was a median under 1,000 ms for every row.

### Daily gate movements

| Parameter set | Before median | Before p95 | After median | After p95 | Speed-up | Reads before -> after | After, two runs | Same rows |
|---|---:|---:|---:|---:|---:|---|---:|:---:|
| last 30 days, all lines | 138 | 164 | 112 | 125 | 1.2x | Visits 66,722 -> Visits 13,421 | 112-152 | yes |
| last 12 months, all lines | 221 | 244 | 88 | 90 | 2.5x | Visits 66,722 -> Visits 9,386 | 88-116 | yes |
| last 12 months, one line | 158 | 254 | 81 | 87 | 2.0x | Visits 66,722 -> Visits 2,647 | 81-82 | yes |

*Before:* Both date filters wrapped the column in `CAST(DATEADD(HOUR, 6, col) AS date)`, so no range could be sought: a clustered index scan of all of `Visits` (66,722 pages) plus a scan of `IX_Visits_ShippingLine_GateIn`, joined with hash and merge joins.

*After:* Gate-outs are a range seek on the filtered covering index `IX_Visits_Released_GateOut_Covering`; gate-ins scan the narrow `IX_Visits_Status_GateIn` index (13,421 pages for 30 days, 9,386 for 12 months; the date range cannot be sought there because Status leads); the one-line case seeks `IX_Visits_ShippingLine_GateIn` (2,647 pages).

### Yard occupancy

| Parameter set | Before median | Before p95 | After median | After p95 | Speed-up | Reads before -> after | After, two runs | Same rows |
|---|---:|---:|---:|---:|---:|---|---:|:---:|
| no parameters | 10 | 12 | 10 | 11 | 1.0x | Visits 14,044 -> Visits 14,044 | 10-10 | yes |

*Before:* Scan of `YardSlots`, then for each slot a seek on `UX_Visits_Slot_Active` and clustered key lookups for the container size (14,044 pages on `Visits`).

*After:* Unchanged. At 10 ms there was nothing worth changing; denormalizing `SizeFeet` onto `Visits` was considered and rejected (see below).

### Average dwell time

| Parameter set | Before median | Before p95 | After median | After p95 | Speed-up | Reads before -> after | After, two runs | Same rows |
|---|---:|---:|---:|---:|---:|---|---:|:---:|
| last 30 days | 143 | 160 | 53 | 57 | 2.7x | Visits 59,020 -> Visits 210 | 53-55 | yes |
| last 12 months | 874 | 1063 | 74 | 85 | 11.8x | Visits 59,020 -> Visits 2,265 | 74-87 | yes |
| all 36 months | 2456 | 2635 | 267 | 363 | 9.2x | Visits 59,020 -> Visits 6,447 | 194-267 | yes |

*Before:* Row-mode plan: clustered index scan of `Visits` (59,020 pages) feeding four `Table Spool` operators for the `PERCENTILE_CONT` window, which read a worktable 6,542,567 times. The optimizer estimated 51,729 rows at the scan; 1,499,510 came back.

*After:* Seek on `IX_Visits_Released_GateOut_Covering` (210 pages for 30 days, 2,265 for 12 months, 6,447 for 36 months), batch mode, counting visits per (line, dwell days) first so the median is read from at most about 1,500 aggregated rows instead of a sort of every visit.

### Revenue by shipping line

| Parameter set | Before median | Before p95 | After median | After p95 | Speed-up | Reads before -> after | After, two runs | Same rows |
|---|---:|---:|---:|---:|---:|---|---:|:---:|
| last 30 days | 148 | 152 | 64 | 89 | 2.3x | Invoices 65,884 -> Invoices 446 | 48-64 | yes |
| last 12 months | 273 | 294 | 63 | 76 | 4.3x | Invoices 65,884 -> Invoices 4,804 | 50-63 | yes |
| all 36 months | 501 | 547 | 104 | 109 | 4.8x | Invoices 65,884 -> Invoices 13,658 | 98-104 | yes |

*Before:* Clustered index scan of `Invoices` (65,884 pages), hash aggregate.

*After:* Seek on the covering index `IX_Invoices_IssuedAt_Covering` (446 pages for 30 days, 4,804 for 12 months, 13,658 for 36 months); `OPTION (RECOMPILE)` gives the optimizer the real dates, so it chooses a parallel scan of the index for the wide range and a seek for the narrow one.

Every parameter set is under the 1,000 ms target, with the slowest "after" median at about 270 ms (dwell time over 36 months). Five of the ten sets now read less than a tenth of the pages they read before (the 30-day and 12-month dwell and revenue sets, and the one-line daily set); the 36-month sets read 11% to 21% of them; yard occupancy was left alone.

## 4. Change log

Each change was its own migration and commit, measured with the harness before it was kept. Medians are milliseconds, before -> after the change, on the same data. Results were identical to the baseline each time.

1. **Sargable date filters** (`c6ac978`). The baseline filters wrapped every row's timestamp in `CAST(DATEADD(HOUR, 6, col) AS date) BETWEEN ...`. The range is now converted to UTC bounds once and the raw column is compared. Dwell over 36 months 2,527 -> 292; over 12 months 869 -> 140; revenue over 36 months 508 -> 134; daily over 12 months 228 -> 104. Logical reads did not change (still full scans), so the gain was not from reading less. The plans show why: the function-wrapped predicate left the optimizer estimating 51,729 rows for the dwell scan (1,499,510 returned) and it chose a row-mode plan with four `Table Spool` operators; with the plain range it estimated 246,530 and chose a batch-mode `Window Aggregate`. I did not isolate whether the better estimate or batch-mode eligibility decided it; the better estimate is the likely cause.
2. **Covering index on `Invoices(IssuedAtUtc)`** (`218c3cf`). `IX_Invoices_IssuedAt_Covering` includes the line, total and status. Revenue over 30 days 83 -> 9 (reads 65,884 -> 446); over 12 months 101 -> 84; over 36 months 134 -> **260**, slower despite 5x fewer reads. The plan had gone serial (degree of parallelism 1, cost 3.38, below the parallelism threshold of 5) because the bounds sit in local variables: the optimizer cannot see their values when it compiles, so it guesses a fixed 16.4% of the table (246,395 of 1,499,510 rows). Kept because it is a large win for narrow ranges and still far under target; the cause is fixed by the next change.
3. **`OPTION (RECOMPILE)` on the three date-range procedures** (`424f104`). The statement is compiled with the real dates, so the estimate is accurate (499,563 estimated against 499,360 actual in a test statement) and the plan fits the range. Revenue over 36 months 260 -> 95; over 12 months 84 -> 43. The cost is real: revenue over 30 days went 9 -> 43 ms, because every call now pays for a compile, and daily and dwell sets with no index yet were 10 to 49 ms slower. Kept: consistent behaviour beats a plan cached for whichever range happened to be asked first (see section 5).
4. **Filtered covering index on released visits** (`52b7a64`). `IX_Visits_Released_GateOut_Covering` on `(GateOutAtUtc) INCLUDE (GateInAtUtc, ShippingLineId) WHERE Status = 2`; the daily procedure now states `Status = 2` on its gate-out count so the filtered index qualifies (a released visit always has both a status and a gate-out time: only `Visit.GateOut` sets either). Logical reads on `Visits`: dwell 30 days 59,020 -> 222, 12 months -> 2,265, 36 months -> 6,447; daily gate-outs now seek. Median dwell 12 months 156 -> 123, 36 months 335 -> 299. Small ranges settle at a floor of roughly 50 to 110 ms, which is the recompile and fixed costs, not reads.
5. **Dwell-time median from per-day counts** (`4507b63`). `PERCENTILE_CONT` has to sort every released visit per line. Dwell days are small whole numbers (1 to 60), so the procedure counts visits per (line, dwell days) and reads the median off the cumulative counts: the mean of the values at ranks (N+1)/2 and N/2+1 (integer division), which is what `PERCENTILE_CONT(0.5)` returns. Dwell 30 days 83 -> 50, 12 months 123 -> 80, 36 months 299 -> 146 in the run right after the change; the hand-computed test dataset (an odd count with median 2.0, even counts with median 3.5) and the row-hash comparison on 1.5 million visits both agree with the baseline.

## 5. What did not help

These attempts were measured and reverted; none is in the final schema.

**Writing the bounds inline instead of recompiling.** To avoid the per-call compile cost, I replaced the local variables with expressions over the parameters so the optimizer could use the sniffed values and cache one plan. Compared with the recompiled version it was meant to replace, dwell over 36 months went 335 -> **3,547** ms, over 12 months 156 -> 885; revenue over 36 months 95 -> 1,011, over 12 months 43 -> 347; daily over 12 months 114 -> 195: classic parameter sniffing, the plan compiled for the first (30-day) call was reused for the wide ranges. Results stayed correct, which is why the hash check cannot catch this class of problem and only timing does.

**A covering index on `Visits(GateInAtUtc)` for the daily gate-in count.** Reads fell (30 days 13,421 -> 6,431) but the median rose 110 -> 206 ms for 30 days and 92 -> 206 for 12 months, reproduced in a second run. The actual plan estimated 50 rows for each seek and received about 500,000, so it chose a serial plan that sorts 499,360 rows into a stream aggregate. Bisecting the statement found the cause: the aggregate alone is estimated correctly (499,563), but joining it to `GENERATE_SERIES` (which the optimizer assumes yields about 50 rows) collapses every estimate in the statement. The index only exposed an existing weakness; it was reverted.

**Rewriting the daily procedure around temp tables, then adding that index and a `MAXDOP 1` hint.** Computing the two aggregates into `#Ins` and `#Outs` first fixed the estimates (504,387 vs 499,360, parallel plan, 5 to 6 ms compile). Median ms for 30 days / 12 months all lines / 12 months one line: 110 / 92 / 80 before; temp tables alone 71 / 234 / 84; with the gate-in index 35 / 196 / 84; with `MAXDOP 1` added 35 / 148 / 79. So it is 3x faster for 30 days and 1.6x slower for 12 months, with three extra moving parts and an extra index written on every gate-in, the system's core transaction. Every set is already far under 1,000 ms, so I stopped and reverted all of it. A parallel plan with accurate estimates was slower than a serial batch-mode plan with bad ones on this emulated machine; plan quality and speed are different things and only the clock decides.

**Denormalizing `SizeFeet` onto `Visits` for yard occupancy.** Considered and not done. The report runs in 10 ms; a copy of the container size on every visit would be one more column to keep consistent for no measurable gain.

## 6. Caveats

- One machine, one data set (seed 42), one as-of date. Different data shapes can give different plans; the *shape* of the findings (sargability, estimation, covering indexes) is general, the milliseconds are not.
- Docker Desktop on a laptop, SQL Server under x86 emulation, and the laptop was running other work during these runs (load average around 7 on 10 cores). Repeat runs of the same code varied by roughly +/-25%: dwell over 36 months, current procedure, measured 146, 267, 228, 208, 215 and 194 ms across six runs, and the baseline 2.5 to 3.5 s. The speed-up column uses the first final run; the "two runs" column shows the spread.
- Warm cache only: the data set fits in memory, so cold-read behaviour is not measured. Single client: concurrent load is not measured.
- The `_Baseline` procedures in the final schema are not the "before": they can use the new indexes too, which is why the "before" figures come from the run made before any change.
- `OPTION (RECOMPILE)` trades a fixed compile cost per call for plans that fit the parameters. That suits reports that run for tens of milliseconds; it would not suit a hot transactional query.
- Writes got slightly more expensive: `Visits` has two more indexes than after Day 2 (the filtered one only for released rows) and `Invoices` has one more. Write cost was not benchmarked.

## 7. Reproduce

```bash
docker compose up -d db
dotnet run --project src/DepotFlow.Seeder -- --visits 1500000 --seed 42 --reset --as-of 2026-10-01
dotnet run --project src/DepotFlow.Seeder -- --bench --as-of 2026-10-01
```

The seeder uses a separate database (`DepotFlowBench`) and refuses to reset anything else. `--bench --only dwell` runs one report. The optimizations are migrations, so a freshly seeded database that has had `dotnet ef database update` applied is in the "after" state; to measure "before", update only to `AddReportProcedures`.

## Appendix: raw harness output

<details><summary>Before any optimization (first run)</summary>

```text
## Setup

- Client machine: macOS 26.5.1, Arm64, 10 logical CPUs, 16 GB RAM
- SQL Server (as seen from inside its container): Microsoft SQL Server 2022 (RTM-CU14) (KB5038325) - 16.0.4135.4 (X64); 10 CPUs and 6.2 GB visible; database compatibility level 160
- Database: DepotFlowBench; data as of 2026-10-01
- Visits: 1,500,710 rows
- Invoices: 1,499,510 rows
- Containers: 348,837 rows
- ShippingLines: 25 rows
- YardSlots: 1,600 rows
- Method: per procedure and parameter set, 1 discarded run, then 10 timed runs on one connection (median and p95 of wall-clock milliseconds); one extra run with STATISTICS IO and STATISTICS XML for logical reads and the plan; a hash of the result rows compares baseline and current.

### Daily gate movements

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| last 30 days, all lines | 138 | 164 | 136 | 143 | Visits 66,722 -> Visits 66,722 | 30 | yes |
| last 12 months, all lines | 221 | 244 | 228 | 232 | Visits 66,722 -> Visits 66,722 | 366 | yes |
| last 12 months, one line | 158 | 254 | 120 | 157 | Visits 66,722 -> Visits 66,722 | 366 | yes |

Plans (main operators of the actual execution plan):
- last 30 days, all lines: baseline plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn); current plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn)
- last 12 months, all lines: baseline plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn); current plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn)
- last 12 months, one line: baseline plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn); current plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn)

### Yard occupancy

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| no parameters | 10 | 12 | 10 | 11 | Visits 14,044 -> Visits 14,044 | 5 | yes |

Plans (main operators of the actual execution plan):
- no parameters: baseline plan = Index Scan (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (Visits.UX_Visits_Slot_Active), Clustered Index Seek (Visits.PK_Visits), Clustered Index Seek (Containers.PK_Containers); current plan = Index Scan (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (Visits.UX_Visits_Slot_Active), Clustered Index Seek (Visits.PK_Visits), Clustered Index Seek (Containers.PK_Containers)

### Average dwell time

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| last 30 days | 143 | 160 | 144 | 146 | Visits 59,020 -> Visits 59,020 | 25 | yes |
| last 12 months | 874 | 1063 | 869 | 904 | Visits 59,020 -> Visits 59,020 | 25 | yes |
| all 36 months | 2456 | 2635 | 2527 | 2978 | Visits 59,020 -> Visits 59,020 | 25 | yes |

Plans (main operators of the actual execution plan):
- last 30 days: baseline plan = Table Spool, Clustered Index Scan (Visits.PK_Visits), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Table Spool, Clustered Index Scan (Visits.PK_Visits), Clustered Index Seek (ShippingLines.PK_ShippingLines)
- last 12 months: baseline plan = Table Spool, Clustered Index Scan (Visits.PK_Visits), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Table Spool, Clustered Index Scan (Visits.PK_Visits), Clustered Index Seek (ShippingLines.PK_ShippingLines)
- all 36 months: baseline plan = Table Spool, Clustered Index Scan (Visits.PK_Visits), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Table Spool, Clustered Index Scan (Visits.PK_Visits), Clustered Index Seek (ShippingLines.PK_ShippingLines)

### Revenue by shipping line

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| last 30 days | 148 | 152 | 164 | 192 | Invoices 65,884 -> Invoices 65,884 | 26 | yes |
| last 12 months | 273 | 294 | 275 | 281 | Invoices 65,884 -> Invoices 65,884 | 26 | yes |
| all 36 months | 501 | 547 | 508 | 583 | Invoices 65,884 -> Invoices 65,884 | 26 | yes |

Plans (main operators of the actual execution plan):
- last 30 days: baseline plan = Hash Match, Clustered Index Scan (Invoices.PK_Invoices), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Clustered Index Scan (Invoices.PK_Invoices), Clustered Index Seek (ShippingLines.PK_ShippingLines)
- last 12 months: baseline plan = Hash Match, Clustered Index Scan (Invoices.PK_Invoices), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Clustered Index Scan (Invoices.PK_Invoices), Clustered Index Seek (ShippingLines.PK_ShippingLines)
- all 36 months: baseline plan = Hash Match, Clustered Index Scan (Invoices.PK_Invoices), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Clustered Index Scan (Invoices.PK_Invoices), Clustered Index Seek (ShippingLines.PK_ShippingLines)

All baseline and current procedures returned identical results.
```
</details>

<details><summary>Final schema (first of two final runs)</summary>

```text
## Setup

- Client machine: macOS 26.5.1, Arm64, 10 logical CPUs, 16 GB RAM
- SQL Server (as seen from inside its container): Microsoft SQL Server 2022 (RTM-CU14) (KB5038325) - 16.0.4135.4 (X64); 10 CPUs and 6.2 GB visible; database compatibility level 160
- Database: DepotFlowBench; data as of 2026-10-01
- Visits: 1,500,710 rows
- Invoices: 1,499,510 rows
- Containers: 348,837 rows
- ShippingLines: 25 rows
- YardSlots: 1,600 rows
- Method: per procedure and parameter set, 1 discarded run, then 10 timed runs on one connection (median and p95 of wall-clock milliseconds); one extra run with STATISTICS IO and STATISTICS XML for logical reads and the plan; a hash of the result rows compares baseline and current.

### Daily gate movements

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| last 30 days, all lines | 146 | 184 | 112 | 125 | Visits 66,722 -> Visits 13,421 | 30 | yes |
| last 12 months, all lines | 231 | 296 | 88 | 90 | Visits 66,722 -> Visits 9,386 | 366 | yes |
| last 12 months, one line | 125 | 130 | 81 | 87 | Visits 66,722 -> Visits 2,647 | 366 | yes |

Plans (main operators of the actual execution plan):
- last 30 days, all lines: baseline plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn); current plan = Merge Join, Hash Match, Index Scan (Visits.IX_Visits_Status_GateIn), Index Seek (Visits.IX_Visits_Released_GateOut_Covering)
- last 12 months, all lines: baseline plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn); current plan = Hash Match, Index Scan (Visits.IX_Visits_Status_GateIn), Index Seek (Visits.IX_Visits_Released_GateOut_Covering)
- last 12 months, one line: baseline plan = Merge Join, Hash Match, Clustered Index Scan (Visits.PK_Visits), Index Scan (Visits.IX_Visits_ShippingLine_GateIn); current plan = Hash Match, Index Seek (Visits.IX_Visits_ShippingLine_GateIn), Index Seek (Visits.IX_Visits_Released_GateOut_Covering)

### Yard occupancy

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| no parameters | 11 | 12 | 10 | 11 | Visits 14,044 -> Visits 14,044 | 5 | yes |

Plans (main operators of the actual execution plan):
- no parameters: baseline plan = Index Scan (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (Visits.UX_Visits_Slot_Active), Clustered Index Seek (Visits.PK_Visits), Clustered Index Seek (Containers.PK_Containers); current plan = Index Scan (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (YardSlots.IX_YardSlots_Block_Row_Bay_Tier), Index Seek (Visits.UX_Visits_Slot_Active), Clustered Index Seek (Visits.PK_Visits), Clustered Index Seek (Containers.PK_Containers)

### Average dwell time

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| last 30 days | 125 | 138 | 53 | 57 | Visits 6,447 -> Visits 210 | 25 | yes |
| last 12 months | 837 | 858 | 74 | 85 | Visits 6,447 -> Visits 2,265 | 25 | yes |
| all 36 months | 2948 | 3568 | 267 | 363 | Visits 6,447 -> Visits 6,447 | 25 | yes |

Plans (main operators of the actual execution plan):
- last 30 days: baseline plan = Table Spool, Index Scan (Visits.IX_Visits_Released_GateOut_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Table Spool, Window Spool, Index Seek (Visits.IX_Visits_Released_GateOut_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines)
- last 12 months: baseline plan = Table Spool, Index Scan (Visits.IX_Visits_Released_GateOut_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Index Scan (ShippingLines.IX_ShippingLines_Code), Window Aggregate, Index Seek (Visits.IX_Visits_Released_GateOut_Covering)
- all 36 months: baseline plan = Table Spool, Index Scan (Visits.IX_Visits_Released_GateOut_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Index Scan (ShippingLines.IX_ShippingLines_Code), Window Aggregate, Index Seek (Visits.IX_Visits_Released_GateOut_Covering)

### Revenue by shipping line

| Parameter set | Baseline median (ms) | Baseline p95 (ms) | Current median (ms) | Current p95 (ms) | Logical reads (largest table) baseline -> current | Rows | Identical |
|---|---:|---:|---:|---:|---|---:|:---:|
| last 30 days | 205 | 286 | 64 | 89 | Invoices 13,658 -> Invoices 446 | 26 | yes |
| last 12 months | 338 | 355 | 63 | 76 | Invoices 13,658 -> Invoices 4,804 | 26 | yes |
| all 36 months | 554 | 582 | 104 | 109 | Invoices 13,658 -> Invoices 13,658 | 26 | yes |

Plans (main operators of the actual execution plan):
- last 30 days: baseline plan = Hash Match, Index Scan (Invoices.IX_Invoices_IssuedAt_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Index Seek (Invoices.IX_Invoices_IssuedAt_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines)
- last 12 months: baseline plan = Hash Match, Index Scan (Invoices.IX_Invoices_IssuedAt_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Index Scan (ShippingLines.IX_ShippingLines_Code), Index Seek (Invoices.IX_Invoices_IssuedAt_Covering)
- all 36 months: baseline plan = Hash Match, Index Scan (Invoices.IX_Invoices_IssuedAt_Covering), Clustered Index Seek (ShippingLines.PK_ShippingLines); current plan = Hash Match, Index Scan (ShippingLines.IX_ShippingLines_Code), Index Seek (Invoices.IX_Invoices_IssuedAt_Covering)

All baseline and current procedures returned identical results.
```
</details>
