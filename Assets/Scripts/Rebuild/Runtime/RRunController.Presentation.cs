using UnityEngine;

public sealed partial class RRunController
{
    public bool IsFailureModalShowing => RRunModalPresenter.IsFailureModalShowing(ResolveGameClearPanel());

    public void RefreshPresentation()
    {
        RRunModalPresenter.Bind(ResolveGameClearPanel(), ResolveSessionController(), playerController);
    }

    public void BindHudCanvas(IRHudCanvas canvas)
    {
        hudCanvas = canvas;
        RRunModalPresenter.Bind(ResolveGameClearPanel(), ResolveSessionController(), playerController);
    }

    private IRGameClearPanelView ResolveGameClearPanel()
    {
        return hudCanvas != null ? hudCanvas.GameClearPanel : null;
    }

    private void ShowFloorClearPanel(int clearedFloorNumber, int destinationFloorNumber)
    {
        IRGameClearPanelView panelView = ResolveGameClearPanel();

        if (panelView == null)
        {
            return;
        }

        RRunModalPresenter.TryShowFloorClear(
            panelView,
            ResolveSessionController(),
            playerController,
            clearedFloorNumber,
            destinationFloorNumber);
    }

    private void ShowFinalClearPanel()
    {
        IRGameClearPanelView panelView = ResolveGameClearPanel();

        if (panelView == null)
        {
            return;
        }

        RRunModalPresenter.TryShowFinalClear(panelView, ResolveSessionController(), playerController);
    }
}
