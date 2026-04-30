using System;

public readonly struct FloorDoorAssemblyResult
{
    public FloorDoorAssemblyResult(DoorController mainGateDoorController, DoorController[] createdDoorControllers)
    {
        MainGateDoorController = mainGateDoorController;
        CreatedDoorControllers = createdDoorControllers ?? Array.Empty<DoorController>();
    }

    public DoorController MainGateDoorController { get; }
    public DoorController[] CreatedDoorControllers { get; }
}
