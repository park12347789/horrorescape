using UnityEngine;
using UnityEngine.SceneManagement;

public readonly struct RAuthoredExitReferenceSet
{
    public RAuthoredExitReferenceSet(
        FloorEscapeGoalPickup goalPickup,
        FloorEscapeTransitionPoint emergencyStairsPoint,
        FloorEscapeTransitionPoint finalExitPoint,
        PrototypeInventoryPickup batteryPickup,
        PrototypeInventoryPickup bottlePickup,
        PrototypeInventoryPickup medkitPickup,
        MainEscapeKeyPickup keyPickup,
        MainEscapeKeyGatePoint keyGatePoint,
        MainEscapeEmergencyStairsPoint authoredStairsPoint)
    {
        GoalPickup = goalPickup;
        EmergencyStairsPoint = emergencyStairsPoint;
        FinalExitPoint = finalExitPoint;
        BatteryPickup = batteryPickup;
        BottlePickup = bottlePickup;
        MedkitPickup = medkitPickup;
        KeyPickup = keyPickup;
        KeyGatePoint = keyGatePoint;
        AuthoredStairsPoint = authoredStairsPoint;
    }

    public FloorEscapeGoalPickup GoalPickup { get; }
    public FloorEscapeTransitionPoint EmergencyStairsPoint { get; }
    public FloorEscapeTransitionPoint FinalExitPoint { get; }
    public PrototypeInventoryPickup BatteryPickup { get; }
    public PrototypeInventoryPickup BottlePickup { get; }
    public PrototypeInventoryPickup MedkitPickup { get; }
    public MainEscapeKeyPickup KeyPickup { get; }
    public MainEscapeKeyGatePoint KeyGatePoint { get; }
    public MainEscapeEmergencyStairsPoint AuthoredStairsPoint { get; }
}

public readonly struct RDirectExitElevatorReferenceSet
{
    public RDirectExitElevatorReferenceSet(
        MainEscapeElevatorExitInteractable elevatorExitPoint,
        SpriteRenderer elevatorExitVisual,
        Transform elevatorExitRoot)
    {
        ElevatorExitPoint = elevatorExitPoint;
        ElevatorExitVisual = elevatorExitVisual;
        ElevatorExitRoot = elevatorExitRoot;
    }

    public MainEscapeElevatorExitInteractable ElevatorExitPoint { get; }
    public SpriteRenderer ElevatorExitVisual { get; }
    public Transform ElevatorExitRoot { get; }
}

public static class RAuthoredExitReferenceResolver
{
    public static RAuthoredExitReferenceSet ResolveSceneInteractables(
        Scene scene,
        UnityEngine.Object owner,
        string ownerLabel,
        MainEscapeRuntimeSettings settings,
        bool usesDirectAuthoredExit,
        int activeFloorNumber,
        RAuthoredExitReferenceSet current)
    {
        int resolvedFloorNumber = Mathf.Max(1, activeFloorNumber);
        bool isTerminalFloor = resolvedFloorNumber <= 1;
        bool shouldRequireKeyGatePoint = !usesDirectAuthoredExit && !isTerminalFloor;
        bool shouldRequireAuthoredStairsPoint = !isTerminalFloor;

        FloorEscapeTransitionPoint resolvedFinalExitPoint = current.FinalExitPoint ?? (usesDirectAuthoredExit
            ? RSceneReferenceLookup.FindOptionalComponentByNames<FloorEscapeTransitionPoint>(scene, "RStreetExit", settings.FinalExitName)
            : RSceneReferenceLookup.FindComponentByNames<FloorEscapeTransitionPoint>(scene, owner, ownerLabel, nameof(current.FinalExitPoint), "RStreetExit", settings.FinalExitName));

        MainEscapeKeyGatePoint resolvedKeyGatePoint = current.KeyGatePoint ?? (shouldRequireKeyGatePoint
            ? RSceneReferenceLookup.FindComponentByNames<MainEscapeKeyGatePoint>(scene, owner, ownerLabel, nameof(current.KeyGatePoint), "RKeyGateVisual", settings.KeyGateVisualName)
            : RSceneReferenceLookup.FindOptionalComponentByNames<MainEscapeKeyGatePoint>(scene, "RKeyGateVisual", settings.KeyGateVisualName));

        MainEscapeEmergencyStairsPoint resolvedAuthoredStairsPoint = current.AuthoredStairsPoint ?? (shouldRequireAuthoredStairsPoint
            ? RSceneReferenceLookup.FindComponentByNames<MainEscapeEmergencyStairsPoint>(scene, owner, ownerLabel, nameof(current.AuthoredStairsPoint), "REmergencyStairsVisual", settings.EmergencyStairsVisualName)
            : RSceneReferenceLookup.FindOptionalComponentByNames<MainEscapeEmergencyStairsPoint>(scene, "REmergencyStairsVisual", settings.EmergencyStairsVisualName));

        return new RAuthoredExitReferenceSet(
            current.GoalPickup ?? RSceneReferenceLookup.FindOptionalComponentByNames<FloorEscapeGoalPickup>(scene, "RFloorTool", settings.FloorToolPickupName),
            current.EmergencyStairsPoint ?? RSceneReferenceLookup.FindOptionalComponentByNames<FloorEscapeTransitionPoint>(scene, "REmergencyStairs", settings.EmergencyStairsName),
            resolvedFinalExitPoint,
            current.BatteryPickup ?? RSceneReferenceLookup.FindOptionalComponentByNames<PrototypeInventoryPickup>(scene, settings.AuthoredBatteryPickupName, settings.BatteryRuntimePickupName),
            current.BottlePickup ?? RSceneReferenceLookup.FindOptionalComponentByNames<PrototypeInventoryPickup>(scene, settings.GlassBottleRuntimePickupName, settings.AuthoredGlassBottlePickupName),
            current.MedkitPickup ?? RSceneReferenceLookup.FindOptionalComponentByNames<PrototypeInventoryPickup>(scene, settings.MedkitRuntimePickupName, settings.AuthoredMedkitPickupName),
            current.KeyPickup ?? RSceneReferenceLookup.FindOptionalComponentByNames<MainEscapeKeyPickup>(scene, settings.AuthoredKeyPickupName),
            resolvedKeyGatePoint,
            resolvedAuthoredStairsPoint);
    }

