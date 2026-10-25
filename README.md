# SuperContinuity 0.1.2-alpha.1 - Rhino 8 test release

This prerelease updates the existing seam inspector. It preserves the G0-G3
workflow and adds availability/coverage safeguards to the sampled G4 analysis.
It is an MIT-licensed analysis tool; it does not edit or optimize your surfaces.

## Download and try

Download the release ZIP from this tag's Assets, extract it to a new folder and
keep earlier working builds separately. Load build/SuperContinuityAlpha.rhp
through Rhino 8 Plug-in Manager, then run ContinuityAllSeams9. A plug-in already
loaded in a Rhino session cannot be replaced by copying a different file over it;
restart Rhino normally before loading another version of the same plug-in.

The supplied reference model is generated test geometry, not a customer design.
Open examples/G4_Reference_Surfaces/SuperContinuity_G4_Reference_Surfaces.3dm in a
separate disposable document. Run ContinuityAllSeams9, select the joined test
objects and inspect the table. The four reference cases should distinguish
G4 / G3 / G2 / G4 under the documented reference settings. Read the reference
README before interpreting its analytical equations or changing tolerances.

Commands: ContinuityAllSeams9 performs analysis; ContinuityAllSeams9Table opens
the inspector; ContinuityAllSeams9CSV exports data; ContinuityAllSeams9Off stops
the display. Inspector export buttons save seam summaries or station samples.

## How to interpret the result

The analyzer samples a seam at finite stations. At each usable station it builds
a shared physical tangent/normal frame and compares the two surfaces' local
height-graph derivatives through fourth order, including mixed components.
This is sampled geometric contact evidence, distinct from a requested nominal
G4 setting and from parametric C4 under a chosen parameter correspondence.

A sampled PASS applies to the measured stations only. It does not prove every
point between stations, arbitrary trimmed boundaries, all parameterizations or
Class-A aesthetic quality. Singular frames, unavailable jets and incomplete
coverage are missing evidence, not automatically detected geometric failures.
An independently observed reliable failure remains a failure even if some other
stations are unavailable. Independently valid lower-order results are retained.

Colors remain: G4 green, G3 blue, G2 purple, G1 gray, G0 orange and below G0 red.
An all-pass G4 observation with explicitly incomplete audit coverage uses a
neutral indication. A color alone is not a continuity certificate. The legacy
lower-order GREEN overview is not a separate G4 verification statement.

## Changes since 0.1.1

- Exact original NURBS interior knots are treated conservatively by the graph
  evaluator. Usable flank samples do not establish a unique jet at the knot.
- Directional section evaluation retains independently available orders. For
  example, a knot with a sufficient C3 guarantee can retain K2/K3 while K4 stays
  unavailable and is exported blank. Unknown is not converted to zero or PASS.
- G4 sampled-pass and attention filters distinguish raw observations from
  incomplete coverage. Measured failures take precedence over missing coverage.
- Source replacement/deletion clears stale analysis; unrelated edits retain it.
- Model-unit changes and native custom-unit name/scale changes clear stale
  sessions. Saved/live settings require matching dimensional context.
- Existing inspector, viewport palette and CSV behavior are preserved.

## Validation and limits

The RHP is the exact existing native-validated development binary:
B670E062D3B0BD6F24D8853726491B3D4F5D51DC865230CB93D573DE2E328034.
Its source production change is recorded in local development commit 86be3b5;
subsequent candidate c96b193 contains additional evidence, not another binary.
This release branch copies only intended production/distribution files, without
publishing the local development history, machine logs or unrelated instructions.

Earlier isolated Rhino mathematical checks cover the section/knot change and
180 native baseline cases. The exact current RHP separately passed 46 coverage/
default assertions and bounded native custom-unit/settings checks. Subsequent
registered command/options use, actual CSV save/cancel, object-event cleanup,
inspector rendering, viewport palette and .NET Core/MCP transport were exercised
in owned Rhino 8 documents. Synthetic presentation checks are not new measured
surface evidence. This packaging step does not claim a new mathematical test run.

Supported evidence includes declared untrimmed NURBS and bounded interior-UV
trimmed reference cases. Arbitrary trims, slivers, singularities, universal
conditioning bounds and whole-seam certification remain outside established
coverage. The ordinary clean first-install/upgrade path was not independently
verified; registered loading was verified. The inspector may still display its
legacy title v7.2.3; use BUILD_INFO.json and the RHP hash for build identity.

The repository handbook PDF inherited from the previous tag is historical;
it is not included in this ZIP as an updated 0.1.2 manual. The explanations above
state the current per-order knot-availability and coverage semantics.

## Source build

The source target is src/SuperContinuityRhino8.csproj, net48, version
0.1.2-alpha.1, referencing Rhino 8's installed RhinoCommon/Rhino.UI/Eto assemblies.
Those host assemblies are not redistributed. Build with an installed .NET SDK:

dotnet build src/SuperContinuityRhino8.csproj -c Release --framework net48

This alpha does not certify geometry or replace the designer's visual judgment.
