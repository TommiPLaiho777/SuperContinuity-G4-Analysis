using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    public class SampleReportV9
    {
        public Line Spike;
        public Point3d TrimA = Point3d.Unset, TrimB = Point3d.Unset;
        public string Method = "SurfaceJet4-v2";
        public string G0State = "INCOMPLETE", G1State = "INCOMPLETE", KState = "INCOMPLETE", SectionG2State = "INCOMPLETE";
        public int Index;
        // Each measurement family retains independent availability and classification.
        public double Gap = double.NaN, AngleDegrees = double.NaN;
        public double MappingResidual = double.NaN, NormalAngleDegrees = double.NaN;
        // Corrected G0: maximum of point-to-opposite-actual-trim distances in both directions.
        // PairedSurfaceGap is the OLD native same-parameter 3D distance (diagnostic only).
        public double PairedSurfaceGap = double.NaN, G0AtoB = double.NaN, G0BtoA = double.NaN;
        public string G0Measurement = "NOT_MEASURED";
        public Point3d BoundaryA = Point3d.Unset, BoundaryB = Point3d.Unset;
        public bool NormalOpposed, G0Pass, G1Pass;
        // Common-frame full surface graph jets, including ALL mixed partial derivatives.
        public double[] GraphA, GraphB;
        public bool[] GraphPass;
        public string GraphReason = "Graph jet not evaluated", GraphState = "INCOMPLETE";
        // Each graph order is a CUMULATIVE continuity classification: G3 requires G2,
        // G4 requires G3. GraphPass holds the raw, order-specific comparison result.
        public string[] GraphOrderState = { "INCOMPLETE", "INCOMPLETE", "INCOMPLETE" };
        public double EdgeParameter = double.NaN;
        public string SampleSource = "Uniform";
        public bool GraphValid { get { return GraphA != null && GraphB != null && GraphA.Length == 14 && GraphB.Length == 14 && GraphA.All(Finite) && GraphB.All(Finite); } }
        public string BoundaryReason = "Boundary evaluation unavailable", FirstIssue = "INCOMPLETE";
        public bool G0Valid { get { return Finite(Gap); } }
        public bool G1Valid { get { return Finite(AngleDegrees); } }
        public bool Complete { get { return G0Valid && G1Valid && Valid && GraphValid; } }
        public static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        public double Fraction, Step = double.NaN;
        public Point3d Point;
        public string Status = "UNAVAILABLE", Reason = "Geometry evaluation unavailable";
        public double[] A, B;
        public bool[] Pass;
        public bool Valid { get { return A != null && B != null && Pass != null && A.Length == 3 && B.Length == 3 && Pass.Length == 3 && A.All(Finite) && B.All(Finite); } }
        public double Difference(int order) { return Valid ? Math.Abs(A[order] - B[order]) : double.NaN; }
    }

    public class SeamReportV9
    {
        public string Id;
        // Explicit user intent, never inferred from an angle or a failed test.
        public string ReviewRole = "Inspect";
        public bool ReviewExcluded { get { return ReviewRole == "Excluded" || ReviewRole == "Design edge"; } }
        public string ReviewState { get { return ReviewExcluded ? "EXCLUDED" :
            ReviewRole == "G1 target" ? G1State : ReviewRole == "Section G2 target" ? SectionG2 : "UNASSIGNED"; } }
        public Guid ObjectId;
        public int EdgeIndex, FaceA, FaceB;
        // Sampling coverage diagnostics, never a numerical continuity certificate.
        public int KnotCrossings;
        public int KnotEstimatedMinimum, KnotEffectiveBudget;
        public bool KnotOverlap, KnotMappingIncomplete, SamplingCapReached;
        public int AdaptiveSamples;
        public bool KnotAuditEnabled;
        public readonly List<KnotCoverageAudit.Row> KnotAuditRows = new List<KnotCoverageAudit.Row>();
        public string KnotAuditState { get { return KnotCoverageAudit.Status(KnotAuditEnabled,
            KnotOverlap,KnotMappingIncomplete,KnotAuditRows); } }
        public string SamplingNote { get { return
            (KnotOverlap ? "Trim overlaps a knot line. " : "") +
            (KnotMappingIncomplete ? "Knot mapping incomplete. " : "") +
            (SamplingCapReached ? "Sampling budget limits added knot/adaptive points (not a geometry FAIL). " : "") +
            KnotCoverageAudit.Summary(this) +
            "SAMPLED_ONLY: unsampled intervals have no certified error bound."; } }
        // Generated lazily for the G0-G4 viewport; refreshed when tolerances change.
        private List<ContinuityGrade.Stroke> gradeStrokes;
        public List<ContinuityGrade.Stroke> GradeStrokes
        { get { return gradeStrokes ?? (gradeStrokes = ContinuityGrade.Strokes(Samples)); } }
        public void InvalidateGradeStrokes() { gradeStrokes = null; }
        public int SampledGrade { get { return ContinuityGrade.Of(this); } }
        public string SampledGradeName { get { return ContinuityGrade.Name(SampledGrade); } }
        public Point3d Anchor;
        public Curve EdgeCurve;
        public List<SampleReportV9> Samples = new List<SampleReportV9>();
        public int G0Count { get { return Samples.Count(x => x.G0Valid); } }
        public int G1Count { get { return Samples.Count(x => x.G1Valid); } }
        public int CompleteCount { get { return Samples.Count(x => x.Complete); } }
        public int GraphValidCount { get { return Samples.Count(x => x.GraphValid); } }
        public string GraphState
        {
            get
            {
                string observed = Aggregate(Samples.Select(x => x.GraphState));
                return observed; // Sampled grade; never a claim about unsampled portions.
            }
        }
        public string GraphOrder(int order)
        {
            string observed = Aggregate(Samples.Select(x => x.GraphOrderState[order]));
            // Budget / mapping limitations belong in coverage diagnostics, not
            // in observed sampled continuity. A sampled PASS remains sampled-only.
            return observed;
        }
        /// <summary>Cross-seam sectional second-curvature-derivative diagnostic, separately from GraphJet.
        /// A sample pass is not a Rhino EdgeContinuity result or a whole-edge proof.</summary>
        public string SectionG2 { get { return Aggregate(Samples.Select(x => x.SectionG2State)); } }
        public int SectionG2PassedSamples { get { return Samples.Count(x => x.SectionG2State == "PASS"); } }
        // Raw angle/curvature checks ignore G0/G1 prerequisite gates for diagnosis only.
        // Do not use these counters as standalone geometric continuity certification.
        public int RawG1AnglePassedSamples(double angleTolerance) { return Samples.Count(x => x.G1Valid && x.AngleDegrees <= angleTolerance); }
        public int RawK2PassedSamples { get { return Samples.Count(x => x.Valid && x.Pass[0]); } }
        public int GraphOrderPassedSamples(int order)
        { return Samples.Count(x => x.GraphOrderState[order] == "PASS"); }
        public double GapMaximum { get { return BoundaryStatistic(x => x.Gap, true); } }
        public double PairedGapMaximum { get { return BoundaryStatistic(x=>x.PairedSurfaceGap,true); } }
        public double G0AtoBMaximum { get { return BoundaryStatistic(x=>x.G0AtoB,true); } }
        public double G0BtoAMaximum { get { return BoundaryStatistic(x=>x.G0BtoA,true); } }
        public double GapMean { get { return BoundaryStatistic(x => x.Gap, false); } }
        public double AngleMaximum { get { return BoundaryStatistic(x => x.AngleDegrees, true); } }
        public double AngleMean { get { return BoundaryStatistic(x => x.AngleDegrees, false); } }
        private double BoundaryStatistic(Func<SampleReportV9,double> selector, bool maximum)
        {
            double[] values = Samples.Select(selector).Where(SampleReportV9.Finite).ToArray();
            return values.Length == 0 ? double.NaN : (maximum ? values.Max() : values.Average());
        }
        public string FirstIssue
        {
            get
            {
                foreach (string issue in new[] { "G0", "G1", "K2", "GRAPH_G2", "INCOMPLETE", "K3/K4", "GRAPH_G3/G4" })
                    if (Samples.Any(x => x.FirstIssue == issue)) return issue;
                return "PASS";
            }
        }
        // Severity is aggregated independently for each measurement family.
        public string G0State { get { return Aggregate(Samples.Select(x => x.G0State)); } }
        public string G1State { get { return Aggregate(Samples.Select(x => x.G1State)); } }
        public string KState { get { return Aggregate(Samples.Select(x => x.KState)); } }
        private static string Aggregate(IEnumerable<string> source)
        {
            string[] states = source.ToArray();
            foreach (string state in new[] { "FAIL", "INCOMPLETE", "BLOCKED", "WARNING" })
                if (states.Contains(state)) return state;
            return states.Length == 0 ? "INCOMPLETE" : "PASS";
        }
        public int MissingEndpointCount { get { return Samples.Count(x => !x.Valid && (x.Fraction == 0 || x.Fraction == 1)); } }
        public string DisplayState(string metric)
        { return metric == "G0" ? G0State : metric == "G1" ? G1State : metric == "K" ? KState : metric == "SectionG2" ? SectionG2 : metric == "Graph4" ? GraphState : metric == "GraphG2" ? GraphOrder(0) : metric == "GraphG3" ? GraphOrder(1) : metric == "GraphG4" ? GraphOrder(2) : Status; }
        public int ValidCount { get { return Samples.Count(x => x.Valid); } }
        public double Maximum(int order)
        {
            var values = Samples.Where(x => x.Valid).Select(x => x.Difference(order)).ToArray();
            return values.Length == 0 ? double.NaN : values.Max();
        }
        public double Mean(int order)
        {
            var values = Samples.Where(x => x.Valid).Select(x => x.Difference(order)).ToArray();
            return values.Length == 0 ? double.NaN : values.Average();
        }
        public static string Number(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "N/A";
            if (v != 0 && Math.Abs(v) < .0000005) return v.ToString("0.###E+0", CultureInfo.InvariantCulture);
            return v.ToString("0.000000", CultureInfo.InvariantCulture);
        }
        public string Status
        {
            get
            {
                if (Samples.Any(x => x.Status == "RED")) return "RED";
                if (Samples.Any(x => !x.G0Valid || !x.G1Valid || !x.Valid)) return "INCOMPLETE";
                if (Samples.Any(x => x.Status == "ORANGE")) return "ORANGE";
                return "GREEN";
            }
        }
        public string[] LabelLines()
        {
            return new string[] {
                Id + " " + Status + " " + CompleteCount + "/" + Samples.Count,
                "G0 trim-to-trim sampled " + Number(GapMaximum) + " (" + Number(GapMean) + ")",
                "G1 deg " + Number(AngleMaximum) + " (" + Number(AngleMean) + ")",
                "dK2 " + Number(Maximum(0)) + " (" + Number(Mean(0)) + ")",
                "dK3 " + Number(Maximum(1)) + " (" + Number(Mean(1)) + ")",
                "dK4 " + Number(Maximum(2)) + " (" + Number(Mean(2)) + ")",
                "Section G2 (cross-seam) "+SectionG2+"; strict Graph G2/G3/G4 " + GraphOrder(0)+"/"+GraphOrder(1)+"/"+GraphOrder(2) +
                " ("+GraphValidCount+"/"+Samples.Count+" graph samples)",
                "Knot crossings " + KnotCrossings + "; added adaptive samples " + AdaptiveSamples +
                (SamplingCapReached ? " (budget-limited; coverage only)" : "") };
        }
        /// <summary>Reports the most exceeded allowance using values from that same sample.</summary>
        public string CurvatureDetails(double[] relativeTolerances, double[] absoluteTolerances)
        {
            var lines = new List<string>();
            var valid = Samples.Where(x => x.Valid).ToArray();
            for (int order = 0; order < 3; order++)
            {
                string name = "K" + (order+2);
                if (valid.Length == 0) { lines.Add(name + ": INCOMPLETE - no valid samples."); continue; }
                double relative = relativeTolerances[order], absolute = absoluteTolerances[order];
                var worst = valid.OrderByDescending(x => ContinuityEvaluator.ToleranceRatio(x.A[order],x.B[order],relative,absolute)).First();
                double a = worst.A[order], b = worst.B[order];
                double difference = Math.Abs(a-b);
                double allowance = ContinuityEvaluator.AllowedDifference(a,b,relative,absolute);
                double ratio = ContinuityEvaluator.ToleranceRatio(a,b,relative,absolute);
                double magnitude = Math.Max(Math.Abs(a),Math.Abs(b));
                double percent = magnitude > 0 ? 100*difference/magnitude : 0;
                int failed = valid.Count(x => !x.Pass[order]);
                string state = failed > 0 ? (order == 0 ? "FAIL" : "WARNING") : valid.Length < Samples.Count ? "INCOMPLETE" : "PASS";
                string unit = order == 0 ? "1/unit" : "1/unit^"+(order+1);
                string multiple = double.IsPositiveInfinity(ratio) ? "unbounded (zero allowance)" : DetailNumber(ratio)+"x";
                if (order == 0)
                {
                    int byAbs=valid.Count(x=>ContinuityEvaluator.AcceptanceBasis(x.A[0],x.B[0],relative,absolute)=="ABSOLUTE");
                    int byRel=valid.Count(x=>ContinuityEvaluator.AcceptanceBasis(x.A[0],x.B[0],relative,absolute)=="RELATIVE");
                    int byBoth=valid.Count(x=>ContinuityEvaluator.AcceptanceBasis(x.A[0],x.B[0],relative,absolute)=="BOTH");
                    lines.Add("K2 decision breakdown: both="+byBoth+", absolute-only="+byAbs+
                        ", relative-only="+byRel+", fail="+failed+
                        " | relative allowance and absolute allowance use OR / MAX, NEVER summed.");
                    lines.Add("At worst K2 sample: abs allowance="+DetailNumber(absolute)+
                        ", rel allowance="+DetailNumber(relative*magnitude)+
                        ", effective="+DetailNumber(allowance)+
                        ", basis="+ContinuityEvaluator.AcceptanceBasis(a,b,relative,absolute));
                }
                lines.Add(name+" "+state+" | failed "+failed+"/"+valid.Length+" valid | worst ratio "+multiple+
                    " at sample #"+worst.Index+" ("+DetailNumber(worst.Fraction*100)+"% along seam)"+
                    " | difference "+DetailNumber(difference)+" / allowed "+DetailNumber(allowance)+" "+unit+
                    " | relative "+DetailNumber(percent)+"% (limit "+DetailNumber(relative*100)+"%)"+
                    " | A="+DetailNumber(a)+", B="+DetailNumber(b));
            }
            return string.Join(Environment.NewLine,lines);
        }
        /// <summary>Full common-frame jet audit at all sampled points; not a seam-wide proof.</summary>
        public string GraphDetails(double[] relativeTolerances, double[] absoluteTolerances)
        {
            var valid=Samples.Where(x=>x.GraphValid).ToArray();
            var output=new List<string>();
            output.Add("Sampled Graph G2="+GraphOrder(0)+", G3="+GraphOrder(1)+", G4="+GraphOrder(2)+"; GraphJet4 overall="+GraphState+" ("+valid.Length+"/"+Samples.Count+
                " points). Every PASS is sample-only, never a globally certified seam.");
            output.Add("Coverage: " + SamplingNote);
            if (valid.Length == 0)
            {
                var unavailable=Samples.FirstOrDefault(x=>!x.GraphValid);
                if (unavailable!=null) output.Add("  Reason: "+unavailable.GraphReason);
                return string.Join(Environment.NewLine,output);
            }
            string[][] names = {
                new [] {"h_xx","h_xy","h_yy"},
                new [] {"h_xxx","h_xxy","h_xyy","h_yyy"},
                new [] {"h_xxxx","h_xxxy","h_xxyy","h_xyyy","h_yyyy"}
            };
            int offset=2;
            for (int order=0;order<3;order++)
            {
                double worstRatio=-1;
                SampleReportV9 worst=null;
                int worstIndex=offset;
                for(int i=offset;i<offset+order+3;i++)
                    foreach(var sample in valid)
                    {
                        double ratio=ContinuityEvaluator.ToleranceRatio(sample.GraphA[i],sample.GraphB[i],
                            relativeTolerances[order],absoluteTolerances[order]);
                        if (ratio>worstRatio) { worstRatio=ratio; worst=sample; worstIndex=i; }
                    }
                if(worst!=null)
                {
                    double a=worst.GraphA[worstIndex], b=worst.GraphB[worstIndex];
                    int fails=valid.Count(p=>p.GraphPass!=null && !p.GraphPass[order]);
                    string unit=order==0 ? "1/unit" : "1/unit^"+(order+1);
                    output.Add("  Graph G"+(order+2)+" "+GraphOrder(order)+
                        " (order-only: "+(fails>0?"FAIL":"PASS")+")"+
                        " | "+fails+" failing samples | worst "+names[order][worstIndex-offset]+
                        " at #"+worst.Index+" | Δ="+DetailNumber(Math.Abs(a-b))+
                        " / allowance="+DetailNumber(ContinuityEvaluator.AllowedDifference(a,b,relativeTolerances[order],absoluteTolerances[order]))+
                        " "+unit);
                }
                offset+=order+3;
            }
            return string.Join(Environment.NewLine,output);
        }
        private static string DetailNumber(double value)
        { return value.ToString("G6",CultureInfo.InvariantCulture); }
        public string Summary() { return string.Join(" | ", LabelLines()); }
    }
}
