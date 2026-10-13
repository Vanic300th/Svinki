#if UNITY_EDITOR_OSX
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

/// <summary>Sign after EOS and the browser have finished adding native build files.</summary>
public sealed class SvinkiMacBuildFinalizer : IPostprocessBuildWithReport
{
    public int callbackOrder => int.MaxValue;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != UnityEditor.BuildTarget.StandaloneOSX ||
            report.summary.result == BuildResult.Failed || report.summary.result == BuildResult.Cancelled) return;

        string app = Path.GetFullPath(report.summary.outputPath);
        string browser = Path.Combine(app, "Contents", "Frameworks", "UnityWebBrowser.Engine.Cef.app");
        if (!Directory.Exists(browser))
            throw new BuildFailedException("The macOS build is missing its YouTube browser. Select ARM64 or x64 and install the matching CEF engine package before building.");

        string[] applications = Directory.GetDirectories(browser, "*.app", SearchOption.AllDirectories)
            .OrderByDescending(path => path.Length).Concat(new[] { browser }).ToArray();
        foreach (string application in applications)
        {
            string contents = Path.Combine(application, "Contents");
            string info = Directory.GetFiles(contents).FirstOrDefault(path =>
                string.Equals(Path.GetFileName(path), "Info.plist", StringComparison.OrdinalIgnoreCase));
            if (info != null && Path.GetFileName(info) != "Info.plist")
            {
                // CEF ships lowercase info.plist, which codesign does not treat
                // as bundle metadata. Use two moves on case-insensitive disks.
                string temporary = info + "." + Guid.NewGuid().ToString("N");
                File.Move(info, temporary);
                File.Move(temporary, Path.Combine(contents, "Info.plist"));
            }
        }

        // Keep the identity and hardened-runtime setting Unity selected. Its
        // original seal is invalidated when package callbacks add native files.
        string metadata = Codesign("--display --verbose=2 " + Quote(app));
        string authority = metadata.Split('\n').FirstOrDefault(line => line.StartsWith("Authority=", StringComparison.Ordinal));
        string identity = authority == null ? "-" : authority.Substring("Authority=".Length).Trim();
        string runtime = metadata.Contains("runtime)") || metadata.Contains("runtime,") ? " --options runtime" : "";
        string sign = "--force --sign " + Quote(identity) + " --preserve-metadata=identifier,entitlements" + runtime + " ";

        foreach (string library in Directory.GetFiles(browser, "*.dylib", SearchOption.AllDirectories)) Codesign(sign + Quote(library));
        foreach (string framework in Directory.GetDirectories(browser, "*.framework", SearchOption.AllDirectories).OrderByDescending(path => path.Length)) Codesign(sign + Quote(framework));
        foreach (string application in applications) Codesign(sign + Quote(application));
        Codesign(sign + Quote(app));
        Codesign("--verify --deep --strict " + Quote(app));
    }

    private static string Codesign(string arguments)
    {
        var start = new ProcessStartInfo("/usr/bin/codesign", arguments)
        {
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using (var process = Process.Start(start))
        {
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120000))
            {
                process.Kill();
                throw new BuildFailedException("macOS code signing timed out.");
            }
            if (process.ExitCode != 0)
                throw new BuildFailedException("macOS code signing failed: " + error.GetAwaiter().GetResult());
            return error.GetAwaiter().GetResult();
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
#endif
