using System;

using UnityEngine;

[CreateAssetMenu(
    fileName = "SceneTestDefaults",
    menuName = "Main Escape/Contracts/Scene Test Defaults")]
public sealed class SceneTestDefaults : ScriptableObject
{
    [SerializeField] private string sceneNodeId;
    [SerializeField] private SceneSpawnRequest spawnRequest;
    [SerializeField] private ScenePlayerStateSnapshot playerState = ScenePlayerStateSnapshot.CreateDefault();
    [SerializeField] private int runSeed = 1337;
    [SerializeField] private string[] grantedProfileEventIds = Array.Empty<string>();

    public string SceneNodeId => sceneNodeId?.Trim() ?? string.Empty;
    public SceneSpawnRequest SpawnRequest => spawnRequest;
    public ScenePlayerStateSnapshot PlayerState => playerState.hasState ? playerState : ScenePlayerStateSnapshot.CreateDefault();
    public int RunSeed => runSeed;
    public string[] GrantedProfileEventIds => grantedProfileEventIds ?? Array.Empty<string>();
}
