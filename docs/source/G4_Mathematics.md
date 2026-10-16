# SuperContinuity G4 Mathematics

## A guided explanation for human developers

**Alpha 0.1.1 - mathematical method, implementation, and evidence limits**

Copyright (c) 2026 Tommi P. Laiho. Distributed with the MIT-licensed SuperContinuity source. This handbook describes the implementation; it does not present the alpha as a certified industrial metrology system.

The central question is simple: when two NURBS faces meet, do their local shapes agree through fourth order? The answer requires more care than comparing a fourth UV derivative. Parameters are coordinates chosen by a modeler or a CAD kernel; they are not the physical shape itself.

We will move from that distinction to the actual algorithm: construct one physical frame, recover each surface as a local height graph, compare every relevant mixed derivative, and keep missing evidence separate from measured results. The appendices derive the computations rather than asking the reader to trust a compact formula.

> Three statements must remain separate: parametric C4 under a specified parameter map; sampled geometric fourth-order contact at measured stations; and a mathematical statement over every point of a seam. The alpha reports the second. The first is not a substitute, and the third is not implemented for arbitrary input.

**How to read this document.** Start with sections 1-4 for the geometric picture. Sections 5-8 explain the numerical computation. Sections 9-12 explain evidence, references, and limitations. The implementation map and appendices connect the mathematics to specific source tags.

---
# 1. What are we comparing?

A regular surface patch is a map S(u,v) into three-dimensional space. Regular means its two first derivative vectors span a plane. Two different maps can describe exactly the same patch. Stretching, reversing, or smoothly warping UV coordinates changes derivatives of the map without necessarily changing the surface.

$$\mathbf{S}:\Omega\subset\mathbb{R}^{2}\longrightarrow\mathbb{R}^{3},\qquad \mathbf{S}_{u}\times\mathbf{S}_{v}\ne\mathbf{0}.$$

**Parametric C4** compares derivatives through order four under a chosen parameter correspondence. That correspondence is part of the statement. A raw difference between fourth UV derivatives can therefore describe coordinate choices rather than a geometric defect.

**Geometric fourth-order contact at a station** can instead be described using a common regular physical graph chart. After position and tangent prerequisites are established, write both local surface germs as heights above the same plane. Equality of all height derivatives through degree four expresses equality of their fourth jets in that chart.

$$\partial_x^{a}\partial_y^{b}h_A(0,0)=\partial_x^{a}\partial_y^{b}h_B(0,0),\qquad a+b\le4.$$

This exact mathematical statement assumes sufficiently differentiable regular graphs at the same point. The software uses finite tolerances, separately checked G0/G1 prerequisites, and finitely many stations. It must not silently promote those numerical checks into an exact theorem.

> A jet is the finite collection of derivatives at one point. It is not the surface on a neighborhood, and certainly not a certificate for an entire trimmed seam.

---
# 2. Finding the two sides of a seam

The analyzer operates on joined Breps. A shared topological edge identifies the two adjacent faces, but the existence of that edge does not prove that the underlying surfaces meet with zero geometric error. Comparing the shared edge curve to itself would hide the very discrepancy we want to measure.

`BoundaryContinuityEvaluator` resolves the face-specific trims, maps an edge parameter onto each trim, and evaluates each surface at its own UV boundary coordinate. Ambiguous self-seam topology or a failed native mapping remains unavailable; the implementation does not guess a correspondence.

The G0 diagnostic uses two local directed trim-to-trim distance searches, one originating from each evaluated side. Their maximum is the station's reported gap. The older same-parameter point gap and the edge-mapping residual are retained separately. This is not a globally certified Hausdorff distance.

$$g(t)=\max\{d_{A\to B}(t),d_{B\to A}(t)\}.$$

For tangent-plane agreement the code compares normals without treating their sign as a tangent-plane difference. An oriented normal mismatch is a separate diagnostic. The numerically stable angle computation uses atan2 of cross-product magnitude and absolute dot product.

$$\theta=\operatorname{atan2}\!\left(\|\mathbf{n}_A\times\mathbf{n}_B\|,|\mathbf{n}_A\cdot\mathbf{n}_B|\right).$$

Opposite normals can describe the same plane. That does not establish correct solid orientation, absence of folds, or absence of overlaps. Those are different geometric questions.

---
# 3. One physical frame, fourteen derivatives

