using UnityEngine;

public static class MainEscapeDoorRuntimeUtility
{
    private const string FrontDoorClosedPrefabResourcePath = "MainEscape/DoorPrefabs/FrontDoorClosed";
    private const string FrontDoorOpenPrefabResourcePath = "MainEscape/DoorPrefabs/FrontDoorOpen";
    private const string SideDoorClosedPrefabResourcePath = "MainEscape/DoorPrefabs/SideDoor/SideDoorClosed";
    private const string SideDoorOpenPrefabResourcePath = "MainEscape/DoorPrefabs/SideDoor/SideDoorOpen";
    private static GameObject cachedFrontDoorClosedPrefab;
    private static GameObject cachedFrontDoorOpenPrefab;
    private static GameObject cachedSideDoorClosedPrefab;
    private static GameObject cachedSideDoorOpenPrefab;

    public static DoorController CreateDoorController(
        Transform parent,
        string objectName,
        Transform floorRoot,
        GridMapService mapService,
        Vector3Int[] doorCells,
        int layer = -1)
    {
        if (parent == null || floorRoot == null || mapService == null || doorCells == null || doorCells.Length == 0)
        {
            return null;
        }

        GameObject doorControllerObject = new(string.IsNullOrWhiteSpace(objectName) ? "DoorController" : objectName);
        doorControllerObject.transform.SetParent(parent, false);

        if (layer >= 0)
        {
            doorControllerObject.layer = layer;
        }

        doorControllerObject.transform.position = ResolveDoorCenter(mapService, doorCells);
        DoorController doorController = doorControllerObject.AddComponent<DoorController>();
        doorController.Configure(null, mapService, string.Empty, doorCells);
        BindAuthoredVisualRoots(doorController, floorRoot, mapService, doorCells);
        ApplyDoorPrefabs(doorController);
        return doorController;
    }

    public static void BindAuthoredVisualRoots(
        DoorController doorController,
        Transform floorRoot,
        GridMapService mapService,
        Vector3Int[] doorCells)
    {
        if (doorController == null
            || floorRoot == null
            || mapService == null
            || mapService.GroundTilemap == null
            || doorCells == null
            || doorCells.Length == 0)
        {
            return;
        }

        Transform[] authoredVisualRoots = MainEscapeVisualAuthoringSynthesis.FindVisualDoorRootsForCells(
            floorRoot,
            mapService.GroundTilemap,
            doorCells);

        if (authoredVisualRoots.Length == 0)
        {
            return;
        }

        doorController.SetBuiltInVisualsEnabled(false);
        doorController.BindAuthoredVisualRoots(authoredVisualRoots);
    }

    private static void ApplyDoorPrefabs(DoorController doorController)
    {
        if (doorController == null)
        {
            return;
        }

        cachedFrontDoorClosedPrefab ??= Resources.Load<GameObject>(FrontDoorClosedPrefabResourcePath);
        cachedFrontDoorOpenPrefab ??= Resources.Load<GameObject>(FrontDoorOpenPrefabResourcePath);
        cachedSideDoorClosedPrefab ??= Resources.Load<GameObject>(SideDoorClosedPrefabResourcePath);
        cachedSideDoorOpenPrefab ??= Resources.Load<GameObject>(SideDoorOpenPrefabResourcePath);

        if (cachedFrontDoorClosedPrefab == null || cachedFrontDoorOpenPrefab == null)
        {
            return;
        }

        if (doorController.IsSideDoorVisual())
        {
            if (cachedSideDoorClosedPrefab != null && cachedSideDoorOpenPrefab != null)
            {
                doorController.SetDoorPrefabs(cachedSideDoorClosedPrefab, cachedSideDoorOpenPrefab);
                return;
            }
        }

        doorController.SetDoorPrefabs(cachedFrontDoorClosedPrefab, cachedFrontDoorOpenPrefab);
    }

    public static Vector3 ResolveDoorCenter(GridMapService mapService, Vector3Int[] doorCells)
    {
        if (mapService == null || doorCells == null || doorCells.Length == 0)
        {
            return Vector3.zero;
        }

        Vector3 center = Vector3.zero;

        for (int index = 0; index < doorCells.Length; index++)
        {
            center += mapService.CellToWorldCenter(doorCells[index]);
        }

        return center / doorCells.Length;
    }

    public static bool CellsMatch(Vector3Int[] candidateCells, Vector3Int[] referenceCells)
    {
        if (candidateCells == null || referenceCells == null || candidateCells.Length != referenceCells.Length)
        {
            return false;
        }

        for (int index = 0; index < candidateCells.Length; index++)
        {
            bool matchedCell = false;

            for (int referenceIndex = 0; referenceIndex < referenceCells.Length; referenceIndex++)
            {
                if (candidateCells[index] == referenceCells[referenceIndex])
                {
                    matchedCell = true;
                    break;
                }
            }

            if (!matchedCell)
            {
                return false;
            }
        }

        return true;
    }
}
