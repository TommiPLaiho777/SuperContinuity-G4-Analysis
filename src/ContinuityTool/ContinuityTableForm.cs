using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Eto.Forms;
using Rhino;

namespace Tommi.ContinuityTool
{
    // Modeless: Rhino remains usable while this table is open.
    public sealed class ContinuityTableForm : Form
    {
        private readonly RhinoDoc doc;
        private readonly AllSeamsConduitV9 result;
        private readonly GridView grid = new GridView();
        private readonly Label status = new Label();
        private readonly Label gradeCounts = new Label();
        private readonly TextArea detail = new TextArea { ReadOnly = true };
        private readonly TextArea diagnostics = new TextArea { ReadOnly = true };
        private readonly TextArea settingsInfo = new TextArea { ReadOnly = true, Height = 130 };
        private readonly DropDown columnView = new DropDown();
        private readonly DropDown role = new DropDown();
        private readonly Label selectionTitle = new Label { Text = "Select a seam" };
        private static readonly string[] DisplayModes = { "Grade", "Overall", "G0", "G1", "K", "SectionG2", "Graph4", "GraphG2", "GraphG3", "GraphG4" };
        private static readonly string[] Roles = { "Inspect", "G1 target", "Section G2 target", "Excluded", "Design edge" };
        private readonly DropDown filter = new DropDown();
        private readonly DropDown sort = new DropDown();
        private readonly DropDown mode = new DropDown();
        private readonly DropDown display = new DropDown();
        private readonly DropDown hairTarget = new DropDown();
        private readonly TextBox search = new TextBox();
        private readonly TextBox g2RelativeEntry = new TextBox { Width = 115 };
        private readonly TextBox g2AbsoluteEntry = new TextBox { Width = 150 };
        private readonly Label g2ApplyStatus = new Label { Text = "G2 limits: sampled diagnostics, not a Rhino G2 certificate." };
        private List<object[]> rows = new List<object[]>();
        private bool refreshing;

