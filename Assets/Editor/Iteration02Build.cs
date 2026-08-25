using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class Iteration02Build
{
    public static void BuildWindowsDevelopment()
    {
        const string output=@"C:\Users\shanghai\Desktop\三国战纪\Build\ThreeKingdomsDemo.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var options=new BuildPlayerOptions
        {
            scenes=new[]{"Assets/Scenes/SC_FinalStage_Entrance.unity","Assets/Scenes/SC_FinalStage_StormGate.unity","Assets/Scenes/SC_FinalBoss_CaoCao.unity"},locationPathName=output,
            target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
        };
        BuildReport report=BuildPipeline.BuildPlayer(options);
        Console.WriteLine("ITERATION02_BUILD result="+report.summary.result+" size="+report.summary.totalSize+" errors="+report.summary.totalErrors+" warnings="+report.summary.totalWarnings);
        if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Iteration 02 Windows build failed: "+report.summary.result);
    }
}