At a station let t be the seam tangent. The implementation uses face A's oriented normal for the physical height direction z. It constructs an across-seam direction x and completes the frame with y. Both faces use these same axes.

$$\mathbf{z}=\widehat{\mathbf{n}}_A,\qquad\mathbf{x}=\widehat{\mathbf{t}\times\mathbf{z}},\qquad\mathbf{y}=\mathbf{z}\times\mathbf{x}.$$

The hat means normalize, not an additional derivative. Failure to construct a regular frame is an availability failure. Giving face B its own axes would compare different components and could conceal a shape difference.

| Total order | Height-graph derivative entries | Count | Units for model length L |
| 1 | hx, hy | 2 | dimensionless |
| 2 | hxx, hxy, hyy | 3 | L^-1 |
| 3 | hxxx, hxxy, hxyy, hyyy | 4 | L^-2 |
| 4 | hxxxx, hxxxy, hxxyy, hxyyy, hyyyy | 5 | L^-3 |

There are fourteen entries in the array. The classifier checks tangent-plane agreement through G1, then compares all three second-, four third-, and five fourth-order components. Testing only hxxxx would miss differences in mixed partials.

Each face's derivative expansion is centered at its evaluated boundary point. Position is checked separately through G0. When that gap is only approximately zero, the numerical result is a gated local diagnostic, not an exact same-point jet identity.

---
# 4. Why a height graph exists locally

Project the surface displacement into the common frame. The planar coordinates X and Y are dot products with x and y; Z is the dot product with z. If the planar projection has an invertible Jacobian, one may locally solve for the original surface parameters as functions of X and Y. The height is then Z evaluated through that inverse.

$$X=(\mathbf{S}-\mathbf{S}_0)\cdot\mathbf{x},\quad Y=(\mathbf{S}-\mathbf{S}_0)\cdot\mathbf{y},\quad Z=(\mathbf{S}-\mathbf{S}_0)\cdot\mathbf{z}.$$

$$h(X,Y)=Z\bigl(u(X,Y),v(X,Y)\bigr).$$

This is a local application of the inverse/implicit function principle [2]. It does not claim that one graph covers the whole face. A vertical fold relative to the chosen plane, singular UV coordinates, or insufficient native derivatives can make the chart unusable at a station.

The code checks a scaled projection determinant against 1e-10. That threshold is an implementation availability guard; it is not the continuity tolerance and not a rigorous condition-number estimate. A nonzero determinant establishes an exact local inverse in mathematics, but a very small determinant can amplify floating-point error in computation.

> UNKNOWN is appropriate when this graph cannot be reliably evaluated. Returning a zero array would turn missing data into a false high-order pass.

---
# 5. Taylor coefficients, not raw partials

The native Rhino derivative ordering is documented by `Surface.Evaluate` [1]. The implementation reads increasing total degree, with descending U power inside each degree. It then converts derivatives to Taylor coefficients before composing polynomials.

At the station define su as the length of S_u and sv as the length of S_v. Both are held constant during the local series calculation. Introduce scaled parameter displacements U and V:

$$U=s_u(u-u_0),\qquad V=s_v(v-v_0).$$

$$\mathbf{c}_{ab}=\frac{\partial_u^a\partial_v^b\mathbf{S}}{s_u^a s_v^b a!b!},\qquad \mathbf{S}-\mathbf{S}_0=\sum_{1\le a+b\le4}\mathbf{c}_{ab}U^aV^b+o(\|(U,V)\|^4).$$

The factorials are not cosmetic. In a univariate example, a second derivative of 6 corresponds to a quadratic coefficient of 3. Multiplying raw derivatives as though they were coefficients would produce the wrong chain rule from degree two onward.

The scaling reduces dependence on large or small parameter domains. It does not convert the chart to an arclength parametrization, remove every conditioning problem, or make a singular chart regular. In particular, su and sv must not themselves be differentiated in the recurrence.

For a surface that is only C4, the remainder notation above is little-o at degree four. A fifth-order big-O remainder would require stronger regularity and must not be assumed for every input NURBS knot configuration.

---
# 6. Solving the inverse without finite differences

We seek bivariate polynomials U(x,y) and V(x,y) with zero constant coefficients. The target planar projection is exactly x,y through degree four. Let a=c10 and b=c01, and let R_ij be the already-known nonlinear contribution to the coefficient x^i y^j of the composed surface.

$$\mathbf{S}(U,V)-\mathbf{S}_0=\mathbf{a}U+\mathbf{b}V+\sum_{a+b\ge2}\mathbf{c}_{ab}U^aV^b.$$

