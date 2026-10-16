using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.VisualBasic.FileIO;
using Tommi.ContinuityTool;

internal static class HighOrderToleranceRegression
{
    internal static void Run(Action<bool,string> check, string[] args)
    {
        const double floor = 1e-10;
        check(ContinuitySettings.absK2 == 0 && ContinuitySettings.absK3 == floor && ContinuitySettings.absK4 == floor,
            "New-session defaults must protect high orders and preserve G2.");
        double a=4.1813219553432716e-15, b=-4.5607961851601427e-16;
        check(!ContinuityEvaluator.CheckContinuity(a,b,.1,0), "Observed endpoint must reproduce the relative-only false failure.");
        check(ContinuityEvaluator.CheckContinuity(a,b,.1,floor), "Explicit default must accept the observed endpoint roundoff.");
        check(ContinuityEvaluator.AcceptanceBasis(a,b,.1,floor)=="ABSOLUTE", "Report the actual absolute acceptance basis.");
        check(ContinuityEvaluator.AllowedDifference(a,b,.1,floor)==floor, "Display and classification must use the same explicit allowance.");
        check(!ContinuityEvaluator.CheckContinuity(0,2*floor,.1,floor), "Do not accept a near-zero discrepancy above the explicit floor.");
        check(!ContinuityEvaluator.CheckContinuity(0,.6,.1,floor), "Real third-order discontinuity must fail.");
        check(!ContinuityEvaluator.CheckContinuity(0,2.4,.15,floor), "Real fourth-order discontinuity must fail.");
        check(!ContinuityEvaluator.CheckContinuity(double.NaN,0,.1,floor), "Unavailable measurements must not become PASS.");
        var s = new SampleReportV9 { Gap=0, AngleDegrees=0, A=new double[3], B=new double[3], Pass=new bool[3], GraphA=new double[14], GraphB=new double[14] };
        s.GraphA[5]=a; s.GraphB[5]=b;
        ContinuityEvaluator.Classify(s,.001,1,new[]{.05,.1,.15},new[]{0.0,floor,floor});
        check(s.GraphOrderState[2]=="PASS", "Endpoint must pass cumulative G4 with default high-order tolerances.");
        s.GraphB[9]=2.4;
        ContinuityEvaluator.Classify(s,.001,1,new[]{.05,.1,.15},new[]{0.0,floor,floor});
        check(s.GraphOrderState[1]=="PASS" && s.GraphOrderState[2]=="FAIL", "G3-only fixture must remain a true G4 failure.");
        s.GraphB[5]=.6;
        ContinuityEvaluator.Classify(s,.001,1,new[]{.05,.1,.15},new[]{0.0,floor,floor});
        check(s.GraphOrderState[1]=="FAIL" && s.GraphOrderState[2]=="BLOCKED", "Real G3 failure must still block cumulative G4.");
        check(s.G0State=="PASS" && s.G1State=="PASS" && s.SectionG2State=="PASS", "G0-G2 classifications must remain unchanged.");

        // Exercise the real saved-settings parser without creating or saving a Rhino document.
        var type=typeof(ContinuitySettings).Assembly.GetType("Tommi.ContinuityTool.ContinuitySettingsSnapshot",true);
        var restore=type.GetMethod("TryRestore",BindingFlags.Static|BindingFlags.NonPublic);
        Func<string,bool> load=raw=>(bool)restore.Invoke(null,new object[]{raw});
        string tail=";0;5;10;15;0.001;1;0;0;0;61;220;1;1;0;2500";
        check(load("v1"+tail) && ContinuitySettings.absK2==0 && ContinuitySettings.absK3==floor && ContinuitySettings.absK4==floor,
            "Legacy zero defaults must migrate without changing G2.");
        check(load("v1;0;5;10;15;0.001;1;0.0002;0.0003;0.0004;61;220;1;1;0;2500") && ContinuitySettings.absK2==.0002 && ContinuitySettings.absK3==.0003 && ContinuitySettings.absK4==.0004,
            "Legacy nonzero user tolerances must be preserved.");
        check(load("v2"+tail) && ContinuitySettings.absK3==0 && ContinuitySettings.absK4==0,
            "New-format deliberate zero must be preserved.");
        var snapshot=type.GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null).ToString();
        check(snapshot.StartsWith("v2;") && load(snapshot) && ContinuitySettings.absK3==0,
            "Explicit zero must survive saving and restoring the new schema.");
        check(!load("v3"+tail), "Unknown settings schema must be rejected.");
        check(!load("v1;0;5;10;15;0.001;1;0;NaN;0;61;220;1;1;0;2500"), "Invalid saved tolerances must be rejected.");
        ContinuitySettings.absK2=0; ContinuitySettings.absK3=ContinuitySettings.absK4=floor;
        if (args.Length>0) ReplayCsv(args[0],check);
    }

    private static void ReplayCsv(string path, Action<bool,string> check)
    {
        var seams=new Dictionary<string,SeamReportV9>();
        int count=0;
        using(var parser=new TextFieldParser(path))
        {
            parser.SetDelimiters(";"); parser.HasFieldsEnclosedInQuotes=true;
            var headers=parser.ReadFields();
            while(!parser.EndOfData)
            {
                var fields=parser.ReadFields();
                var row=headers.Select((h,i)=>new{h,v=fields[i]}).ToDictionary(x=>x.h,x=>x.v);
                Func<string,double> value=k=>double.Parse(row[k],NumberStyles.Float,CultureInfo.GetCultureInfo("fi-FI"));
                var sample=new SampleReportV9 {Gap=value("G0_gap_modelUnits"),AngleDegrees=value("G1_angle_deg"),A=new double[3],B=new double[3],Pass=new bool[3],GraphA=new double[14],GraphB=new double[14]};
                for(int i=0;i<3;i++){sample.A[i]=value("K"+(i+2)+"_A_1perUnit"+(i==0?"":(i+1).ToString())); sample.B[i]=value("K"+(i+2)+"_B_1perUnit"+(i==0?"":(i+1).ToString()));}
                string[] names={"hx","hy","hxx","hxy","hyy","hxxx","hxxy","hxyy","hyyy","hxxxx","hxxxy","hxxyy","hxyyy","hyyyy"};
                for(int i=0;i<14;i++){sample.GraphA[i]=value("GraphJet4_"+names[i]+"_A");sample.GraphB[i]=value("GraphJet4_"+names[i]+"_B");}
                ContinuityEvaluator.Classify(sample,value("G0_gap_tolerance_modelUnits"),value("G1_angle_tolerance_deg"),new[]{value("TolK2_pct")/100,value("TolK3_pct")/100,value("TolK4_pct")/100},new[]{value("AbsK2_1perUnit"),1e-10,1e-10});
                check(sample.G0State==row["G0_state"] && sample.G1State==row["G1_state"] && sample.SectionG2State==row["SectionG2_sampled_state"], "CSV replay must not change G0-G2.");
                if(!seams.ContainsKey(row["Seam"])) seams.Add(row["Seam"],new SeamReportV9{Id=row["Seam"]});
                seams[row["Seam"]].Samples.Add(sample); count++;
            }
        }
        check(count==546 && seams.Count==4,"Replay must cover all four original CSV seams and all 546 samples.");
        check(seams["S1"].GraphOrder(2)=="PASS" && seams["S1"].GraphOrderPassedSamples(2)==121,"S1 replay must pass G4 121/121.");
        check(seams["S2"].GraphOrder(1)=="PASS" && seams["S2"].GraphOrder(2)=="FAIL","S2 replay must pass G3 and retain G4 FAIL.");
        check(seams["S3"].GraphOrder(1)=="FAIL","S3 replay must retain true G3 failure.");
        check(seams["S4"].GraphOrder(2)=="PASS" && seams["S4"].GraphOrderPassedSamples(2)==121,"S4 replay must pass G4 121/121.");
        Console.WriteLine("Original CSV replay: 546 samples; S1 G4, S2 G3/G4 FAIL, S3 G2/G3 FAIL, S4 G4. This is reclassification, not new Rhino geometry evaluation.");
    }
}
