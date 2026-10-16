using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Tommi.ContinuityTool
{
    public static class ContinuityCsv
    {
        private static readonly string[] GraphJetNames = {
            "hx", "hy", "hxx", "hxy", "hyy", "hxxx", "hxxy", "hxyy", "hyyy",
            "hxxxx", "hxxxy", "hxxyy", "hxyyy", "hyyyy"
        };
        private static string Cell(object value)
        {
            if (value == null) return "";
            if (value is double)
            {
                double d = (double)value;
                if (double.IsNaN(d) || double.IsInfinity(d)) return "";
                return d.ToString("G17", CultureInfo.CurrentCulture);
            }
            string text = Convert.ToString(value, CultureInfo.CurrentCulture);
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        private static void Row(StreamWriter writer, params object[] cells)
        { writer.WriteLine(string.Join(";", cells.Select(Cell))); }

        public static Result Export(RhinoDoc doc, bool? detailed = null)
        {
            AllSeamsConduitV9 current;
            if (!ContinuitySessionV9.TryGet(doc, out current))
            { RhinoApp.WriteLine("Analyze geometry first. No current results to export."); return Result.Nothing; }
            bool detail = detailed ?? false;
            if (!detailed.HasValue)
            using (var choose = new GetOption())
            {
                choose.SetCommandPrompt("CSV table: ENTER = one row per seam");
                choose.AcceptNothing(true);
                int summary = choose.AddOption("Summary");
                int samples = choose.AddOption("Samples");
                GetResult result = choose.Get();
                if (result != GetResult.Option && result != GetResult.Nothing) return choose.CommandResult();
                detail = result == GetResult.Option && choose.OptionIndex() == samples;
            }
            var dialog = new Rhino.UI.SaveFileDialog();
            dialog.Title = "Export continuity estimates";
            dialog.Filter = "CSV table (*.csv)|*.csv";
            dialog.DefaultExt = "csv";
            dialog.FileName = detail ? "Continuity_samples.csv" : "Continuity_seams.csv";
            if (!dialog.ShowSaveDialog()) return Result.Cancel;
            try
            {
                using (var writer = new StreamWriter(dialog.FileName, false, new UTF8Encoding(true)))
                {
                    if (!detail)
                    Row(writer, "Seam", "ObjectId", "EdgeIndex", "FaceA", "FaceB", "Mode", "ModelUnits", "Status",
                    "CurvatureValidSamples", "TotalSamples", "dK2_max_1perUnit", "dK2_mean_1perUnit",
                    "dK3_max_1perUnit2", "dK3_mean_1perUnit2", "dK4_max_1perUnit3", "dK4_mean_1perUnit3",
                    "TolK2_pct", "TolK3_pct", "TolK4_pct", "RedSamples", "OrangeSamples", "GreenSamples", "UnavailableSamples",
                    "FirstIssue", "G0_gap_max_modelUnits", "G0_gap_mean_modelUnits", "G1_angle_max_deg", "G1_angle_mean_deg",
                    "G0_valid_samples", "G1_valid_samples", "G0_gap_tolerance_modelUnits", "G1_angle_tolerance_deg",
                    "G0_state", "G1_state", "K_state", "K_missing_endpoints", "AbsK2_1perUnit", "AbsK3_1perUnit2", "AbsK4_1perUnit3", "Method", "IncompleteSamples", "GraphJet4_state", "GraphJet4_valid", "GraphJet4_G2_failed", "GraphJet4_G3_failed", "GraphJet4_G4_failed",
                    "GraphG2_sampled_state", "GraphG3_sampled_state", "GraphG4_sampled_state",
                    "GraphG2_passed_samples", "GraphG3_passed_samples", "GraphG4_passed_samples",
                    "KnotCrossings", "KnotOverlap", "KnotMappingIncomplete", "SamplingCapReached", "AdaptiveSamples", "CoverageClassification", "SectionG2_sampled_state", "SectionG2_passed_samples", "ReviewRole", "ReviewState", "ReviewExcluded", "DisplayGrade_sampled", "DisplayGrade_semantics", "G4_user_facing_state", "G4_coverage_state", "G4_raw_sampled_state", "G4_raw_passed_samples", "RawG1AnglePassSamples_ignoreG0", "RawK2PassSamples_ignoreG0G1", "G0_method", "G0_old_paired_max_modelUnits", "G0_A_to_B_max_modelUnits", "G0_B_to_A_max_modelUnits", "K2_absOnly_samples", "K2_relativeOnly_samples", "K2_both_samples", "K2_failed_samples", "K2_tolerance_policy", "KnotAuditStatus", "KnotAuditCrossings", "KnotAuditBothFlanks", "KnotAuditPartialOrMissing", "IntervalBoundStatus", "KnotEffectiveBudget", "KnotEstimatedMinimum", "AutoKnotBudgetSetting");
                    else
                    {
                    var sampleHeaders = new List<object> { "Seam", "ObjectId", "EdgeIndex", "FaceA", "FaceB", "Mode", "ModelUnits", "Sample", "EdgeFraction", "X", "Y", "Z", "Step_modelUnits", "Status", "Reason",
                    "K2_A_1perUnit", "K2_B_1perUnit", "dK2_1perUnit", "K3_A_1perUnit2", "K3_B_1perUnit2", "dK3_1perUnit2", "K4_A_1perUnit3", "K4_B_1perUnit3", "dK4_1perUnit3",
                    "K2_pass", "K3_pass", "K4_pass", "TolK2_pct", "TolK3_pct", "TolK4_pct",
                    "FirstIssue", "G0_gap_modelUnits", "G1_angle_deg", "G0_valid", "G1_valid", "G0_pass", "G1_pass_with_G0_gate",
                    "G0_gap_tolerance_modelUnits", "G1_angle_tolerance_deg", "MappingResidual_modelUnits", "OrientedNormalAngle_deg", "NormalsOpposed",
                    "BoundaryA_X", "BoundaryA_Y", "BoundaryA_Z", "BoundaryB_X", "BoundaryB_Y", "BoundaryB_Z", "BoundaryReason",
                    "G0_state", "G1_state", "K_state", "AbsK2_1perUnit", "AbsK3_1perUnit2", "AbsK4_1perUnit3", "Method", "GraphJet4_state", "GraphJet4_reason", "GraphJet4_available", "GraphJet4_G2_pass", "GraphJet4_G3_pass", "GraphJet4_G4_pass",
                    "GraphG2_cumulative_state", "GraphG3_cumulative_state", "GraphG4_cumulative_state", "SampleSource", "EdgeParameter", "SectionG2_sampled_state", "ReviewRole", "ReviewState", "ReviewExcluded", "DisplayGrade_atSample", "Seam_G4_user_facing_state", "Seam_G4_coverage_state", "Seam_G4_raw_sampled_state", "Seam_G4_raw_passed_samples", "RawG1AnglePass_ignoreG0", "RawK2Pass_ignoreG0G1", "G0_method", "G0_old_paired_UV_gap_modelUnits", "G0_A_to_B_nearest_modelUnits", "G0_B_to_A_nearest_modelUnits", "K2_absolute_difference_1perUnit", "K2_relative_difference_pct", "K2_relative_allowance_1perUnit", "K2_absolute_allowance_1perUnit", "K2_effective_allowance_1perUnit", "K2_acceptance_basis" };
                    foreach (var name in GraphJetNames)
                    {
                        sampleHeaders.Add("GraphJet4_"+name+"_A");
                        sampleHeaders.Add("GraphJet4_"+name+"_B");
                        sampleHeaders.Add("GraphJet4_"+name+"_delta");
                    }
                    Row(writer,sampleHeaders.ToArray());
                    }
                    foreach (SeamReportV9 seam in current.Seams)
                    {
                        if (!detail)
                        Row(writer, seam.Id, seam.ObjectId, seam.EdgeIndex, seam.FaceA, seam.FaceB,
                        current.ModeName, current.UnitName, seam.Status, seam.ValidCount, seam.Samples.Count,
                        seam.Maximum(0), seam.Mean(0), seam.Maximum(1), seam.Mean(1), seam.Maximum(2), seam.Mean(2),
                        current.Tolerances[0]*100, current.Tolerances[1]*100, current.Tolerances[2]*100,
                        seam.Samples.Count(x=>x.Status=="RED"), seam.Samples.Count(x=>x.Status=="ORANGE"),
                        seam.Samples.Count(x=>x.Status=="GREEN"), seam.Samples.Count(x=>x.Status=="UNAVAILABLE"),
                        seam.FirstIssue, seam.GapMaximum, seam.GapMean, seam.AngleMaximum, seam.AngleMean,
                        seam.G0Count, seam.G1Count, current.GapTolerance, current.AngleTolerance,
                        seam.G0State, seam.G1State, seam.KState, seam.MissingEndpointCount,
                        current.AbsoluteTolerances[0], current.AbsoluteTolerances[1], current.AbsoluteTolerances[2], "SurfaceJet4-v2+GraphJet4-v1+KnotAdaptive-v2+SectionG2-DualTol-v7.2.3+TrimG0-v1", seam.Samples.Count(x=>!x.Complete),
                        seam.GraphState, seam.GraphValidCount,
                        seam.Samples.Count(x=>x.GraphPass!=null && !x.GraphPass[0]),
                        seam.Samples.Count(x=>x.GraphPass!=null && !x.GraphPass[1]),
                        seam.Samples.Count(x=>x.GraphPass!=null && !x.GraphPass[2]),
                        seam.GraphOrder(0), seam.GraphOrder(1), seam.GraphOrder(2),
                        seam.GraphOrderPassedSamples(0), seam.GraphOrderPassedSamples(1), seam.GraphOrderPassedSamples(2),
                        seam.KnotCrossings, seam.KnotOverlap, seam.KnotMappingIncomplete, seam.SamplingCapReached,
                        seam.AdaptiveSamples, "SAMPLED_ONLY", seam.SectionG2, seam.SectionG2PassedSamples, seam.ReviewRole, seam.ReviewState, seam.ReviewExcluded,
                        seam.SampledGradeName, "HYBRID: G2=section K2, G3/G4=strict cumulative GraphJet4; sampled only",
                        seam.G4DisplayState, seam.G4CoverageState, seam.GraphOrder(2), seam.GraphOrderPassedSamples(2),
                        seam.RawG1AnglePassedSamples(current.AngleTolerance), seam.RawK2PassedSamples,
                        "DIRECTED_TRIM_TO_TRIM_LOCAL", seam.PairedGapMaximum, seam.G0AtoBMaximum, seam.G0BtoAMaximum,
                        seam.Samples.Count(x=>x.Valid && ContinuityEvaluator.AcceptanceBasis(x.A[0],x.B[0],current.Tolerances[0],current.AbsoluteTolerances[0])=="ABSOLUTE"),
                        seam.Samples.Count(x=>x.Valid && ContinuityEvaluator.AcceptanceBasis(x.A[0],x.B[0],current.Tolerances[0],current.AbsoluteTolerances[0])=="RELATIVE"),
                        seam.Samples.Count(x=>x.Valid && ContinuityEvaluator.AcceptanceBasis(x.A[0],x.B[0],current.Tolerances[0],current.AbsoluteTolerances[0])=="BOTH"),
                        seam.Samples.Count(x=>x.Valid && !x.Pass[0]), "MAX_OF_ABSOLUTE_AND_RELATIVE_K2",
                        seam.KnotAuditState, seam.KnotAuditRows.Count,
                        seam.KnotAuditRows.Count(x=>x.State=="BOTH"),
                        seam.KnotAuditRows.Count(x=>x.State!="BOTH"),
                        "NO_BOUND_AVAILABLE", seam.KnotEffectiveBudget, seam.KnotEstimatedMinimum, ContinuitySettings.autoKnotBudget);
                        else foreach (SampleReportV9 sample in seam.Samples)
                        {
                            var cells = new List<object> { seam.Id, seam.ObjectId, seam.EdgeIndex, seam.FaceA, seam.FaceB,
                                current.ModeName, current.UnitName, sample.Index, sample.Fraction, sample.Point.X, sample.Point.Y,
                                sample.Point.Z, sample.Step, sample.Status, sample.Reason };
                            for (int order=0; order<3; order++)
                            {
                                cells.Add(sample.Valid ? sample.A[order] : double.NaN);
                                cells.Add(sample.Valid ? sample.B[order] : double.NaN);
                                cells.Add(sample.Difference(order));
                            }
                            for (int order=0; order<3; order++) cells.Add(sample.Valid ? (object)sample.Pass[order] : null);
                            for (int order=0; order<3; order++) cells.Add(current.Tolerances[order]*100);
                            cells.AddRange(new object[] {
                                sample.FirstIssue, sample.Gap, sample.AngleDegrees, sample.G0Valid, sample.G1Valid,
                                sample.G0Valid ? (object)sample.G0Pass : null,
                                sample.G0Valid && sample.G1Valid ? (object)sample.G1Pass : null,
                                current.GapTolerance, current.AngleTolerance, sample.MappingResidual, sample.NormalAngleDegrees,
                                sample.G1Valid ? (object)sample.NormalOpposed : null,
                                sample.G0Valid ? sample.BoundaryA.X : double.NaN,
                                sample.G0Valid ? sample.BoundaryA.Y : double.NaN,
                                sample.G0Valid ? sample.BoundaryA.Z : double.NaN,
                                sample.G0Valid ? sample.BoundaryB.X : double.NaN,
                                sample.G0Valid ? sample.BoundaryB.Y : double.NaN,
                                sample.G0Valid ? sample.BoundaryB.Z : double.NaN,
                                sample.BoundaryReason, sample.G0State, sample.G1State, sample.KState,
                                current.AbsoluteTolerances[0], current.AbsoluteTolerances[1], current.AbsoluteTolerances[2], sample.Method,
                                sample.GraphState, sample.GraphReason, sample.GraphValid,
                                sample.GraphPass!=null ? (object)sample.GraphPass[0] : null,
                                sample.GraphPass!=null ? (object)sample.GraphPass[1] : null,
                                sample.GraphPass!=null ? (object)sample.GraphPass[2] : null,
                                sample.GraphOrderState[0], sample.GraphOrderState[1], sample.GraphOrderState[2],
                                sample.SampleSource, sample.EdgeParameter, sample.SectionG2State, seam.ReviewRole, seam.ReviewState, seam.ReviewExcluded,
                                ContinuityGrade.Name(ContinuityGrade.Of(sample)), seam.G4DisplayState, seam.G4CoverageState,
                                seam.GraphOrder(2), seam.GraphOrderPassedSamples(2),
                                sample.G1Valid ? (object)(sample.AngleDegrees <= current.AngleTolerance) : null,
                                sample.Valid ? (object)sample.Pass[0] : null,
                                sample.G0Measurement, sample.PairedSurfaceGap, sample.G0AtoB, sample.G0BtoA,
                                sample.Difference(0),
                                sample.Valid ? (Math.Max(Math.Abs(sample.A[0]),Math.Abs(sample.B[0]))>0 ?
                                    100.0*sample.Difference(0)/Math.Max(Math.Abs(sample.A[0]),Math.Abs(sample.B[0])) :
                                    (sample.Difference(0)==0 ? 0 : double.PositiveInfinity)) : double.NaN,
                                sample.Valid ? current.Tolerances[0]*Math.Max(Math.Abs(sample.A[0]),Math.Abs(sample.B[0])) : double.NaN,
                                current.AbsoluteTolerances[0],
                                sample.Valid ? ContinuityEvaluator.AllowedDifference(sample.A[0],sample.B[0],current.Tolerances[0],current.AbsoluteTolerances[0]) : double.NaN,
                                sample.Valid ? ContinuityEvaluator.AcceptanceBasis(sample.A[0],sample.B[0],current.Tolerances[0],current.AbsoluteTolerances[0]) : "UNAVAILABLE" });
                            for(int jet=0;jet<14;jet++)
                            {
                                cells.Add(sample.GraphValid ? sample.GraphA[jet] : double.NaN);
                                cells.Add(sample.GraphValid ? sample.GraphB[jet] : double.NaN);
                                cells.Add(sample.GraphValid ? Math.Abs(sample.GraphA[jet]-sample.GraphB[jet]) : double.NaN);
                            }
                            Row(writer, cells.ToArray());
                        }
                    }
                }
                RhinoApp.WriteLine("CSV saved: " + dialog.FileName);
                return Result.Success;
            }
            catch (Exception ex)
            { RhinoApp.WriteLine("CSV export failed: " + ex.Message); return Result.Failure; }
        }
    }
}
