using UnityEngine;

public sealed partial class RRunController
{
    private void AdvanceToFloorHandoff(
        RRunSessionController sessionController,
        int clearedFloorNumber,
        int destinationFloorNumber)
    {
        sessionController.CaptureBoundPlayerState(playerRuntime);
        sessionController.UnbindGameplayRuntime(playerRuntime);
        sessionController.RecordFloorClear(clearedFloorNumber, destinationFloorNumber);

        if (RSceneRouter.LoadFloorSceneThroughElevatorTransition(sessionController, clearedFloorNumber, destinationFloorNumber))
        {
            return;
        }

        PrototypeAudioManager.TryPlayFloorTransition(destinationFloorNumber);
        RSceneRouter.LoadFloorScene(sessionController, destinationFloorNumber);
    }

    private void RecordFinalClearHandoff(RRunSessionController sessionController, int clearedFloorNumber)
    {
        sessionController?.RecordFinalClear(clearedFloorNumber);
    }

    private void RecordFailureHandoff(RRunSessionController sessionController, int floorNumber, string caughtBy)
    {
        sessionController?.RecordFailure(floorNumber, caughtBy);
    }

    private void RefreshFloorPresentation()
    {
        SyncAuthoredGateRouting();
        RefreshFloorPresentationVisuals();
    }

    private void SyncAuthoredGateRouting()
    {
        if (floorDirector == null)
        {
            return;
        }

        // Direct-exit floors gate progress on key ownership at the exit itself, so their
        // authored main gate must stay open or the player can never physically reach it.
        bool gateShouldBeOpen = currentFloorGateUnlocked || UsesDirectAuthoredExitInteraction;
        floorDirector.ApplyMainGateRouting(UsesAuthoredKeyGateSequence, gateShouldBeOpen);
    }

    private void RefreshFloorPresentationVisuals()
    {
        EscapeFloorDefinition floor = GetCurrentFloor();

        if (accentBackdrop != null)
        {
            Color accentColor = floor.AccentColor;
            accentColor.a = escaped ? 0.18f : 0.24f;
            accentBackdrop.color = accentColor;
        }

        goalPickup?.RefreshState();
        keyGatePoint?.RefreshState();

        if (authoredStairsPoint != null)
        {
            authoredStairsPoint.RefreshState();
        }
        else
        {
            stairPoint?.RefreshState();
        }

        finalExitPoint?.RefreshState();
    }
}
