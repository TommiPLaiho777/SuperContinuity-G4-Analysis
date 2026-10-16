# Programmatic NURBS references

The plugin creates these non-rational tensor-product Bezier/NURBS patches in memory. They are test-only surfaces and do not read or modify existing model geometry.

For each pair, the left patch is planar and the right patch is

`z = 0.1 * x^m * (1 + 0.5*y)`, with the seam at `x = 0`, `0 <= y <= 1`.

- `m = 4`: the first three transverse geometric derivatives match. The fourth derivative differs by `2.4 * (1 + 0.5*y)`, so the fixture is G3 but not G4.
- `m = 5`: derivatives through order four match, so the reference is G4 (but not G5).

The Rhino 8 self-test checks five seam stations (`y = 0, 0.25, 0.5, 0.8, 1`), affine parameter domains `[0,1]` and `[-2,3]`, a reversed `[2,11]` U direction, and nonlinear regular reparameterizations `x = phi(s)`, where `phi(s) = s + 0.4*s*(1-s)`, in both U directions. The nonlinear fixture is represented exactly as a degree-`2m` Bezier/NURBS polynomial.

The `m=4` and `m=5` formulas have known analytic continuity by construction. The plugin's reported G4 grades are still based on finite stations and tolerance checks; the analyzer does not use the formula as an interval-wide certificate.
