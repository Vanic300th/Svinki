Based on upstream UnityWebBrowser 2.2.8 (MIT; original LICENSE.md retained).

Local compatibility patch: select the operating system of the running Editor before standalone target defines in WebBrowserUtils.GetRunningPlatform, EngineProcess, and EngineConfiguration. Compile the corresponding process wrapper for UNITY_EDITOR_* as well. This prevents macOS Editor + Windows active build target from calling Windows CreateJobObject or launching an .exe. Player platform selection is unchanged. Native engine binaries remain pinned upstream UPM packages; no binaries were modified.

Resolution can be configured before Init without attempting IPC Resize on an uninitialized browser. This lets the TV start at its final 640x360 size, avoiding a startup resize and unnecessary full-HD buffers. Post-init resize behavior is unchanged.
