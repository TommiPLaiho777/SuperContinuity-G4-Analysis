using System;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Commands;
using Rhino.Geometry;

namespace Tommi.ContinuityTool
{
    public static class ContinuitySessionV9
    {
        private static readonly Dictionary<uint, AllSeamsConduitV9> Sessions = new Dictionary<uint, AllSeamsConduitV9>();
        private static readonly Dictionary<uint, ContinuityTableForm> Tables = new Dictionary<uint, ContinuityTableForm>();
        static ContinuitySessionV9()
        {
            RhinoDoc.CloseDocument += (sender,e) => { if (e.Document != null) Clear(e.Document, false); };
            RhinoDoc.DocumentPropertiesChanged += (sender,e) =>
            {
                AllSeamsConduitV9 current;
                // #SC-STATE-01 DIMENSIONAL MEASUREMENTS BELONG TO THEIR MODEL UNITS.
                // A unit change without scaling does not replace source objects,
                // so geometry events alone leave an enabled report with old units.
                // Gap and order-n derivatives have units L and L^(1-n): require a
                // fresh analysis rather than relabeling or partly converting them.
                // Other property changes retain the explicit analysis thresholds.
                if (e.Document != null && Sessions.TryGetValue(e.Document.RuntimeSerialNumber, out current) &&
                    !ModelUnitsMatch(e.Document, current))
                {
                    Clear(e.Document);
                    RhinoApp.WriteLine("Continuity results cleared: model units changed. Rerun ContinuityAllSeams9.");
                }
            };
            RhinoDoc.ReplaceRhinoObject += (sender,e) => Invalidate(e.Document, e.ObjectId);
            RhinoDoc.DeleteRhinoObject += (sender,e) =>
            {
                foreach (var current in Sessions.Values.Where(x => x.SourceIds.Contains(e.ObjectId)).ToArray())
                Invalidate(current.SourceDocument, e.ObjectId);
            };
        }
        public static void Shutdown()
        {
            foreach (var current in Sessions.Values.ToArray()) Clear(current.SourceDocument, false);
        }
        public static bool TryGet(RhinoDoc doc, out AllSeamsConduitV9 current)
        {
            current = null;
            return doc != null && Sessions.TryGetValue(doc.RuntimeSerialNumber, out current);
        }
        private static bool ModelUnitsMatch(RhinoDoc doc, AllSeamsConduitV9 current)
        {
            if (current.UnitName != doc.ModelUnitSystem.ToString()) return false;
            if (doc.ModelUnitSystem != UnitSystem.CustomUnits) return true;
            // #SC-STATE-02 THE CUSTOMUNITS ENUM DOES NOT IDENTIFY A UNIT.
            // Rhino can change meters-per-unit (or its name) while retaining the
            // enum and every source coordinate. Compare the native snapshot too;
            // if its identity cannot be read, do not retain dimensional evidence.
            string name;
            double meters;
            return current.HasCustomUnitContext && doc.GetCustomUnitSystem(true, out name, out meters) &&
                current.CustomUnitName == name && current.CustomMetersPerUnit == meters;
        }
        private static void Invalidate(RhinoDoc doc, Guid id)
        {
            AllSeamsConduitV9 current;
            if (TryGet(doc, out current) && current.SourceIds.Contains(id))
            {
                Clear(doc, false);
                RhinoApp.WriteLine("Continuity results cleared: geometry changed. Rerun ContinuityAllSeams9.");
            }
        }
        public static void Clear(RhinoDoc doc, bool redraw = true)
        {
            if (doc == null) return;
            uint key = doc.RuntimeSerialNumber;
            ContinuityTableForm table;
            if (Tables.TryGetValue(key, out table)) { Tables.Remove(key); table.Close(); }
            AllSeamsConduitV9 current;
            if (Sessions.TryGetValue(key, out current)) { Sessions.Remove(key); current.Release(); }
            if (redraw) doc.Views.Redraw();
        }
        public static void Install(RhinoDoc doc, AllSeamsConduitV9 current)
        {
            Clear(doc, false);
            current.SourceDocument = doc;
            current.HasCustomUnitContext = doc.ModelUnitSystem == UnitSystem.CustomUnits &&
                doc.GetCustomUnitSystem(true, out current.CustomUnitName, out current.CustomMetersPerUnit);
            Sessions[doc.RuntimeSerialNumber] = current;
            current.Enabled = true;
        }
        public static void SetLabels(RhinoDoc doc, bool show)
        {
            AllSeamsConduitV9 current;
            if (TryGet(doc, out current)) { current.ShowNumbers = show; doc.Views.Redraw(); }
        }
        public static Result ShowTable(RhinoDoc doc)
        {
            AllSeamsConduitV9 current;
            if (!TryGet(doc, out current))
            { RhinoApp.WriteLine("Run ContinuityAllSeams9 first."); return Result.Nothing; }
            uint key = doc.RuntimeSerialNumber;
            ContinuityTableForm table;
            if (Tables.TryGetValue(key, out table)) { table.BringToFront(); return Result.Success; }
            table = new ContinuityTableForm(doc, current);
            Tables[key] = table;
            var exactTable = table;
            table.Closed += (sender,e) =>
            {
                ContinuityTableForm stored;
                if (Tables.TryGetValue(key, out stored) && ReferenceEquals(stored, exactTable)) Tables.Remove(key);
            };
            table.Owner = Rhino.UI.RhinoEtoApp.MainWindowForDocument(doc);
            table.Show();
            return Result.Success;
        }
        public static void SetMode(RhinoDoc doc, bool strict)
        {
            AllSeamsConduitV9 current;
            if (!TryGet(doc, out current)) return;
            ContinuitySettings.strict = strict;
            current.ModeName = strict ? "Strict" : "Normal";
            current.Tolerances = strict ? new double[] { .01,.02,.05 } :
            new double[] { ContinuitySettings.customG2/100, ContinuitySettings.customG3/100, ContinuitySettings.customG4/100 };
            // Existing measured values are reclassified, not remeasured.
            foreach (var seam in current.Seams)
                foreach (var sample in seam.Samples)
                    ContinuityEvaluator.Classify(sample, current.GapTolerance, current.AngleTolerance, current.Tolerances, current.AbsoluteTolerances);
            foreach (var seam in current.Seams) seam.InvalidateGradeStrokes();
            ContinuityDocumentStore.SaveSettings(doc);
            doc.Views.Redraw();
        }
        /// <summary>Reclassify existing G2 data with explicit absolute AND relative limits.
        /// The numerical K2/GraphJet values and all geometry stay unchanged.</summary>
        public static bool SetG2Limits(RhinoDoc doc, double relativePercent, double absoluteK2)
        {
            AllSeamsConduitV9 current;
            if (!TryGet(doc, out current) || !SampleReportV9.Finite(relativePercent) ||
                !SampleReportV9.Finite(absoluteK2) || relativePercent < 0 ||
                relativePercent > 50 || absoluteK2 < 0) return false;
            current.Tolerances[0] = relativePercent/100.0;
            current.AbsoluteTolerances[0] = absoluteK2;
            // Stored values are explicit session thresholds; switching Normal/Strict
            // presets will restore preset relative values.
            ContinuitySettings.absK2 = absoluteK2;
            if (current.ModeName == "Normal") ContinuitySettings.customG2 = relativePercent;
            foreach (var seam in current.Seams)
            {
                foreach (var sample in seam.Samples)
                    ContinuityEvaluator.Classify(sample, current.GapTolerance, current.AngleTolerance,
                        current.Tolerances, current.AbsoluteTolerances);
                seam.InvalidateGradeStrokes();
            }
            ContinuityDocumentStore.SaveSettings(doc);
            doc.Views.Redraw();
            return true;
        }

        public static void Focus(RhinoDoc doc, SeamReportV9 seam, bool zoom)
        {
            AllSeamsConduitV9 current;
            if (seam == null || !TryGet(doc, out current)) return;
            current.SelectedId = seam.Id;
            if (zoom && seam.EdgeCurve != null && doc.Views.ActiveView != null)
            {
                BoundingBox box = seam.EdgeCurve.GetBoundingBox(true);
                box.Inflate(Math.Max(box.Diagonal.Length*.35,doc.ModelAbsoluteTolerance*100));
                doc.Views.ActiveView.ActiveViewport.ZoomBoundingBox(box);
            }
            doc.Views.Redraw();
        }
    }
}
