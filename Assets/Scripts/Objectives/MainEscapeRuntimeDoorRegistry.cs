using System.Collections.Generic;
using UnityEngine;

public static class MainEscapeRuntimeDoorRegistry
{
    private static readonly Dictionary<Vector3Int, IMainEscapeRuntimeDoor> DoorByCell = new();

    public static void Register(IMainEscapeRuntimeDoor door)
    {
        if (door == null)
        {
            return;
        }

        Vector3Int[] cells = door.DoorCells;
        if (cells == null || cells.Length == 0)
        {
            return;
        }

        for (int index = 0; index < cells.Length; index++)
        {
            DoorByCell[cells[index]] = door;
        }
    }

    public static void Unregister(IMainEscapeRuntimeDoor door)
    {
        if (door == null)
        {
            return;
        }

        List<Vector3Int> cellsToRemove = null;

        foreach ((Vector3Int cell, IMainEscapeRuntimeDoor registeredDoor) in DoorByCell)
        {
            if (ReferenceEquals(registeredDoor, door))
            {
                cellsToRemove ??= new List<Vector3Int>();
                cellsToRemove.Add(cell);
            }
        }

        if (cellsToRemove == null)
        {
            return;
        }

        for (int index = 0; index < cellsToRemove.Count; index++)
        {
            DoorByCell.Remove(cellsToRemove[index]);
        }
    }

    public static bool TryGetAtCell(Vector3Int cellPosition, out IMainEscapeRuntimeDoor door)
    {
        if (DoorByCell.TryGetValue(cellPosition, out door)
            && door != null
            && door.IsAvailable)
        {
            return true;
        }

        DoorByCell.Remove(cellPosition);
        door = null;
        return false;
    }
}
