using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class OutfitFlowCheck
{
    const string Key = "Svinki.Rating.FlowCheck";
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Step(string value) { SessionState.SetString(Key, value); }
    public static string Main() { NetworkLobby.Instance.StartCoroutine(Guard(Test())); return "Started round/results/continue and offline checks."; }
    static IEnumerator Guard(IEnumerator test)
    {
        while (true)
        {
            bool running; object current;
            try { running = test.MoveNext(); current = running ? test.Current : null; }
            catch (Exception e) { Step("FAILED: " + e.Message); Debug.LogException(e); yield break; }
            if (!running) yield break;
            yield return current;
        }
    }
    static void MoveToStart(PlayerAvatar player)
    {
        var station = RoundFinishStation.ForScene(player.gameObject.scene);
        Check(station != null, "Finish station missing.");
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.position = station.Center + Vector3.up * .05f;
        if (controller != null) controller.enabled = true;
    }
    static IEnumerator Test()
    {
        var lobby = NetworkLobby.Instance;
        float startDeadline = Time.realtimeSinceStartup + 60;
        while ((lobby.Snapshot.phase != SessionPhase.Round || !PlayerRegistry.Players.Any(p => p.IsLocal) ||
            SessionState.GetString("Svinki.EditBackup.status", "") == "restoring") && Time.realtimeSinceStartup < startDeadline) yield return null;
        Check(lobby.IsHost && lobby.Snapshot.phase == SessionPhase.Round && lobby.Snapshot.players.Length == 1, "Requires isolated single-player host.");
        Check(lobby.Results == null, "Empty serialized results blocked a running round.");
        var rules = OutfitRatingCatalog.Load();
        var player = PlayerRegistry.Players.Single(p => p.IsLocal);
        var original = player.Outfit.Items.ToArray();
        var expected = rules.Evaluate(original).score;
        Step("ending online round"); MoveToStart(player); lobby.EndRound();
        float deadline = Time.realtimeSinceStartup + 10;
        while (lobby.Snapshot.phase != SessionPhase.Results && Time.realtimeSinceStartup < deadline) yield return null;
        Check(lobby.Results != null && lobby.Results.entries.Length == 1 && lobby.Results.entries[0].rating.score == expected && !lobby.InputAllowed, "Host result or input lock failed.");
        yield return null; yield return null;
        var root = lobby.transform.Find("Session Canvas/Outfit Results");
        Check(root != null && root.gameObject.activeSelf && !lobby.transform.Find("Session Canvas/Lobby panel").gameObject.activeSelf, "Result panel visibility failed.");
        var bar = root.Find("Полнота комплекта track/Полнота комплекта fill").GetComponent<RectTransform>();
        Check(Mathf.Abs(bar.sizeDelta.x - 670 * original.Length / 4f) < 1, "Completeness bar did not match score.");
        var canonical = lobby.Snapshot.results;
        Step("previewing six supplied outfits");
        lobby.Snapshot.results = new OutfitRoundResults { round = lobby.Snapshot.round, entries = rules.references.Select((r, i) => OutfitRoundResults.Rate(rules, "reference-" + i, r.name, PigFace.Selected, r.items)).ToArray() };
        yield return null; yield return null;
        var buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>();
        Check(buttons.Count(b => b.name == "Participant") == 6, "Not all six participants visible.");
        foreach (var button in buttons.Where(b => b.name == "Participant"))
        {
            button.onClick.Invoke(); yield return null;
            var model = lobby.transform.Find("Outfit Presentation Studio/Presented outfit");
            Check(model != null && model.GetComponentsInChildren<SkinnedMeshRenderer>().Length >= 5, "A reference outfit was not fitted to the pig preview.");
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>()) Check(skin.bones.All(b => b != null), "Missing preview bone binding.");
        }
        buttons.First(b => b.name == "Participant").onClick.Invoke();
        buttons.First(b => b.name == "Вправо").onClick.Invoke();
        buttons.First(b => b.name == "Влево").onClick.Invoke();
        yield return null; yield return new WaitForEndOfFrame();
        Directory.CreateDirectory("ArtSource/OutfitRating");
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes("ArtSource/OutfitRating/results.png", image.EncodeToPNG()); UnityEngine.Object.Destroy(image);
        Check(root.GetComponentsInChildren<TMP_Text>().All(t => !t.isTextOverflowing), "A result label overflowed its bounds.");
        lobby.Snapshot.results = canonical; yield return null;
        Step("continuing online round");
        root.Find("Следующий раунд").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        deadline = Time.realtimeSinceStartup + 15;
        while (lobby.Snapshot.phase != SessionPhase.Lobby && Time.realtimeSinceStartup < deadline) yield return null;
        Check(lobby.Snapshot.phase == SessionPhase.Lobby && lobby.Results == null && lobby.Snapshot.round == canonical.round + 1, "Next round did not return to lobby.");
        Step("starting offline check"); lobby.Leave();
        deadline = Time.realtimeSinceStartup + 15;
        while (lobby.InSession && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!lobby.InSession, "Could not end isolated test session.");
        lobby.StartOffline();
        deadline = Time.realtimeSinceStartup + 15;
        while ((!lobby.Offline || !PlayerRegistry.Players.Any(p => p.IsLocal)) && Time.realtimeSinceStartup < deadline) yield return null;
        player = PlayerRegistry.Players.FirstOrDefault(p => p.IsLocal);
        Check(player != null && lobby.Offline, "Offline player did not spawn.");
        player.Outfit.SetItems(original); MoveToStart(player); lobby.EndRound(); yield return null; yield return null;
        Check(lobby.Results != null && lobby.Results.entries[0].rating.score == expected && !lobby.InputAllowed && root.gameObject.activeSelf, "Offline rating failed.");
        root.Find("Следующий раунд").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
        Check(lobby.Results == null && lobby.InputAllowed && player.Outfit.Items.Count() == original.Length, "Offline continue lost clothing or remained locked.");
        lobby.Leave();
        deadline = Time.realtimeSinceStartup + 15;
        while (lobby.Offline && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!lobby.Offline, "Offline leave failed.");
        Step("passed: online host rating, six fitted previews, UI text bounds, rotation, next round, offline rating and continue");
    }
}