        public ContinuityTableForm(RhinoDoc document, AllSeamsConduitV9 analysis)
        {
            doc = document; result = analysis;
            Title = "SuperContinuity v7.2.3 - Seam inspector (preview)";
            ClientSize = new Eto.Drawing.Size(940, 720);
            MinimumSize = new Eto.Drawing.Size(820, 620);
            Padding = new Eto.Drawing.Padding(10);
            Resizable = true;

            mode.Items.Add("Normal"); mode.Items.Add("Strict");
            mode.SelectedIndex = result.ModeName == "Strict" ? 1 : 0;
            foreach (string item in new[] { "All", "Needs attention", "Red failures", "Section K2 warnings", "Incomplete samples", "G0 failures", "G1 failures", "Section G2 PASS", "Strict Graph G2 PASS", "Strict Graph G3 PASS (sampled)", "Strict Graph G4 PASS (sampled)", "Excluded / design edges" }) filter.Items.Add(item);
            filter.SelectedIndex = 0;
            foreach (string item in new[] { "Priority", "Seam number", "Largest G0 gap", "Largest G1 angle", "Largest dK2", "Largest dK3", "Largest dK4" }) sort.Items.Add(item);
            sort.SelectedIndex = 0;
            search.Width = 150;
            foreach (string item in DisplayModes) display.Items.Add(item);
            display.SelectedIndex = Math.Max(0,Array.IndexOf(DisplayModes, result.DisplayMetric));
            foreach (string item in new[] { "G1", "G2 (section)", "G3 (sampled geometric graph)", "G4 (sampled geometric graph)" }) hairTarget.Items.Add(item);
            hairTarget.SelectedIndex = Math.Max(0, Math.Min(3, result.HairTargetGrade - 1));

            var onlySelected = new CheckBox { Text = "Selected seam hairs only", Checked = result.OnlySelected };
            var ids = new CheckBox { Text = "Show all seam IDs", Checked = result.ShowNumbers };
            var zoom = new Button { Text = "Zoom to seam" };
            var summary = new Button { Text = "CSV: seams" };
            var samples = new Button { Text = "CSV: samples" };
            var off = new Button { Text = "Stop analysis / clear graphics" };
            var closeWindow = new Button { Text = "Close window" };

            grid.AllowMultipleSelection = false;
            grid.ShowHeader = true;
            grid.ToolTip = "G0: sampled paired-boundary gap in model units. G1: tangent-plane angle in degrees. K columns: one directional section. GraphJet4: geometric surface derivatives through order 4 at sampled stations. This is not parametric C4 and does not certify the entire seam. See Help / Definitions.";
            foreach (string item in new[] { "Overview", "Position / tangent", "Graph G2-G4", "Sampling" }) columnView.Items.Add(item);
            columnView.SelectedIndex = 0;
            foreach (string item in Roles) role.Items.Add(item);
            var help = new Button { Text = "Definitions" };
            help.Click += (sender,e) => ContinuityDefinitions.Show(this);
            var length = new Slider { MinValue = -20, MaxValue = 20, Value = (int)Math.Round(20*Math.Log10(result.HairScale)) };
            var lengthValue = new Label { Text = result.HairScale.ToString("0.00") + " x", Width = 65 };
            var density = new Slider { MinValue = 1, MaxValue = 10, Value = 11-result.HairStride };
            var densityValue = new Label { Text = "1 / " + result.HairStride, Width = 65 };
            var hairs = new CheckBox { Text = "Show sample hairs", Checked = result.ShowHairs };
            length.ToolTip = "Logarithmic display scale: 0.1x to 10x. Does not change measurements.";
            density.ToolTip = "Draw every nth measured sample. CSV and analysis keep every sample.";
            length.ValueChanged += (sender,e) => {
                result.HairScale = Math.Pow(10,length.Value/20.0);
                lengthValue.Text = result.HairScale.ToString("0.00") + " x"; doc.Views.Redraw();
            };
            density.ValueChanged += (sender,e) => {
                result.HairStride = 11-density.Value;
                densityValue.Text = "1 / " + result.HairStride; doc.Views.Redraw();
            };
            hairs.CheckedChanged += (sender,e) => { result.ShowHairs = hairs.Checked == true; doc.Views.Redraw(); };
            var resetDisplay = new Button { Text = "Reset display" };
            resetDisplay.Click += (sender,e) => { length.Value = 0; density.Value = 10; hairs.Checked = true; };

            var viewLayout = new DynamicLayout { Padding = 10, Spacing = new Eto.Drawing.Size(8,8) };
            viewLayout.Add(ToolRow(new Label { Text = "Color by" }, display), xscale: true);
            viewLayout.AddRow(new Label { Text = "Sampled geometric G4 green  |  G3 blue  |  section G2 violet  |  G1 gray  |  G0 orange  |  below G0 red" });
            viewLayout.Add(ToolRow(new Label { Text = "Red hairs below" }, hairTarget), xscale: true);
            viewLayout.Add(ToolRow(hairs, onlySelected), xscale: true);
            viewLayout.Add(ToolRow(new Label { Text = "Hair length" }, length, lengthValue), xscale: true);
            viewLayout.Add(ToolRow(new Label { Text = "Hair density" }, density, densityValue), xscale: true);
            viewLayout.Add(ToolRow(ids, resetDisplay), xscale: true);
            viewLayout.AddRow(new Label { Text = "Selected seam outlined cyan. Excluded edges gray; unavailable samples light gray." });
            viewLayout.AddRow(new Label { Text = "G2 = sectional K2; G3/G4 = sampled strict geometric GraphJet. G1 target overrides red-hair threshold." });
            viewLayout.AddRow(new Label { Text = "Display changes leave measurements and thresholds unchanged." });
            viewLayout.Add(null, yscale: true);

            var toleranceLayout = new DynamicLayout { Padding = 10, Spacing = new Eto.Drawing.Size(8,8) };
            toleranceLayout.Add(ToolRow(new Label { Text = "Relative threshold preset" }, mode, help), xscale: true);
            toleranceLayout.AddRow(new Label { Text = "G2 precision: accept if absolute OR relative curvature threshold passes." });
            g2RelativeEntry.Text = G2Precision.Format(result.Tolerances[0]*100);
            g2AbsoluteEntry.Text = G2Precision.Format(result.AbsoluteTolerances[0]);
            g2RelativeEntry.ToolTip = "Relative K2 tolerance in percent. Accepts decimals, e.g. 5 or 0.5.";
            g2AbsoluteEntry.ToolTip = "Absolute K2 tolerance in reciprocal model units, e.g. 0.00010 (mm^-1 when using mm). Supports scientific notation.";
            var applyG2 = new Button { Text = "Apply G2 limits" };
            applyG2.Click += (sender,e) =>
            {
                double relativePercent, absoluteK2;
                if (!G2Precision.TryNonnegative(g2RelativeEntry.Text, 50, out relativePercent) ||
                    !G2Precision.TryNonnegative(g2AbsoluteEntry.Text, double.MaxValue, out absoluteK2))
                {
                    g2ApplyStatus.Text = "Invalid G2 limits. Use nonnegative numbers, relative 0..50%. Nothing changed.";
                    return;
                }
                if (!ContinuitySessionV9.SetG2Limits(doc,relativePercent,absoluteK2))
                {
                    g2ApplyStatus.Text = "Cannot update G2: no active analysis. Nothing changed.";
                    return;
                }
                g2ApplyStatus.Text = "Applied: K2 passes when the absolute OR relative allowance passes. Measurements unchanged.";
                RefreshRows();
            };
            toleranceLayout.Add(ToolRow(new Label { Text = "G2 relative %" }, g2RelativeEntry,
                new Label { Text = "G2 absolute (1/unit)" }, g2AbsoluteEntry, applyG2), xscale: true);
            toleranceLayout.AddRow(g2ApplyStatus);
            toleranceLayout.Add(settingsInfo, xscale: true, yscale: true);
            toleranceLayout.AddRow(new Label { Text = "S25 trial: relative 5%, absolute 0.00010 (1/mm); not a certified tolerance." });
            toleranceLayout.AddRow(new Label { Text = "K2 shared with Graph G2; G3/G4 tolerances unchanged. Presets restore relative K2." });

            var tabs = new TabControl();
            tabs.Pages.Add(new TabPage { Text = "Selected seam", Content = Fill(detail) });
            tabs.Pages.Add(new TabPage { Text = "Graph diagnostics", Content = Fill(diagnostics) });
            tabs.Pages.Add(new TabPage { Text = "Viewport", Content = viewLayout });
            tabs.Pages.Add(new TabPage { Text = "Thresholds", Content = toleranceLayout });
            var inspector = new DynamicLayout { Spacing = new Eto.Drawing.Size(8,8) };
            var textWindow = new Button { Text = "Open text window" };
            textWindow.Click += (sender,e) => OpenTextWindow();
            inspector.Add(ToolRow(selectionTitle, role, zoom, textWindow), xscale: true);
            inspector.Add(tabs, xscale: true, yscale: true);
            var listLayout = new DynamicLayout { Spacing = new Eto.Drawing.Size(8,8) };
            listLayout.Add(ToolRow(new Label { Text = "Find" }, search, new Label { Text = "Filter" }, filter), xscale: true);
            listLayout.Add(ToolRow(new Label { Text = "Columns" }, columnView, new Label { Text = "Sort" }, sort), xscale: true);
            listLayout.Add(grid, xscale: true, yscale: true);
            var split = new Splitter { Orientation = Orientation.Vertical, Panel1 = listLayout, Panel2 = inspector, Position = 290, Panel1MinimumSize = 150, Panel2MinimumSize = 180 };
            var layout = new DynamicLayout { Spacing = new Eto.Drawing.Size(8,8) };
            layout.AddRow(gradeCounts);
            // Always-visible key for the viewport's actual grade colors.
            // Keep it separate from the Viewport tab so it can be read from any tab.
            layout.Add(GradeLegend(), xscale: true);
            layout.Add(split, xscale: true, yscale: true);
            layout.AddRow(status);
            layout.Add(ToolRow(summary, samples, null, closeWindow, off), xscale: true);
            Content = layout;
            columnView.SelectedIndexChanged += (sender,e) => { ConfigureColumns(); RefreshRows(); };
            role.SelectedIndexChanged += (sender,e) => {
                if (refreshing || role.SelectedIndex < 0) return;
                var seam = Selected(); if (seam == null) return;
                seam.ReviewRole = Roles[role.SelectedIndex];
                ContinuityDocumentStore.SaveRole(doc,seam); RefreshRows(); doc.Views.Redraw();
            };
            ConfigureColumns();

            display.SelectedIndexChanged += (sender,e) =>
            { result.DisplayMetric = DisplayModes[Math.Max(0,display.SelectedIndex)]; doc.Views.Redraw(); };
            hairTarget.SelectedIndexChanged += (sender,e) =>
            { result.HairTargetGrade = Math.Max(1,hairTarget.SelectedIndex+1); doc.Views.Redraw(); };
            grid.SelectionChanged += (sender,e) => { if (!refreshing) SelectCurrent(false); };
            filter.SelectedIndexChanged += (sender,e) => RefreshRows();
            sort.SelectedIndexChanged += (sender,e) => RefreshRows();
            search.TextChanged += (sender,e) => RefreshRows();
            mode.SelectedIndexChanged += (sender,e) =>
            {
                ContinuitySessionV9.SetMode(doc, mode.SelectedIndex==1);
                g2RelativeEntry.Text = G2Precision.Format(result.Tolerances[0]*100);
                g2AbsoluteEntry.Text = G2Precision.Format(result.AbsoluteTolerances[0]);
                g2ApplyStatus.Text = "Preset relative K2 restored; absolute K2 unchanged.";
                RefreshRows();
            };
            onlySelected.CheckedChanged += (sender,e) =>
            { result.OnlySelected = onlySelected.Checked == true; doc.Views.Redraw(); };
            ids.CheckedChanged += (sender,e) =>
            { result.ShowNumbers = ids.Checked == true; ContinuitySettings.labels = result.ShowNumbers; doc.Views.Redraw(); };
            zoom.Click += (sender,e) => SelectCurrent(true);
            summary.Click += (sender,e) => ContinuityCsv.Export(doc, false);
            samples.Click += (sender,e) => ContinuityCsv.Export(doc, true);
            off.Click += (sender,e) => ContinuitySessionV9.Clear(doc);
            closeWindow.Click += (sender,e) => Close();
            RefreshRows();
        }
        // Visual key only: colors come from the same function used to draw seam curves.
        // No continuity thresholds, classifications or viewport behaviors are changed.
        private static Control GradeLegend()
        {
            var items = new List<Control> { new Label { Text = "Seam colors:" } };
            int[] grades = { 4, 3, 2, 1, 0, ContinuityGrade.BelowG0, ContinuityGrade.Unknown };
            string[] names = { "G4", "G3", "G2", "G1", "G0", "<G0", "N/A" };
            for (int i = 0; i < grades.Length; i++)
            {
                var rgb = AllSeamsConduitV9.GradeColor(grades[i]);
                items.Add(new Panel {
                    Size = new Eto.Drawing.Size(14, 14),
                    BackgroundColor = Eto.Drawing.Color.FromArgb(rgb.R, rgb.G, rgb.B)
                });
                items.Add(new Label { Text = names[i] });
            }
            return ToolRow(items.ToArray());
        }

