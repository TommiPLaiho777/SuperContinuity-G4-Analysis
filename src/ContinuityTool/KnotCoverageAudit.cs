using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.Commands;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Audit of the requested two-sided samples at each mapped knot crossing.
    /// This audits ACTUAL measured parameters, not merely planned candidates.
    /// BOTH means two samples, NOT continuity at the knot or interval certification.
    /// </summary>
    public static class KnotCoverageAudit
    {
        // #SC-EVIDENCE-03 AUDIT OBSERVATIONS, NOT PLANNER INTENT.
        // A requested left/right parameter pair is not evidence that either point
        // was actually measured. EvaluateMeasured checks measured parameters AND
        // usable graph-jet arrays. BOTH means that two requested flank probes were
        // observed, not that the exact knot or the intervening interval is smooth.
        // Status precedence retains unresolved topology/mapping before reporting
        // complete flank evidence. Never infer completeness just from the count
        // of passing ordinary samples or from a false SamplingCapReached flag.
        public sealed class Crossing
        {
            public double Center;
            public double Left = double.NaN;
            public double Right = double.NaN;
            public string PlanIssue = "";
        }

        public sealed class Row
        {
            public double Center, Left, Right;
            public bool LeftMeasured, RightMeasured;
            public bool LeftGraphJetValid, RightGraphJetValid;
            public string State, Issue;
        }

        public static List<Row> Evaluate(IList<Crossing> requested, IList<double> measured,
            double epsilon, bool enabled, bool overlap, bool mappingIncomplete)
        {
            var rows = new List<Row>();
            if (!enabled) return rows;
            foreach (var c in requested ?? new List<Crossing>())
            {
                bool valid = Finite(c.Left) && Finite(c.Right) && c.Left < c.Center && c.Center < c.Right;
                bool l = valid && measured != null && measured.Any(t => Finite(t) && Math.Abs(t - c.Left) <= epsilon);
                bool r = valid && measured != null && measured.Any(t => Finite(t) && Math.Abs(t - c.Right) <= epsilon);
                string state = !valid ? "UNPROBEABLE" : l && r ? "BOTH" : l ? "LEFT_ONLY" : r ? "RIGHT_ONLY" : "NONE";
                rows.Add(new Row { Center = c.Center, Left = c.Left, Right = c.Right,
                    LeftMeasured = l, RightMeasured = r, State = state, Issue = c.PlanIssue ?? "" });
            }
            return rows;
        }
        public static List<Row> EvaluateMeasured(IList<Crossing> requested,
            IList<SampleReportV9> samples,double epsilon,bool enabled,bool overlap,bool mappingIncomplete)
        {
            var rows=Evaluate(requested,samples==null ? new double[0] :
                samples.Select(s=>s.EdgeParameter).ToArray(),epsilon,enabled,overlap,mappingIncomplete);
            foreach(var row in rows)
            {
                if(samples==null) continue;
                row.LeftGraphJetValid=row.LeftMeasured && samples.Any(s=>
                    Finite(row.Left) && Math.Abs(s.EdgeParameter-row.Left)<=epsilon && s.GraphValid);
                row.RightGraphJetValid=row.RightMeasured && samples.Any(s=>
                    Finite(row.Right) && Math.Abs(s.EdgeParameter-row.Right)<=epsilon && s.GraphValid);
            }
            return rows;
        }
        public static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }

        public static string Status(bool enabled, bool overlap, bool mappingIncomplete, IList<Row> rows)
        {
            if (!enabled) return "DISABLED";
            if (overlap) return "KNOT_OVERLAP_UNRESOLVED";
            if (mappingIncomplete) return "MAPPING_INCOMPLETE";
            if (rows == null || rows.Count == 0) return "NO_MAPPED_INTERIOR_KNOTS";
            if (rows.Any(r => r.State == "UNPROBEABLE")) return "UNPROBEABLE_FLANKS";
            if(!rows.All(r=>r.State=="BOTH")) return "PARTIAL_FLANK_SAMPLING";
            return rows.All(r=>r.LeftGraphJetValid && r.RightGraphJetValid) ?
                "BOTH_FLANKS_WITH_GRAPH_JETS" : "BOTH_FLANKS_JETS_INCOMPLETE";
        }
        public static string Summary(SeamReportV9 seam)
        {
            if (!seam.KnotAuditEnabled) return "KnotAudit DISABLED";
            int both = seam.KnotAuditRows.Count(r=>r.State=="BOTH");
            return "KnotAudit " + seam.KnotAuditState + " (" + both + "/" +
                seam.KnotAuditRows.Count + " mapped knot crossings have both flank probes); ";
        }

        private static string Q(object x)
        { return "\"" + Convert.ToString(x, CultureInfo.InvariantCulture).Replace("\"","\"\"") + "\""; }
        private static string Number(double x)
        { return Finite(x) ? x.ToString("G17",CultureInfo.InvariantCulture) : ""; }
        private static string Line(params object[] fields)
        { return string.Join(",", fields.Select(Q)); }

        public static void Write(AllSeamsConduitV9 analysis, string path)
        {
            using(var writer = new StreamWriter(path,false,new UTF8Encoding(true)))
            {
                writer.WriteLine(Line("Seam", "ObjectId", "EdgeIndex", "KnotIndex", "KnotParameter",
                    "LeftProbeParameter", "RightProbeParameter", "LeftMeasured", "RightMeasured",
                    "LeftGraphJetValid", "RightGraphJetValid",
                    "FlankState", "PlanIssue", "SeamAuditStatus", "MappedCrossings",
                    "BothFlanks", "PartialOrMissing", "KnotOverlap", "KnotMappingIncomplete",
                    "SamplingCapReached", "IntervalCertificate", "Caveat",
                    "MissingBothFlanks", "MissingLeftFlank", "MissingRightFlank",
                    "ConservativeBudgetForAllMappedFlanks", "SamplingPriorityPolicy",
                    "ReviewRole", "ReviewExcluded", "QualityScope",
                    "EffectiveBudget", "EstimatedKnotMinimum", "RemainingForCompleteKnotPairs"));
                foreach(var seam in analysis.Seams)
                {
                    var rows=seam.KnotAuditRows;
                    int both=rows.Count(r=>r.State=="BOTH");
                    int missingBoth=rows.Count(x=>x.State=="NONE");
                    int missingLeft=rows.Count(x=>x.State=="RIGHT_ONLY");
                    int missingRight=rows.Count(x=>x.State=="LEFT_ONLY");
                    // Missing/overlapping knot mapping cannot be repaired by
                    // simply increasing MaxSamples, so never suggest a finite
                    // budget for those cases.
                    object estimatedBudget=seam.KnotOverlap || seam.KnotMappingIncomplete ||
                        rows.Any(x=>x.State=="UNPROBEABLE") ? (object)"" :
                        (object)(seam.Samples.Count+rows.Sum(x=>
                            (x.LeftMeasured?0:1)+(x.RightMeasured?0:1)));
                    Action<int,Row> emit = (i,r) => writer.WriteLine(Line(seam.Id,seam.ObjectId,seam.EdgeIndex,i,
                        r==null?"":Number(r.Center),r==null?"":Number(r.Left),r==null?"":Number(r.Right),
                        r==null?"":r.LeftMeasured.ToString(),r==null?"":r.RightMeasured.ToString(),
                        r==null?"":r.LeftGraphJetValid.ToString(),r==null?"":r.RightGraphJetValid.ToString(),
                        r==null?"NO_TARGET_ROWS":r.State,r==null?"":r.Issue,
                        seam.KnotAuditState,rows.Count,both,rows.Count-both,
                        seam.KnotOverlap,seam.KnotMappingIncomplete,seam.SamplingCapReached,
                        "NO_BOUND_AVAILABLE", "Flank samples are point diagnostics; no G2/G3/G4 interval bound",
                        missingBoth,missingLeft,missingRight,estimatedBudget,
                        "KNOT_PAIRS_FIRST_WHEN_ADAPTIVE_AND_KNOTAWARE",
                        seam.ReviewRole,seam.ReviewExcluded,
                        seam.ReviewExcluded ? "EXCLUDED_FROM_SMOOTH_SEAM_QUALITY" : "INCLUDED_IN_SMOOTH_SEAM_QUALITY",
                        seam.KnotEffectiveBudget,seam.KnotEstimatedMinimum,
                        Math.Max(0,seam.KnotEstimatedMinimum-seam.KnotEffectiveBudget)));
                    if(rows.Count==0) emit(0,null);
                    else for(int i=0;i<rows.Count;i++) emit(i+1,rows[i]);
                }
            }
        }
    }

    public class ContinuityAllSeams9KnotCoverageCSVCommand : Command
    {
        public override string EnglishName { get { return "ContinuityAllSeams9KnotCoverageCSV"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            AllSeamsConduitV9 current;
            if(!ContinuitySessionV9.TryGet(doc,out current))
            { RhinoApp.WriteLine("Run ContinuityAllSeams9 first, with KnotAware=On."); return Result.Nothing; }
            var dialog=new Rhino.UI.SaveFileDialog { Title="Save per-knot flank coverage audit",
                Filter="CSV table (*.csv)|*.csv", DefaultExt="csv", FileName="Continuity_KnotCoverage.csv" };
            if(!dialog.ShowSaveDialog()) return Result.Cancel;
            try
            {
                KnotCoverageAudit.Write(current,dialog.FileName);
                RhinoApp.WriteLine("Saved knot-flank coverage audit: "+dialog.FileName);
                RhinoApp.WriteLine("BOTH means both PLANNED flanks were measured, not certified continuity; full-seam bounds unavailable.");
                return Result.Success;
            }
            catch(Exception ex) { RhinoApp.WriteLine("Knot coverage export failed: "+ex.Message); return Result.Failure; }
        }
    }
}
