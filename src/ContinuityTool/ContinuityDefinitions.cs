using Eto.Forms;

namespace Tommi.ContinuityTool
{
    /// <summary>English definitions for the measurements, classifications and limits.</summary>
    public static class ContinuityDefinitions
    {
        public const string Text = @"MEASUREMENTS - Section G2 / Strict GraphJet4 + KnotAdaptive (v7.2)

G0 - positional continuity:
SAMPLED, BIDIRECTIONAL boundary-to-boundary nearest distance. At each native edge station, evaluate the exact 3D point on both trimmed faces. Find the closest point on the opposite actual 3D face-on-2D-trim boundary within a parameter neighborhood. Use max(A-to-B,B-to-A). Unlike the older paired-UV distance, a drift along the seam does not count as a gap. Search is 1D numerical and locally windowed; this is NOT a rigorous global nearest-bound or an interval-wide certification. Native UV paired-point gap remains available separately for diagnostics. Inconclusive nearest searches are INCOMPLETE, never automatically PASS.

G1 - tangent continuity:
Acute tangent-plane angle, in degrees (0 to 90). Opposite normals alone do not cause failure. G1 PASS also requires G0 PASS. BLOCKED means the angle passes but the gap fails. Missing G0 prevents a G1 pass. Overlap and surface-interior direction are not certified.

K2, K3, K4 - directional section checks:
K2 is signed section curvature (1/unit); K3 is its first arc-length derivative (1/unit^2); K4 is its second arc-length derivative (1/unit^3). dK is the absolute difference between face A and face B.

These quantities now use native surface partial derivatives through order four. Local plane-section derivatives are obtained by implicit Taylor-series composition. No section intersection curve, finite-difference step, neighboring endpoint sample, zeroing or extrapolation is used.

Both faces use the same section axes. The section X axis is perpendicular to the shared-edge tangent and face A's oriented normal; Z is that normal. Each local section passes through its own paired boundary point. K3 is oriented consistently along increasing section X on both faces. When a gap exists, the sections are parallel planes through different points.

K state summarises K2/K3/K4, and therefore must not be used alone to judge G2. Section G2 uses G0, G1 and only the cross-seam K2 test. Section G2 is sampled and NOT exactly Rhino EdgeContinuity. Use the strict Graph G2, Graph G3 and Graph G4 columns for additional whole-local-surface-jet diagnostics.

GraphJet4 - full local geometric surface jet at every SAMPLE:
Using all 14 first-through-fourth native surface partials, invert the local XY surface projection as a two-variable Taylor series and express both surfaces in a shared orthonormal frame as z=h(x,y). Compare all three degree-2 derivatives (hxx,hxy,hyy), all four degree-3 derivatives and all five degree-4 derivatives. These are derivatives of geometry in a common physical frame, not raw Rhino UV partials.

Do not confuse parametric C4 with geometric G4. Parametric C4 compares fourth derivatives under a specified matching parameter map; changing or reversing surface parameters can change those raw derivatives without changing the physical surfaces. This plugin's GraphJet4 result is a tolerance-based comparison of physical geometric derivatives at sampled seam stations. It is not a whole-seam mathematical certificate. No general whole-seam G4 certificate is implemented.

Graph G2, G3, G4 - hierarchical, separately reported SAMPLED continuity grades:
Each order compares all its mixed derivatives in the same physical local coordinate frame. A sampled G2 PASS requires G0/G1 plus all order-2 derivatives to pass at ALL evaluated seam locations. Sampled G3 PASS additionally requires sampled G2 and all order-3 derivatives to pass; sampled G4 PASS requires sampled G3 and all order-4 derivatives to pass. If a prerequisite is known to fail, a higher grade is BLOCKED, although raw order-specific pass/fail is retained in CSV and detailed diagnostics. Incomplete evaluations cannot count as a PASS.

GraphJet4 PASS requires valid G0/G1 prerequisites and all 12 higher derivatives within the configured K2/K3/K4 tolerances at the sampled positions. GRAPH4 PASS describes pointwise FULL-jet agreement at those samples, not a bound on the entire seam. Reparameterization of the UV domains cannot by itself make mismatched geometric derivatives match. The two local height graphs are expressed about their own boundary points; any gap and plane angle remain independent G0/G1 tests. For singular graph projections and one-sided surface knots, status is INCOMPLETE.

A full-SEAM G2, G3 or G4 certificate needs an additional exhaustive proof or bounds between samples over all knot/trim segments. Increasing sample count improves diagnostic coverage but never becomes such a proof. GraphJet4 PASS is not a guarantee of Class-A surface quality.

AVAILABILITY AND ENDPOINTS

Both seam endpoints are sampled at their actual parameters. Regular surface derivatives can be evaluated there without constructing a trimmed section curve. Singular parameterizations, tangent/ill-conditioned section projections and non-finite derivatives remain INCOMPLETE.

Internal surface knots are conservatively rejected when Rhino's Cinfinity continuity check fails: the implementation does not silently choose one side as a unique fourth-order derivative. A knot rejection does not prove a surface defect. K missing ends counts unavailable endpoint samples; the row detail and sample CSV give the reason.

G0, G1 and K retain independent states and valid counts. G1 normal comparison still uses the native paired UV positions (unchanged in this update). Missing K does not erase measured G0/G1. A failure can coexist with missing samples; inspect valid counts even on a red row.

TOLERANCES

G0Gap: model units. G1Degrees: degrees. Initial values come from document tolerances.

Normal relative K2/K3/K4 and GraphJet4 order-2/3/4 thresholds initially equal 5/10/15 percent. Strict uses 1/2/5 percent. Normal values are editable in the analysis options (maximum 50 percent); neither mode is a generally recognised Class-A or G4 industry standard. The former 70/150/400 percent presets were too permissive: at 200 percent all finite values automatically pass the relative tolerance formula. Existing measured values can be reclassified with the mode control. User-specified absolute tolerances remain important for near-zero derivatives.

K2Abs, K3Abs and K4Abs are separate, explicit absolute tolerances in 1/unit, 1/unit^2 and 1/unit^3 shared with the corresponding orders of GraphJet4. All default to zero (disabled). They are displayed in the table header and exported to CSV. Normal/Strict changes only relative thresholds; it does not change the absolute thresholds or G0/G1 limits.

For each K order: abs(A-B) <= max(absolute tolerance, relative tolerance * max(abs(A),abs(B))). There is no hidden small-value pass or noise floor. Near-zero derivatives may fail a relative-only check due to numerical noise. Choose any nonzero absolute tolerance from the model's units and your required accuracy, not just to obtain a green result. Dimensional options reset when switching documents or when a unit change is detected at the next analysis command.

TABLE AND VIEWPORT

G0 state, G1 state, K state and GraphJet4 state are separate sampled summaries. Viewport defaults to Grade. Overall RED indicates measured G0 or G1 divergence, which may be an intentional sharp corner rather than a defect. Overview ORANGE indicates failing cross-section K2 only. K3/K4 and strict GraphJet findings are kept in their separate columns and do not demote Overall. Missing G0/G1/section K2 remains INCOMPLETE. No Overall result certifies Class-A quality.

The default Grade mode colors every seam interval by the highest achieved diagnostic grade AT THE SAMPLES. Colors: green = sampled strict Graph G4, blue = sampled strict Graph G3, violet = sampled sectional G2, gray = sampled G1, orange = sampled G0, red = below G0; light gray = unknown measurement. This is intentionally a MIXED grading convention: sectional K2 is less demanding than strict Graph G2. G3/G4 grades still require all lower graph-jet orders to pass. The grade is NOT an exact Rhino EdgeContinuity label nor a full seam certificate. The table's grade column uses the WORST sampled grade along a whole seam. A single weak interval must not turn every displayed interval red.

In Grade mode the porcupine teeth are red only when the chosen target G1/G2/G3/G4 actually fails at that location. Missing evaluation remains light gray, never red. Passing teeth use the local grade color. The Viewport tab has a Red hairs below selector (default G2). A seam manually labeled G1 target is checked against G1 even when the global target is higher. Excluded or Design edge seams are gray and have no hairs. The cyan SELECTED outline is drawn UNDER the grade colors, so selection never conceals the classification.

Other viewport modes (Overall, G0, G1, K, SectionG2, Graph4, GraphG2, GraphG3, GraphG4) retain their own independent binary/diagnostic PASS/FAIL colors. Green = PASS, red = FAIL, orange = WARNING, gray = INCOMPLETE or BLOCKED in these LEGACY modes. Color mode never modifies calculated results or test tolerances.

The selected-row detail lists each K order, failed/valid sample count, worst tolerance ratio, sample location, actual difference and allowed difference with units. A ratio of 1x is the acceptance boundary; 2x means twice the allowed difference. Each line uses values from the same sample. Worst ratio need not occur at the largest absolute difference. Relative percent is shown alongside the effective allowance, which also includes any configured absolute tolerance. This ratio is a threshold comparison, not a visual defect severity score.

v7.3.2 research update: ReviewRole and numerical options are written into the current Rhino .3dm document metadata; use Save to make them persistent on disk. Roles are indexed by object identity and an edge geometry guard, NOT the unstable S-number. Altered topology or geometry invalidates a match, and missing matches return to Inspect. The optional command-line AutoKnotBudget (default OFF) increases the per-seam knot flank budget up to KnotBudgetCap only when KnotAware AND Adaptive are On. Completing flank probes does NOT bound unsampled intervals. A second restricted exact Bernstein proof now handles matching multiple V knot spans with verified C4 V continuity, a single U span, nonrational untrimmed affine Cartesian height graph coordinates and identical segment partitions. General trimmed/rational Breps are still unsupported.

v7.3.1 research update: When KnotAware and Adaptive are both On, mapped knot crossing flanks are measured as whole left/right pairs first; adaptive probing uses leftover budget. Half-pairs are not committed when remaining budget is odd. This preserves a stable knot-station prefix as MaxSamples grows, but can consume all available slots. The independent experimental Bernstein proof supports only strictly affine, nonrational, single-span untrimmed NURBS height-graph pairs and does NOT certify arbitrary car Breps or trimmed seam continuity. SAMPLED_ONLY remains the normal seam classification.

Maxima are the largest successful samples, not guaranteed global maxima. Means average successful samples only. N/A and blank numeric CSV cells mean missing values, not zero. More decimal places do not prove greater accuracy. Increase Samples to assess sampling sensitivity. KnotAware=On adds points at surface knot intersections with the trim and just to either side. Adaptive=On tests segment midpoints and refines where measurements are incomplete, state changes, approach thresholds or have unexpectedly nonlinear derivative differences. MaxSamples caps each seam. Raw measured-point classification and planned coverage are reported separately. If the enabled knot audit reports partial/unprobeable flanks, incomplete mapping, unresolved overlap or incomplete flank jets, an all-pass G4 result is shown as UNKNOWN — INCOMPLETE COVERAGE; a measured G4 failure remains FAIL. SamplingCapReached alone is not sufficient to change the measured classification, whether knot auditing is enabled or disabled. Uniform edge samples are preserved across the entire seam before adding representative knot flanks and adaptive samples. At internal knots, derivative evaluation may be one-sided, so near-knot flank samples are preferred over exact-knot fourth jets. Knot/mapping limitations and unsampled intervals remain explicitly uncertified. Even green is STILL sampled-only, never a full-seam certificate.

CSV AND VERSIONING

Methods are SurfaceJet4-v2 for section diagnostics, GraphJet4-v1 for common-frame derivatives, and KnotAdaptive-v2 for knot-aware and adaptive sample placement. GraphJet4 exports each of its 14 physical-frame partials separately for faces A and B, plus differences. K values from the earlier finite-difference version are not directly interchangeable with the present section values. Step_modelUnits is retained as a compatibility column and is blank because this method has no difference step.

RedSamples, OrangeSamples, GreenSamples and UnavailableSamples are mutually exclusive counts that sum to TotalSamples. IncompleteSamples separately counts any sample with missing measurements, including red failures with unavailable K. In the earlier export, UnavailableSamples included those overlapping cases.

MappingResidual is the maximum distance from the paired boundary points to the nominal edge point. It is a diagnostic, not an error bound. The sample CSV also includes both boundary coordinates, oriented normal angle, states, tolerances and failure reasons. CSV exports the entire analysis, including filtered-out rows.

RESULT LIFETIME

Click a row to highlight or zoom to its seam. Sorting and filtering preserve object/edge association. IDs such as S12 are local to one analysis. Geometry edits invalidate results; rerun the analysis. Changing the curvature mode reclassifies cached values without remeasuring.

The mathematics has analytic fixture checks, but successful installation is not a proof of accuracy on every model. Run ContinuityAllSeams9SelfTest and compare known geometry before precision use. The new self-tests include an explicit mixed fourth derivative which a single section cannot detect, hierarchy tests for G2/G3/G4 and endpoint sample-planning validation. Source requires RhinoCommon + Eto and must be compiled and exercised in the target Rhino version. No accuracy superiority over Rhino 9 has been established.
";
        public static void Show(Form owner)
        {
            var window = new Form
            {
                Title = "Continuity - Definitions and Limitations",
                ClientSize = new Eto.Drawing.Size(800,680),
                Padding = new Eto.Drawing.Padding(12),
                Owner = owner,
                Content = new TextArea { ReadOnly = true, Text = Text }
            };
            window.Show();
        }
    }
}
