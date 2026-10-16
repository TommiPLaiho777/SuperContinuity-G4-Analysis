using System;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    public static class ContinuityEvaluator
    {
        /// <summary>Independent measurements; overall classification retains missing-data information.</summary>
        public static void Classify(SampleReportV9 sample, double gapTolerance, double angleTolerance,
            double[] relativeTolerances, double[] absoluteTolerances)
        {
            sample.G0Pass = sample.G0Valid && sample.Gap <= gapTolerance;
            sample.G1Pass = sample.G0Pass && sample.G1Valid && sample.AngleDegrees <= angleTolerance;
            sample.G0State = !sample.G0Valid ? "INCOMPLETE" : sample.G0Pass ? "PASS" : "FAIL";
            sample.G1State = !sample.G1Valid ? "INCOMPLETE" : sample.AngleDegrees > angleTolerance ? "FAIL" :
                !sample.G0Valid ? "INCOMPLETE" : !sample.G0Pass ? "BLOCKED" : "PASS";
            if (sample.Valid)
                for (int order = 0; order < 3; order++)
                    sample.Pass[order] = CheckContinuity(sample.A[order], sample.B[order],
                        relativeTolerances[order], absoluteTolerances[order]);
            sample.KState = !sample.Valid ? "INCOMPLETE" : !sample.Pass[0] ? "FAIL" :
                !sample.Pass[1] || !sample.Pass[2] ? "WARNING" : "PASS";

            // This *sectional* G2 curvature test is comparable in scope to a cross-edge
            // curvature analysis, though it does not duplicate Rhino's exact metric.
            // In contrast to KState it does not require K3/K4. GraphJet is reported separately.
            sample.SectionG2State = !sample.G0Valid || !sample.G1Valid || !sample.Valid ? "INCOMPLETE" :
                !sample.G0Pass || !sample.G1Pass ? "BLOCKED" :
                sample.Pass[0] ? "PASS" : "FAIL";

            // Graph jet order 1 is equivalent to the tangent-plane prerequisite (G1).
            // Compare every independent coefficient at orders 2, 3 and 4, not just
            // one normal cross-section. No claim is made about unsampled locations.
            if (sample.GraphValid)
            {
                sample.GraphPass = new bool[3] { true,true,true };
                int index=2;
                for (int order=0;order<3;order++)
                    for (int j=0;j<order+3;j++,index++)
                        if (!CheckContinuity(sample.GraphA[index],sample.GraphB[index],
                            relativeTolerances[order],absoluteTolerances[order]))
                            sample.GraphPass[order]=false;
                if (!sample.G0Valid || !sample.G1Valid)
                    sample.GraphOrderState = new [] {"INCOMPLETE", "INCOMPLETE", "INCOMPLETE"};
                else if (!sample.G0Pass || !sample.G1Pass)
                    sample.GraphOrderState = new [] {"BLOCKED", "BLOCKED", "BLOCKED"};
                else
                {
                    sample.GraphOrderState = new string[3];
                    for (int order=0;order<3;order++)
                        sample.GraphOrderState[order] = order>0 && sample.GraphOrderState[order-1]!="PASS"
                            ? "BLOCKED" : sample.GraphPass[order] ? "PASS" : "FAIL";
                }
                sample.GraphState = !sample.G0Valid || !sample.G1Valid ? "INCOMPLETE" :
                    !sample.G0Pass || !sample.G1Pass ? "BLOCKED" :
                    !sample.GraphPass[0] ? "FAIL" :
                    !sample.GraphPass[1] || !sample.GraphPass[2] ? "WARNING" : "PASS";
            }
            else
            {
                sample.GraphPass = null; sample.GraphState = "INCOMPLETE";
                sample.GraphOrderState = new [] {"INCOMPLETE", "INCOMPLETE", "INCOMPLETE"};
            }

            if (sample.G0State == "FAIL") { sample.Status="RED"; sample.FirstIssue="G0"; }
            else if (sample.G1State == "FAIL") { sample.Status="RED"; sample.FirstIssue="G1"; }
            // OVERVIEW is anchored to boundary continuity and section K2 only.
            // G3/G4 and strict Graph jets are valuable independent diagnostics,
            // but are not automatic assessments of the physical model's quality.
            else if (!sample.G0Valid || !sample.G1Valid || !sample.Valid)
            { sample.Status="UNAVAILABLE"; sample.FirstIssue="INCOMPLETE"; }
            else if (!sample.Pass[0]) { sample.Status="ORANGE"; sample.FirstIssue="K2"; }
            else { sample.Status="GREEN"; sample.FirstIssue="PASS"; }
        }

        /// <summary>
        /// |A-B| <= max(absolute tolerance, relative tolerance * max(|A|,|B|)).
        /// No hidden noise floor, rounding, small-value zeroing or automatic acceptance.
        /// Absolute tolerance has the same units as the measured order.
        /// </summary>
        public static bool CheckContinuity(double a, double b, double relative, double absolute)
        {
            if (!SampleReportV9.Finite(a) || !SampleReportV9.Finite(b) ||
                !SampleReportV9.Finite(relative) || !SampleReportV9.Finite(absolute) || relative < 0 || absolute < 0)
                return false;
            double limit = AllowedDifference(a,b,relative,absolute);
            return Math.Abs(a-b) <= limit;
        }

        // The acceptance policy is a union of two explicit criteria, not the sum of
        // allowances. Near-zero K2 values can be assessed with the absolute limit
        // without silently declaring a large relative mismatch to be zero.
        public static string AcceptanceBasis(double a, double b, double relative, double absolute)
        {
            if (!SampleReportV9.Finite(a) || !SampleReportV9.Finite(b) ||
                !SampleReportV9.Finite(relative) || !SampleReportV9.Finite(absolute) ||
                relative < 0 || absolute < 0) return "UNAVAILABLE";
            double diff = Math.Abs(a-b);
            bool byAbsolute = diff <= absolute;
            bool byRelative = diff <= relative * Math.Max(Math.Abs(a),Math.Abs(b));
            return byAbsolute && byRelative ? "BOTH" : byAbsolute ? "ABSOLUTE" :
                byRelative ? "RELATIVE" : "FAIL";
        }

        /// <summary>The displayed and enforced allowance are identical, in the measured order's units.</summary>
        public static double AllowedDifference(double a, double b, double relative, double absolute)
        { return Math.Max(absolute, relative*Math.Max(Math.Abs(a),Math.Abs(b))); }

        public static double ToleranceRatio(double a, double b, double relative, double absolute)
        {
            double difference = Math.Abs(a-b);
            double limit = AllowedDifference(a,b,relative,absolute);
            return limit > 0 ? difference/limit : difference == 0 ? 0 : double.PositiveInfinity;
        }

        public static void Evaluate(BrepEdge edge, BrepFace faceA, BrepFace faceB,
            double t, double spikeLength, SampleReportV9 report)
        {
            report.Method = "SurfaceJet4-v2";
            report.Spike = new Line(report.Point, Vector3d.ZAxis, spikeLength);
            if (!report.TrimA.IsValid || !report.TrimB.IsValid)
            { report.Reason = "Paired trim coordinates are unavailable."; report.GraphReason=report.Reason; return; }
            Vector3d z = faceA.NormalAt(report.TrimA.X,report.TrimA.Y);
            if (faceA.OrientationIsReversed) z.Reverse();
            Vector3d x = Vector3d.CrossProduct(edge.TangentAt(t), z);
            if (!z.Unitize() || !x.Unitize())
            { report.Reason = "Singular normal or seam tangent."; report.GraphReason=report.Reason; return; }
            report.Spike = new Line(report.Point, z, spikeLength);
            Vector3d y = Vector3d.CrossProduct(z, x);
            if (y.Unitize())
            {
                double[] graphA, graphB;
                string whyA, whyB;
                bool okA = SurfaceGraphJetEvaluator.Evaluate(faceA,report.TrimA,x,y,z,out graphA,out whyA);
                bool okB = SurfaceGraphJetEvaluator.Evaluate(faceB,report.TrimB,x,y,z,out graphB,out whyB);
                if (okA && okB) { report.GraphA=graphA; report.GraphB=graphB; report.GraphReason=""; }
                else report.GraphReason=(!okA ? "A: "+whyA+" " : "") + (!okB ? "B: "+whyB : "");
            }
            double[] a, b;
            string reasonA, reasonB;
            bool validA = SectionJetEvaluator.Evaluate(faceA,report.TrimA,x,z,out a,out reasonA);
            bool validB = SectionJetEvaluator.Evaluate(faceB,report.TrimB,x,z,out b,out reasonB);
            if (!validA || !validB)
            { report.Reason = (!validA ? "Face A: "+reasonA+" " : "") + (!validB ? "Face B: "+reasonB : ""); return; }
            report.A = a;
            report.B = b;
            report.Pass = new bool[3];
            report.Reason = "";
        }
    }
}
