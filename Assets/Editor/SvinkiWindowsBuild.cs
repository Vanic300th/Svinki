using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;

// SessionState keeps the request alive while Unity recompiles for another platform.
[InitializeOnLoad]
public static class SvinkiWindowsBuild
{
    private const string Key = "Svinki.WindowsBuild.";
    private const string BuildMenu = "Svinki/Сборка Windows/Собрать EXE и ZIP";
    private static readonly NamedBuildTarget Standalone = NamedBuildTarget.Standalone;
    private enum Stage { Idle, Prepare, SwitchToWindows, Build, Restore, Finish }
    private static Stage Current
    {
        get => (Stage)SessionState.GetInt(Key + "Stage", 0);
        set => SessionState.SetInt(Key + "Stage", (int)value);
    }
    public static bool IsBusy => Current != Stage.Idle;
    public static string LastResult => SessionState.GetString(Key + "Result", "Сборка ещё не запускалась.");
    private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
    public static string BuildsFolder => Path.Combine(ProjectRoot, "Builds");
    public static string WindowsFolder => Path.Combine(BuildsFolder, "Windows");
    public static string ArchivePath => Path.Combine(BuildsFolder, "Svinki-Windows.zip");

    static SvinkiWindowsBuild() { EditorApplication.update += ContinueBuild; }

    [MenuItem(BuildMenu, false, 2000)]
    public static void BuildWindows() => StartBuild();

    [MenuItem(BuildMenu, true)]
    private static bool CanBuild() => !IsBusy && !BuildPipeline.isBuildingPlayer && !EditorApplication.isCompiling;

    [MenuItem("Svinki/Сборка Windows/Открыть папку сборок", false, 2001)]
    public static void OpenBuilds()
    {
        Directory.CreateDirectory(BuildsFolder);
        EditorUtility.RevealInFinder(BuildsFolder);
    }

    [MenuItem("Svinki/Сборка Windows/Инструкция", false, 2002)]
    private static void OpenInstructions() =>
        EditorUtility.OpenWithDefaultApp(Path.Combine(ProjectRoot, "BUILD-WINDOWS.md"));

    public static void StartBuild(bool interactive = true)
    {
        if (IsBusy || BuildPipeline.isBuildingPlayer) return;
        SessionState.SetBool(Key + "Interactive", interactive);
        SessionState.SetBool(Key + "Success", false);
        SessionState.SetBool(Key + "SettingsSaved", false);
        SessionState.SetBool(Key + "RestoreQueued", false);
        SessionState.SetString(Key + "Result", "Подготовка Windows-сборки…");
        Current = Stage.Prepare;
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
    }

