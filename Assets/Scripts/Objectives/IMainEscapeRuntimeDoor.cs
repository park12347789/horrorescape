using UnityEngine;

public interface IMainEscapeRuntimeDoor
{
    Vector3Int[] DoorCells { get; }
    bool IsOpen { get; }
    bool IsAvailable { get; }

    bool SetOpenState(bool open);
    bool TryOpenForEnemy(EnemyStateMachine opener);
}
