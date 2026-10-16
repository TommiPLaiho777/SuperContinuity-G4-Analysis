# SuperContinuity G4 Analysis Alpha 0.1.1

SuperContinuity is an MIT-licensed Rhino 8 plug-in for inspecting continuity between adjacent surfaces. This alpha adds a fourth-order sampled geometric surface-jet comparison to the seam analysis table.

## What the G4 result means

At finite stations along a seam, the analyzer evaluates each surface through fourth order, expresses the local surface jets as height-graph derivatives in a shared physical frame, and compares the resulting mixed derivatives with configured absolute and relative tolerances. A passing result means only that the measured stations passed: it is **sampled geometric G4** evidence.

The result is distinct from parametric C4. Parametric C4 compares derivatives under a specified parameter correspondence, so its values can change when parameter domains or directions change even if the physical surface does not. The alpha's sampled geometric G4 comparison is intended to be independent of regular reparameterizations represented by the shared local physical frame; validation covers the reference cases listed in `examples/REFERENCE_GEOMETRY.md`, not every possible map or surface.

A pass is not a mathematical certificate over the entire seam. The analyzer does not prove that unsampled intervals, knot spans, or all points on arbitrary trimmed NURBS boundaries satisfy G4. Where the local graph frame is singular, derivatives are unavailable, or samples cannot be evaluated reliably, the result is `UNKNOWN`/incomplete rather than a pass. Tolerances and sampling density affect classifications. This alpha does not certify Class-A quality.

## Coverage-aware G4 status

The release separates raw measured-point results from coverage status. For example, a seam may report `GraphG4_sampled_state=PASS` and `219/219` passing measured samples while its user-facing result is `G4 UNKNOWN — INCOMPLETE COVERAGE` when `KnotAuditStatus` reports missing or unprobeable knot flanks, incomplete mapping, unresolved knot overlap, or incomplete flank jets. The table, selected-seam details, and CSV retain both the user-facing status and raw sample counts. The viewport uses a neutral color for an all-pass G4 result with explicitly incomplete coverage.

A measured G4 failure remains `G4 FAIL`, even when coverage is incomplete. `SamplingCapReached` is reported separately; it does not by itself turn a measured pass into `UNKNOWN` when the knot audit is disabled. Where the audit is active, a reached cap is treated as incomplete planned coverage. `UNKNOWN` describes missing evidence, not a detected geometric defect. All passing results remain sampled-only and do not certify the complete seam.

## Install and use

The `build/SuperContinuityAlpha.rhp` binary is built from the included source for Rhino 8 / .NET Framework 4.8 and uses Rhino's installed `RhinoCommon`, `Rhino.UI`, and `Eto` assemblies. In Rhino 8, use Plug-in Manager to load the `.rhp`, then run `ContinuityAllSeams9` on a document containing adjacent surfaces. The table labels identify G3/G4 as sampled geometric results. `ContinuityAllSeams9Table`, `ContinuityAllSeams9CSV`, and `ContinuityAllSeams9Off` open the table, export measurements, and stop the display respectively.

Review `examples/REFERENCE_GEOMETRY.md` for the programmatically defined positive and negative G3/G4 references and `validation/RHINO8_G4_RESULTS.md` for the recorded Rhino 8 measurements. Run `dotnet run --project validation/CoverageHonestyRegression.csproj` for the coverage classification regressions. The reference equations are analytic fixtures; the plugin's general-purpose result remains sample-based.

## Build

Install the .NET SDK and Rhino 8. Build from `src` with:

```powershell
dotnet build .\SuperContinuityRhino8.csproj --configuration Release --framework net48 -p:RhinoSystemDir="C:\Program Files\Rhino 8\System"
```

The compiled plug-in target-framework build is configured for the installed Rhino 8 SDK assemblies; those host assemblies are not bundled in this source package.

## License

MIT. See `LICENSE` for the full terms. Copyright (c) 2026 Tommi P. Laiho.
