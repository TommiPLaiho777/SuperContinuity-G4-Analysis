using System;
using System.Collections.Generic;
using System.Drawing;
using Rhino;
using Rhino.Display;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    public class AllSeamsConduitV9 : DisplayConduit
    {
        public uint DocumentSerial;
        public RhinoDoc SourceDocument;
        public bool ShowNumbers;
        public bool OnlySelected = true;
        public bool ShowHairs = true;
        public double HairScale = 1.0;
        public int HairStride = 1;
        public int HairTargetGrade = 2;
        public string SelectedId;
        public string ModeName, UnitName;
        public double[] Tolerances;
        public double[] AbsoluteTolerances = new double[3];
        public string DisplayMetric = "Grade";
        public double GapTolerance, AngleTolerance;
        public HashSet<Guid> SourceIds = new HashSet<Guid>();
        public List<SeamReportV9> Seams = new List<SeamReportV9>();

        public void Release()
        {
            Enabled = false;
            foreach (var seam in Seams)
                if (seam.EdgeCurve != null) { seam.EdgeCurve.Dispose(); seam.EdgeCurve = null; }
        }

        // Intentionally distinct: below G0 red, G0 orange, G1 gray,
        // sectional G2 violet, strict Graph G3 blue, strict Graph G4 green.
        public static Color GradeColor(int grade)
        {
            switch (grade)
            {
                case ContinuityGrade.BelowG0: return Color.FromArgb(230, 45, 45);
                case 0: return Color.FromArgb(245, 145, 25);
                case 1: return Color.FromArgb(145, 145, 145);
                case 2: return Color.FromArgb(180, 75, 230);
                case 3: return Color.FromArgb(35, 105, 245);
                case 4: return Color.FromArgb(25, 180, 70);
                default: return Color.LightGray; // Measurement unavailable; NOT a red failure.
            }
        }
        private static Color SampleColor(string status)
        {
            switch (status)
            {
                case "FAIL":
                case "RED": return Color.Red;
                case "WARNING":
                case "ORANGE": return Color.Orange;
                case "PASS":
                case "GREEN": return Color.LimeGreen;
                default: return Color.Gray;
            }
        }
        private static string SelectedSampleState(SampleReportV9 sample, string metric)
        {
            switch (metric)
            {
                case "G0": return sample.G0State;
                case "G1": return sample.G1State;
                case "K": return sample.KState;
                case "Graph4": return sample.GraphState;
                case "SectionG2": return sample.SectionG2State;
                case "GraphG2": return sample.GraphOrderState[0];
                case "GraphG3": return sample.GraphOrderState[1];
                case "GraphG4": return sample.GraphOrderState[2];
                default: return sample.Status;
            }
        }
        protected override void DrawForeground(DrawEventArgs e)
        {
            if (e.RhinoDoc == null || e.RhinoDoc.RuntimeSerialNumber != DocumentSerial) return;
            SeamReportV9 selected = null;
            foreach (var seam in Seams)
            {
                bool isSelected = seam.Id == SelectedId;
                if (isSelected) selected = seam;

                if (seam.EdgeCurve != null)
                {
                    if (seam.ReviewExcluded)
                        e.Display.DrawCurve(seam.EdgeCurve, Color.Gray, isSelected ? 4 : 2);
                    else if (DisplayMetric == "Grade")
                    {
                        // Selected seam gets a subtle outline, but the real grade
                        // colors must remain visible (never masked by solid cyan).
                        if (isSelected)
                        {
                            e.Display.DrawCurve(seam.EdgeCurve, Color.Black, 7);
                            e.Display.DrawCurve(seam.EdgeCurve, Color.Cyan, 5);
                        }
                        e.Display.DrawCurve(seam.EdgeCurve, GradeColor(seam.SampledGrade), isSelected ? 3 : 2);
                        // Actual geometry is kept beneath; local sample intervals are
                        // overlaid so a small defect doesn't recolor the entire edge.
                        foreach (var stroke in seam.GradeStrokes)
                            if (stroke.Points.Count >= 2)
                                e.Display.DrawPolyline(stroke.Points, GradeColor(stroke.Grade), isSelected ? 3 : 2);
                    }
                    else
                    {
                        if (isSelected) e.Display.DrawCurve(seam.EdgeCurve, Color.Black, 6);
                        e.Display.DrawCurve(seam.EdgeCurve, SampleColor(seam.DisplayState(DisplayMetric)), isSelected ? 4 : 2);
                    }
                }
                if (seam.ReviewExcluded || !ShowHairs || (OnlySelected && !isSelected)) continue;

                int target = seam.ReviewRole == "G1 target" ? 1 : HairTargetGrade;
                for (int i = 0; i < seam.Samples.Count; i++)
                {
                    if (i % Math.Max(1,HairStride) != 0 && i != seam.Samples.Count-1) continue;
                    var sample = seam.Samples[i];
                    if (!sample.Spike.IsValid) continue;
                    Color hairColor;
                    if (DisplayMetric == "Grade")
                    {
                        string targetState = ContinuityGrade.AtTarget(sample,target);
                        hairColor = targetState == "FAIL" ? Color.Red :
                            targetState == "INCOMPLETE" ? Color.LightGray : GradeColor(ContinuityGrade.Of(sample));
                    }
                    else hairColor = SampleColor(SelectedSampleState(sample, DisplayMetric));
                    e.Display.DrawLine(new Line(sample.Spike.From,
                        sample.Spike.From + HairScale*(sample.Spike.To - sample.Spike.From)), hairColor, 2);
                }
            }
            var occupied = new List<RectangleF>();
            if (selected != null) DrawId(e, selected, true, occupied);
            if (ShowNumbers)
                foreach (var seam in Seams)
                    if (seam != selected) DrawId(e, seam, false, occupied);
        }
        private static void DrawId(DrawEventArgs e, SeamReportV9 seam, bool selected, List<RectangleF> occupied)
        {
            Point2d p = e.Viewport.WorldToClient(seam.Anchor);
            if (double.IsNaN(p.X) || double.IsNaN(p.Y)) return;
            var box = new RectangleF((float)p.X+7, (float)p.Y-14, seam.Id.Length*9+10, 20);
            if (!selected)
                foreach (var other in occupied) if (box.IntersectsWith(other)) return;
            occupied.Add(box);
            var position = new Point2d(p.X+8, p.Y);
            e.Display.Draw2dText(seam.Id, Color.Black, new Point2d(position.X+1,position.Y+1), false, selected ? 16 : 12);
            e.Display.Draw2dText(seam.Id, selected ? Color.Cyan : Color.White, position, false, selected ? 16 : 12);
        }
    }
}
