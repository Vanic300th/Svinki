using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public sealed class SpectatorCamera : MonoBehaviour
{
    private NetworkLobby lobby;
    private NetworkPlayer target;
    private int index;
    private void Awake() => lobby = GetComponent<NetworkLobby>();
    private void LateUpdate()
    {
        if (!lobby.IsSpectator || lobby.Snapshot.phase != SessionPhase.Round)
        { Clear(); return; }
        Camera camera = Camera.main; if (camera == null) return;
        var players = new List<NetworkPlayer>();
        foreach (PlayerAvatar avatar in PlayerRegistry.Players)
        {
            var player = avatar != null ? avatar.GetComponent<NetworkPlayer>() : null;
            if (player != null && player.IsSpawned) players.Add(player);
        }
        players.Sort((a, b) => string.CompareOrdinal(a.ParticipantId, b.ParticipantId));
        var keyboard = Keyboard.current;
        if (!lobby.MenuVisible && keyboard != null)
        {
            if (keyboard.rightArrowKey.wasPressedThisFrame) index++;
            if (keyboard.leftArrowKey.wasPressedThisFrame) index--;
        }
        if (players.Count == 0) { Clear(); return; }
        index = (index % players.Count + players.Count) % players.Count;
        if (target != players[index])
        {
            Clear(); target = players[index];
            target.GetComponent<WorldOutfitRenderer>()?.SetFirstPersonHidden(true);
        }
        var firstPerson = camera.GetComponent<GrayboxFirstPersonCamera>();
        if (firstPerson != null) firstPerson.SetSpectatorMode();
        var pickup = camera.GetComponent<PlayerPickupInteractor>(); if (pickup != null) pickup.enabled = false;
        camera.transform.SetPositionAndRotation(target.EyePosition, target.EyeRotation);
        camera.GetComponentInChildren<FlashlightController>()?.SetOn(target.FlashlightOn);
    }
    private void Clear()
    {
        if (target != null) target.GetComponent<WorldOutfitRenderer>()?.SetFirstPersonHidden(target.IsOwner);
        target = null;
    }
    private void OnDisable() => Clear();
}
