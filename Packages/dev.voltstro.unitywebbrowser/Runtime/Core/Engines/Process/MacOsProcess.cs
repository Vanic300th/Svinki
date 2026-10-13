// UnityWebBrowser (UWB)
// Copyright (c) 2021-2024 Voltstro-Studios
// 
// This project is under the MIT license.See the LICENSE.md file for more details.

using System.Diagnostics;
using System.IO;
using VoltstroStudios.UnityWebBrowser.Helper;

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX

namespace VoltstroStudios.UnityWebBrowser.Core.Engines.Process
{
    internal sealed class MacOsProcess : IProcess
    {
        private readonly System.Diagnostics.Process process;
        
        public MacOsProcess()
        {
            process = new System.Diagnostics.Process();
        }

        public void StartProcess(string executable, string workingDir, string arguments, DataReceivedEventHandler onLogEvent,
            DataReceivedEventHandler onErrorLogEvent)
        {
            // CEF otherwise selects the outer Unity player bundle when its
            // engine lives inside <Game>.app/Contents/Frameworks.
            if (Path.GetFileName(executable).StartsWith("UnityWebBrowser.Engine.Cef", System.StringComparison.Ordinal))
            {
                string bundle = Path.GetFullPath(Path.Combine(workingDir, "../.."));
                arguments += " --main-bundle-path=\"" + bundle.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            }
            ProcessStartInfo startInfo = new(executable, arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = workingDir
            };

            process.StartInfo = startInfo;
            process.OutputDataReceived += onLogEvent;
            process.ErrorDataReceived += onErrorLogEvent;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        public void KillProcess()
        {
            process.KillTree();
        }

        public bool HasExited => process.HasExited;
        public int ExitCode => process.ExitCode;
        
        public void Dispose()
        {
            process.Dispose();
        }
    }
}

#endif
