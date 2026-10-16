# Alpha 0.1.1 native Rhino 8 regression result

Validation completed on 2026-10-16 at 20:23:24 UTC against production C# files from candidate commit `f7f0909`.

| Check | Observed result |
| --- | --- |
| Isolated harness build, Release/net48 | 0 warnings, 0 errors |
| Rhino-hosted self-tests | 180 passed, 0 failed |
| Temporary fixture CSV exports | 19 generated |
| Overall harness status | PASS |
| Harness elapsed time | 24.1 seconds |
| Test Rhino process after completion | Exited |
| `Global\RhinoCI_RhinoActive` after completion | Clear, not abandoned |
| `Global\RhinoCI_Exclusive` after completion | Available |

The existing test harness copied the baseline test fixtures into an isolated source directory and overlaid the candidate production C# files. Each overlaid file's SHA-256 was checked against the candidate before execution. The existing harness then used its dedicated test plug-in GUID and `SCAT` command names. It retained the test project and test-only fixtures, so its executable is not byte-identical to the distributed release `.rhp`. The production C# source was identical before those test-identity transformations.

The first rebuilt `build/SuperContinuityAlpha.rhp` passed 16 coverage-honesty assertions. That earlier binary's SHA-256 was `AE1733D1A80BCCCAB5A50EE98BDB6E7E72C4F3A51C4A0DC61324A3AF38E72E92`; it is not the current release-candidate binary.

The private diagnostic archive is `alpha011-native-20261016-232300/runs/r001.zip`, SHA-256 `7150CDE41364CB4586147E61A9064A788EB80BD0CC803FF2C01F978609F86314`. It is retained separately from the public release content. The shared mutex and ACC guard remained in use. The protected approval manifest and job-start hook were unchanged. Only temporary reference fixtures were analyzed; user modeling documents were not opened.

This native run complements the 16 standalone coverage-honesty assertions. It does not claim that the 180 tests include the new coverage assertions, manually validate the seam table or viewport, reproduce the Lucé S122 seam, or validate the supplied four-Brep `.3dm` at its intended nonzero absolute K3/K4 tolerances. Those distinctions remain explicit in the release checklist. Passing finite fixtures is not whole-seam mathematical certification.

## Subsequent cap-only correction and developer handbook

During documentation review, the first snapshot was found to downgrade a capped sample plan even when an enabled knot audit had complete evidence. The current correction bases incomplete coverage on explicit deficient audit states, preserving the cap as a separate diagnostic. Five added assertions cover completed enabled audits and the no-mapped-interior-knot case. Extensive source comments and the mathematics handbook explain the implementation and its limits.

The current Release/net48 binary built successfully and passed **21/21 standalone assertions**. Its SHA-256 is `FF04A4D014958F9AAEDA5E362B29A7F22C2956A25A18F931E022513297042ADE`. The attempted final native regression was **DEFERRED before launching Rhino**, because `AC2-Win64-Shipping.exe` was running. No test Rhino was started or user process stopped. The current revision therefore still needs that native regression and the separate release-UI/reference-model check before publication.
