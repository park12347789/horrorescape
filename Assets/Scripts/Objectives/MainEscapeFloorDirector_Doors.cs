using UnityEngine;

public sealed partial class MainEscapeFloorDirector : MonoBehaviour
{
    public void SetMainGateInteractionEnabled(bool enabled)
    {
        if (mainGateDoorController != null)
        {
            mainGateDoorController.gameObject.SetActive(enabled);
        }
    }

    public bool SetMainGateOpen(bool open)
    {
        if (!HasMainGate || currentBuild.MapService == null)
        {
            return false;
        }

        bool changed = false;
        Vector3Int[] mainGateCells = currentBuild.Layout.MainDoorCells;

        for (int index = 0; index < mainGateCells.Length; index++)
        {
            changed |= open
                ? currentBuild.MapService.OpenDoor(mainGateCells[index])
                : currentBuild.MapService.CloseDoor(mainGateCells[index]);
        }

        return changed;
    }

    public bool SetDoorInteractionEnabledNear(Vector3 worldPosition, float maxDistance, bool enabled)
    {
        DoorController doorController = FindNearestDoorController(worldPosition, maxDistance);

        if (doorController == null)
        {
            return false;
        }

        doorController.gameObject.SetActive(enabled);
        return true;
    }

    public bool SetDoorOpenNear(Vector3 worldPosition, float maxDistance, bool open)
    {
        DoorController doorController = FindNearestDoorController(worldPosition, maxDistance);
        return doorController != null && doorController.SetOpenState(open);
    }

    public bool TryUnlockGateRoute(bool hasAuthoredGateWorldPosition, Vector3 authoredGateWorldPosition, float gateSearchRadius)
    {
        if (hasAuthoredGateWorldPosition)
        {
            bool openedDoor = SetDoorOpenNear(authoredGateWorldPosition, gateSearchRadius, true);

            if (openedDoor)
            {
                SetDoorInteractionEnabledNear(authoredGateWorldPosition, gateSearchRadius, false);
            }

            return openedDoor;
        }

        if (!HasMainGate)
        {
            return true;
        }

        bool openedMainGate = SetMainGateOpen(true);

        if (openedMainGate)
        {
            SetMainGateInteractionEnabled(false);
        }

        return openedMainGate;
    }

    public void ApplyGateRouting(
        bool usesAuthoredGateSequence,
        bool hasAuthoredGateWorldPosition,
        Vector3 authoredGateWorldPosition,
        float gateSearchRadius,
        bool gateUnlocked)
    {
        if (usesAuthoredGateSequence && hasAuthoredGateWorldPosition)
        {
            SetDoorInteractionEnabledNear(authoredGateWorldPosition, gateSearchRadius, false);
            SetDoorOpenNear(authoredGateWorldPosition, gateSearchRadius, gateUnlocked);
            return;
        }

        SetMainGateInteractionEnabled(!usesAuthoredGateSequence);

        if (usesAuthoredGateSequence)
        {
            SetMainGateOpen(gateUnlocked);
        }
    }

    private void DestroyCurrentFloor()
    {
        encounterSpawner?.ClearFloor();
        DestroyRuntimeDoorControllers();
        mainGateDoorController = null;
        runtimeDoorControllers.Clear();

        if (currentBuild.FloorRoot != null && currentBuild.FloorRoot.parent == transform)
        {
            currentBuild.FloorRoot.gameObject.SetActive(false);
            Destroy(currentBuild.FloorRoot.gameObject);
        }
        else if (Application.isPlaying && currentBuild.IsAuthored && currentBuild.FloorRoot != null)
        {
            // The authored 5F floor lives in-scene instead of under the runtime root, so
            // leaving it active causes its colliders to overlap generated lower floors.
            currentBuild.FloorRoot.gameObject.SetActive(false);
        }

        currentBuild = default;
    }

    private void CreateDoorControllers()
    {
        if (currentBuild.FloorRoot == null || currentBuild.MapService == null || currentBuild.Layout == null)
        {
            return;
        }

        GeneratedDoorGroupData[] doorGroups = currentBuild.Layout.DoorGroups;

        if (doorGroups != null && doorGroups.Length > 0)
        {
            for (int groupIndex = 0; groupIndex < doorGroups.Length; groupIndex++)
            {
                GeneratedDoorGroupData doorGroup = doorGroups[groupIndex];
                DoorController doorController = CreateDoorController(doorGroup.Cells, $"MainEscapeDoor_{doorGroup.DoorGroupId}");

                if (mainGateDoorController == null && MatchesMainGateCells(doorGroup.Cells))
                {
                    mainGateDoorController = doorController;
                }
            }

            if (mainGateDoorController == null && currentBuild.Layout.MainDoorCells.Length > 0)
            {
                mainGateDoorController = CreateDoorController(currentBuild.Layout.MainDoorCells, "MainEscapeDoor_MainGate");
            }

            return;
        }

        if (currentBuild.Layout.MainDoorCells.Length > 0)
        {
            mainGateDoorController = CreateDoorController(currentBuild.Layout.MainDoorCells, "MainEscapeDoor_MainGate");
        }
    }

    private DoorController CreateDoorController(Vector3Int[] doorCells, string objectName)
    {
        if (doorCells == null || doorCells.Length == 0 || currentBuild.MapService == null || currentBuild.FloorRoot == null)
        {
            return null;
        }

        DoorController doorController = MainEscapeDoorRuntimeUtility.CreateDoorController(
            transform,
            objectName,
            currentBuild.FloorRoot,
            currentBuild.MapService,
            doorCells);

        if (doorController != null)
        {
            runtimeDoorControllers.Add(doorController);
        }

        return doorController;
    }

    private void DestroyRuntimeDoorControllers()
    {
        DoorController[] doorControllers = GetComponentsInChildren<DoorController>(true);

        for (int index = 0; index < doorControllers.Length; index++)
        {
            DoorController doorController = doorControllers[index];

            if (doorController != null)
            {
                Destroy(doorController.gameObject);
            }
        }

        mainGateDoorController = null;
        runtimeDoorControllers.Clear();
    }

    private bool MatchesMainGateCells(Vector3Int[] candidateCells)
    {
        Vector3Int[] mainGateCells = currentBuild.Layout != null ? currentBuild.Layout.MainDoorCells : null;
        return MainEscapeDoorRuntimeUtility.CellsMatch(candidateCells, mainGateCells);
    }

    private DoorController FindNearestDoorController(Vector3 worldPosition, float maxDistance)
    {
        float bestDistance = maxDistance * maxDistance;
        DoorController nearest = null;

        for (int index = 0; index < runtimeDoorControllers.Count; index++)
        {
            DoorController doorController = runtimeDoorControllers[index];

            if (doorController == null)
            {
                continue;
            }

            float distance = (doorController.transform.position - worldPosition).sqrMagnitude;

            if (distance > bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            nearest = doorController;
        }

        return nearest;
    }
}
