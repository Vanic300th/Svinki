using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SvinWebBuild
{
    public static void Build()
    {
        var originalProfile = BuildProfile.GetActiveBuildProfile();
        BuildProfile.SetActiveBuildProfile(null);
        var compression = PlayerSettings.WebGL.compressionFormat;
        bool fallback = PlayerSettings.WebGL.decompressionFallback;
        var optimization = UnityEditor.WebGL.UserBuildSettings.codeOptimization;
        try
        {
            // Gzip avoids the lengthy Brotli pass and can run on ordinary static hosts.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.BuildTimes;
            var args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-buildOutput");
            string output = at >= 0 && at + 1 < args.Length ? args[at + 1] : "Builds/Web";
            var scenes = new[] { SvinkiAssetPaths.BootstrapScene, SvinkiAssetPaths.SocialLobbyScene, SvinkiAssetPaths.GameplayScene };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = output, target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL, options = BuildOptions.DetailedBuildReport
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Web build failed: " + report.summary.result);
            Debug.Log("SVIN_WEB_BUILD_SUCCEEDED: " + report.summary.totalTime + "; errors=" + report.summary.totalErrors);
        }
        finally
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = fallback;
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = optimization;
            BuildProfile.SetActiveBuildProfile(originalProfile);
        }
    }
}
