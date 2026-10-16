# SuperContinuity G4 — exact NURBS reference surfaces

Four analytically defined surface-seam cases for Rhino 8. Each case consists of **two exact unweighted degree-(5,2) Bézier/NURBS surfaces**, joined into one two-face Brep along the seam at **x = 0**, and laid out separately along Y.

These are **not meshes, not point-sampled fitted approximations**. Their NURBS control points are computed from exact polynomial-to-Bernstein conversion (with floating-point storage precision).

## How to create the `.3dm`

In **Rhino 8**, execute `_-RunPythonScript` and select `Create_G4_Reference_Surfaces.py`. It creates `SuperContinuity_G4_Reference_Surfaces.3dm` **next to this script**, without changing the active Rhino document. Open the new file, select a joined two-face Brep, and run `ContinuityAllSeams9`. The pre-generated `.3dm` in this folder was created by this script. The script writes a fresh copy next to itself and does not edit the active Rhino document.

The generated Rhino 8 `.3dm` is included with the script and this README.

## Mathematics

Let the common nonflat surface graph be

\[
F(x,y) = 0.12x^2 + 0.05xy + 0.08y^2 + 0.03x^3y + 0.02x^4 + 0.01x^2y^2.
\]

Each example has a left-hand patch \(z_L(x,y)=F(x,y)\), defined on \(-1 \le x \le 0\), and a right-hand patch \(z_R(x,y)=F(x,y)+0.10x^m(1+0.5y)\), defined on \(0 \le x \le 1\). The \(y\) interval is 0–1 **within each example**; the geometry is translated in world Y to separate examples visually.

| Case | Right-hand extra term | Mathematical seam behavior | Expected sampled analysis |
| --- | --- | --- | --- |
| `01_G4_NOT_G5` | \(0.10x^5(1+0.5y)\) | **G4 holds along the entire seam, but G5 does not** | G4 sampled PASS if coverage sufficient |
| `02_G3_NOT_G4` | \(0.10x^4(1+0.5y)\) | **G3 holds, G4 does not** | G3 sampled PASS, G4 FAIL |
| `03_G2_NOT_G3` | \(0.10x^3(1+0.5y)\) | **G2 holds, G3 does not** | G2 PASS, G3 FAIL |
| `04_SMOOTH_BOTH_SIDES` | 0 | Same analytic graph on both sides; smooth to all orders | G4 sampled PASS if coverage sufficient |

Why this is *genuine geometric continuity*: because the patches have exactly matching height-graph derivatives \(\partial_x^a\partial_y^b z\) at every point \(x=0\) for all total orders \(a+b\le m-1\). They use the same physical \((x,y,z)\) coordinates, so the matching is stronger than simply matching UV derivatives under an arbitrary parameterization. The \(m\)th normal-direction derivative differs by \(0.10m!(1+0.5y)\), proving the specific failure at order \(m\).

For example 01, the **fifth** derivative discontinuity is \(12(1+0.5y)\), whereas **all derivatives through fourth order agree** on the seam. For example 02, the fourth derivative discontinuity is \(2.4(1+0.5y)\), giving \(2.4, 2.7, 3.0, 3.36, 3.6\) at local y = 0, 0.25, 0.5, 0.8, 1.

This establishes the mathematical properties of these fixtures; it does not mean SuperContinuity mathematically certifies arbitrary user seams. Its reported PASS is **sampled geometric** and sensitive to tolerances, sample budget, knot coverage and incomplete jets.

## Release checklist

1. Generate the file with Rhino 8, ensure each case contains exactly one joined Brep with two faces and an internal two-face seam.
2. Run `ContinuityAllSeams9`; match observed G2/G3/G4 behavior to the table, inspect any `UNKNOWN` coverage states.
3. Confirm no geometry is added to or changed in the previously open document.
4. Distribute the generated `.3dm` with this README and the script so reviewers can regenerate the examples.

Do not publish the `.3dm` as validated until the Rhino 8 run and actual plug-in results have been checked.

### Rhino 8 File3dm API compatibility note

Version 3 uses `File3dm.Layers.AddLayer(name, color)` and type-specific
`File3dm.Objects.AddBrep` and `AddTextDot` overloads, avoiding the overload
ambiguities experienced with `Layers.Add` and `Objects.Add` in Rhino Python 3.
Run the distributed **`Create_G4_Reference_Surfaces.py`**, which contains the v3 API corrections, rather than a previously opened `Superflow2.py` tab.
It prints `SuperContinuity G4 REFERENCE KIT v3` at the beginning of execution.

The supplied Rhino 8 CSV run used zero absolute K3/K4 tolerances and records false G3 failures at near-zero endpoint derivatives (round-off deltas around 1e-14). To reproduce the analytic expected classifications in this unit-scale fixture, set K3Abs=1e-10 and K4Abs=1e-10 in the analysis options, then rerun and inspect the new CSV. A post-hoc recomputation from the CSV raw A/B jet values with this explicit absolute allowance gives the expected sampled counts: S1 121/121 G3+G4 PASS; S2 121/121 G3 PASS and G4 FAIL; S3 G3 FAIL; S4 121/121 G3+G4 PASS. This is a reclassification of recorded measurements, not a Rhino rerun; use the settings and regenerate the CSV before presenting it as verified.


The stored CSVs from the initial run are diagnostics, not release validation: do not distribute them as passing reference results until the endpoint tolerance rerun matches the table above.
