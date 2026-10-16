# High-order near-zero tolerance correction

Alpha 0.1.1 now starts with K2Abs=0 (unchanged), K3Abs=1e-10 and K4Abs=1e-10. These are explicit configurable tolerances in inverse model units squared and cubed, not certified error bounds across all geometry scales and units. The classifier remains `difference <= max(absolute, relative * max(abs(A),abs(B)))`. There is no hidden epsilon, rounded measurement, or automatic acceptance of unavailable derivatives.

Legacy v1 document settings cannot distinguish a default zero from an intentionally saved zero. Their high-order zeros are upgraded with a command-line notice; nonzero values and G0-G2 settings are retained. Users may explicitly choose zero again. The new v2 format preserves that deliberate zero and warns about relative-only comparison near zero. This update does not rewrite modeling files on disk; document metadata follows the existing settings/save behavior.

The reported endpoint had third-order values 4.1813219553432716e-15 and -4.5607961851601427e-16, a difference of 4.6374015738592855e-15. The explicit default accepts this analytically matching endpoint, but still rejects reference third-order difference 0.6 and fourth-order difference 2.4. CSV measurements are never replaced by zero.

## Validation on 2026-10-17

- Standalone coverage/default/migration tests: **40/40 assertions PASS** against the shipped binary.
- Optional original four-seam CSV replay: **591/591 assertions PASS**, including 546 checks that G0/G1/section G2 remain unchanged and five aggregate checks. This reclassifies existing measurements; it is not a new geometry evaluation. Results: G4 / G3 / G2 / G4; S2 retains G4 FAIL and S3 retains G3 FAIL.
- Existing isolated native Rhino 8 harness: **180/180 PASS**, 19 fixture CSVs, build 0 warnings/errors. Private run: `alpha011-numeric-defaults-20261017-005147`.
- Separate actual release-binary Rhino 8 run: defaults read back as **[0, 1e-10, 1e-10]**, without explicit absolute-tolerance command overrides. S1: G4 PASS 121/121; S2: G3 PASS, G4 FAIL; S3: G3 FAIL, cumulative G4 BLOCKED; S4: G4 PASS 121/121. Private run: `alpha011-defaults-20261017-005222`. A temporary bridge invoked the actual release analyzer. There was no manual UI interaction in this second run.
- Both test Rhino sessions exited normally. RhinoActive was clear and the exclusive mutex available afterwards. Original user CSVs and modeling files were untouched.

Current binary SHA-256: `FFCABA93CA1EABC2DB99F25BACB7AF2F11B6AD6AC7045D437D7C35CE5C71D377`.

Run `dotnet run --project validation/CoverageHonestyRegression.csproj` for the 40 self-contained assertions. Optionally append `-- <path-to-original-Continuity_samples_4.csv>` to replay the original Finnish-decimal, semicolon-separated CSV. The private input CSV is not distributed. These results supersede earlier binary hashes; earlier manual UI observations apply to that earlier binary. An installed old plug-in does not update itself when a candidate is built.

Build limitation: the release/standalone builds reported NU1900 because NuGet vulnerability metadata was unreachable; compilation succeeded. The native harness build had no warnings. This does not constitute a completed dependency vulnerability audit.
