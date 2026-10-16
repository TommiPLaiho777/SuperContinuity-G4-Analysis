using System;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.DocObjects;
using Rhino.Geometry;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Tommi.ContinuityTool
{
    public static class PorcupineAllSeamsV9
    {
        public static Result RunAnalyzer(RhinoDoc doc)
        {
            bool runAnalysis;
            Result result = ContinuitySettings.ReadOptions(doc, out runAnalysis);
            if (!runAnalysis) return result;
            double tol2 = ContinuitySettings.strict ? .01 : ContinuitySettings.customG2 / 100;
            double tol3 = ContinuitySettings.strict ? .02 : ContinuitySettings.customG3 / 100;
            double tol4 = ContinuitySettings.strict ? .05 : ContinuitySettings.customG4 / 100;
            var conduit = new AllSeamsConduitV9();
            conduit.DocumentSerial = doc.RuntimeSerialNumber;
            conduit.ShowNumbers = ContinuitySettings.labels;
            conduit.ModeName = ContinuitySettings.strict ? "Strict" : "Normal";
            conduit.GapTolerance = ContinuitySettings.gapTolerance;
            conduit.AngleTolerance = ContinuitySettings.angleTolerance;
            conduit.UnitName = doc.ModelUnitSystem.ToString();
            conduit.Tolerances = new double[] { tol2, tol3, tol4 };
            conduit.AbsoluteTolerances = new[] { ContinuitySettings.absK2, ContinuitySettings.absK3, ContinuitySettings.absK4 };
            int seamCount = 0, skipped = 0;
            bool installed = false;
            using (GetObject go = new GetObject())
            {
                go.SetCommandPrompt("Select joined polysurfaces; ENTER confirms selection");
                go.GeometryFilter = ObjectType.Brep;
                go.SubObjectSelect = false;
                if (go.GetMultiple(1, 0) != GetResult.Object) return go.CommandResult();
                try
                {
                    for (int n = 0; n < go.ObjectCount; n++)
                    {
                        ObjRef reference = go.Object(n);
                        if (!conduit.SourceIds.Add(reference.ObjectId)) continue;
                        Brep brep = reference.Brep();
                        if (brep == null) continue;
                        foreach (BrepEdge edge in brep.Edges)
                        {
                            int[] faces = edge.AdjacentFaces();
                            if (faces.Length != 2 || faces[0] == faces[1]) { skipped++; continue; }
                            seamCount++;
                            double spikeLength = Math.Max(edge.GetLength() * .08, doc.ModelAbsoluteTolerance * 10);
                            var seam = new SeamReportV9();
                            seam.Id = "S" + seamCount;
                            seam.ObjectId = reference.ObjectId;
                            seam.EdgeIndex = edge.EdgeIndex;
                            seam.FaceA = faces[0]; seam.FaceB = faces[1];
                            seam.Anchor = edge.PointAt(edge.Domain.Mid);
                            seam.EdgeCurve = edge.DuplicateCurve();
                            ContinuityDocumentStore.RestoreRole(doc,seam);
                            conduit.Seams.Add(seam);
                            BoundaryContinuityEvaluator.PreparedPair boundaryPair = null;
                            try { boundaryPair = BoundaryContinuityEvaluator.Prepare(edge, brep.Faces[faces[0]], brep.Faces[faces[1]]); }
                            catch { /* Preserve the original per-sample exception/reporting path if preparation fails. */ }
                            var plan = KnotAwareSampler.Build(edge, brep.Faces[faces[0]], brep.Faces[faces[1]],
                                ContinuitySettings.sampleCount, ContinuitySettings.knotAware,
                                ContinuitySettings.maximumSamples,
                                ContinuitySettings.knotAware && ContinuitySettings.adaptive);
                            seam.KnotAuditEnabled = ContinuitySettings.knotAware;
                            seam.KnotCrossings = plan.KnotCrossings;
                            seam.KnotOverlap = plan.KnotOverlap;
                            seam.KnotMappingIncomplete = plan.KnotMappingIncomplete;
                            seam.SamplingCapReached = plan.CapacityReached;
                            int seamMaximum = ContinuitySettings.maximumSamples;
                            seam.KnotEstimatedMinimum = KnotAwareSampler.MinimumKnotBudget(plan, seamMaximum);
                            if (ContinuitySettings.autoKnotBudget && ContinuitySettings.knotAware && ContinuitySettings.adaptive)
                                seamMaximum = Math.Min(Math.Max(seamMaximum,seam.KnotEstimatedMinimum),
                                    ContinuitySettings.knotBudgetCap);
                            seam.KnotEffectiveBudget = seamMaximum;
                            double totalLength = edge.GetLength();
                            double paramEpsilon = Math.Max(Math.Abs(edge.Domain.Length)*1e-10, 1e-14);
                            // Evaluate each parameter once. The source records why it was selected.
                            SampleReportV9 Measure(double t, string sourceName)
                            {
                                var existing = seam.Samples.FirstOrDefault(s => Math.Abs(s.EdgeParameter-t) < paramEpsilon);
                                if (existing != null) return existing;
                                if (seam.Samples.Count >= seamMaximum)
                                { seam.SamplingCapReached = true; return null; }
                                var sample = new SampleReportV9();
                                sample.EdgeParameter = t;
                                sample.SampleSource = sourceName;
                                sample.Point = edge.PointAt(t);
                                sample.Fraction = t<=edge.Domain.T0 ? 0 : t>=edge.Domain.T1 ? 1 :
                                    totalLength > 0 ? edge.GetLength(new Interval(edge.Domain.T0,t))/totalLength :
                                    (t-edge.Domain.T0)/edge.Domain.Length;
                                seam.Samples.Add(sample);
                                try
                                {
                                    if (boundaryPair != null) boundaryPair.Measure(t, sample);
                                    else BoundaryContinuityEvaluator.Measure(edge, brep.Faces[faces[0]], brep.Faces[faces[1]], t, sample);
                                }
                                catch (Exception ex) { sample.BoundaryReason = ex.Message; }
                                try
                                {
                                    ContinuityEvaluator.Evaluate(edge, brep.Faces[faces[0]], brep.Faces[faces[1]],
                                        t, spikeLength, sample);
                                }
                                catch (Exception ex)
                                {
                                    sample.Status = "UNAVAILABLE"; sample.Reason = ex.Message;
                                    sample.GraphReason = ex.Message;
                                    sample.Spike = new Line(sample.Point, Vector3d.ZAxis, spikeLength);
                                }
                                ContinuityEvaluator.Classify(sample, conduit.GapTolerance, conduit.AngleTolerance,
                                    conduit.Tolerances, conduit.AbsoluteTolerances);
                                return sample;
                            }
                            foreach (var point in plan.Points) Measure(point.T,point.Source);
                            if (ContinuitySettings.adaptive)
                            {
                                bool adaptiveCap;
                                if(ContinuitySettings.knotAware)
                                    KnotAwareSampler.ExpandKnotFirst(seam.Samples,plan.KnotProbes,Measure,
                                        seamMaximum,paramEpsilon,
                                        conduit.Tolerances,conduit.AbsoluteTolerances,out adaptiveCap);
                                else
                                    KnotAwareSampler.ExpandAdaptive(seam.Samples, Measure,
                                        ContinuitySettings.maximumSamples, paramEpsilon,
                                        conduit.Tolerances, conduit.AbsoluteTolerances,
                                        out adaptiveCap);
                                if(adaptiveCap) seam.SamplingCapReached=true;
                            }
                            // Audit actual measured stations against BOTH planned knot flanks.
                            // Never turn a single successful flank into a complete knot check.
                            seam.KnotAuditRows.AddRange(KnotCoverageAudit.EvaluateMeasured(
                                plan.KnotTargets, seam.Samples,
                                paramEpsilon, seam.KnotAuditEnabled,
                                seam.KnotOverlap, seam.KnotMappingIncomplete));
                            seam.Samples.Sort((x,y)=>x.EdgeParameter.CompareTo(y.EdgeParameter));
                            for(int i=0;i<seam.Samples.Count;i++) seam.Samples[i].Index=i+1;
                            seam.AdaptiveSamples = seam.Samples.Count(s=>s.SampleSource.StartsWith("Adaptive"));
                            RhinoApp.WriteLine(seam.Summary());
                        }
                    }
                    if (seamCount == 0)
                    {
                        RhinoApp.WriteLine("No shared seams between distinct faces. Join surfaces first.");
                        return Result.Failure;
                    }
                    ContinuitySessionV9.Install(doc, conduit);
                    installed = true;
                    doc.Objects.UnselectAll();
                    doc.Views.Redraw();
                    RhinoApp.WriteLine($"{seamCount} seams; mode={conduit.ModeName}; tolerances={tol2*100}/{tol3*100}/{tol4*100} percent. Sample-budget limits are diagnostics, not geometry errors.");
                    RhinoApp.WriteLine("Use Close window or Stop analysis / clear graphics in the table. Rhino command: ContinuityAllSeams9Off. Export: ContinuityAllSeams9CSV.");
                    ContinuitySessionV9.ShowTable(doc);
                    RhinoApp.WriteLine("G0: two-sided nearest actual trim-boundary distances; old paired UV distance remains a diagnostic. G1: paired-UV tangent samples. K2/K3/K4: section diagnostics. Graph G2/G3/G4: separately reported full common-frame jets at samples; NEVER full-seam certificates. Geometry edits clear results.");
                    return Result.Success;
                }
                finally
                {
                    if (!installed) conduit.Release();
                }
            }
        }

    }
}
