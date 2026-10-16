using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;

namespace Tommi.ContinuityTool
{
    public static class ContinuitySettings
    {
        public static bool strict = false;
        public static double customG2 = 5, customG3 = 10, customG4 = 15;
        public static int sampleCount = 61;
        public static bool knotAware = true, adaptive = true;
        public static int maximumSamples = 220;
        public static bool autoKnotBudget = false;
        public static int knotBudgetCap = 2500;
        // #SC-NUM-03 EXPLICIT HIGH-ORDER DEFAULT, NOT A HIDDEN EPSILON.
        // The reference endpoint's analytically equal third derivatives differ
        // by 4.64e-15 in floating point. Relative-only comparison near zero
        // rejects this. Use visible, configurable absolute allowances; never
        // round measurements to zero or silently alter the comparison function.
        // Values are in inverse model units squared/cubed, not a certified error
        // bound or a universal scale-invariant tolerance. Preserve G2's policy.
        public const double DefaultHighOrderAbsoluteTolerance = 1e-10;
        public static double absK2 = 0, absK3 = DefaultHighOrderAbsoluteTolerance,
            absK4 = DefaultHighOrderAbsoluteTolerance;
        private static string toleranceUnits;
        public static bool labels = false;
        public static double gapTolerance = double.NaN, angleTolerance = double.NaN;
        private static uint toleranceDocument;

        public static Result ReadOptions(RhinoDoc doc, out bool runAnalysis)
        {
            runAnalysis = false;
            if (doc == null) return Result.Failure;
            if (toleranceDocument != doc.RuntimeSerialNumber || double.IsNaN(gapTolerance) || toleranceUnits != doc.ModelUnitSystem.ToString())
            {
                toleranceDocument = doc.RuntimeSerialNumber;
                toleranceUnits = doc.ModelUnitSystem.ToString();
                // Settings from a different open document must not leak here.
                strict=false; customG2=5; customG3=10; customG4=15;
                sampleCount=61; maximumSamples=220; knotAware=true; adaptive=true;
                autoKnotBudget=false; knotBudgetCap=2500;
                absK2 = 0;
                absK3 = absK4 = DefaultHighOrderAbsoluteTolerance;
                gapTolerance = doc.ModelAbsoluteTolerance;
                angleTolerance = doc.ModelAngleToleranceRadians * 180.0 / System.Math.PI;
                // .3dm data is authoritative only when stored unit system matches.
                ContinuityDocumentStore.RestoreSettings(doc);
            }
            // Separate options from object selection so preselection cannot bypass settings.
            using (var options = new GetOption())
            {
                options.SetCommandPrompt("ENTER to analyze. G0=model units; G1=degrees. K/GraphJet absolute tolerances: 1/unit, 1/unit^2, 1/unit^3. Normal/Strict controls relative K thresholds.");
                options.AcceptNothing(true);
                var mode = new OptionToggle(strict, "Normal", "Strict");
                var numbers = new OptionToggle(labels, "Off", "On");
                var g2 = new OptionDouble(customG2, 0.0, 50.0);
                var g3 = new OptionDouble(customG3, 0.0, 50.0);
                var g4 = new OptionDouble(customG4, 0.0, 50.0);
                var gap = new OptionDouble(gapTolerance, 0.0, double.MaxValue);
                var angle = new OptionDouble(angleTolerance, 0.0, 90.0);
                options.AddOptionDouble("G0Gap", ref gap);
                options.AddOptionDouble("G1Degrees", ref angle);
                var absolute2 = new OptionDouble(absK2, 0.0, double.MaxValue);
                var absolute3 = new OptionDouble(absK3, 0.0, double.MaxValue);
                var absolute4 = new OptionDouble(absK4, 0.0, double.MaxValue);
                options.AddOptionDouble("K2Abs", ref absolute2);
                options.AddOptionDouble("K3Abs", ref absolute3);
                options.AddOptionDouble("K4Abs", ref absolute4);
                var samples = new OptionInteger(sampleCount, 3, 1000);
                var maxSamples = new OptionInteger(maximumSamples, 3, 2000);
                var knotMode = new OptionToggle(knotAware, "Off", "On");
                var adaptiveMode = new OptionToggle(adaptive, "Off", "On");
                var knotBudgetMode = new OptionToggle(autoKnotBudget, "Off", "On");
                var knotCap = new OptionInteger(knotBudgetCap, 3, 10000);
                options.AddOptionToggle("CurvatureMode", ref mode);
                options.AddOptionToggle("SeamIDs", ref numbers);
                options.AddOptionDouble("NormalK2pct", ref g2);
                options.AddOptionDouble("NormalK3pct", ref g3);
                options.AddOptionDouble("NormalK4pct", ref g4);
                options.AddOptionInteger("Samples", ref samples);
                options.AddOptionInteger("MaxSamples", ref maxSamples);
                options.AddOptionToggle("KnotAware", ref knotMode);
                options.AddOptionToggle("Adaptive", ref adaptiveMode);
                options.AddOptionToggle("AutoKnotBudget", ref knotBudgetMode);
                options.AddOptionInteger("KnotBudgetCap", ref knotCap);
                int analyze = options.AddOption("Analyze");
                int off = options.AddOption("Off");
                int csv = options.AddOption("CSV");
                int table = options.AddOption("Table");
                while (true)
                {
                    GetResult result = options.Get();
                    strict = mode.CurrentValue; labels = numbers.CurrentValue;
                    customG2 = g2.CurrentValue; customG3 = g3.CurrentValue; customG4 = g4.CurrentValue;
                    sampleCount = samples.CurrentValue;
                    maximumSamples = System.Math.Max(sampleCount, maxSamples.CurrentValue);
                    knotAware = knotMode.CurrentValue;
                    adaptive = adaptiveMode.CurrentValue;
                    autoKnotBudget = knotBudgetMode.CurrentValue;
                    knotBudgetCap = System.Math.Max(maximumSamples, knotCap.CurrentValue);
                    absK2 = absolute2.CurrentValue; absK3 = absolute3.CurrentValue; absK4 = absolute4.CurrentValue;
                    gapTolerance = gap.CurrentValue; angleTolerance = angle.CurrentValue;
                    ContinuitySessionV9.SetLabels(doc, labels);
                    if (result == GetResult.Nothing) break;
                    if (result != GetResult.Option) return options.CommandResult();
                    int selected = options.OptionIndex();
                    if (selected == analyze) break;
                    if (selected == off) { ContinuitySessionV9.Clear(doc); return Result.Success; }
                    if (selected == csv) return ContinuityCsv.Export(doc);
                    if (selected == table) return ContinuitySessionV9.ShowTable(doc);
                }
            }
            if (autoKnotBudget && (!knotAware || !adaptive))
                RhinoApp.WriteLine("AutoKnotBudget is active only with KnotAware=On AND Adaptive=On; fixed MaxSamples will be used.");
            if (absK3 == 0 || absK4 == 0)
                RhinoApp.WriteLine("WARNING: zero K3Abs/K4Abs uses relative-only high-order comparisons; roundoff near zero can produce false failures. The explicit default is 1e-10 in the respective inverse model units. Your selected zero is retained.");
            ContinuityDocumentStore.SaveSettings(doc);
            runAnalysis = true;
            return Result.Success;
        }
    }
}
