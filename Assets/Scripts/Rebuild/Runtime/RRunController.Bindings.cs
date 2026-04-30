using UnityEngine;

public sealed partial class RRunController
{
    public void Initialize(
        RRunSessionController sessionController,
        RRunSceneBindings sceneBindings,
        int activeFloorNumber = 5)
    {
        if (sceneBindings == null)
        {
            Debug.LogError($"{nameof(RRunController)} cannot initialize because no {nameof(RRunSceneBindings)} instance was supplied.", this);
            return;
        }

        if (!sceneBindings.TryValidate(out string errorMessage))
        {
            Debug.LogError(errorMessage, this);
            return;
        }

        Initialize(
            sessionController,
            sceneBindings.PlayerController,
            sceneBindings.PlayerInventory,
            sceneBindings.GoalPickup,
            sceneBindings.LegacyStairProxy,
            sceneBindings.FinalExitPoint,
            sceneBindings.BatteryPickup,
            sceneBindings.BottlePickup,
            sceneBindings.MedkitPickup,
            sceneBindings.KeyGatePoint,
            sceneBindings.AuthoredStairsPoint,
            sceneBindings.FogOfWarOverlay,
            sceneBindings.AccentBackdrop,
            sceneBindings.FloorDirector,
            sceneBindings.HudCanvas,
            activeFloorNumber);
    }
}
