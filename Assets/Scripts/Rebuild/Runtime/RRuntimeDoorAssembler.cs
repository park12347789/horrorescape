using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Tilemaps;

public sealed class RRuntimeDoorAssembler : IFloorDoorAssembler
{
    public FloorDoorAssemblyResult AssembleDoors(OfficeFloorBuildResult buildResult, Transform runtimeParent)
    {
        if (runtimeParent == null || buildResult.FloorRoot == null || buildResult.MapService == null || buildResult.Layout == null)
        {
            return new FloorDoorAssemblyResult(null, null);
        }

        List<DoorController> createdDoorControllers = new();
        DoorController mainGateDoorController = null;
        GeneratedDoorGroupData[] doorGroups = buildResult.Layout.DoorGroups;
        MainEscapeSelfContainedDoor[] selfContainedDoors = buildResult.FloorRoot.GetComponentsInChildren<MainEscapeSelfContainedDoor>(true);
        Tilemap groundTilemap = buildResult.MapService.GroundTilemap;

        if (doorGroups != null && doorGroups.Length > 0)
        {
            for (int groupIndex = 0; groupIndex < doorGroups.Length; groupIndex++)
            {
                GeneratedDoorGroupData doorGroup = doorGroups[groupIndex];

                if (ShouldSkipDoorGroup(selfContainedDoors, groundTilemap, doorGroup.Cells))
                {
                    continue;
                }

                DoorController doorController = CreateDoorController(buildResult, runtimeParent, doorGroup.Cells, $"RDoor_{doorGroup.DoorGroupId}");

                if (doorController == null)
                {
                    continue;
                }

                createdDoorControllers.Add(doorController);

                if (mainGateDoorController == null && MatchesMainGateCells(buildResult, doorGroup.Cells))
                {
                    mainGateDoorController = doorController;
                }
            }

            if (mainGateDoorController == null && buildResult.Layout.MainDoorCells.Length > 0)
            {
                mainGateDoorController = CreateDoorController(buildResult, runtimeParent, buildResult.Layout.MainDoorCells, "RDoor_MainGate");

                if (mainGateDoorController != null)
                {
                    createdDoorControllers.Add(mainGateDoorController);
                }
            }

            return new FloorDoorAssemblyResult(mainGateDoorController, createdDoorControllers.ToArray());
        }

        if (buildResult.Layout.MainDoorCells.Length > 0)
        {
            mainGateDoorController = CreateDoorController(buildResult, runtimeParent, buildResult.Layout.MainDoorCells, "RDoor_MainGate");

            if (mainGateDoorController != null)
            {
                createdDoorControllers.Add(mainGateDoorController);
            }
        }

        return new FloorDoorAssemblyResult(mainGateDoorController, createdDoorControllers.ToArray());
    }

    public void DestroyDoors(Transform runtimeParent)
    {
        if (runtimeParent == null)
        {
            return;
        }

        DoorController[] doorControllers = runtimeParent.GetComponentsInChildren<DoorController>(true);

        for (int index = 0; index < doorControllers.Length; index++)
        {
            DoorController doorController = doorControllers[index];

            if (doorController != null)
            {
                Object.Destroy(doorController.gameObject);
            }
        }
    }

    private static DoorController CreateDoorController(
        OfficeFloorBuildResult buildResult,
        Transform runtimeParent,
        Vector3Int[] doorCells,
        string objectName)
    {
        if (doorCells == null || doorCells.Length == 0 || buildResult.MapService == null || buildResult.FloorRoot == null)
        {
            return null;
        }

        return MainEscapeDoorRuntimeUtility.CreateDoorController(
            runtimeParent,
            objectName,
            buildResult.FloorRoot,
            buildResult.MapService,
            doorCells,
            GameLayers.DoorIndex);
    }

    private static bool MatchesMainGateCells(OfficeFloorBuildResult buildResult, Vector3Int[] candidateCells)
    {
        Vector3Int[] mainGateCells = buildResult.Layout != null ? buildResult.Layout.MainDoorCells : null;
        return MainEscapeDoorRuntimeUtility.CellsMatch(candidateCells, mainGateCells);
    }

    private static bool ShouldSkipDoorGroup(
        MainEscapeSelfContainedDoor[] selfContainedDoors,
        Tilemap groundTilemap,
        Vector3Int[] candidateCells)
    {
        if (groundTilemap == null || selfContainedDoors == null || candidateCells == null || candidateCells.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < selfContainedDoors.Length; index++)
        {
            MainEscapeSelfContainedDoor door = selfContainedDoors[index];

            if (door != null && door.MatchesDoorCells(groundTilemap, candidateCells))
            {
                return true;
            }
        }

        return false;
    }
}
