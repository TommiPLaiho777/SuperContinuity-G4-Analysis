# Alpha 0.1.1 coverage-honesty regressions

The regression program constructs seam-report objects with controlled per-sample states. It verifies the distinction between observed measurements and knot/audit coverage; it does not claim to analyze the Lucé model or certify an entire seam.

- S122-shaped input: 219/219 measured Graph G4 PASS, 398 of 477 knot crossings missing at least one flank, `PARTIAL_FLANK_SAMPLING`, and `SamplingCapReached=true`. Expected user-facing result: `G4 UNKNOWN — INCOMPLETE COVERAGE`; raw state/count remain PASS and 219/219.
- Partial knot audit with `SamplingCapReached=false`. Expected: still UNKNOWN, proving the audit state itself drives the coverage decision.
- Full two-sided flank and graph-jet coverage with all sample results passing. Expected: `G4 SAMPLED PASS`.
- One measured cumulative G4 failure under partial coverage. Expected: `G4 FAIL`, retaining the lower cumulative measured grade.
- Sampling cap reached with knot audit disabled. Expected: cap is visible as a separate diagnostic and does not by itself change sampled G4 PASS to UNKNOWN.
- Graph G2/G3 sample states remain unchanged.

Run with `dotnet run --project validation/CoverageHonestyRegression.csproj` on Windows with Rhino 8 installed.

The local Rhino 8 harness was attempted on 2026-10-16. The isolated test Rhino process exited before writing `result.json`; the user reported Rhino showed a licensing warning. That attempt is not counted as a passing Rhino run. The candidate built successfully and the standalone coverage regression program passed.
