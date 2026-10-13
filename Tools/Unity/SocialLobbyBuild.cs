using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
public static class SocialLobbyBuild
{
    public static string Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || EditorUserBuildSettings.activeBuildTarget!=BuildTarget.StandaloneOSX)throw new Exception("Mac edit mode required");
        Directory.CreateDirectory("Builds/SocialLobby-Test");
        File.WriteAllText("ArtSource/SocialLobby/build.txt","RUNNING");
        System.Action build=()=>
        {
            try
            {
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{SvinkiAssetPaths.BootstrapScene,SvinkiAssetPaths.SocialLobbyScene,SvinkiAssetPaths.GameplayScene},locationPathName="Builds/SocialLobby-Test/Svinki.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.Development|BuildOptions.CompressWithLz4});
                File.WriteAllText("ArtSource/SocialLobby/build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; time="+report.summary.totalTime);
            }
            catch(Exception error){File.WriteAllText("ArtSource/SocialLobby/build.txt",error.ToString());}
        };
        build(); return File.ReadAllText("ArtSource/SocialLobby/build.txt");
    }
}