The same two-by-two linear system determines every new coefficient:

$$a_xU_{ij}+b_xV_{ij}=\delta_{i1}\delta_{j0}-R_{ij,x},$$
$$a_yU_{ij}+b_yV_{ij}=\delta_{i0}\delta_{j1}-R_{ij,y}.$$

Why are all other terms already known? Every nonlinear monomial contains at least two factors with zero constant term. If it used an unknown coefficient of total degree n, at least one other positive-degree factor would force the product to degree n+1 or higher. Thus an unknown degree-n coefficient cannot appear in the nonlinear degree-n remainder.

The algorithm starts at degree one and proceeds to four. It composes truncated series from lower degrees, solves the linear projection equations, then projects the resulting displacement onto z. This is algebraic series reversion from native partial derivatives, not a finite-difference estimate along the seam.

> The induction argument is the reason the implementation may rebuild powers once per degree. Unknown coefficients of the same degree cannot contaminate one another through nonlinear terms at that degree.

---
# 7. Recovering the graph jet and accepting a sample

If H_ij is the resulting height-series coefficient, the mixed derivative is i!j! H_ij. This reverses the earlier coefficient normalization. Output entries must keep the same fourteen-element order used by the classifier and CSV.

$$h_{ij}=i!j!\,H_{ij},\qquad h(x,y)=\sum H_{ij}x^iy^j.$$

For two finite entries a and b, the explicit tolerance rule is:

$$|a-b|\le\max\!\left(\varepsilon_{\rm abs},\varepsilon_{\rm rel}\max(|a|,|b|)\right).$$

It is the union of an absolute and a relative criterion, not their sum. Relative tolerances are dimensionless; the absolute allowance has the units of that derivative order. Fourth graph derivatives therefore need an absolute tolerance in L^-3.

The classifier checks every mixed entry of an order. Its raw `GraphPass` flags describe each order independently. Its `GraphOrderState` flags are cumulative: G4 cannot pass if the G3 prerequisite does not pass. A blocked higher-order result is not automatically evidence of a fourth-order defect.

Near zero, relative tolerance alone is fragile. In the supplied reference CSV, a nominally zero third-order discrepancy was about 4.64e-15. Comparing 0 to that value with 10% relative allowance alone still fails: the allowance is only about 4.64e-16. A deliberate nonzero absolute allowance can express the intended numerical resolution. The code must not insert a hidden epsilon on the user's behalf.

---
# 8. Section curvature is a separate diagnostic

The plug-in also computes the curve obtained by intersecting the local surface with the plane spanned by the shared physical x,z axes. Its remaining coordinate m is constrained to zero. Algebraic parameter inversion again provides a local height curve (x,h(x)).

