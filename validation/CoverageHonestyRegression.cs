using System;
using Tommi.ContinuityTool;

internal static class CoverageHonestyRegression
{
    private static int assertions;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static SeamReportV9 MakeSeam(int sampleCount, int partialKnotRows, bool g4Failure, bool samplingCap, bool auditEnabled)
    {
        var seam = new SeamReportV9 { Id = "S122 (Edge 150)", KnotAuditEnabled = auditEnabled, SamplingCapReached = samplingCap };
        for (int i = 0; i < sampleCount; i++)
        {
            seam.Samples.Add(new SampleReportV9
            {
                G0State = "PASS", G1State = "PASS", SectionG2State = "PASS",
                GraphOrderState = new[] { "PASS", "PASS", g4Failure && i == sampleCount - 1 ? "FAIL" : "PASS" }
            });
        }
        if (auditEnabled)
        {
            for (int i = 0; i < 477; i++)
            {
                bool both = i >= partialKnotRows;
                seam.KnotAuditRows.Add(new KnotCoverageAudit.Row
                {
                    State = both ? "BOTH" : "LEFT_ONLY",
                    LeftGraphJetValid = both, RightGraphJetValid = both
                });
            }
        }
        return seam;
    }

    private static void Run()
    {
        var s122 = MakeSeam(219, 398, false, true, true);
        Check(s122.GraphOrder(2) == "PASS", "S122 raw sampled G4 state must remain PASS.");
        Check(s122.GraphOrderPassedSamples(2) == 219, "S122 must preserve the raw 219/219 count.");
        Check(s122.KnotAuditState == "PARTIAL_FLANK_SAMPLING", "S122 audit state must identify missing flanks.");
        Check(s122.G4DisplayState == "G4 UNKNOWN — INCOMPLETE COVERAGE", "S122 user-facing G4 must be UNKNOWN.");
        Check(s122.DisplayGrade == ContinuityGrade.Unknown, "Incomplete all-pass G4 must not receive the green grade.");
        Check(s122.DisplayState("GraphG4") == "INCOMPLETE", "The G4 viewport metric must not render incomplete overall coverage as a green pass.");

        var partialWithoutCap = MakeSeam(219, 398, false, false, true);
        Check(partialWithoutCap.G4DisplayState == "G4 UNKNOWN — INCOMPLETE COVERAGE", "KnotAuditStatus alone must be sufficient to identify incomplete coverage.");

        var full = MakeSeam(219, 0, false, false, true);
        Check(full.KnotAuditState == "BOTH_FLANKS_WITH_GRAPH_JETS", "Full planned flank audit should be complete.");
        Check(full.G4DisplayState == "G4 SAMPLED PASS", "Full coverage and all passing samples should remain sampled PASS.");
        Check(full.DisplayGrade == 4, "Full sampled G4 pass should retain grade 4.");

        var failed = MakeSeam(219, 398, true, true, true);
        Check(failed.GraphOrder(2) == "FAIL", "A reliable observed G4 failure must remain visible.");
        Check(failed.G4DisplayState == "G4 FAIL", "Observed G4 failure must take precedence over incomplete coverage.");
        Check(failed.DisplayGrade == 3, "A measured G4 failure should retain the cumulative G3 grade.");

        var capAlone = MakeSeam(219, 0, false, true, false);
        Check(capAlone.G4CoverageState == "SAMPLING_CAP_REACHED_AUDIT_DISABLED", "Cap-only condition must be distinguished from audited incomplete coverage.");
        Check(capAlone.G4DisplayState == "G4 SAMPLED PASS", "SamplingCapReached alone must not change the measured grade to UNKNOWN.");

        var capWithCompleteAudit = MakeSeam(219, 0, false, true, true);
        Check(capWithCompleteAudit.KnotAuditState == "BOTH_FLANKS_WITH_GRAPH_JETS", "The cap must not erase completed flank evidence.");
        Check(capWithCompleteAudit.G4DisplayState == "G4 SAMPLED PASS", "A cap alone must not cause UNKNOWN with a complete enabled knot audit.");
        Check(!capWithCompleteAudit.G4CoverageIncomplete, "An enabled audit is not itself evidence of missing coverage.");
        capWithCompleteAudit.KnotAuditRows.Clear();
        Check(capWithCompleteAudit.KnotAuditState == "NO_MAPPED_INTERIOR_KNOTS", "No-knot audit case must remain distinguishable.");
        Check(capWithCompleteAudit.G4DisplayState == "G4 SAMPLED PASS", "A cap alone must not cause UNKNOWN when there are no mapped interior knots.");

        Check(s122.GraphOrder(0) == "PASS" && s122.GraphOrder(1) == "PASS", "G2/G3 measured classifications must remain unchanged.");
    }

    private static int Main()
    {
        Run();
        Console.WriteLine("Coverage honesty regressions: " + assertions + " assertions PASS.");
        return 0;
    }
}
