using System;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Sampled, directed point-to-trimmed-surface-boundary distance, evaluated on the
    /// actual face at the actual trim UV. Does NOT use the shared BrepEdge as either
    /// of the compared curves, and does NOT use approximating Pushup curves.
    ///
    /// This is a local 1D nearest-point estimate, NOT a Hausdorff certificate:
    /// it searches a generous window around the native edge-to-trim station. An
    /// unresolved window boundary yields unavailable data, never an invented pass.
    /// </summary>
    public static class TrimToTrimG0
    {
        public struct Nearest
        {
            public bool Valid;
            public double Distance, Parameter;
            public string Reason;
        }

        public static Nearest Find(BrepFace targetFace, BrepTrim targetTrim,
            Point3d sourcePoint, double mappedTrimParameter, double pairedDistance,
            double modelTolerance)
        {
            Interval domain = targetTrim.Domain;
            return Find(sourcePoint, mappedTrimParameter, pairedDistance,
                modelTolerance, domain.T0, domain.T1,
                t =>
                {
                    Point3d uv=targetTrim.PointAt(t);
                    return uv.IsValid ? targetFace.PointAt(uv.X,uv.Y) : Point3d.Unset;
                });
        }

        // The geometric 1D kernel is also callable with known analytic curves in self-tests.
        public static Nearest Find(Point3d source, double station, double pairedDistance,
            double modelTolerance, double domainStart, double domainEnd,
            Func<double,Point3d> curve)
        {
            var missing = new Nearest { Valid=false, Distance=double.NaN, Parameter=double.NaN,
                Reason="Closest boundary point unavailable" };
            if (!source.IsValid || curve==null || !SampleReportV9.Finite(station) ||
                !SampleReportV9.Finite(pairedDistance) || pairedDistance<0 ||
                !SampleReportV9.Finite(modelTolerance) || modelTolerance<0 ||
                !SampleReportV9.Finite(domainStart) || !SampleReportV9.Finite(domainEnd) ||
                domainEnd<=domainStart) return missing;

            double span=domainEnd-domainStart;
            double t=Math.Max(domainStart,Math.Min(domainEnd,station));
            Point3d initial=curve(t);
            if(!initial.IsValid) { missing.Reason="Native trim point unavailable"; return missing; }
            double initialDistance=source.DistanceTo(initial);
            // A native paired point already gives a conservative distance bound. At
            // exact coincidence no search is needed, and roundoff cannot create a gap.
            if(initialDistance==0)
                return new Nearest { Valid=true, Distance=0, Parameter=t, Reason="EXACT_NATIVE" };

            // Convert geometric mismatch into a local 1D search radius via the
            // actual pushed-on-surface boundary speed (not trim UV speed alone).
            double dt=Math.Max(span*1e-6, 1e-12*Math.Max(1,Math.Abs(t)));
            double lo=Math.Max(domainStart,t-dt), hi=Math.Min(domainEnd,t+dt);
            Point3d pLo=curve(lo),pHi=curve(hi);
            double speed=pLo.IsValid && pHi.IsValid && hi>lo ?
                pLo.DistanceTo(pHi)/(hi-lo) : double.NaN;
            if(!SampleReportV9.Finite(speed) || speed<=1e-15)
            { missing.Reason="Degenerate/singular 3D trim speed"; return missing; }

            double half=Math.Max(span*5e-5,
                8*Math.Max(initialDistance,modelTolerance)/speed);
            half=Math.Min(span,half);
            Nearest best=missing;
            for(int expansion=0;expansion<6;expansion++)
            {
                double left=Math.Max(domainStart,t-half),right=Math.Min(domainEnd,t+half);
                if(right<=left) break;
                const int intervals=16;
                double step=(right-left)/intervals;
                int minIndex=-1;
                double minSq=double.PositiveInfinity;
                for(int j=0;j<=intervals;j++)
                {
                    double param=j==intervals ? right : left+j*step;
                    Point3d candidate=curve(param);
                    if(!candidate.IsValid) continue;
                    double sq=source.DistanceToSquared(candidate);
                    if(sq<minSq) { minSq=sq; minIndex=j; }
                }
                if(minIndex<0) { missing.Reason="No finite candidate in search window"; return missing; }

                double a=left+Math.Max(0,minIndex-1)*step;
                double b=left+Math.Min(intervals,minIndex+1)*step;
                // Refine the best coarse local candidate on the exact face-on-trim
                // geometry, without interpolating or fitting a proxy 3D curve.
                const double golden=0.6180339887498948482;
                double c=b-golden*(b-a),d=a+golden*(b-a);
                double fc=SquareDistance(source,curve(c)),fd=SquareDistance(source,curve(d));
                if(!SampleReportV9.Finite(fc) || !SampleReportV9.Finite(fd))
                { missing.Reason="Non-finite nearest-point refinement"; return missing; }
                for(int iter=0;iter<65 && b-a>span*1e-13;iter++)
                {
                    if(fc<=fd) { b=d; d=c; fd=fc; c=b-golden*(b-a); fc=SquareDistance(source,curve(c)); }
                    else { a=c; c=d; fc=fd; d=a+golden*(b-a); fd=SquareDistance(source,curve(d)); }
                    if(!SampleReportV9.Finite(fc)||!SampleReportV9.Finite(fd))
                    { missing.Reason="Non-finite nearest-point refinement"; return missing; }
                }
                double minT=fc<=fd?c:d, minD=Math.Sqrt(Math.Min(fc,fd));
                // Include both exact search-boundary candidates and the native
                // correspondence. This avoids bias at true trim endpoints.
                foreach(double candidateT in new[]{left,right,t})
                {
                    double sq=SquareDistance(source,curve(candidateT));
                    if(SampleReportV9.Finite(sq) && sq<minD*minD)
                    {minD=Math.Sqrt(sq); minT=candidateT;}
                }
                best=new Nearest { Valid=true, Distance=minD, Parameter=minT, Reason="LOCAL_NEAREST" };
                // If the optimal point is on an *artificial* window boundary,
                // expand instead of silently classifying a truncated search.
                double edgeMargin=step*.1;
                bool croppedLeft=left>domainStart+span*1e-13 && minT<=left+edgeMargin;
                bool croppedRight=right<domainEnd-span*1e-13 && minT>=right-edgeMargin;
                if(!croppedLeft && !croppedRight) return best;
                half=Math.Min(span,half*4);
            }
            // Endpoint minima are legitimate; artificial boundaries are not.
            missing.Reason="Nearest search window was not resolved";
            return missing;
        }

        private static double SquareDistance(Point3d source,Point3d target)
        { if (!target.IsValid) return double.NaN;
          double d=source.DistanceTo(target); return d*d; }
    }
}
