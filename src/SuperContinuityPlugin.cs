using System.Runtime.InteropServices;
using Rhino.PlugIns;

[assembly: Guid("15e0bf14-9a1e-4183-b753-11b1d97846f2")]

namespace Tommi.ContinuityTool
{
    public sealed class SuperContinuityPlugin : PlugIn
    {
        protected override void OnShutdown()
        {
            ContinuitySessionV9.Shutdown();
            base.OnShutdown();
        }
    }
}
