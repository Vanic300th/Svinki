using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class ExpandedRoundBuild
{
    public static string MacImmediate()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneOSX)
            throw new Exception("Mac target in edit mode required");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/Lobby.unity","Assets/Scenes/SampleScene.unity"},locationPathName="Builds/ExpandedRound-Test/Svinki.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development|BuildOptions.CompressWithLz4});
        var result=report.summary.result+"; errors="+report.summary.totalErrors+"; time="+report.summary.totalTime;
        File.WriteAllText("ArtSource/ExpandedRound/mac-build-status.txt",result);
        return result;
    }
    public static string MacTest()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first");
        if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneOSX)throw new Exception("Mac target required");
        Directory.CreateDirectory("Builds/ExpandedRound-Test");Directory.CreateDirectory("ArtSource/ExpandedRound");
        File.WriteAllText("ArtSource/ExpandedRound/mac-build-status.txt","RUNNING");
        EditorApplication.delayCall+=()=>{
            try{
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{"Assets/Scenes/Lobby.unity","Assets/Scenes/SampleScene.unity"},locationPathName="Builds/ExpandedRound-Test/Svinki.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development|BuildOptions.CompressWithLz4});
                File.WriteAllText("ArtSource/ExpandedRound/mac-build-status.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; time="+report.summary.totalTime);
            }catch(Exception error){File.WriteAllText("ArtSource/ExpandedRound/mac-build-status.txt","FAILED: "+error);}
        };return "Queued Mac development build for multi-process checks";
    }
}
