using UnityEngine;

public static class RRunModalPresenter
{
    public static bool IsFailureModalShowing(IRGameClearPanelView panelView)
    {
        return panelView != null && panelView.IsFailureModal;
    }

    public static void Bind(IRGameClearPanelView panelView, RRunSessionController sessionController, WasdPlayerController playerController)
    {
        if (panelView == null)
        {
            return;
        }

        panelView.BindSessionController(sessionController);
        panelView.BindPlayer(playerController);
    }

    public static bool TryShowFloorClear(
        IRGameClearPanelView panelView,
        RRunSessionController sessionController,
        WasdPlayerController playerController,
        int clearedFloorNumber,
        int destinationFloorNumber)
    {
        if (panelView == null)
        {
            return false;
        }

        Bind(panelView, sessionController, playerController);
        panelView.ShowFloorClear(clearedFloorNumber, destinationFloorNumber);
        return true;
    }

    public static bool TryShowFinalClear(
        IRGameClearPanelView panelView,
        RRunSessionController sessionController,
        WasdPlayerController playerController)
    {
        if (panelView == null)
        {
            return false;
        }

        Bind(panelView, sessionController, playerController);
        panelView.ShowFinalClear();
        return true;
    }

    public static bool TryShowFailure(
        IRGameClearPanelView panelView,
        RRunSessionController sessionController,
        WasdPlayerController playerController,
        string caughtBy)
    {
        if (panelView == null)
        {
            return false;
        }

        Bind(panelView, sessionController, playerController);
        panelView.ShowFailure(caughtBy);
        return true;
    }
}
