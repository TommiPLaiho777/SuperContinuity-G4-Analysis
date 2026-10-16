using System;
using Rhino;
using Rhino.Commands;

namespace Tommi.ContinuityTool
{
    public class Continuity_Tool_Command9 : Command
    {
        public override string EnglishName { get { return "ContinuityAllSeams9"; } }

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                return PorcupineAllSeamsV9.RunAnalyzer(doc);
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine("Continuity analysis failed: " + ex.Message);
                return Result.Failure;
            }
        }
    }

    public class ContinuityAllSeams9OffCommand : Command
    {
        public override string EnglishName { get { return "ContinuityAllSeams9Off"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        { ContinuitySessionV9.Clear(doc); return Result.Success; }
    }
    public class ContinuityAllSeams9CsvCommand : Command
    {
        public override string EnglishName { get { return "ContinuityAllSeams9CSV"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        { return ContinuityCsv.Export(doc); }
    }

    public class ContinuityAllSeams9TableCommand : Command
    {
        public override string EnglishName { get { return "ContinuityAllSeams9Table"; } }
        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        { return ContinuitySessionV9.ShowTable(doc); }
    }
}
