# Alpha 0.1.1 coverage-honesty regressions

The regression program constructs seam-report objects with controlled per-sample states. It verifies the distinction between observed measurements and knot/audit coverage; it does not claim to analyze the Lucé model or certify an entire seam.

S122 is the run-specific label of a door seam in the author's Luce car model (Edge 150), not a mathematical term or a universal seam number. The car geometry is not distributed in the reference kit. The regression uses synthetic report objects to reproduce its reported counts and coverage state.

- S122-shaped input: 219/219 measured Graph G4 PASS, 398 of 477 knot crossings missing at least one flank, `PARTIAL_FLANK_SAMPLING`, and `SamplingCapReached=true`. Expected user-facing result: `G4 UNKNOWN — INCOMPLETE COVERAGE`; raw state/count remain PASS and 219/219.
- Partial knot audit with `SamplingCapReached=false`. Expected: still UNKNOWN, proving the audit state itself drives the coverage decision.
- Full two-sided flank and graph-jet coverage with all sample results passing. Expected: `G4 SAMPLED PASS`.
- One measured cumulative G4 failure under partial coverage. Expected: `G4 FAIL`, retaining the lower cumulative measured grade.
- Sampling cap reached with knot audit disabled. Expected: cap is visible as a separate diagnostic and does not by itself change sampled G4 PASS to UNKNOWN.
- Sampling cap reached with a complete enabled audit, and with no mapped interior knots. Expected: both remain sampled PASS; an enabled audit is not itself evidence of missing coverage.
- Graph G2/G3 sample states remain unchanged.

Run with `dotnet run --project validation/CoverageHonestyRegression.csproj` on Windows with Rhino 8 installed.

The current standalone regression passed all 21 assertions against the rebuilt release binary. The final native rerun of the cap-only correction subsequently passed 180/180 self-tests and generated 19 fixture CSVs. See `RHINO8_ALPHA011_RESULTS.md`. The four-Brep release-binary UI check is documented separately in `ALPHA011_UI_REFERENCE_RESULTS.md`. These checks do not constitute a manual UI verification on the private Luce S122 geometry.