        // Every toolbar owns its columns. It cannot constrain a full-width content row.
        private static Control ToolRow(params Control[] controls)
        {
            var row = new DynamicLayout { Spacing = new Eto.Drawing.Size(8,8) };
            row.BeginHorizontal();
            foreach (var control in controls) row.Add(control, xscale: control == null);
            row.EndHorizontal();
            return row;
        }
        private static Control Fill(Control control)
        {
            var layout = new DynamicLayout();
            layout.Add(control, xscale: true, yscale: true);
            return layout;
        }
        private void OpenTextWindow()
        {
            var seam = Selected();
            if (seam == null) return;
            // Deliberate snapshot: changing the selection in the main inspector must not
            // silently replace the seam being compared in this independent window.
            var selectedText = new TextArea { ReadOnly = true, Wrap = true, Text = detail.Text };
            var graphText = new TextArea { ReadOnly = true, Wrap = true, Text = diagnostics.Text };
            var pages = new TabControl();
            pages.Pages.Add(new TabPage { Text = "Selected seam", Content = Fill(selectedText) });
            pages.Pages.Add(new TabPage { Text = "Graph diagnostics", Content = Fill(graphText) });
            var window = new Form {
                Title = "SuperContinuity - " + seam.Id + " - text snapshot",
                ClientSize = new Eto.Drawing.Size(1000,600),
                MinimumSize = new Eto.Drawing.Size(480,320),
                Padding = new Eto.Drawing.Padding(10), Resizable = true, Owner = this
            };
            var wrap = new CheckBox { Text = "Wrap long lines", Checked = true };
            wrap.CheckedChanged += (sender,e) => { selectedText.Wrap = graphText.Wrap = wrap.Checked == true; };
            var close = new Button { Text = "Close" };
            close.Click += (sender,e) => window.Close();
            var content = new DynamicLayout { Spacing = new Eto.Drawing.Size(8,8) };
            content.AddRow(new Label { Text = "Snapshot of " + seam.Id + "; values do not update when selection or thresholds change." });
            content.Add(pages, xscale: true, yscale: true);
            content.Add(ToolRow(wrap, null, close), xscale: true);
            window.Content = content;
            EventHandler<EventArgs> closeWithInspector = (sender,e) => window.Close();
            Closed += closeWithInspector;
            window.Closed += (sender,e) => Closed -= closeWithInspector;
            window.Show();
        }
        private SeamReportV9 Selected()
        {
            var row = grid.SelectedItem as object[];
            return row != null && row.Length > 0 ? row[row.Length-1] as SeamReportV9 : null;
        }
        private void SelectCurrent(bool zoom)
        {
            var seam = Selected();
            if (seam == null)
            { result.SelectedId = null; detail.Text = "No seam selected."; diagnostics.Text = ""; selectionTitle.Text = "No seam selected"; role.Enabled = false; doc.Views.Redraw(); return; }
            ContinuitySessionV9.Focus(doc, seam, zoom);
            refreshing = true;
            role.SelectedIndex = Array.IndexOf(Roles, seam.ReviewRole);
            role.Enabled = true;
            refreshing = false;
            selectionTitle.Text = seam.Id + " | " + seam.ReviewState;
            detail.Text = "REVIEW: " + seam.ReviewRole + " -> " + seam.ReviewState + Environment.NewLine +
                "Sampled grade: " + seam.SampledGradeName + " (worst measured location)" + Environment.NewLine +
                "G0 " + seam.G0State + " | G1 " + seam.G1State + " | Section G2 " + seam.SectionG2 + Environment.NewLine +
                "Independent checks: G1 angle " + seam.RawG1AnglePassedSamples(result.AngleTolerance) + "/" + seam.Samples.Count +
                ", Section K2 " + seam.RawK2PassedSamples + "/" + seam.Samples.Count +
                " (prerequisite gates ignored; NOT independent continuity grades)" + Environment.NewLine +
                "G0 nearest trim gap max / mean: " + SeamReportV9.Number(seam.GapMaximum) + " / " + SeamReportV9.Number(seam.GapMean) + " " + result.UnitName + Environment.NewLine +
                "Old paired UV gap max: " + SeamReportV9.Number(seam.PairedGapMaximum) +
                " | directed A->B/B->A max: " + SeamReportV9.Number(seam.G0AtoBMaximum) + "/" + SeamReportV9.Number(seam.G0BtoAMaximum) + " " + result.UnitName + Environment.NewLine +
                "Angle max / mean: " + SeamReportV9.Number(seam.AngleMaximum) + " / " + SeamReportV9.Number(seam.AngleMean) + " degrees" + Environment.NewLine +
                "Object: " + seam.ObjectId + " | Edge: " + seam.EdgeIndex + " | Faces: " + seam.FaceA + " / " + seam.FaceB + Environment.NewLine +
                "Samples: " + seam.Samples.Count + " | Complete jets: " + seam.CompleteCount + Environment.NewLine +
                "Coverage: " + seam.SamplingNote + Environment.NewLine + Environment.NewLine +
                "Section G2: K2 relative " + G2Precision.Format(result.Tolerances[0]*100) + "% OR absolute " +
                G2Precision.Format(result.AbsoluteTolerances[0]) + " (1/" + result.UnitName + "). " +
                "These limits are shared with strict Graph G2. " + Environment.NewLine +
                "Section G2 is a directional test, not calibrated to Rhino EdgeContinuity. " +
                "Review roles are stored in the .3dm on Save; changing topology invalidates their geometric key.";
            diagnostics.Text = seam.CurvatureDetails(result.Tolerances,result.AbsoluteTolerances) + Environment.NewLine + Environment.NewLine +
                seam.GraphDetails(result.Tolerances,result.AbsoluteTolerances);
        }
        private void ConfigureColumns()
        {
            // Bind a compact column set to the unchanged backing row; no 28-column scrolling table.
            int[][] sets = { new[] { 0, 30, 28, 29, 2, 3, 5 }, new[] { 0, 2, 7, 8, 3, 9, 10 },
                new[] { 0, 5, 23, 24, 25, 22 }, new[] { 0, 11, 12, 13, 22, 26, 27 } };
            string[] names = { "Seam", "Overview", "G0", "G1", "K", "Section G2", "Ends", "Gap max", "Gap mean", "Angle max (deg)", "Angle mean (deg)",
                "G0 valid", "G1 valid", "K valid", "dK2 max", "dK2 mean", "dK3 max", "dK3 mean", "dK4 max", "dK4 mean", "Faces", "GraphJet4", "Graph valid", "Graph G2", "Graph G3 sampled", "Graph G4 sampled", "Knots", "Budget", "Role", "Review", "Grade (sampled)" };
            refreshing = true;
            grid.Columns.Clear();
            foreach (int index in sets[Math.Max(0,columnView.SelectedIndex)])
                grid.Columns.Add(new GridColumn { HeaderText = names[index], DataCell = new TextBoxCell(index), Editable = false,
                    Width = index == 0 ? 55 : index == 28 ? 150 : index == 29 ? 125 : index == 30 ? 130 : 100 });
            refreshing = false;
        }
        private static int Rank(SeamReportV9 seam)
        {
            if (seam.ReviewExcluded) return 4;
            if (seam.Status == "RED") return 0;
            if (seam.CompleteCount != seam.Samples.Count) return 1;
            if (seam.Status == "ORANGE") return 2;
            return 3;
        }
        private static string State(SeamReportV9 seam)
        {
            string text = seam.Status == "RED" ? "FAIL" : seam.Status == "ORANGE" ? "WARNING" : seam.Status == "GREEN" ? "PASS" : "INCOMPLETE";
            if (seam.Status != "INCOMPLETE" && seam.CompleteCount != seam.Samples.Count) text += " *jets";
            return text + (seam.FirstIssue == "PASS" ? "" : " " + seam.FirstIssue);
        }
        private static double SortValue(SeamReportV9 seam, int column)
        {
            double value = column == 2 ? seam.GapMaximum : column == 3 ? seam.AngleMaximum : seam.Maximum(column-4);
            return SampleReportV9.Finite(value) ? value : double.NegativeInfinity;
        }
        private void RefreshRows()
        {
            string keep = result.SelectedId;
            IEnumerable<SeamReportV9> seams = result.Seams;
            if (filter.SelectedIndex != 0 && filter.SelectedIndex != 11) seams = seams.Where(x => !x.ReviewExcluded);
            switch (filter.SelectedIndex)
            {
                case 1: seams = seams.Where(x => x.Status != "GREEN"); break;
                case 2: seams = seams.Where(x => x.Status == "RED"); break;
                case 3: seams = seams.Where(x => x.Status == "ORANGE"); break;
                case 4: seams = seams.Where(x => x.CompleteCount != x.Samples.Count); break;
                case 5: seams = seams.Where(x => x.Samples.Any(p => p.G0Valid && !p.G0Pass)); break;
                case 6: seams = seams.Where(x => x.Samples.Any(p => p.G1Valid && p.AngleDegrees > result.AngleTolerance)); break;
                case 7: seams = seams.Where(x => x.SectionG2 == "PASS"); break;
                case 8: seams = seams.Where(x => x.GraphOrder(0) == "PASS"); break;
                case 9: seams = seams.Where(x => x.GraphOrder(1) == "PASS"); break;
                case 10: seams = seams.Where(x => x.GraphOrder(2) == "PASS"); break;
                case 11: seams = seams.Where(x => x.ReviewExcluded); break;
            }
            string query = (search.Text ?? "").Trim();
            if (query.Length > 0) seams = seams.Where(x => x.Id.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || x.ObjectId.ToString().IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
            if (sort.SelectedIndex >= 2)
            {
                int column = sort.SelectedIndex;
                seams = seams.OrderByDescending(x => SortValue(x,column));
            }
            else if (sort.SelectedIndex == 0) seams = seams.OrderBy(Rank);
            // result.Seams remains in natural numeric order when no sorting is requested.
            rows = seams.Select(x => new object[] { x.Id, State(x), x.G0State, x.G1State, x.KState, x.SectionG2, x.MissingEndpointCount,
                SeamReportV9.Number(x.GapMaximum), SeamReportV9.Number(x.GapMean),
                SeamReportV9.Number(x.AngleMaximum), SeamReportV9.Number(x.AngleMean),
                x.G0Count+"/"+x.Samples.Count, x.G1Count+"/"+x.Samples.Count, x.ValidCount+"/"+x.Samples.Count,
                SeamReportV9.Number(x.Maximum(0)), SeamReportV9.Number(x.Mean(0)),
                SeamReportV9.Number(x.Maximum(1)), SeamReportV9.Number(x.Mean(1)),
                SeamReportV9.Number(x.Maximum(2)), SeamReportV9.Number(x.Mean(2)), x.FaceA+" / "+x.FaceB, x.GraphState, x.GraphValidCount+"/"+x.Samples.Count,
                x.GraphOrder(0), x.GraphOrder(1), x.GraphOrder(2), x.KnotCrossings,
                x.SamplingCapReached ? "BUDGET" : "OK", x.ReviewRole, x.ReviewState, x.SampledGradeName, x }).ToList();
            refreshing = true;
            grid.DataStore = rows;
            int index = rows.FindIndex(x => ((SeamReportV9)x[x.Length-1]).Id == keep);
            grid.SelectedRow = rows.Count == 0 ? -1 : (index >= 0 ? index : 0);
            refreshing = false;
            status.Text = rows.Count + " / " + result.Seams.Count + " shown | Sampled results only | Roles saved in .3dm after document Save";
            var included = result.Seams.Where(x => !x.ReviewExcluded).ToArray();
            gradeCounts.Text = "Sampled grades (included): " +
                string.Join(" | ", new[] {4,3,2,1,0,-1,-2}.Select(g =>
                    (g==-1 ? "<G0" : g==-2 ? "N/A" : "G"+g) + ":" + included.Count(x=>x.SampledGrade==g))) +
                " | Excluded " + (result.Seams.Count-included.Length);
            settingsInfo.Text = "Current classification settings" + Environment.NewLine +
                "G0 gap: " + SeamReportV9.Number(result.GapTolerance) + " " + result.UnitName + Environment.NewLine +
                "G1 angle: " + SeamReportV9.Number(result.AngleTolerance) + " degrees" + Environment.NewLine +
                result.ModeName + " relative K2 / K3 / K4: " + string.Join(" / ",result.Tolerances.Select(x=>G2Precision.Format(x*100))) + " %" + Environment.NewLine +
                "Absolute K2 / K3 / K4: " + string.Join(" / ",result.AbsoluteTolerances.Select(G2Precision.Format)) + Environment.NewLine +
                "K2 accepts abs OR rel allowance; exact effective allowance = max(abs, rel*max(|K2A|,|K2B|))." + Environment.NewLine +
                "Absolute units: 1/unit, 1/unit^2, 1/unit^3. Shared by section and graph tests.";
            SelectCurrent(false);
        }
    }
}
