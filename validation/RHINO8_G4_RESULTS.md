# Rhino 8 G4 reference results

Local validation snapshot: 2026-10-16, test run `r004`, test harness `SC autotest v0.1.11`, Rhino 8. Build completed with 0 warnings and 0 errors. The full harness reported **180 passed, 0 failed**, with 19 fixture CSV reports. No document geometry was changed. Test output was isolated under the user's local Temp directory.

The exact affine references use `z = 0.1*x^m*(1 + 0.5*y)`. At `y = 0, 0.25, 0.5, 0.8, 1`, the measured fourth-order geometric mismatch for `m=4` was respectively `2.4, 2.7, 3.0, 3.36, 3.6` (model-length units to the power `-3`). Rhino returned `0` at all five stations for `m=5`. The outcomes were:

| Reference | Parametric result under matched unit map | Sampled geometric result |
| --- | --- | --- |
| `m=4` | C3 PASS, C4 FAIL | G3 PASS, G4 FAIL |
| `m=5` | C3 PASS, C4 PASS | G3 PASS, G4 PASS |

The affine G3/G4 classifier used a 5% relative tolerance and `1e-9` absolute tolerance for each graph-jet order. The direct expected-D4 comparison used `1e-8` numerical error tolerance.

The nonlinear references tested both forward and reversed U directions at the same five y stations. For `m=4`, measured D4 was `2.4, 2.7, 3.0, 3.36, 3.6` within `2e-5`; sampled G3 passed and sampled G4 failed in all 10 cases. For `m=5`, D4 was `0` within `2e-5` and sampled G3/G4 passed in all 10 cases. Nonlinear cases used 5% relative and `1e-8` absolute classifier tolerances. Parametric C4 was not compared across unlike parameter maps.

These tests establish that the implementation distinguishes the constructed positive and negative references at the tested stations, including affine domain changes, U reversal, and one nonlinear regular map. The reference formulas themselves give the exact whole-seam answer for these fixtures. The plugin does **not** prove a whole-seam result for arbitrary input; production output remains `sampled only`.
