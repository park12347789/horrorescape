using System;

using UnityEngine;

[Serializable]
public sealed class RRunSceneBindings
{
    [Header("Player")]
    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private RPlayerRuntimeReferences playerRuntime;
    [SerializeField] private PlayerInventory playerInventory;

    [Header("Progression")]
    [SerializeField] private FloorEscapeGoalPickup goalPickup;
    [SerializeField] private FloorEscapeTransitionPoint stairPoint;
    [SerializeField] private FloorEscapeTransitionPoint finalExitPoint;
    [SerializeField] private PrototypeInventoryPickup batteryPickup;
    [SerializeField] private PrototypeInventoryPickup bottlePickup;
    [SerializeField] private PrototypeInventoryPickup medkitPickup;
    [SerializeField] private MainEscapeKeyGatePoint keyGatePoint;
    [SerializeField] private MainEscapeEmergencyStairsPoint authoredStairsPoint;

    [Header("Presentation")]
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private SpriteRenderer accentBackdrop;
    [SerializeField] private RFloorDirector floorDirector;
    [SerializeField] private MonoBehaviour hudCanvasSource;

    public static RRunSceneBindings Create(
        WasdPlayerController playerController,
        RPlayerRuntimeReferences playerRuntime,
        PlayerInventory playerInventory,
        FloorEscapeGoalPickup goalPickup,
        FloorEscapeTransitionPoint stairPoint,
        FloorEscapeTransitionPoint finalExitPoint,
        PrototypeInventoryPickup batteryPickup,
        PrototypeInventoryPickup bottlePickup,
        PrototypeInventoryPickup medkitPickup,
        MainEscapeKeyGatePoint keyGatePoint,
        MainEscapeEmergencyStairsPoint authoredStairsPoint,
        FlashlightFogOfWarOverlay fogOfWarOverlay,
        SpriteRenderer accentBackdrop,
        RFloorDirector floorDirector,
        MonoBehaviour hudCanvasSource)
    {
        return new RRunSceneBindings
        {
            playerController = playerController,
            playerRuntime = playerRuntime,
            playerInventory = playerInventory,
            goalPickup = goalPickup,
            stairPoint = stairPoint,
            finalExitPoint = finalExitPoint,
            batteryPickup = batteryPickup,
            bottlePickup = bottlePickup,
            medkitPickup = medkitPickup,
            keyGatePoint = keyGatePoint,
            authoredStairsPoint = authoredStairsPoint,
            fogOfWarOverlay = fogOfWarOverlay,
            accentBackdrop = accentBackdrop,
            floorDirector = floorDirector,
            hudCanvasSource = hudCanvasSource
        };
    }

    public WasdPlayerController PlayerController => playerController;

    public RPlayerRuntimeReferences PlayerRuntime
    {
        get
        {
            if (playerRuntime != null)
            {
                return playerRuntime;
            }

            return playerController != null
                ? playerController.GetComponent<RPlayerRuntimeReferences>()
                : null;
        }
    }

    public PlayerInventory PlayerInventory
    {
        get
        {
            if (playerInventory != null)
            {
                return playerInventory;
            }

            return PlayerRuntime != null ? PlayerRuntime.Inventory : null;
        }
    }

    public FloorEscapeGoalPickup GoalPickup => goalPickup;
    public FloorEscapeTransitionPoint LegacyStairProxy => stairPoint;
    // Compatibility alias. Prefer LegacyStairProxy for new read paths.
    public FloorEscapeTransitionPoint StairPoint => stairPoint;
    public FloorEscapeTransitionPoint FinalExitPoint => finalExitPoint;
    public PrototypeInventoryPickup BatteryPickup => batteryPickup;
    public PrototypeInventoryPickup BottlePickup => bottlePickup;
    public PrototypeInventoryPickup MedkitPickup => medkitPickup;
    public MainEscapeKeyGatePoint KeyGatePoint => keyGatePoint;
    public MainEscapeEmergencyStairsPoint AuthoredStairsPoint => authoredStairsPoint;
    public FlashlightFogOfWarOverlay FogOfWarOverlay => fogOfWarOverlay;
    public SpriteRenderer AccentBackdrop => accentBackdrop;
    public RFloorDirector FloorDirector => floorDirector;
    public IRHudCanvas HudCanvas => hudCanvasSource as IRHudCanvas;
    public bool HasLegacyTransitionProxy => LegacyStairProxy != null;
    // Legacy stair proxies can still be consumed when present, but authored anchors
    // define the required progression contract for rebuilt floor scenes.
    public bool HasAnyProgressionAnchor => GoalPickup != null
        || FinalExitPoint != null
        || KeyGatePoint != null
        || AuthoredStairsPoint != null;

    public bool TryValidate(out string errorMessage)
    {
        if (PlayerController == null)
        {
            errorMessage = $"{nameof(RRunSceneBindings)} requires a {nameof(WasdPlayerController)} reference.";
            return false;
        }

        if (PlayerRuntime == null)
        {
            errorMessage = $"{nameof(RRunSceneBindings)} requires a {nameof(RPlayerRuntimeReferences)} reference.";
            return false;
        }

        if (PlayerInventory == null)
        {
            errorMessage = $"{nameof(RRunSceneBindings)} requires a {nameof(PlayerInventory)} reference.";
            return false;
        }

        if (FloorDirector == null)
        {
            errorMessage = $"{nameof(RRunSceneBindings)} requires a {nameof(RFloorDirector)} reference.";
            return false;
        }

        if (HudCanvas == null)
        {
            errorMessage = $"{nameof(RRunSceneBindings)} requires a HUD canvas source that implements {nameof(IRHudCanvas)}.";
            return false;
        }

        if (!HasAnyProgressionAnchor)
        {
            errorMessage = $"{nameof(RRunSceneBindings)} requires at least one authored progression anchor.";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }
}
