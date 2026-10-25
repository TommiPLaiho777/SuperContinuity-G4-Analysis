using System;
using System.Globalization;
using System.Linq;
using Rhino;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Rhino .3dm document strings, not session-wide static annotations. Saved with
    /// the document when the user saves it. Numeric settings are unit-scoped;
    /// role keys use the persistent Brep GUID, topology indices and a sampled
    /// geometric guard, NEVER the unstable S-number.
    /// </summary>
    internal static class ContinuityDocumentStore
    {
        private const string Section = "SuperContinuity.v732";
        private static readonly CultureInfo I = CultureInfo.InvariantCulture;
        private static readonly string[] Roles = { "Inspect", "G1 target", "Section G2 target", "Excluded", "Design edge" };
        private static string F(double x) { return x.ToString("R", I); }
        internal static string GeometryKey(SeamReportV9 seam)
        {
            if (seam == null || seam.EdgeCurve == null || !seam.EdgeCurve.IsValid) return null;
            var c = seam.EdgeCurve;
            var values = new System.Collections.Generic.List<string>();
            // Fractions of the native edge parameter, plus an orientation-independent
            // ordered coordinate set. Changed edges do not inherit a review role.
            for (int i = 0; i <= 8; i++)
            {
                var p = c.PointAt(c.Domain.ParameterAt(i / 8.0));
                values.Add(F(p.X) + ";" + F(p.Y) + ";" + F(p.Z));
            }
            string direct = string.Join("|", values);
            values.Reverse();
            string reverse = string.Join("|", values);
            string curveKey = string.CompareOrdinal(direct, reverse) <= 0 ? direct : reverse;
            int a = Math.Min(seam.FaceA, seam.FaceB), b = Math.Max(seam.FaceA, seam.FaceB);
            string serialized = seam.ObjectId.ToString("D") + ":" + seam.EdgeIndex + ":" + a + ":" + b + ":" + F(c.GetLength()) + ":" + curveKey;
            // Deterministic SHA256 prevents oversized per-edge document string keys.
            using (var sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(serialized))).Replace("-", "");
        }
        internal static void SaveRole(RhinoDoc doc, SeamReportV9 seam)
        {
            string key = GeometryKey(seam);
            if (doc == null || key == null || !Roles.Contains(seam.ReviewRole)) return;
            doc.Strings.SetString(Section, "Role." + key, seam.ReviewRole);
        }
        internal static void RestoreRole(RhinoDoc doc, SeamReportV9 seam)
        {
            string key = GeometryKey(seam);
            if (doc == null || key == null) return;
            var role = doc.Strings.GetValue(Section, "Role." + key);
            if (Roles.Contains(role)) seam.ReviewRole = role;
        }
        internal static string ModelUnitContext(RhinoDoc doc)
        {
            if (doc == null) return null;
            string unit = doc.ModelUnitSystem.ToString();
            if (doc.ModelUnitSystem != UnitSystem.CustomUnits) return unit;
            string name;
            double meters;
            if (!doc.GetCustomUnitSystem(true, out name, out meters) || name == null ||
                !SampleReportV9.Finite(meters) || meters <= 0) return null;
            // Length-prefix the name so punctuation in a custom name is unambiguous.
            return unit + ";" + name.Length.ToString(I) + ":" + name + ";" + F(meters);
        }
        internal static void SaveSettings(RhinoDoc doc)
        {
            if (doc == null) return;
            var s = ContinuitySettingsSnapshot.Capture();
            doc.Strings.SetString(Section, "Units", doc.ModelUnitSystem.ToString());
            string context = ModelUnitContext(doc);
            if (context == null) doc.Strings.Delete(Section, "UnitContext");
            else doc.Strings.SetString(Section, "UnitContext", context);
            doc.Strings.SetString(Section, "Settings", s.ToString());
        }
        internal static bool RestoreSettings(RhinoDoc doc)
        {
            if (doc == null) return false;
            if (doc.Strings.GetValue(Section, "Units") != doc.ModelUnitSystem.ToString()) return false;
            string context = ModelUnitContext(doc);
            string savedContext = doc.Strings.GetValue(Section, "UnitContext");
            // #SC-STATE-03 SAVED DIMENSIONAL LIMITS REQUIRE THE SAME UNIT DEFINITION.
            // Legacy ordinary units are identified by their enum. Legacy custom
            // settings lack a scale/name witness and cannot safely be restored.
            if (context == null || (string.IsNullOrEmpty(savedContext)
                ? doc.ModelUnitSystem == UnitSystem.CustomUnits
                : savedContext != context)) return false;
            string raw = doc.Strings.GetValue(Section, "Settings");
            bool restored = ContinuitySettingsSnapshot.TryRestore(raw);
            if (restored && raw.StartsWith("v1;", StringComparison.Ordinal))
                RhinoApp.WriteLine("SuperContinuity: legacy settings loaded. Zero K3Abs/K4Abs defaults are upgraded to explicit 1e-10; nonzero values and G2 are unchanged. Current K3Abs={0:R}, K4Abs={1:R}. You can explicitly select zero for relative-only comparison.", ContinuitySettings.absK3, ContinuitySettings.absK4);
            return restored;
        }
        internal static bool ValidRole(string role) { return Roles.Contains(role); }
    }

    internal sealed class ContinuitySettingsSnapshot
    {
        // Delimited invariant text; explicit schema and validation; no untrusted deserialization.
        private static readonly CultureInfo I = CultureInfo.InvariantCulture;
        internal static ContinuitySettingsSnapshot Capture() { return new ContinuitySettingsSnapshot(); }
        public override string ToString()
        {
            return string.Join(";", new [] { "v2", ContinuitySettings.strict ? "1" : "0",
                ContinuitySettings.customG2.ToString("R",I), ContinuitySettings.customG3.ToString("R",I),
                ContinuitySettings.customG4.ToString("R",I), ContinuitySettings.gapTolerance.ToString("R",I),
                ContinuitySettings.angleTolerance.ToString("R",I), ContinuitySettings.absK2.ToString("R",I),
                ContinuitySettings.absK3.ToString("R",I), ContinuitySettings.absK4.ToString("R",I),
                ContinuitySettings.sampleCount.ToString(I), ContinuitySettings.maximumSamples.ToString(I),
                ContinuitySettings.knotAware ? "1" : "0", ContinuitySettings.adaptive ? "1" : "0",
                ContinuitySettings.autoKnotBudget ? "1" : "0", ContinuitySettings.knotBudgetCap.ToString(I) });
        }
        internal static bool TryRestore(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            var p = raw.Split(';');
            if (p.Length != 16 || (p[0] != "v1" && p[0] != "v2")) return false;
            try
            {
                double[] d = new double[8];
                for (int i = 0; i < 8; i++)
                {
                    if (!double.TryParse(p[i + 2], NumberStyles.Float, I, out d[i]) ||
                        double.IsNaN(d[i]) || double.IsInfinity(d[i]) || d[i] < 0) return false;
                }
                int initial, maximum, knotCap;
                if (!int.TryParse(p[10], NumberStyles.Integer, I, out initial) ||
                    !int.TryParse(p[11], NumberStyles.Integer, I, out maximum) ||
                    !int.TryParse(p[15], NumberStyles.Integer, I, out knotCap) ||
                    initial < 3 || initial > 1000 || maximum < initial || maximum > 2000 ||
                    knotCap < maximum || knotCap > 10000 || d[0] > 50 || d[1] > 50 ||
                    d[2] > 50 || d[4] > 90) return false;
                if ((p[1] != "0" && p[1] != "1") ||
                    new[] { p[12],p[13],p[14] }.Any(x => x != "0" && x != "1")) return false;
                // #SC-NUM-04 VERSIONED LEGACY-ZERO MIGRATION.
                // v1 persisted zeros as defaults, even without an explicit user
                // choice. Migrate high-order zeros only; keep nonzero thresholds
                // and G2. v2 preserves deliberate zeros on every subsequent load.
                if (p[0] == "v1")
                {
                    if (d[6] == 0) d[6] = ContinuitySettings.DefaultHighOrderAbsoluteTolerance;
                    if (d[7] == 0) d[7] = ContinuitySettings.DefaultHighOrderAbsoluteTolerance;
                }
                ContinuitySettings.strict = p[1] == "1";
                ContinuitySettings.customG2 = d[0]; ContinuitySettings.customG3 = d[1]; ContinuitySettings.customG4 = d[2];
                ContinuitySettings.gapTolerance = d[3]; ContinuitySettings.angleTolerance = d[4];
                ContinuitySettings.absK2 = d[5]; ContinuitySettings.absK3 = d[6]; ContinuitySettings.absK4 = d[7];
                ContinuitySettings.sampleCount = initial; ContinuitySettings.maximumSamples = maximum;
                ContinuitySettings.knotAware = p[12] == "1"; ContinuitySettings.adaptive = p[13] == "1";
                ContinuitySettings.autoKnotBudget = p[14] == "1"; ContinuitySettings.knotBudgetCap = knotCap;
                return true;
            }
            catch { return false; }
        }
    }
}
