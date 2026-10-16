using System;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Measures paired surface-boundary positions and tangent planes at one shared-edge parameter.
    /// Uses native edge-to-trim correspondence; never compares the shared 3D edge with itself.
    /// These are sampled boundary measurements, not a global maximum or an error bound.
    /// </summary>
    public static class BoundaryContinuityEvaluator
    {
        // #SC-MATH-13 TOPOLOGY DOES NOT PROVE GEOMETRIC CONTINUITY.
        // A joined Brep edge supplies topology and a parameter correspondence, but
        // comparing that shared edge to itself would ALWAYS report a zero gap.
        // Evaluate the separate face trims instead. The current G0 value is the
        // maximum of two LOCAL directed trim-distance searches, not a certified
        // Hausdorff distance for the whole seam. Preserve same-UV paired distance
        // and mapping residual as separate diagnostics; they answer different
        // questions. Prepare caches only topology and must not outlive geometry edits.
        public static void Measure(BrepEdge edge, BrepFace faceA, BrepFace faceB,
            double edgeParameter, SampleReportV9 report)
        {
            BrepTrim trimA = FindTrim(edge, faceA);
            BrepTrim trimB = FindTrim(edge, faceB);
            if (trimA == null || trimB == null)
            { report.BoundaryReason = "Missing or ambiguous face trim for this edge."; return; }
            double ta, tb;
            if (!trimA.GetTrimParameter(edgeParameter, out ta) || !trimB.GetTrimParameter(edgeParameter, out tb))
            { report.BoundaryReason = "Native edge-to-trim parameter mapping failed."; return; }
            Point3d uvA = trimA.PointAt(ta), uvB = trimB.PointAt(tb);
            if (!uvA.IsValid || !uvB.IsValid)
            { report.BoundaryReason = "Invalid trim-space coordinates."; return; }
            report.TrimA = uvA; report.TrimB = uvB;
            Point3d pa = faceA.PointAt(uvA.X, uvA.Y), pb = faceB.PointAt(uvB.X, uvB.Y);
            if (!pa.IsValid || !pb.IsValid)
            { report.BoundaryReason = "Surface boundary evaluation failed."; return; }
            report.BoundaryA = pa; report.BoundaryB = pb;
            report.PairedSurfaceGap = pa.DistanceTo(pb);
            var nearestOnB=TrimToTrimG0.Find(faceB,trimB,pa,tb,report.PairedSurfaceGap,0);
            var nearestOnA=TrimToTrimG0.Find(faceA,trimA,pb,ta,report.PairedSurfaceGap,0);
            if(nearestOnA.Valid && nearestOnB.Valid)
            {
                report.G0AtoB=nearestOnB.Distance;
                report.G0BtoA=nearestOnA.Distance;
                report.Gap=Math.Max(report.G0AtoB,report.G0BtoA);
                report.G0Measurement="DIRECTED_TRIM_TO_TRIM_LOCAL";
            }
            else
            { report.G0Measurement="UNAVAILABLE: "+nearestOnB.Reason+" / "+nearestOnA.Reason;
              report.BoundaryReason=report.G0Measurement; }
            Point3d reference = edge.PointAt(edgeParameter);
            report.MappingResidual = Math.Max(pa.DistanceTo(reference), pb.DistanceTo(reference));
            if (report.G0Valid) report.BoundaryReason = "";

            Vector3d na = faceA.NormalAt(uvA.X, uvA.Y), nb = faceB.NormalAt(uvB.X, uvB.Y);
            if (!na.IsValid || !nb.IsValid || !na.Unitize() || !nb.Unitize())
            { report.BoundaryReason = "A surface normal is singular or invalid, so G1 is unavailable."; return; }
            // Face orientation is used only for the separate normal-orientation diagnostic.
            if (faceA.OrientationIsReversed) na.Reverse();
            if (faceB.OrientationIsReversed) nb.Reverse();
            report.NormalOpposed = na * nb < 0;
            report.NormalAngleDegrees = AngleDegrees(na, nb, false);
            report.AngleDegrees = AngleDegrees(na, nb, true);
        }

        /// <summary>Resolve fixed trim topology once per seam. Do not retain across geometry edits.</summary>
        public static PreparedPair Prepare(BrepEdge edge, BrepFace faceA, BrepFace faceB)
        { return new PreparedPair(edge, faceA, faceB, FindTrim(edge, faceA), FindTrim(edge, faceB)); }

        public sealed class PreparedPair
        {
            private readonly BrepEdge edge;
            private readonly BrepFace faceA, faceB;
            private readonly BrepTrim trimA, trimB;
            internal PreparedPair(BrepEdge edge, BrepFace faceA, BrepFace faceB, BrepTrim trimA, BrepTrim trimB)
            { this.edge=edge; this.faceA=faceA; this.faceB=faceB; this.trimA=trimA; this.trimB=trimB; }
            public void Measure(double edgeParameter, SampleReportV9 report)
            {
            if (trimA == null || trimB == null)
            { report.BoundaryReason = "Missing or ambiguous face trim for this edge."; return; }
            double ta, tb;
            if (!trimA.GetTrimParameter(edgeParameter, out ta) || !trimB.GetTrimParameter(edgeParameter, out tb))
            { report.BoundaryReason = "Native edge-to-trim parameter mapping failed."; return; }
            Point3d uvA = trimA.PointAt(ta), uvB = trimB.PointAt(tb);
            if (!uvA.IsValid || !uvB.IsValid)
            { report.BoundaryReason = "Invalid trim-space coordinates."; return; }
            report.TrimA = uvA; report.TrimB = uvB;
            Point3d pa = faceA.PointAt(uvA.X, uvA.Y), pb = faceB.PointAt(uvB.X, uvB.Y);
            if (!pa.IsValid || !pb.IsValid)
            { report.BoundaryReason = "Surface boundary evaluation failed."; return; }
            report.BoundaryA = pa; report.BoundaryB = pb;
            report.PairedSurfaceGap = pa.DistanceTo(pb);
            var nearestOnB=TrimToTrimG0.Find(faceB,trimB,pa,tb,report.PairedSurfaceGap,0);
            var nearestOnA=TrimToTrimG0.Find(faceA,trimA,pb,ta,report.PairedSurfaceGap,0);
            if(nearestOnA.Valid && nearestOnB.Valid)
            {
                report.G0AtoB=nearestOnB.Distance;
                report.G0BtoA=nearestOnA.Distance;
                report.Gap=Math.Max(report.G0AtoB,report.G0BtoA);
                report.G0Measurement="DIRECTED_TRIM_TO_TRIM_LOCAL";
            }
            else
            { report.G0Measurement="UNAVAILABLE: "+nearestOnB.Reason+" / "+nearestOnA.Reason;
              report.BoundaryReason=report.G0Measurement; }
            Point3d reference = edge.PointAt(edgeParameter);
            report.MappingResidual = Math.Max(pa.DistanceTo(reference), pb.DistanceTo(reference));
            if (report.G0Valid) report.BoundaryReason = "";

            Vector3d na = faceA.NormalAt(uvA.X, uvA.Y), nb = faceB.NormalAt(uvB.X, uvB.Y);
            if (!na.IsValid || !nb.IsValid || !na.Unitize() || !nb.Unitize())
            { report.BoundaryReason = "A surface normal is singular or invalid, so G1 is unavailable."; return; }
            // Face orientation is used only for the separate normal-orientation diagnostic.
            if (faceA.OrientationIsReversed) na.Reverse();
            if (faceB.OrientationIsReversed) nb.Reverse();
            report.NormalOpposed = na * nb < 0;
            report.NormalAngleDegrees = AngleDegrees(na, nb, false);
            report.AngleDegrees = AngleDegrees(na, nb, true);
        }
        }


        /// <summary>
        /// atan2 avoids acos cancellation for very small angles.
        /// With unoriented=true, opposite normals describe the same tangent plane (0..90 degrees).
        /// Oriented normal mismatch is retained separately; overlap/fold validity is not tested here.
        /// </summary>
        public static double AngleDegrees(Vector3d a, Vector3d b, bool unoriented)
        {
            // #SC-NUM-02 PLANE AGREEMENT VERSUS NORMAL ORIENTATION.
            // atan2(|a cross b|,|a dot b|) avoids acos cancellation near zero and
            // identifies opposite normals with the same tangent plane. An oriented
            // angle is recorded separately: no inference about solid orientation,
            // self-overlap, or correct joining should be drawn from G1 alone.
            if (!a.IsValid || !b.IsValid || !a.Unitize() || !b.Unitize()) return double.NaN;
            double dot = a * b;
            if (unoriented) dot = Math.Abs(dot);
            double sine = Vector3d.CrossProduct(a,b).Length;
            return Math.Atan2(sine,dot) * 180.0 / Math.PI;
        }

        private static BrepTrim FindTrim(BrepEdge edge, BrepFace face)
        {
            BrepTrim match = null;
            foreach (int index in edge.TrimIndices())
            {
                BrepTrim trim = edge.Brep.Trims[index];
                if (trim.Face == null || trim.Face.FaceIndex != face.FaceIndex) continue;
                if (match != null) return null; // Do not guess on self-seams or ambiguous topology.
                match = trim;
            }
            return match;
        }
    }
}