    public static RDirectExitElevatorReferenceSet ResolveDirectElevatorExit(
        Scene scene,
        MainEscapeFloorAuthoring floorAuthoring,
        MainEscapeElevatorExitInteractable currentPoint,
        SpriteRenderer currentVisual,
        Transform currentRoot,
        string namedVisualName)
    {
        MainEscapeElevatorExitInteractable resolvedPoint = currentPoint;
        SpriteRenderer resolvedVisual = currentVisual;
        Transform resolvedRoot = currentRoot;

        if (resolvedPoint != null)
        {
            resolvedVisual ??= resolvedPoint.GetComponent<SpriteRenderer>();
            resolvedRoot ??= resolvedPoint.transform.parent;
            return new RDirectExitElevatorReferenceSet(resolvedPoint, resolvedVisual, resolvedRoot);
        }

        resolvedVisual ??= ResolveNamedElevatorExitVisual(scene, namedVisualName);

        if (resolvedRoot == null)
        {
            resolvedRoot = ResolveElevatorExitRoot(scene, floorAuthoring, resolvedVisual);
        }

        return new RDirectExitElevatorReferenceSet(resolvedPoint, resolvedVisual, resolvedRoot);
    }

    private static Transform ResolveElevatorExitRoot(
        Scene scene,
        MainEscapeFloorAuthoring floorAuthoring,
        SpriteRenderer elevatorExitVisual)
    {
        if (elevatorExitVisual != null)
        {
            return elevatorExitVisual.transform.parent;
        }

        if (floorAuthoring != null)
        {
            Transform visualRoot = RSceneReferenceLookup.FindDirectChild(floorAuthoring.transform, "Visual");
            Transform propsRoot = RSceneReferenceLookup.FindDirectChild(visualRoot, "Props");
            Transform authoredRoot = RSceneReferenceLookup.FindDirectChild(propsRoot, "elevator");

            if (authoredRoot != null)
            {
                return authoredRoot;
            }

            authoredRoot = RSceneReferenceLookup.FindTransformInHierarchy(floorAuthoring.transform, "elevator");

            if (authoredRoot != null)
            {
                return authoredRoot;
            }
        }

        return RSceneReferenceLookup.FindTransformInScene(scene, "elevator");
    }

    private static SpriteRenderer ResolveNamedElevatorExitVisual(Scene scene, string namedVisualName)
    {
        Transform elevatorVisualTransform = RSceneReferenceLookup.FindTransformInScene(scene, namedVisualName);
        return elevatorVisualTransform != null
            ? elevatorVisualTransform.GetComponent<SpriteRenderer>()
            : null;
    }
}