Let p=h', q=h'', r=h''', t=h'''' and w=1+p^2. Arc length satisfies ds/dx=sqrt(w), so differentiation along arc length is w^-1/2 times differentiation in x.

$$\kappa=\frac{q}{w^{3/2}},\qquad \frac{d\kappa}{ds}=\frac{r}{w^2}-\frac{3pq^2}{w^3}.$$

$$\frac{d^2\kappa}{ds^2}=\frac{t}{w^{5/2}}-\frac{10pqr+3q^3}{w^{7/2}}+\frac{18p^2q^3}{w^{9/2}}.$$

The historical K2, K3, K4 labels refer to these three quantities. Their units are L^-1, L^-2, L^-3. The signs depend on the shared section orientation.

A single section samples only one direction. It cannot establish agreement of all mixed surface derivatives. The viewport's grade ladder is deliberately a hybrid diagnostic: section G2 is used at the lower grade, while higher G3/G4 grades require the independent cumulative graph-jet checks. It is not equivalent to Rhino's own EdgeContinuity metric.

Appendix B differentiates the curvature formula step by step. In particular, w changes with x; treating it as constant would omit exactly the terms most easily lost in a hand implementation.

---
# 9. Measurements and coverage are different axes

The S122 regression illustrates the central evidence problem. All 219 measured G4 samples pass, but the knot audit reports 398 of 477 rows without the required two-sided flank evidence. The numerical observation and the completeness of the requested evidence are both true and must both remain visible.

| Measured G4 state | Independent coverage evidence | User-facing result |
| PASS | Explicitly incomplete audit | G4 UNKNOWN - INCOMPLETE COVERAGE |
| PASS | No independently identified audit gap | G4 SAMPLED PASS |
| FAIL | Complete or incomplete audit | G4 FAIL |
| Unavailable or blocked | Any coverage | G4 UNKNOWN |

An observed reliable failure is a counterexample. Missing other samples cannot erase it. Conversely, a collection of passes cannot supply derivatives at locations that were never measured.

`SamplingCapReached` is retained as a diagnostic. A cap alone is not proof of missing required knot evidence, even when knot auditing is enabled. Explicit states such as partial flanks, unprobeable flanks, incomplete mapping, unresolved overlap, or incomplete flank jets drive the UNKNOWN coverage override.

The table, selected-seam details, CSV, and viewport consume this separation. All-pass G4 with explicit incomplete coverage must not receive the confirmed green grade. Raw sample counts and raw sampled states remain available. A sampled pass is still finite evidence when the planned audit completes.

---
# 10. Knots, flanks, and unsampled intervals

A NURBS surface is piecewise rational polynomial. Repeated knots can reduce derivative continuity. A degree-p spline with an interior knot of multiplicity r has a generic guaranteed continuity of C^(p-r), though special coefficients may yield greater smoothness. Degree alone does not prove existence of a unique fourth jet at every knot.

The current evaluator conservatively asks Rhino for Cinfinity continuity at the station before accepting a native fourth-derivative evaluation. That guard can reject cases that are sufficiently smooth through order four. It is an availability policy, not a theorem that all such knots are defective.

The sampler adds mapped knot-flank probes and may refine intervals adaptively. The audit checks the parameters actually measured and whether their graph jets were usable. `BOTH` means that both requested probes were observed; it does not certify the exact knot or every point between them.

Even a complete planned audit leaves unsampled intervals. A sufficiently narrow defect can lie between all measured stations. Adaptive refinement may improve evidence where observed values change, but cannot exclude every unseen feature without further mathematical bounds.

> Whole-seam certification would require additional work such as rigorous parameter correspondence and interval bounds on every relevant span, with explicit treatment of trims, knots, singularities and numerical error. That is not an alpha capability.

---
# 11. Positive and negative analytic references

The flat-baseline reference family compares a flat patch to a graph with an added monomial term. Both use matching physical x,y coordinates at the seam x=0.

$$\Delta h(x,y)=0.1x^m(1+0.5y).$$

Every mixed derivative of total order less than m vanishes on x=0. The pure mth across-seam derivative is:

$$\partial_x^m\Delta h(0,y)=0.1m!(1+0.5y).$$

For m=4, third jets agree but the fourth jet does not. At y=0,0.25,0.5,0.8,1 the mismatch is 2.4,2.7,3.0,3.36,3.6. For m=5, all fourth-order differences vanish on the seam while the fifth-order difference does not. These are constructed negative and positive G4 cases, not merely two examples that happen to look smooth.

The supplied four-Brep model adds a shared nonflat base F(x,y). It preserves the exact contact-order argument, because both sides share F. However, a derivative measured in its tilted tangent graph frame need not have the same scalar value as the world-vertical derivative above. The order of exact contact is coordinate-independent under a regular chart; an individual derivative component is not.

Finite tolerances can classify a sufficiently small nonzero mismatch as passing. Reference amplitudes, model units, and tolerances must therefore be recorded together.

---
# 12. What parameter independence does and does not mean

For a smooth invertible change of surface coordinates Phi, the maps S and S composed with Phi describe the same local surface. Recovering the height graph over fixed physical axes removes that coordinate choice in exact arithmetic: both maps lead to the same h(x,y).

$$\widetilde{\mathbf{S}}=\mathbf{S}\circ\Phi,\qquad \det D\Phi\ne0.$$

This is why the algorithm inverts the planar projection instead of comparing raw UV partials. Appendix A works through a nonlinear example where raw C4 derivatives differ but the recovered graph jet agrees.

The tested Rhino references cover affine parameter-domain changes, direction reversal, and a particular nonlinear regular map. They do not exhaust every NURBS representation or every badly conditioned reparametrization. Floating-point roundoff, native derivative accuracy, and the graph determinant guard still matter.

Signed reversal of a common axis changes matching derivative components by matching parity signs, which the symmetric absolute/relative comparison handles consistently. A general in-plane rotation mixes components. Exact equality is preserved, but a componentwise nonzero tolerance box is not a fully rotation-invariant tensor norm. Near-threshold decisions can therefore depend on the chosen frame. Changing which approximately tangent face supplies the frame can also matter numerically.

> The defensible claim is sampled graph-jet analysis in a shared physical frame, with tested parameter changes. Do not advertise an unrestricted, exact, representation-independent G4 certificate.

---
# 13. A human developer's source map

The following tags are searchable comments in the source. They connect an implementation decision to the mathematical explanation instead of restating obvious assignments.

| Tag | Main source | Read it to understand |
| #SC-MATH-01 to 07 | SurfaceGraphJetEvaluator.cs | Jet layout, coefficients, inverse recurrence |
| #SC-MATH-08 to 09 | SectionJetEvaluator.cs | Directional section diagnostic and arc length |
| #SC-MATH-10 to 12 | ContinuityEvaluator.cs | Prerequisites, mixed terms, shared frame |
| #SC-MATH-13 | BoundaryContinuityEvaluator.cs | Topology versus measured geometry |
| #SC-NUM-01 | ContinuityEvaluator.cs | Explicit tolerances and near-zero values |
| #SC-NUM-02 | BoundaryContinuityEvaluator.cs | Stable unoriented normal angles |
| #SC-EVIDENCE-01 to 03 | Reports / KnotCoverageAudit | Raw observations versus coverage |
| #SC-UI-01 to 03 | Grade / CSV / Conduit | Display and export semantics |
| #SC-REF-01 to 03 | Reference generator | Exact basis conversion and file isolation |

Read `BoundaryContinuityEvaluator` before changing station correspondence. Read `KnotCoverageAudit` before changing UNKNOWN semantics. Read `ContinuityGrade` before changing the hybrid display ladder. The CSV must preserve the distinction between a per-sample grade and an overall coverage-aware seam interpretation.

For a change to the numerical method, first create an analytic counterexample that would detect the proposed mistake. For a display change, construct controlled seam-report objects with passing, failing, and missing evidence. These tests answer different questions and should not be conflated.

Comments should remain synchronized with behavior. If the policy changes, update the relevant tag, this handbook, regressions, and README together. A long explanation attached to obsolete behavior is more misleading than a short honest limitation.

---
# Appendix A1. Deriving the inverse recurrence

Let U(x,y) and V(x,y) be formal power series with zero constant term. Let [x^i y^j] select a coefficient. Multiplication is ordinary convolution of coefficients, because the factorial normalization has already been performed.

$$[x^iy^j](AB)=\sum_{r=0}^{i}\sum_{s=0}^{j}A_{rs}B_{i-r,j-s}.$$

Separate the linear part of the projected surface from its higher-degree terms. Suppose every inverse coefficient below total degree n is known. For a monomial U^a V^b with a+b at least two, a term containing an unknown degree-n inverse coefficient also contains another positive-degree factor. Its total degree exceeds n. It cannot contribute to the degree-n coefficient currently being solved.

Consequently R_ij, the nonlinear degree-n contribution, is known from previous degrees. Define eX as 1 only for (i,j)=(1,0), and eY as 1 only for (0,1). With D=a_x b_y-b_x a_y, elementary elimination gives:

$$U_{ij}=\frac{(e_X-R_x)b_y-b_x(e_Y-R_y)}{D},$$
$$V_{ij}=\frac{a_x(e_Y-R_y)-(e_X-R_x)a_y}{D}.$$

The corresponding height coefficient is z dotted with R_ij+a U_ij+b V_ij. Multiplication by i!j! gives the output derivative. This proves the recurrence by induction from degree one through four. In exact arithmetic it recovers the fourth Taylor jet of the local graph whenever the required derivatives and the inverse exist.

The proof explains the code's small fixed arrays. Terms above degree four cannot affect a lower coefficient, so truncation is algebraically exact for the requested jet, before floating-point error is considered.

---
# Appendix A2. A nonlinear worked example

Take the same physical surface z=x^2, extruded along y. One parametrization is S(u,v)=(u,v,u^2). Another uses u=t+a t^2, with a fixed nonzero constant a. At t=0 its planar derivative is 1, so the change is locally regular.

$$\widetilde{\mathbf{S}}(t,v)=(t+at^2,v,t^2+2at^3+a^2t^4).$$

The original fourth U derivative of height is zero; the new fourth t derivative is 24a^2. A raw C4 comparison under t=u would fail even though these maps describe the same physical surface.

Solve x=t+a t^2 by seeking t=x+b2 x^2+b3 x^3+b4 x^4. Equating coefficients gives b2+a=0; b3+2a b2=0; and b4+a(2b3+b2^2)=0. Hence:

$$t=x-ax^2+2a^2x^3-5a^3x^4+O(x^5).$$

Now substitute into height. Through degree four, t^2=x^2-2a x^3+5a^2 x^4; 2a t^3=2a x^3-6a^2 x^4; and a^2 t^4=a^2 x^4. Cubic and quartic terms cancel:

$$h(x,y)=x^2+(-2a+2a)x^3+(5-6+1)a^2x^4+O(x^5)=x^2+O(x^5).$$

Thus hxx=2, hxxx=0, hxxxx=0 in both descriptions; all mixed y derivatives vanish. This is the concrete reason the inverse computation is necessary. Agreement did not come from ignoring the extra derivatives, but from transforming them into the same physical coordinates.

---
# Appendix B. Deriving the section formulas

For the plane graph c(x)=(x,h(x)), the speed is sqrt(1+p^2), where p=h'. With q=h'', signed curvature is q/(1+p^2)^(3/2). Set w=1+p^2 and note w'=2pq.

$$\frac{d}{ds}=w^{-1/2}\frac{d}{dx},\qquad\kappa=qw^{-3/2}.$$

Differentiate once. The product rule gives r w^-3/2 minus 3p q^2 w^-5/2 in the x derivative. Multiplying by w^-1/2 yields:

$$\kappa_s=rw^{-2}-3pq^2w^{-3}.$$

Differentiate this expression in x before multiplying by w^-1/2 again. The first term contributes t w^-2-4pqr w^-3. For the second, the derivative of pq^2 is q^3+2pqr, while differentiating w^-3 contributes +18p^2q^3 w^-4. Combining terms gives:

$$\frac{d\kappa_s}{dx}=tw^{-2}-(10pqr+3q^3)w^{-3}+18p^2q^3w^{-4}.$$

$$\kappa_{ss}=tw^{-5/2}-(10pqr+3q^3)w^{-7/2}+18p^2q^3w^{-9/2}.$$

Two sanity checks help a maintainer. A straight line has q=r=t=0, so all three quantities vanish. At a horizontal tangent p=0, kappa=q and kappa_s=r, but kappa_ss=t-3q^3: a horizontal tangent does not justify replacing the second arclength curvature derivative by the fourth height derivative alone.

---
# Appendix C. Why the NURBS references are exact

The creation script does not fit a cloud of sampled points. It converts a known polynomial to a tensor-product Bernstein representation and stores the resulting control points as an unweighted degree-(5,2) NURBS surface.

For degree n, define the Bernstein basis B_i^n(t). A monomial of degree k at most n has the exact polynomial identity:

$$B_i^n(t)=\binom{n}{i}t^i(1-t)^{n-i},\qquad t^k=\sum_{i=k}^{n}\frac{\binom{i}{k}}{\binom{n}{k}}B_i^n(t).$$

To derive it, multiply the coefficient ratio by the basis's binomial factor. The identity binom(n,i) binom(i,k)=binom(n,k) binom(n-k,i-k) reduces the sum to t^k times the binomial expansion of (t+(1-t))^(n-k), which is 1.

If a polynomial is a sum of power-basis coefficients A_kl t^k s^l, apply that identity independently in each direction. Its control coefficient is:

$$C_{ij}=\sum_{k\le i,\,l\le j}A_{kl}\frac{\binom{i}{k}}{\binom{n}{k}}\frac{\binom{j}{l}}{\binom{m}{l}}.$$

The left patch first substitutes x=-1+t; the right uses x=t. This substitution must happen before converting the height polynomial. Simply sampling polynomial values at control-point indices would generally not produce the intended polynomial surface.

With unit weights, the rational denominator is 1 and the representation is polynomial. Floating-point storage and Brep joining still introduce numerical considerations. Exact here means exact polynomial construction in real arithmetic, not exact rational arithmetic inside Rhino or automatic validation of the exported model's measured UI results.

---
# Appendix D. What the tests actually establish

The historical Rhino 8 reference run r004 passed 180 tests and produced 19 fixture CSVs. Its flat-baseline monomials separated the m=4 G3-only case from the m=5 G4 case at five stations. Affine domain changes, direction reversal, and one nonlinear regular map were included. The recorded tolerances and observed values are retained in the validation directory.

A subsequent native run tested the Alpha 0.1.1 production C# files in the isolated harness: 180 passed, zero failed, 19 fixture exports. The test identity and project differ from the distributed release binary. The coverage-honesty executable separately tests constructed report states against the release binary; these assertions are not silently counted as part of the 180.

A later cap-only coverage-policy correction passes 21 standalone assertions, including complete enabled-audit cases. Its final native rerun was deferred before Rhino launch because ACC was active. The earlier native pass must not be presented as a native pass of that later revision. Consult the included validation records for the current release-candidate status.

The analytic references have two layers of evidence. Their formulas give exact contact-order statements along x=0 in mathematics. The software's finite measurements show that this implementation reproduces the intended outcomes at the tested stations and settings. The former is a proof about these specific formulas; the latter is a regression test of software behavior.

> Do not extrapolate either result to an arbitrary imported automotive seam. Neither an all-pass finite sample list nor a completed flank plan proves an interval bound between samples.

For the four-Brep model, retain the generator, model, tolerances, and actual exported measurements together. The initial zero-absolute-tolerance CSV showed roundoff-scale false negatives. Reclassifying its numbers after the fact is diagnostic work, not a new native plug-in run. Fresh measurements with the intended explicit tolerances must be labeled as such.

---
# Appendix E. Debugging S122 without hiding evidence

Start with raw observations. Inspect the measured GraphG4 states and the number of passes. If 219 of 219 measured points pass, preserve that observation. Next inspect `KnotAuditStatus`, not just a budget flag. A partial-flank state with missing required probes is independently incomplete evidence.

The display path must then produce `G4 UNKNOWN - INCOMPLETE COVERAGE` while keeping the raw PASS state and count in diagnostics. The actual UI uses an em dash in that label; this handbook uses an ASCII hyphen for portable typesetting. The viewport must use a neutral treatment for the uncertain overall G4 grade.

Now change one trustworthy measured point to G4 FAIL in a controlled regression. The overall G4 state must become FAIL even though the coverage is still incomplete. This checks that an UNKNOWN override cannot bury a real counterexample.

Finally, keep all measured points passing and set only the sampling-cap flag. With no independently incomplete audit state, the cap must remain a diagnostic and must not alone force UNKNOWN. Test this both with auditing disabled and with a complete enabled audit, including the no-mapped-interior-knot case.

G0-G2 classification is not part of this coverage correction. Preserve those results unless a separate defect is demonstrated. Keep table text, selected details, CSV columns, grade colors, and raw statistics synchronized; changing only the legend would leave conflicting conclusions in exported data.

---
# References and maintenance glossary

**[1] McNeel, RhinoCommon Surface.Evaluate.** Primary API documentation for native partial derivative ordering. The source explicitly follows its total-degree ordering. Accessed 2026-10-16.

https://developer.rhino3d.com/api/rhinocommon/rhino.geometry.surface/evaluate

**[2] MIT OpenCourseWare, Seminar in Geometry, Chapter 4: Implicit Function Theorem.** Background for the local regular-graph principle. The series recurrence and worked examples in this handbook are derived here to explain this implementation; the external notes do not certify the plug-in. Accessed 2026-10-16.

https://ocw.mit.edu/courses/18-994-seminar-in-geometry-fall-2004/pages/lecture-notes/

**Primary implementation evidence.** `SurfaceGraphJetEvaluator.cs`, `SectionJetEvaluator.cs`, `ContinuityEvaluator.cs`, `BoundaryContinuityEvaluator.cs`, `ContinuityReports.cs`, `KnotCoverageAudit.cs`, `ContinuityGrade.cs`, and the included reference generator. Validation scope and station values are in the release's `validation` directory.

| Term | Meaning in this alpha |
| Station | One evaluated edge location and its mapped face parameters |
| Jet | Derivatives at a point through a specified total order |
| Regular chart | Local coordinates with an invertible relevant Jacobian |
| C4 | Fourth-order parametric agreement under a specified map |
| Sampled G4 | Fourth-order graph-contact evidence at measured stations |
| Coverage | Availability of the required planned observations |
| UNKNOWN | Insufficient evidence, not a demonstrated shape failure |
| Certificate | A proof over all relevant points; not supplied here |

**Maintainer's final question:** does this change alter a number, its availability, its tolerance, or the strength of the claim shown to the user? Identify that layer first, then choose the corresponding reference or regression.
