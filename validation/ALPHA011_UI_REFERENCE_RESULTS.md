# Release-binary UI and four-reference check

On 2026-10-17 a dedicated Rhino 8 / .NET Framework session loaded the distributed `build/SuperContinuityAlpha.rhp` with SHA-256 `FF04A4D014958F9AAEDA5E362B29A7F22C2956A25A18F931E022513297042ADE`. A temporary command bridge invoked that assembly's actual analyzer and seam inspector; it did not replace or rebuild the production UI. The user's previously installed Alpha 0.1 registration was left unchanged. Consequently this verifies release-binary operation, rather than installation over an existing plug-in registration.

All four named two-face Breps were valid and each contained one internal seam. Only copies in a new disposable document were analyzed. Settings: millimetres; G0 gap 1e-6; G1 angle 0.1 degrees; K2/K3/K4 absolute floors 1e-10; relative limits 5/10/15 percent; 61 initial samples, maximum 220, knot-aware and adaptive sampling enabled.

| Reference | Samples | Observed result |
| --- | --- | --- |
| G4, not G5 | 121 | G4 SAMPLED PASS, 121/121 |
| G3, not G4 | 121 | G3 sampled; G4 FAIL, 0/121 |
| G2, not G3 | 183 | G2 section; Graph G3 FAIL; cumulative G4 BLOCKED, displayed G4 UNKNOWN |
| Smooth both sides | 121 | G4 SAMPLED PASS, 121/121 |

The table, selected-seam details and viewport were inspected directly. G4 seams were green, G3 blue, and G2 purple. Selecting rows updated the details and highlighted the selected seam. The default table uses horizontal scrolling for additional columns. Both UI export buttons successfully wrote CSV files; parsing found four seam records and 546 sample records. The extracted measured summaries are in `ALPHA011_UI_REFERENCE_RESULTS.csv`.

The inspector title still says `SuperContinuity v7.2.3 - Seam inspector (preview)`, inherited from the implementation; the assembly path/hash above identifies the tested Alpha 0.1.1 binary. This is a cosmetic release-label inconsistency, not evidence of loading the old binary.

Limits: these four knot-free references do not exercise the Luce S122 incomplete-flank case visually, nor a localized defect along a variable seam. The 21 standalone coverage assertions separately validate S122-shaped UNKNOWN and observed-FAIL precedence. The original Luce model has not been remeasured here. No whole-seam certification or general invariance theorem follows from these finite tests. A standard Plug-in Manager installation and the incomplete-coverage visual check remain separate checklist items.

The test Rhino exited normally with code 0. No user modeling file was modified, and no Rhino process was forcibly stopped. Private diagnostics and full CSVs remain outside the public package.
