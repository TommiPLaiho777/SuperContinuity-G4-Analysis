using System;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Local plane-section curvature from surface partial derivatives through order four.
    /// No intersection curve, finite-difference step, endpoint offset or extrapolation is used.
    /// K2 = signed curvature, K3 = dK2/ds, K4 = d2K2/ds2 in a common section frame.
    /// These directional section quantities do not certify full surface G2/G3/G4 continuity.
    /// </summary>
    public static class SectionJetEvaluator
    {
        private static readonly double[] Factorial = { 1, 1, 2, 6, 24 };

        public static bool Evaluate(Surface surface, Point3d uv, Vector3d x, Vector3d z,
            out double[] values, out string reason)
        {
            values = null;
            // At an internal knot, native evaluation may choose one side. Do not silently
            // report that side as a unique fourth-order derivative.
            if (uv.IsValid && !surface.IsContinuous(Continuity.Cinfinity_continuous, uv.X, uv.Y))
            { reason = "Internal surface knot: a unique fourth-order jet is not established."; return false; }
            Point3d point;
            Vector3d[] derivatives;
            if (!uv.IsValid || !surface.Evaluate(uv.X, uv.Y, 4, out point, out derivatives))
            { reason = "Surface partial derivatives through order four are unavailable."; return false; }
            return FromDerivatives(derivatives, x, z, out values, out reason);
        }

        /// <summary>
        /// Solve S(u(t),v(t)) with section X=t and section-normal coordinate zero.
        /// Taylor coefficients are derivative/factorial. At each order the same 2x2
        /// Jacobian determines the two new parameter coefficients. Parameters are
        /// rescaled by |Su| and |Sv| to reduce sensitivity to parameter-domain scale.
        /// </summary>
        public static bool FromDerivatives(Vector3d[] derivatives, Vector3d x, Vector3d z,
            out double[] values, out string reason)
        {
            values = null;
            reason = "";
            if (derivatives == null || derivatives.Length < 14 || !x.Unitize() || !z.Unitize()
                || Math.Abs(x * z) > 1e-10)
            { reason = "Invalid derivative array or section frame."; return false; }
            Vector3d m = Vector3d.CrossProduct(x, z);
            if (!m.Unitize()) { reason = "Degenerate section plane."; return false; }
            double su = derivatives[0].Length, sv = derivatives[1].Length;
            if (!SampleReportV9.Finite(su) || !SampleReportV9.Finite(sv) || su <= 0 || sv <= 0)
            { reason = "Singular surface parameterization."; return false; }

            var coefficients = new Vector3d[5,5];
            int index = 0;
            for (int degree = 1; degree <= 4; degree++)
                for (int j = 0; j <= degree; j++)
                {
                    int i = degree - j;
                    Vector3d d = derivatives[index++];
                    // Sequential division avoids forming an unnecessarily large scale product.
                    for (int k = 0; k < i; k++) d /= su;
                    for (int k = 0; k < j; k++) d /= sv;
                    d /= Factorial[i] * Factorial[j];
                    if (!d.IsValid) { reason = "Non-finite surface derivative."; return false; }
                    coefficients[i,j] = d;
                }
            Vector3d a = coefficients[1,0], b = coefficients[0,1];
            double ax = a*x, bx = b*x, am = a*m, bm = b*m;
            double determinant = ax*bm - bx*am;
            // This is an availability guard, not a continuity acceptance tolerance.
            if (Math.Abs(determinant) < 1e-10)
            { reason = "Section is tangent to the surface or its parameterization is ill-conditioned."; return false; }

            var u = new double[5];
            var v = new double[5];
            var height = new double[5];
            for (int order = 1; order <= 4; order++)
            {
                Vector3d remainder = Compose(coefficients, u, v, order);
                double targetX = (order == 1 ? 1.0 : 0.0) - remainder*x;
                double targetM = -remainder*m;
                u[order] = (targetX*bm - bx*targetM)/determinant;
                v[order] = (ax*targetM - targetX*am)/determinant;
                Vector3d term = remainder + a*u[order] + b*v[order];
                if (!term.IsValid) { reason = "Non-finite implicit section derivative."; return false; }
                height[order] = (term*z)*Factorial[order];
            }
            double p = height[1], q = height[2], r = height[3], t = height[4];
            double w = 1 + p*p;
            values = new[] {
                q/Math.Pow(w,1.5),
                r/(w*w) - 3*p*q*q/(w*w*w),
                t/Math.Pow(w,2.5) - (10*p*q*r + 3*q*q*q)/Math.Pow(w,3.5)
                    + 18*p*p*q*q*q/Math.Pow(w,4.5)
            };
            foreach (double value in values)
                if (!SampleReportV9.Finite(value))
                { values = null; reason = "Non-finite section-curvature result."; return false; }
            return true;
        }

        private static Vector3d Compose(Vector3d[,] coefficients, double[] u, double[] v, int order)
        {
            var up = Powers(u, order);
            var vp = Powers(v, order);
            var result = Vector3d.Zero;
            for (int degree = 1; degree <= order; degree++)
                for (int j = 0; j <= degree; j++)
                {
                    int i = degree-j;
                    double weight = 0;
                    for (int k = 0; k <= order; k++) weight += up[i,k]*vp[j,order-k];
                    result += coefficients[i,j]*weight;
                }
            return result;
        }

        private static double[,] Powers(double[] series, int order)
        {
            var result = new double[5,5];
            result[0,0] = 1;
            for (int power = 1; power <= order; power++)
                for (int n = 1; n <= order; n++)
                    for (int k = 1; k <= n; k++) result[power,n] += result[power-1,n-k]*series[k];
            return result;
        }
    }
}