    private static void ContinueBuild()
    {
        if (!IsBusy || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer) return;
        if (EditorApplication.timeSinceStartup < SessionState.GetFloat(Key + "ReadyAfter", 0)) return;
        try
        {
            switch (Current)
            {
                case Stage.Prepare:
                    Prepare();
                    break;
                case Stage.SwitchToWindows:
                    if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64) return;
                    Current = Stage.Build;
                    // Allow the platform's import/compile callbacks to finish before building.
                    Delay();
                    break;
                case Stage.Build:
                    BuildAndPackage();
                    Current = Stage.Restore;
                    break;
                case Stage.Restore:
                    RestoreSettings();
                    break;
                case Stage.Finish:
                    Finish();
                    break;
            }
        }
        catch (Exception error)
        {
            SessionState.SetBool(Key + "Success", false);
            SessionState.SetString(Key + "Result", error.Message);
            Debug.LogError("Windows-сборка: " + error.Message);
            // A failed restoration must not leave the editor in an endless retry loop.
            Current = Current == Stage.Restore || Current == Stage.Finish ? Stage.Finish : Stage.Restore;
        }
    }

    private static void Prepare()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Не установлен модуль Windows. В Unity Hub откройте Installs → шестерёнка у " +
                Application.unityVersion + " → Add modules → Windows Build Support (Mono).");
        CollectScenes();
        if (PlayerSettings.GetScriptingDefineSymbols(Standalone).Split(';').Contains("EOS_DISABLE"))
            throw new InvalidOperationException("В Player Settings включён EOS_DISABLE для локального теста. " +
                "Уберите его из Scripting Define Symbols для обычной сборки с онлайном.");
        if (!File.Exists(Path.Combine(Application.streamingAssetsPath, "svinki-eos.json")))
            throw new InvalidOperationException("Нет Assets/StreamingAssets/svinki-eos.json. Настройте EOS по MULTIPLAYER.md.");
        if (SessionState.GetBool(Key + "Interactive", true))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            { Current = Stage.Idle; SessionState.SetString(Key + "Result", "Сборка отменена."); return; }
        }
        else
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
                if (EditorSceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Сохраните изменения сцен перед автоматической сборкой.");
        }
        AssetDatabase.SaveAssets();
        SessionState.SetInt(Key + "OriginalTarget", (int)EditorUserBuildSettings.activeBuildTarget);
        SessionState.SetString(Key + "OriginalProfile", AssetDatabase.GetAssetPath(BuildProfile.GetActiveBuildProfile()));
        SessionState.SetInt(Key + "OriginalBackend", (int)PlayerSettings.GetScriptingBackend(Standalone));
        SessionState.SetBool(Key + "SettingsSaved", true);
        // Mono builds on both macOS and Windows without a Windows C++ toolchain.
        if (PlayerSettings.GetScriptingBackend(Standalone) != ScriptingImplementation.Mono2x)
            PlayerSettings.SetScriptingBackend(Standalone, ScriptingImplementation.Mono2x);
        Current = Stage.SwitchToWindows;
        SessionState.SetString(Key + "Result", "Переключение на Windows x64…");
        var windowsProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>("Assets/Settings/Build Profiles/Windows.asset");
        // Activating a profile already queues the platform switch. Do not queue it twice.
        if (windowsProfile != null && BuildProfile.GetActiveBuildProfile() != windowsProfile)
            BuildProfile.SetActiveBuildProfile(windowsProfile);
        else if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
            !EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
            throw new InvalidOperationException("Unity не смогла переключиться на Windows x64. Проверьте Console.");
        Delay();
    }

    private static string[] CollectScenes()
    {
        var required = new[] { "Assets/Scenes/Lobby.unity", "Assets/Scenes/SampleScene.unity" };
        foreach (string path in required)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Не найдена сцена: " + path);
        // Lobby is always first; additional enabled levels are also included.
        return required.Concat(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path)).Distinct().ToArray();
    }

    private static void BuildAndPackage()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
            throw new InvalidOperationException("Активная платформа изменилась во время подготовки. Повторите сборку.");
        Directory.CreateDirectory(BuildsFolder);
        string id = Guid.NewGuid().ToString("N");
        string staging = Path.Combine(BuildsFolder, ".Windows-new-" + id);
        string tempArchive = Path.Combine(BuildsFolder, ".Windows-new-" + id + ".zip");
        string backup = Path.Combine(BuildsFolder, ".Windows-previous-" + id);
        bool published = false;
        bool completed = false;
        try
        {
            SessionState.SetString(Key + "Result", "Сборка Windows x64…");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = CollectScenes(),
                locationPathName = Path.Combine(staging, "Svinki.exe"),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.CompressWithLz4
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Сборка " + report.summary.result + ". Ошибок: " +
                    report.summary.totalErrors + ". Подробности в Console. Предыдущая сборка сохранена.");
            if (!File.Exists(Path.Combine(staging, "Svinki.exe")) ||
                !File.Exists(Path.Combine(staging, "UnityPlayer.dll")) ||
                !File.Exists(Path.Combine(staging, "Svinki_Data", "StreamingAssets", "svinki-eos.json")))
                throw new InvalidOperationException("В сборке не хватает EXE, UnityPlayer.dll или EOS-конфигурации.");
            foreach (string folder in Directory.GetDirectories(staging))
                if (Path.GetFileName(folder).Contains("BackUpThisFolder_ButDontShipItWithYourGame") ||
                    Path.GetFileName(folder).Contains("BurstDebugInformation_DoNotShip"))
                    Directory.Delete(folder, true);
            File.WriteAllText(Path.Combine(staging, "Как запустить.txt"),
                "Распакуйте весь ZIP в отдельную папку и запустите Svinki.exe.\r\n" +
                "Все файлы и папка Svinki_Data должны оставаться рядом с EXE.\r\n" +
                "Для игры с друзьями создайте лобби и передайте другу код.\r\n");
            SessionState.SetString(Key + "Result", "Создание ZIP…");
            ZipFile.CreateFromDirectory(staging, tempArchive, System.IO.Compression.CompressionLevel.Optimal, false);
            if (Directory.Exists(WindowsFolder)) Directory.Move(WindowsFolder, backup);
            Directory.Move(staging, WindowsFolder);
            published = true;
            if (File.Exists(ArchivePath)) File.Replace(tempArchive, ArchivePath, null);
            else File.Move(tempArchive, ArchivePath);
            completed = true;
            SessionState.SetBool(Key + "Success", true);
            SessionState.SetString(Key + "Result", "Готово: Builds/Windows/Svinki.exe и Builds/Svinki-Windows.zip\n" +
                "Время сборки: " + Mathf.RoundToInt((float)report.summary.totalTime.TotalSeconds) + " сек.");
        }
        catch
        {
            if (published && Directory.Exists(WindowsFolder)) Directory.Move(WindowsFolder, staging);
            if (Directory.Exists(backup)) Directory.Move(backup, WindowsFolder);
            throw;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            if (File.Exists(tempArchive)) File.Delete(tempArchive);
            if (completed && Directory.Exists(backup)) Directory.Delete(backup, true);
        }
    }

    private static void RestoreSettings()
    {
        Current = Stage.Finish;
        if (!SessionState.GetBool(Key + "SettingsSaved", false)) return;
        // Keep the successful build's platform/backend for the next native Unity build.
        // Restoring macOS here left a Windows profile selected while Build produced a .app.
        if (SessionState.GetBool(Key + "Success", false))
        {
            EditorUserBuildSettings.selectedStandaloneTarget = BuildTarget.StandaloneWindows64;
            Delay();
            return;
        }
        var backend = (ScriptingImplementation)SessionState.GetInt(Key + "OriginalBackend", (int)ScriptingImplementation.Mono2x);
        if (PlayerSettings.GetScriptingBackend(Standalone) != backend)
            PlayerSettings.SetScriptingBackend(Standalone, backend);
        var target = (BuildTarget)SessionState.GetInt(Key + "OriginalTarget", (int)BuildTarget.StandaloneWindows64);
        var originalProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>(SessionState.GetString(Key + "OriginalProfile", ""));
        if (originalProfile != null && BuildProfile.GetActiveBuildProfile() != originalProfile)
        {
            BuildProfile.SetActiveBuildProfile(originalProfile);
            SessionState.SetBool(Key + "RestoreQueued", true);
        }
        else if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildPipeline.GetBuildTargetGroup(target), target))
                throw new InvalidOperationException("Сборка закончилась, но Unity не смогла вернуть платформу " + target + ". Выберите её в Build Profiles.");
            SessionState.SetBool(Key + "RestoreQueued", true);
        }
        Delay();
    }

    private static void Delay() => SessionState.SetFloat(Key + "ReadyAfter", (float)EditorApplication.timeSinceStartup + 2f);

    private static void Finish()
    {
        if (SessionState.GetBool(Key + "RestoreQueued", false) && EditorUserBuildSettings.activeBuildTarget !=
            (BuildTarget)SessionState.GetInt(Key + "OriginalTarget", (int)BuildTarget.StandaloneWindows64)) return;
        Current = Stage.Idle;
        bool success = SessionState.GetBool(Key + "Success", false);
        if (success) Debug.Log(LastResult);
        if (!SessionState.GetBool(Key + "Interactive", true)) return;
        if (success) OpenBuilds();
        else EditorUtility.DisplayDialog("Windows-сборка не завершена", LastResult, "Понятно");
    }
}
