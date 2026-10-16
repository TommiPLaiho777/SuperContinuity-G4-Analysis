"""Create exact polynomial NURBS seam references for Rhino 8.

Run inside Rhino 8 with _-RunPythonScript. Creates a NEW .3dm model next to this script;
does not add or modify objects in the currently opened document.

Author: SuperContinuity reference geometry example, 2026.
"""

import os
import math
import Rhino
import System
from Rhino.Geometry import NurbsSurface, Point3d, Interval, Brep, TextDot
from Rhino.DocObjects import ObjectAttributes
from Rhino.FileIO import File3dm
from System.Drawing import Color

OUTPUT = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                      "SuperContinuity_G4_Reference_Surfaces.3dm")
TOLERANCE = 1.0e-6
DEGREE_U = 5
DEGREE_V = 2

# A nonflat polynomial background common to BOTH sides of every seam.
# Terms: (coefficient, x_degree, y_degree), y is local to each sample group.
COMMON = (
    (0.12, 2, 0),
    (0.05, 1, 1),
    (0.08, 0, 2),
    (0.03, 3, 1),
    (0.02, 4, 0),
    (0.01, 2, 2),
)

# right-hand addition, relative to the identical COMMON background.
# A x^m (1 + 0.5 y), A=0.10. Left-hand addition is identically zero.
CASES = (
    ("01_G4_NOT_G5", 5, 0.0, "TRUE G4, not G5; 5th-order jump", (29, 143, 88)),
    ("02_G3_NOT_G4", 4, 3.2, "G3 only; 4th-order jump", (44, 100, 216)),
    ("03_G2_NOT_G3", 3, 6.4, "G2 only; 3rd-order jump", (146, 78, 180)),
    ("04_SMOOTH_BOTH_SIDES", None, 9.6, "Smooth polynomial, all orders", (63, 164, 168)),
)


def binomial(n, k):
    """Integer binomial coefficient compatible with Rhino 8 Python engines."""
    if k < 0 or k > n:
        return 0
    k = min(k, n-k)
    value = 1
    for step in range(1, k+1):
        value = (value * (n-k+step)) // step
    return value


def bernstein_monomial(p, i, degree):
    """Bezier coefficient for t^p represented at degree 'degree'."""
    # #SC-REF-01 EXACT BASIS CONVERSION, NOT A SAMPLED FIT (PDF Appendix C).
    # t^p = sum_{i=p}^n C(i,p)/C(n,p) * B_i^n(t). Derive the identity
    # by inserting B_i^n=C(n,i)t^i(1-t)^(n-i) and using the binomial
    # theorem. Evaluating t^p at i/n would generally give DIFFERENT control
    # values. 'Exact' describes the polynomial identity; storage is floating point.
    if i < p:
        return 0.0
    return float(binomial(i, p)) / float(binomial(degree, p))


def height_control_value(x0, i, j, extra_degree, right):
    """Exact Bezier control coefficient of F(x0+u, v) on u,v in [0,1]."""
    # #SC-REF-02 MOVE THE DOMAIN BEFORE CONVERTING THE HEIGHT BASIS.
    # Left uses x=-1+u and right x=u. Expand each x power by the binomial
    # theorem, then apply the tensor-product Bernstein identity independently
    # to u and v. y offsets arrange examples in the document but must NOT
    # enter the local polynomial y used for each example's reference formula.
    terms = list(COMMON)
    if right and extra_degree is not None:
        terms.extend(((0.10, extra_degree, 0),
                      (0.05, extra_degree, 1)))
    value = 0.0
    for coefficient, px, py in terms:
        bv = bernstein_monomial(py, j, DEGREE_V)
        if bv == 0.0:
            continue
        for k in range(px+1):
            bu = bernstein_monomial(k, i, DEGREE_U)
            if bu:
                # x=(x0+u), so x^px=sum_k binom(px,k)*x0^(px-k)*u^k
                value += (coefficient * binomial(px, k) *
                          (x0 ** (px-k)) * bu * bv)
    return value


