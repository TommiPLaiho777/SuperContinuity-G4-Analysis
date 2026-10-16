using System;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    /// <summary>
    /// Geometric 4-jet of a regular surface written as a height graph z=h(x,y)
    /// in a common orthonormal frame. The 14 entries are h_x,h_y (order 1),
    /// h_xx,h_xy,h_yy (order 2), four third-order partials and five fourth-order
    /// partials. Local regular UV reparameterisation effects are removed in exact
    /// arithmetic by graph-coordinate inversion; numerical tolerance decisions
    /// remain subject to conditioning and the chosen common physical frame.
    /// This establishes local differential-jet agreement only at EVALUATED POINTS.
    /// It does NOT bound the maximum deviation between samples.
    /// </summary>
    public static class SurfaceGraphJetEvaluator
    {
        // #SC-MATH-01 GRAPH-JET CONTRACT - see docs/SuperContinuity_G4_Mathematics.pdf.
        // A UV derivative is a derivative of a particular parametrization, not an
        // intrinsic measurement of surface contact. Both faces must first be written
        // over the SAME physical (x,y) plane, with the SAME height direction z.
        // We recover h(x,y) by inverting the projected surface coordinates as a
        // truncated Taylor series. Matching all mixed partials through degree four
        // then tests fourth-order contact at this station, subject to regularity.
        // This routine provides numbers and availability, never a PASS or a seam-wide
        // proof. ContinuityEvaluator owns tolerances; SeamReportV9 owns coverage.
        // Array order is total degree, descending x power: hx,hy; hxx,hxy,hyy;
        // hxxx,hxxy,hxyy,hyyy; hxxxx,hxxxy,hxxyy,hxyyy,hyyyy. Do not reorder it
        // without changing the native-derivative reader, classifier, and CSV together.
        private const int Order = 4;
        private static readonly double[] Fact = { 1, 1, 2, 6, 24 };

        public static bool Evaluate(Surface surface, Point3d uv, Vector3d x, Vector3d y, Vector3d z,
            out double[] jet, out string reason)
        {
            jet = null;
            if (surface == null || !uv.IsValid)
            { reason = "Invalid surface or UV parameter."; return false; }
            // #SC-MATH-02 KNOT AVAILABILITY IS NOT A GEOMETRY FAILURE.
            // The Cinfinity guard is intentionally more conservative than the
            // mathematical requirement of a unique fourth jet. A knot can preserve
            // sufficient smoothness in a special surface, yet this code declines it.
            // Callers audit nearby left/right probes instead of silently treating a
            // native one-sided evaluation as a unique two-sided derivative.
            // A derivative from only one side of an internal knot is not an unambiguous 4-jet.
            // Conservative: even a high-continuity internal knot is reported unavailable.
            if (!surface.IsContinuous(Continuity.Cinfinity_continuous, uv.X, uv.Y))
            { reason = "Internal knot: unique fourth-order partials not established."; return false; }
            Point3d point;
            Vector3d[] derivatives;
            if (!surface.Evaluate(uv.X, uv.Y, Order, out point, out derivatives))
            { reason = "Native fourth-order derivatives unavailable."; return false; }
            return FromDerivatives(derivatives, x, y, z, out jet, out reason);
        }

        public static bool FromDerivatives(Vector3d[] derivatives, Vector3d x, Vector3d y, Vector3d z,
            out double[] jet, out string reason)
        {
            jet = null;
            reason = "";
            if (derivatives == null || derivatives.Length < 14 ||
                !x.Unitize() || !y.Unitize() || !z.Unitize() ||
                Math.Abs(x*y)>1e-10 || Math.Abs(y*z)>1e-10 || Math.Abs(z*x)>1e-10 ||
                Vector3d.CrossProduct(x,y)*z < 1-1e-10)
            { reason = "Invalid common orthonormal frame or derivative array."; return false; }

            double su = derivatives[0].Length, sv = derivatives[1].Length;
            if (!SampleReportV9.Finite(su) || !SampleReportV9.Finite(sv) || su <= 0 || sv <= 0)
            { reason = "Singular surface UV coordinates."; return false; }

            // #SC-MATH-03 COEFFICIENTS ARE NOT RAW DERIVATIVES.
            // su=|S_u| and sv=|S_v| are CONSTANT scalings at the current station;
            // they are not differentiated and do not turn U,V into arclength fields.
            // c[i,j] = S_(u^i v^j)/(su^i sv^j i! j!) is the coefficient of U^i V^j.
            // Dividing by i!j! is essential: polynomial convolution composes Taylor
            // coefficients. The inverse factorial conversion is applied only when
            // output derivatives are recovered below. Scaling reduces domain-size
            // sensitivity, but cannot repair a singular or badly conditioned chart.
            // Normalised Taylor coefficients in coordinates U=su*(u-u0), V=sv*(v-v0).
            var coeff = new Vector3d[5,5];
            int index=0;
            for (int total=1; total<=Order; total++)
                for (int j=0; j<=total; j++)
                {
                    int i=total-j;
                    Vector3d derivative=derivatives[index++];
                    for (int k=0;k<i;k++) derivative/=su;
                    for (int k=0;k<j;k++) derivative/=sv;
                    derivative/=Fact[i]*Fact[j];
                    if (!derivative.IsValid)
                    { reason="Non-finite native partial derivative."; return false; }
                    coeff[i,j]=derivative;
                }

            Vector3d a=coeff[1,0], b=coeff[0,1];
            double ax=a*x, ay=a*y, bx=b*x, by=b*y;
            double det=ax*by-bx*ay;
            // #SC-MATH-04 INVERSE-FUNCTION GUARD.
            // J=[a.x b.x; a.y b.y] is the Jacobian of the physical planar projection.
            // det(J)!=0 permits a LOCAL inverse, not a global parameter inverse.
            // 1e-10 is this implementation's availability cutoff after scaling;
            // it is neither the G4 acceptance tolerance nor a condition-number bound.
            if (!SampleReportV9.Finite(det) || Math.Abs(det)<1e-10)
            { reason="Surface cannot be written as a regular graph over the common tangent plane."; return false; }

            var u=new double[5,5];
            var v=new double[5,5];
            var height=new double[5,5];
            for (int total=1; total<=Order; total++)
            {
                // #SC-MATH-05 DEGREE-BY-DEGREE SERIES REVERSION.
                // Write S(U(x,y),V(x,y))-S0 = a*U + b*V + nonlinear terms.
                // U,V have no constant coefficient. At total degree n, a new
                // unknown coefficient U_ij or V_ij therefore enters ONLY a*U+b*V:
                // any nonlinear monomial containing it has degree at least n+1.
                // Hence all nonlinear remainder terms R_ij are already determined
                // by lower degrees. Solve the SAME 2x2 J for each pair (i,j):
                // J [U_ij,V_ij]^T = [delta_(i,1;j,0),delta_(i,0;j,1)]^T - R_xy.
                // That is why one matrix solves every order without estimating
                // high derivatives by finite differences. Powers are rebuilt per
                // degree; same-degree unknowns cannot enter the nonlinear remainder.
                // Powers from all *lower* orders suffice: the unknown order-n terms
                // contribute only through the linear surface coefficients a and b.
                double[][,] up=Powers(u);
                double[][,] vp=Powers(v);
                for (int j=0; j<=total; j++)
                {
                    int i=total-j;
                    Vector3d rem=Vector3d.Zero;
                    for (int uvDegree=2; uvDegree<=total; uvDegree++)
                        for (int vDegree=0; vDegree<=uvDegree; vDegree++)
                        {
                            int uDegree=uvDegree-vDegree;
                            double weight=CoefficientOfProduct(up[uDegree],vp[vDegree],i,j);
                            rem+=coeff[uDegree,vDegree]*weight;
                        }
                    double targetX=(i==1 && j==0 ? 1.0 : 0.0)-rem*x;
                    double targetY=(i==0 && j==1 ? 1.0 : 0.0)-rem*y;
                    double ui=(targetX*by-bx*targetY)/det;
                    double vi=(ax*targetY-targetX*ay)/det;
                    Vector3d position=rem+a*ui+b*vi;
                    if (!SampleReportV9.Finite(ui) || !SampleReportV9.Finite(vi) || !position.IsValid)
                    { reason="Non-finite implicit 2D parameter inversion."; return false; }
                    u[i,j]=ui;
                    v[i,j]=vi;
                    height[i,j]=(position*z)*Fact[i]*Fact[j];
                    // #SC-MATH-06 UNITS AND FACTORIAL RESTORATION.
                    // Projection gives the coefficient of x^i*y^j in h, so multiply
                    // by i!j! to return the actual mixed partial. For model length L,
                    // a degree-n height derivative has units L^(1-n). In particular,
                    // fourth graph derivatives use L^-3, not curvature's L^-1.
                }
            }
            jet=new double[14];
            index=0;
            for (int total=1; total<=Order; total++)
                for (int j=0; j<=total; j++)
                {
                    double value=height[total-j,j];
                    if (!SampleReportV9.Finite(value))
                    { jet=null; reason="Non-finite height graph jet."; return false; }
                    jet[index++]=value;
                }
            return true;
        }

        private static double[][,] Powers(double[,] series)
        {
            var result=new double[5][,];
            result[0]=new double[5,5];
            result[0][0,0]=1;
            for (int i=1;i<=Order;i++) result[i]=Multiply(result[i-1],series);
            return result;
        }

        private static double[,] Multiply(double[,] left,double[,] right)
        {
            var product=new double[5,5];
            for (int i=0;i<=Order;i++)
                for (int j=0;j<=Order-i;j++)
                    product[i,j]=CoefficientOfProduct(left,right,i,j);
            return product;
        }

        private static double CoefficientOfProduct(double[,] a,double[,] b,int i,int j)
        {
            // #SC-MATH-07 TRUNCATED POWER-SERIES ALGEBRA.
            // [x^i y^j](A*B) = sum A_(di,dj) B_(i-di,j-dj).
            // There are no binomial factors here because entries are factorial-
            // normalized Taylor coefficients, not derivatives. Discarded degrees
            // >4 cannot feed back into lower degrees of a polynomial composition.
            double result=0;
            for(int di=0;di<=i;di++)
                for(int dj=0;dj<=j;dj++) result+=a[di,dj]*b[i-di,j-dj];
            return result;
        }
    }
}
