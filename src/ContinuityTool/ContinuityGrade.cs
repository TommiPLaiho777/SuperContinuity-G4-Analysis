using System;
using System.Collections.Generic;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Viewport diagnostics ONLY: the highest measured grade shared by the considered
    /// samples. G2 here uses the sectional K2 test, while G3/G4 require the independent
    /// cumulative full-graph-jet tests. This hybrid display is NOT a mathematical
    /// full-seam G2/G3/G4 certificate, nor equivalent to Rhino EdgeContinuity.
    /// </summary>
    public static class ContinuityGrade
    {
        // #SC-UI-01 THE DISPLAY LADDER IS NOT A NEW MATHEMATICAL METRIC.
        // This class summarizes existing point observations. Its G2 step is the
        // directional section check; its G3/G4 steps require cumulative graph jets.
        // SeamReportV9.DisplayGrade adds the independent coverage override. UI code
        // must use that property for the overall seam, while raw per-sample grades
        // remain available for diagnostics. Changing this ladder must not rewrite
        // G0-G2 observations merely to hide an incomplete high-order result.
        // -2: insufficient G0 measurement; -1: below G0; 0..4: sampled grade.
        public const int Unknown = -2;
        public const int BelowG0 = -1;

        public static int Of(SampleReportV9 s)
        {
            if (s == null || s.G0State == "INCOMPLETE") return Unknown;
            if (s.G0State != "PASS") return BelowG0;
            if (s.G1State != "PASS") return 0;
            if (s.SectionG2State != "PASS") return 1;
            // Do not equate the Section G2 result with the stronger GraphJet G2;
            // use the graph's existing G2 prerequisite for higher grades.
            if (s.GraphOrderState == null || s.GraphOrderState.Length < 3 ||
                s.GraphOrderState[0] != "PASS" || s.GraphOrderState[1] != "PASS") return 2;
            if (s.GraphOrderState[2] != "PASS") return 3;
            return 4;
        }

        public static int Of(SeamReportV9 seam)
        {
            if (seam == null || seam.Samples.Count == 0) return Unknown;
            int grade = 4;
            foreach (var sample in seam.Samples)
            {
                int here = Of(sample);
                if (here == Unknown) return Unknown; // Never turn unavailable G0 into a red failure.
                grade = Math.Min(grade, here);
            }
            return grade;
        }

        public static string Name(int grade)
        {
            switch (grade)
            {
                case 4: return "G4 sampled (geometric jet)";
                case 3: return "G3 sampled (geometric jet)";
                case 2: return "G2 (section)";
                case 1: return "G1";
                case 0: return "G0";
                case BelowG0: return "BELOW G0";
                default: return "UNAVAILABLE";
            }
        }

        // The porcupine target is independent of the grade color. A missing
        // measurement is not evidence of a failed geometric continuity test.
        public static string AtTarget(SampleReportV9 s, int target)
        {
            if (s == null || !s.G0Valid) return "INCOMPLETE";
            if (!s.G0Pass) return "FAIL";
            if (target <= 0) return "PASS";
            if (!s.G1Valid) return "INCOMPLETE";
            if (!s.G1Pass) return "FAIL";
            if (target <= 1) return "PASS";
            if (!s.Valid) return "INCOMPLETE";
            if (s.SectionG2State == "FAIL") return "FAIL";
            if (s.SectionG2State != "PASS") return "INCOMPLETE";
            if (target <= 2) return "PASS";
            if (!s.GraphValid || s.GraphPass == null || s.GraphPass.Length < 3) return "INCOMPLETE";
            if (!s.GraphPass[0] || !s.GraphPass[1]) return "FAIL";
            if (target <= 3) return "PASS";
            return s.GraphPass[2] ? "PASS" : "FAIL";
        }

        public sealed class Stroke
        {
            public int Grade;
            public List<Point3d> Points;
        }

        // Group contiguous equal-grade intervals so Rhino draws few polylines
        // instead of issuing a DrawLine call for every sample interval.
        public static List<Stroke> Strokes(IList<SampleReportV9> samples)
        {
            var strokes = new List<Stroke>();
            if (samples == null || samples.Count < 2) return strokes;
            Stroke current = null;
            for (int i = 1; i < samples.Count; i++)
            {
                var a = samples[i-1]; var b = samples[i];
                if (!a.Point.IsValid || !b.Point.IsValid) { current = null; continue; }
                int grade = Math.Min(Of(a),Of(b));
                if (current == null || current.Grade != grade)
                {
                    current = new Stroke { Grade = grade, Points = new List<Point3d> { a.Point, b.Point } };
                    strokes.Add(current);
                }
                else current.Points.Add(b.Point);
            }
            return strokes;
        }
    }
}