def polynomial_nurbs(left, extra_degree, offset_y):
    """Create an exact degree-(5,2) unweighted Bezier NURBS patch."""
    ns = NurbsSurface.Create(3, False,
                             DEGREE_U+1, DEGREE_V+1,
                             DEGREE_U+1, DEGREE_V+1)
    if ns is None:
        raise RuntimeError("NurbsSurface.Create returned None")
    for k in range(ns.KnotsU.Count):
        ns.KnotsU[k] = 0.0 if k < DEGREE_U else 1.0
    for k in range(ns.KnotsV.Count):
        ns.KnotsV[k] = 0.0 if k < DEGREE_V else 1.0
    x0 = -1.0 if left else 0.0
    for i in range(DEGREE_U+1):
        for j in range(DEGREE_V+1):
            x = x0 + float(i)/DEGREE_U
            y = offset_y + float(j)/DEGREE_V
            z = height_control_value(x0, i, j, extra_degree, not left)
            ns.Points.SetControlPoint(i, j, Point3d(x, y, z))
    ns.SetDomain(0, Interval(0.0, 1.0))
    ns.SetDomain(1, Interval(0.0, 1.0))
    if not ns.IsValid:
        raise RuntimeError("Generated NURBS surface is invalid")
    return ns


def add_layer(model, name, rgb):
    """Use File3dm's typed API: AddLayer returns an actual Int32 index."""
    color = Color.FromArgb(int(rgb[0]), int(rgb[1]), int(rgb[2]))
    index = model.Layers.AddLayer(name, color)
    if index is None:
        raise RuntimeError("File3dm.Layers.AddLayer unexpectedly returned None: " + name)
    index = int(index)
    if index < 0 or index >= model.Layers.Count:
        raise RuntimeError("Failed to create layer {} (index {})".format(name, index))
    if model.Layers[index].Name != name:
        raise RuntimeError("Layer mismatch {} -> {}".format(name, model.Layers[index].Name))
    return index


def add_object(model, geometry, layer_index, name):
    """Avoid overload ambiguity in File3dm.Objects.Add(GeometryBase, Attributes)."""
    if layer_index is None:
        raise ValueError("Missing layer index for " + name)
    attributes = ObjectAttributes()
    attributes.LayerIndex = int(layer_index)
    attributes.Name = name
    if isinstance(geometry, Brep):
        obj_id = model.Objects.AddBrep(geometry, attributes)
    elif isinstance(geometry, TextDot):
        obj_id = model.Objects.AddTextDot(geometry, attributes)
    else:
        raise TypeError("Unsupported reference geometry type: " + str(type(geometry)))
    if obj_id is None or obj_id == System.Guid.Empty:
        raise RuntimeError("Failed to add object: " + name)
    return obj_id


def main():
    # #SC-REF-03 OWN THE OUTPUT, NEVER THE USER'S ACTIVE DOCUMENT.
    # File3dm is a separate in-memory file model. No scriptcontext.doc.Objects
    # operation is used. Add exactly four joined two-face Breps plus four labels;
    # a single joined seam is required by the analyzer's topological selection.
    # The output path is explicit and only this generated reference file is saved.
    print("SuperContinuity G4 REFERENCE KIT v3 — creating new .3dm")
    model = File3dm()
    created = []
    for name, extra_degree, offset, caption, rgb in CASES:
        left = polynomial_nurbs(True, extra_degree, offset)
        right = polynomial_nurbs(False, extra_degree, offset)
        # A joined two-face Brep is REQUIRED: the analyzer inspects Brep edges
        # with exactly two adjacent faces, not pairs of separate surface objects.
        pairs = System.Array[Brep]([left.ToBrep(), right.ToBrep()])
        joined = Brep.JoinBreps(pairs, TOLERANCE)
        if joined is None or len(joined) != 1 or joined[0].Faces.Count != 2:
            raise RuntimeError(
                "Could not join exactly two patches at x=0: " + name +
                ". Check Rhino version / numerical tolerance.")
        layer_index = add_layer(model, name, rgb)
        add_object(model, joined[0], layer_index, name + "_JoinedTwoFaceBrep")
        add_object(model,
                   TextDot(name + " | " + caption,
                           Point3d(-1.7, offset+0.5, 0.5)),
                   layer_index, name + "_Label")
        created.append(name)
    if model.Objects.Count != 8:
        raise RuntimeError("Expected exactly four joined Breps and four labels; found {} objects".format(model.Objects.Count))
    if not model.Write(OUTPUT, 8):
        raise RuntimeError("File3dm.Write failed: " + OUTPUT)
    print("Success (reference kit v3): " + OUTPUT)
    print("Exact analytic NURBS cases: " + ", ".join(created))
    print("Each case is a joined, two-face Brep; seam at x=0.")
    print("Open generated .3dm in Rhino 8 and run ContinuityAllSeams9.")
    print("WARNING: The plug-in's observed results are sampled, not certified.")


if __name__ == "__main__":
    main()
