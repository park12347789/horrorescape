using System;

using UnityEngine;

public enum SceneNodeKind
{
    Level,
    Support
}

public enum RouteEdgePolicy
{
    Fixed,
    Choice,
    WeightedRandom,
    Loop,
    Conditional
}

public enum SceneEntryReason
{
    ChapterStart,
    ContinueRun,
    Retry,
    Transition,
    StandaloneTest
}

public enum SceneExitOutcome
{
    Continue,
    Cleared,
    Failed,
    ReturnedToMenu,
    Abandoned
}

public enum SceneRouteActionKind
{
    Continue,
    PickRandomBranch,
    Loop,
    JumpToNode,
    ReturnToMenu
}

public enum ProfileEventKind
{
    CollectedNote,
    AchievementUnlocked,
    ChapterUnlocked,
    TutorialSeen
}

[Serializable]
public struct SceneInventoryItemState
{
    public string itemId;
    public string displayName;
    public int quantity;
}

[Serializable]
public struct ScenePlayerStateSnapshot
{
    public bool hasState;
    public int health;
    [Range(0f, 1f)] public float flashlightChargeNormalized;
    public bool flashlightEnabled;
    public SceneInventoryItemState[] inventoryItems;

    public static ScenePlayerStateSnapshot CreateDefault(int healthValue = 3)
    {
        return new ScenePlayerStateSnapshot
        {
            hasState = true,
            health = Mathf.Max(1, healthValue),
            flashlightChargeNormalized = 1f,
            flashlightEnabled = true,
            inventoryItems = Array.Empty<SceneInventoryItemState>()
        };
    }
}

[Serializable]
public struct SceneSpawnRequest
{
    public string spawnId;
    public bool allowFallbackSpawn;
}

[Serializable]
public struct SceneEntryContext
{
    public string gameModeId;
    public string chapterId;
    public string routeGraphId;
    public string sceneNodeId;
    public string runId;
    public int runSeed;
    public ScenePlayerStateSnapshot playerState;
    public SceneSpawnRequest spawnRequest;
    public bool testDefaultsAllowed;
    public string difficultyProfileId;
    public SceneEntryReason entryReason;
    public string[] sceneFlags;

    public bool IsValid => !string.IsNullOrWhiteSpace(gameModeId)
        && !string.IsNullOrWhiteSpace(chapterId)
        && !string.IsNullOrWhiteSpace(routeGraphId)
        && !string.IsNullOrWhiteSpace(sceneNodeId)
        && !string.IsNullOrWhiteSpace(runId);
}

[Serializable]
public struct ProfileProgressEvent
{
    public ProfileEventKind kind;
    public string id;
    public int quantity;
}

[Serializable]
public struct SceneRouteActionRequest
{
    public SceneRouteActionKind kind;
    public string targetSceneNodeId;
    public string exitId;
}

[Serializable]
public struct SceneExitResult
{
    public SceneExitOutcome outcome;
    public string exitId;
    public ScenePlayerStateSnapshot playerState;
    public ProfileProgressEvent[] profileEvents;
    public SceneRouteActionRequest routeAction;
    public string failureReason;
    public string debugSummary;
}

[Serializable]
public struct SceneNodeDefinition
{
    public string nodeId;
    public string scenePath;
    public SceneNodeKind kind;
    public string chapterLocalLevelId;
    public SceneTestDefaults testDefaults;
    public string[] tags;

    public bool IsValid => !string.IsNullOrWhiteSpace(nodeId)
        && !string.IsNullOrWhiteSpace(scenePath);
}

[Serializable]
public struct RouteEdgeDefinition
{
    public string fromNodeId;
    public string exitId;
    public string toNodeId;
    public RouteEdgePolicy policy;
    [Min(0f)] public float weight;
    public string requiredProfileEventId;
    public string requiredRunFlag;

    public bool IsValid => !string.IsNullOrWhiteSpace(fromNodeId)
        && !string.IsNullOrWhiteSpace(exitId)
        && !string.IsNullOrWhiteSpace(toNodeId);
}
