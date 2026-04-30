using UnityEngine;

public static class RDirectExitRouteUtility
{
    public static bool UsesElevatorPropDirectExit(int floorNumber)
    {
        int normalizedFloorNumber = Mathf.Max(1, floorNumber);
        return normalizedFloorNumber >= 2;
    }
}
