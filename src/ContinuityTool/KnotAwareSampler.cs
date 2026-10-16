using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Knot-aware measurement point planning. Trim/knot intersection parameters are
    /// computed geometrically in the UV parameter plane, then mapped to BrepEdge.
    /// These sample positions are diagnostics, NOT certified interval bounds.
    /// </summary>
    public static class KnotAwareSampler
    {
        public sealed class Point
        {
            public double T;
            public string Source;
            public Point(double t, string source) { T=t; Source=source; }
        }
        public sealed class Plan
        {
            public readonly List<Point> Points = new List<Point>();
            // Deterministic stream used only when KnotAware + Adaptive are both On.
            public readonly List<Point> KnotProbes = new List<Point>();
            // ALL mapped knot crossings, even if no flank budget remains.
            public readonly List<KnotCoverageAudit.Crossing> KnotTargets = new List<KnotCoverageAudit.Crossing>();
            public int KnotCrossings;
            public bool KnotOverlap, KnotMappingIncomplete, CapacityReached;
            public string Note { get { return (KnotOverlap ? "Trim lies along a knot line. " : "") +
                (KnotMappingIncomplete ? "Some knot intersections could not be mapped. " : "") +
                (CapacityReached ? "Sampling cap reached. " : ""); } }
        }
        private static bool Interior(double t, Interval domain)
        { return t > domain.T0 + Math.Abs(domain.Length)*1e-10 && t < domain.T1 - Math.Abs(domain.Length)*1e-10; }

        public static Plan Build(BrepEdge edge, BrepFace a, BrepFace b,
            int uniformCount, bool knotAware, int maximum)
        { return Build(edge, a, b, uniformCount, knotAware, maximum, false); }

        // In combined mode, only uniform stations are measured up front. Knot
        // candidates remain in an independently ordered stream interleaved with
        // adaptive probes. MaxSamples must NOT influence this initial sample set.
        public static Plan Build(BrepEdge edge, BrepFace a, BrepFace b,
            int uniformCount, bool knotAware, int maximum, bool combinedAdaptive)
        {
            var plan = new Plan();
            var knots = new List<double>();
            if (knotAware)
            {
                Gather(edge,a,knots,plan);
                Gather(edge,b,knots,plan);
            }
            double eps = Math.Max(Math.Abs(edge.Domain.Length)*1e-9, 1e-14);
            knots.Sort();
            var distinct = new List<double>();
            foreach (double k in knots)
                if (distinct.Count==0 || k-distinct[distinct.Count-1] > eps) distinct.Add(k);
            plan.KnotCrossings = distinct.Count;

            // Keep the full edge covered. Knot-heavy edges must NEVER displace the
            // evenly-spaced baseline by exhausting the capacity at the first knot.
            var uniform = new List<Point>();
            uniform.Add(new Point(edge.Domain.T0,"Endpoint"));
            for(int i=1;i<uniformCount-1;i++)
            {
                double f=i/(uniformCount-1.0), t;
                if (!edge.NormalizedLengthParameter(f,out t)) t=edge.Domain.ParameterAt(f);
                uniform.Add(new Point(t,"Uniform"));
            }
            uniform.Add(new Point(edge.Domain.T1,"Endpoint"));
            foreach(var p in uniform.OrderBy(x=>x.T))
                AddUnique(plan.Points,p,eps);
            if (!knotAware || distinct.Count==0) return plan;

            // At a knot a high derivative can be one-sided; probe both flanks.
            // The progressive order never depends on remaining capacity.
            var flankPairs = new List<Point[]>();
            for(int i=0;i<distinct.Count;i++)
            {
                double t=distinct[i];
                double left=i==0?edge.Domain.T0:distinct[i-1];
                double right=i==distinct.Count-1?edge.Domain.T1:distinct[i+1];
                double shift=Math.Min(t-left,right-t)*.001;
                if (shift <= 3*eps)
                {
                    plan.KnotTargets.Add(new KnotCoverageAudit.Crossing {
                        Center=t, PlanIssue="FLANK_TOO_CLOSE_TO_NEIGHBOUR_OR_ENDPOINT" });
                    continue;
                }
                plan.KnotTargets.Add(new KnotCoverageAudit.Crossing {
                    Center=t, Left=t-shift, Right=t+shift });
                flankPairs.Add(new[]{new Point(t-shift,"KnotLeft"),
                                      new Point(t+shift,"KnotRight")});
            }
            foreach(int index in ProgressiveKnotOrder(flankPairs,edge.Domain))
            {
                var pair=flankPairs[index];
                if(combinedAdaptive)
                {
                    plan.KnotProbes.Add(pair[0]);
                    plan.KnotProbes.Add(pair[1]);
                }
                else
                {
                    // Do not commit only one flank in the knot-only mode.
                    if(plan.Points.Count+2>maximum)
                    { plan.CapacityReached=true; break; }
                    AddUnique(plan.Points,pair[0],eps);
                    AddUnique(plan.Points,pair[1],eps);
                }
            }
            plan.Points.Sort((p,q)=>p.T.CompareTo(q.T));
            return plan;
        }

        /// <summary>
        /// Farthest-first knot PAIR order based on fractional parameter position.
        /// First centre, then maximize distance to previously selected knots.
        /// Stable under increasing MaxSamples, with lower index tie-breaking.
        /// </summary>
        private static IEnumerable<int> ProgressiveKnotOrder(List<Point[]> pairs, Interval domain)
        {
            int n=pairs.Count;
            var positions=pairs.Select(p=>(p[0].T+p[1].T)*.5).ToArray();
            var distances=Enumerable.Repeat(double.PositiveInfinity,n).ToArray();
            var taken=new bool[n];
            for(int step=0;step<n;step++)
            {
                int best=-1;
                double score=double.NegativeInfinity;
                for(int i=0;i<n;i++)
                {
                    if(taken[i]) continue;
                    double priority=step==0 ? -Math.Abs(positions[i]-domain.Mid) : distances[i];
                    if(best<0 || priority>score+Math.Abs(domain.Length)*1e-13 ||
                        (Math.Abs(priority-score)<=Math.Abs(domain.Length)*1e-13 && i<best))
                    { best=i; score=priority; }
                }
                taken[best]=true;
                yield return best;
                for(int i=0;i<n;i++)
                    if(!taken[i]) distances[i]=Math.Min(distances[i],Math.Abs(positions[i]-positions[best]));
            }
        }

        /// <summary>
        /// Combined deterministic sampling for KnotAware + Adaptive. The baseline
        /// never changes with MaxSamples. Alternate one adaptive probe and two
        /// knot flanks until streams are exhausted. Both streams are deterministic,
        /// so increasing MaxSamples can only append measurements. This is not an
        /// interval-wide certificate and cannot guarantee detection of all defects.
        /// </summary>
        public static void ExpandCombined(List<SampleReportV9> samples,
            IList<Point> knotProbes, Func<double,string,SampleReportV9> measure,
            int maximumSamples,double parameterEpsilon,double[] relative,double[] absolute,
            out bool capacityReached)
        {
            capacityReached=false;
            var baseline=samples.OrderBy(x=>x.EdgeParameter).ToArray();
            SampleReportV9 Find(double t)
            { return samples.FirstOrDefault(s=>Math.Abs(s.EdgeParameter-t)<=parameterEpsilon); }
            using(var adaptive=AdaptiveCandidateStream(baseline,parameterEpsilon,
                relative,absolute,Find).GetEnumerator())
            {
                bool moreAdaptive=true;
                int knotIndex=0;
                while(true)
                {
                    bool any=false;
                    if(moreAdaptive)
                    {
                        moreAdaptive=adaptive.MoveNext();
                        if(moreAdaptive)
                        {
                            if(samples.Count>=maximumSamples) { capacityReached=true; return; }
                            if(measure(adaptive.Current.T,adaptive.Current.Source)==null)
                            { capacityReached=true; return; }
                            any=true;
                        }
                    }
                    // A whole pair is kept adjacent in the stream. With an odd
                    // remaining budget only its left flank is committed; this is
                    // explicitly a capacity-limited, sampled-only result.
                    for(int k=0;k<2 && knotIndex<knotProbes.Count;k++)
                    {
                        Point point=knotProbes[knotIndex++];
                        if(samples.Count>=maximumSamples) { capacityReached=true; return; }
                        if(measure(point.T,point.Source)==null)
                        { capacityReached=true; return; }
                        any=true;
                    }
                    if(!any) return;
                }
            }
        }


        /// <summary>Conservative number of stations necessary for every probeable
        /// knot pair, given the FIXED uniform baseline. Overlapping/failed trim
        /// mappings still require separate warnings: this is no certificate.
        /// </summary>
        public static int MinimumKnotBudget(Plan plan, int baselineMinimum)
        {
            if (plan == null) return baselineMinimum;
            var selected = plan.Points.Select(p => p.T).ToList();
            double eps = selected.Count < 2 ? 1e-14 :
                Math.Max(Math.Abs(selected.Max()-selected.Min())*1e-10,1e-14);
            foreach (var p in plan.KnotProbes)
                if (!selected.Any(t => Math.Abs(t-p.T)<=eps)) selected.Add(p.T);
            return Math.Max(baselineMinimum, selected.Count);
        }

        /// <summary>
        /// Knot-first mode for coverage audits. All knot flanks are tried as
        /// ATOMIC left/right pairs before adaptive stations. Existing baseline
        /// samples count toward the pair. If the cap cannot fit a complete pair,
        /// no half-pair is silently accepted. For a fixed baseline and knot list,
        /// increasing the cap keeps every earlier station.
        /// This prioritizes knot coverage; it cannot guarantee all flanks at a
        /// finite cap and does not certify the between-knot intervals.
        /// </summary>
        public static void ExpandKnotFirst(List<SampleReportV9> samples,
            IList<Point> knotProbes, Func<double,string,SampleReportV9> measure,
            int maximumSamples, double parameterEpsilon,double[] relative,
            double[] absolute,out bool capacityReached)
        {
            capacityReached=false;
            if(knotProbes==null) knotProbes=new Point[0];
            if(knotProbes.Count%2!=0)
                throw new ArgumentException("Knot flanks must be atomic left/right pairs.");
            for(int i=0;i<knotProbes.Count;i+=2)
            {
                var left=knotProbes[i];
                var right=knotProbes[i+1];
                int required=0;
                if(!samples.Any(s=>Math.Abs(s.EdgeParameter-left.T)<=parameterEpsilon)) required++;
                if(!samples.Any(s=>Math.Abs(s.EdgeParameter-right.T)<=parameterEpsilon) &&
                   Math.Abs(right.T-left.T)>parameterEpsilon) required++;
                if(samples.Count+required>maximumSamples)
                { capacityReached=true; return; }
                if(measure(left.T,left.Source)==null || measure(right.T,right.Source)==null)
                { capacityReached=true; return; }
            }
            bool adaptiveCap;
            ExpandAdaptive(samples,measure,maximumSamples,parameterEpsilon,
                relative,absolute,out adaptiveCap);
            capacityReached=adaptiveCap;
        }

        private static IEnumerable<Point> AdaptiveCandidateStream(
            SampleReportV9[] roots,double parameterEpsilon,double[] relative,
            double[] absolute,Func<double,SampleReportV9> lookup)
        {
            var intervals=Enumerable.Range(0,Math.Max(0,roots.Length-1))
                .Where(j=>roots[j+1].EdgeParameter-roots[j].EdgeParameter >= 4*parameterEpsilon)
                .ToArray();
            foreach(int j in ProgressiveIntervalOrder(roots,intervals))
            {
                double lo=roots[j].EdgeParameter, hi=roots[j+1].EdgeParameter;
                double mid=(lo+hi)*.5;
                yield return new Point(mid,"AdaptiveMid");
                SampleReportV9 measured=lookup(mid);
                if(measured!=null && hi-lo>=8*parameterEpsilon &&
                    NeedsRefinement(roots[j],measured,roots[j+1],relative,absolute))
                {
                    yield return new Point((lo+mid)*.5,"AdaptiveQuarter");
                    yield return new Point((mid+hi)*.5,"AdaptiveQuarter");
                }
            }
        }
        /// <summary>
        /// Finite-budget adaptive inspection used by the analyzer and NURBS proof.
        /// For an IDENTICAL input baseline and fixed classification thresholds,
        /// MaxSamples merely truncates a deterministic stream of probes.
        /// Once sampled, a point cannot disappear when the budget increases.
        /// This is NOT a certified bound between samples.
        /// </summary>
        public static void ExpandAdaptive(List<SampleReportV9> samples,
            Func<double,string,SampleReportV9> measure, int maximumSamples,
            double parameterEpsilon, double[] relative, double[] absolute,
            out bool capacityReached)
        {
            capacityReached = false;
            var roots = samples.OrderBy(s=>s.EdgeParameter).ToArray();
            var intervals = Enumerable.Range(0,Math.Max(0,roots.Length-1))
                .Where(j=>roots[j+1].EdgeParameter-roots[j].EdgeParameter >= 4*parameterEpsilon)
                .ToArray();
            // No capacity-dependent midpointBudget, quantiles or interval ordering.
            // In particular, the midpoint at 37.5% selected with cap=6 must also
            // appear at cap=7, 9, 13, etc. (for the same baseline sample points).
            foreach(int j in ProgressiveIntervalOrder(roots,intervals))
            {
                if(samples.Count>=maximumSamples) { capacityReached=true; return; }
                double leftT=roots[j].EdgeParameter, rightT=roots[j+1].EdgeParameter;
                var mid=measure((leftT+rightT)*.5,"AdaptiveMid");
                if(mid==null) { capacityReached=true; return; }
                if(rightT-leftT < 8*parameterEpsilon ||
                   !NeedsRefinement(roots[j],mid,roots[j+1],relative,absolute)) continue;

                // Quarters are part of the SAME budget-independent probe stream.
                // If the cap is reached after the first quarter, a larger cap
                // repeats that quarter and then proceeds to the second one.
                if(samples.Count>=maximumSamples) { capacityReached=true; return; }
                if(measure((leftT+mid.EdgeParameter)*.5,"AdaptiveQuarter")==null)
                { capacityReached=true; return; }
                if(samples.Count>=maximumSamples) { capacityReached=true; return; }
                if(measure((mid.EdgeParameter+rightT)*.5,"AdaptiveQuarter")==null)
                { capacityReached=true; return; }
            }
        }

        /// <summary>
        /// Greedy farthest-first coverage of BASELINE intervals. The initial
        /// interval is nearest the seam centre (lower index breaks ties); later
        /// intervals maximise distance from prior interval centres. Selection
        /// depends only on baseline fractions, never on MaxSamples. O(n^2).
        /// </summary>
        private static IEnumerable<int> ProgressiveIntervalOrder(
            SampleReportV9[] roots, int[] intervals)
        {
            int n=intervals.Length;
            var centres=new double[n];
            var nearest=new double[n];
            var taken=new bool[n];
            for(int k=0;k<n;k++)
            {
                int j=intervals[k];
                centres[k]=(roots[j].Fraction+roots[j+1].Fraction)*.5;
                nearest[k]=double.PositiveInfinity;
            }
            for(int count=0;count<n;count++)
            {
                int best=-1;
                double score=double.NegativeInfinity;
                for(int k=0;k<n;k++)
                {
                    if(taken[k]) continue;
                    double value=count==0 ? -Math.Abs(centres[k]-.5) : nearest[k];
                    if(best<0 || value>score+1e-13 ||
                        (Math.Abs(value-score)<=1e-13 && intervals[k]<intervals[best]))
                    { best=k; score=value; }
                }
                taken[best]=true;
                yield return intervals[best];
                for(int k=0;k<n;k++)
                    if(!taken[k]) nearest[k]=Math.Min(nearest[k],
                        Math.Abs(centres[k]-centres[best]));
            }
        }

        private static void AddUnique(List<Point> points, Point candidate, double epsilon)
        {
            if (!SampleReportV9.Finite(candidate.T)) return;
            if (!points.Any(p=>Math.Abs(p.T-candidate.T)<=epsilon)) points.Add(candidate);
        }
        private static void Gather(BrepEdge edge,BrepFace face,List<double> knots,Plan plan)
        {
            BrepTrim trim=null;
            foreach(BrepTrim candidate in edge.TrimIndices().Select(i=>face.Brep.Trims[i]))
            {
                if (candidate.Face!=null && candidate.Face.FaceIndex==face.FaceIndex)
                { trim=candidate; break; }
            }
            if(trim==null) { plan.KnotMappingIncomplete=true; return; }
            try
            {
                using(NurbsSurface surface=face.ToNurbsSurface())
                {
                    if(surface==null) { plan.KnotMappingIncomplete=true; return; }
                    for(int axis=0;axis<2;axis++)
                    {
                        var values=axis==0?surface.KnotsU:surface.KnotsV;
                        var domain=surface.Domain(axis);
                        double last=double.NaN;
                        for(int i=0;i<values.Count;i++)
                        {
                            double k=values[i];
                            if (!SampleReportV9.Finite(k) || !Interior(k,domain)) continue;
                            if(SampleReportV9.Finite(last) && Math.Abs(k-last)<=Math.Abs(domain.Length)*1e-12) continue;
                            last=k;
                            // Parameter-space knot iso-lines are infinite; CurveLine
                            // works for any trim curve in its native UV coordinates.
                            // Use the real UV domain of the opposite coordinate;
                            // production Rhino surfaces need not have a 0..1 domain.
                            Interval other=surface.Domain(1-axis);
                            Line line=axis==0 ?
                                new Line(new Point3d(k,other.T0,0),new Point3d(k,other.T1,0)) :
                                new Line(new Point3d(other.T0,k,0),new Point3d(other.T1,k,0));
                            double tolerance=Math.Max(Math.Abs(domain.Length)*1e-11,1e-12);
                            var intersections=Intersection.CurveLine(trim,line,tolerance,0);
                            if(intersections==null) { plan.KnotMappingIncomplete=true; continue; }
                            foreach(var ev in intersections)
                            {
                                if(ev.IsOverlap)
                                {
                                    plan.KnotOverlap=true;
                                    // Conservatively include overlap interval ends.
                                    AddMapped(edge,trim,ev.OverlapA.T0,knots,plan);
                                    AddMapped(edge,trim,ev.OverlapA.T1,knots,plan);
                                }
                                else AddMapped(edge,trim,ev.ParameterA,knots,plan);
                            }
                        }
                    }
                }
            }
            catch { plan.KnotMappingIncomplete=true; }
        }
        private static void AddMapped(BrepEdge edge,BrepTrim trim,double u,List<double> knots,Plan plan)
        {
            double t;
            if(edge.GetEdgeParameter(trim.TrimIndex,u,out t) && SampleReportV9.Finite(t))
            {
                if(Interior(t,edge.Domain)) knots.Add(t);
            }
            else plan.KnotMappingIncomplete=true;
        }
        /// <summary>Subdivision predicate: boundary transitions, near-limit jets,
        /// or nonlinear deviation of derivative differences from interpolation.</summary>
        public static bool NeedsRefinement(SampleReportV9 left,SampleReportV9 mid,SampleReportV9 right,
            double[] relative,double[] absolute)
        {
            if(!left.Complete || !mid.Complete || !right.Complete) return true;
            if(left.Status!=mid.Status || mid.Status!=right.Status) return true;
            for(int i=0;i<14;i++)
            {
                int order=i<2?-1:i<5?0:i<9?1:2;
                if(order<0) continue;
                double a=left.GraphA[i]-left.GraphB[i], b=mid.GraphA[i]-mid.GraphB[i], c=right.GraphA[i]-right.GraphB[i];
                double allowed=ContinuityEvaluator.AllowedDifference(mid.GraphA[i],mid.GraphB[i],relative[order],absolute[order]);
                double ratio=ContinuityEvaluator.ToleranceRatio(mid.GraphA[i],mid.GraphB[i],relative[order],absolute[order]);
                if(ratio>=.8 && ratio<=2.0) return true;
                double magnitude=Math.Max(Math.Abs(a),Math.Max(Math.Abs(b),Math.Abs(c)));
                if(magnitude>0 && Math.Abs(b-(a+c)*.5)>.25*magnitude &&
                    (allowed==0 || magnitude>allowed*.25)) return true;
            }
            return false;
        }
    }
}
