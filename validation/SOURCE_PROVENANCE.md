# Rhino 8 alpha source and validation provenance

The source snapshot was tested in local Rhino 8 run `r004` on 2026-10-16. Recorded harness result: 180/180 tests passed and 19 fixture CSV exports; the build reported 0 warnings and 0 errors. The harness note limits its result to temporary reference fixtures and does not claim a full-trimmed-seam G4 certificate.

The G4 implementation and UI semantics in this package (`ContinuityAnalyzer.cs`, `ContinuityDefinitions.cs`, `ContinuityEvaluator.cs`, `ContinuityGrade.cs`, `ContinuityReports.cs`, `ContinuityTableForm.cs`, `SectionJetEvaluator.cs`, `SurfaceGraphJetEvaluator.cs`, and `KnotAwareSampler.cs`) match the r004 tested source files byte for byte. `KnotCoverageAudit.cs` has the same implementation; only its command name lacks the `SCAT` prefix that the isolated test harness added to avoid command collisions.

The packaged Rhino 8 `.rhp` was freshly built from the included source against the installed Rhino 8 SDK. It is not a copied runner/test-harness binary. Reference formulas and measured station results are documented in `../examples/REFERENCE_GEOMETRY.md` and `RHINO8_G4_RESULTS.md`. They establish outcomes for those analytic fixtures and finite sample stations only. General plugin results remain sampled and are not interval-wide certificates.
