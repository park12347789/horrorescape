using UnityEngine.InputSystem;

public enum IRRunModalAction
{
    None,
    HideAndResume,
    RetryCurrentRun,
    ReturnToLobby
}

public static class IRGameClearPanelInputRouter
{
    public static IRRunModalAction ResolveAction(
        IRRunModalMode mode,
        bool confirmPressed,
        bool retryPressed,
        bool returnToLobbyPressed)
    {
        return mode switch
        {
            IRRunModalMode.FloorClear when confirmPressed => IRRunModalAction.HideAndResume,
            IRRunModalMode.FinalClear when confirmPressed || returnToLobbyPressed => IRRunModalAction.ReturnToLobby,
            IRRunModalMode.Failure when retryPressed => IRRunModalAction.RetryCurrentRun,
            IRRunModalMode.Failure when confirmPressed || returnToLobbyPressed => IRRunModalAction.ReturnToLobby,
            IRRunModalMode.Custom when confirmPressed => IRRunModalAction.HideAndResume,
            _ => IRRunModalAction.None
        };
    }

    public static bool WasConfirmPressed(Keyboard keyboard)
    {
        return keyboard != null
            && (keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
                || keyboard.cKey.wasPressedThisFrame);
    }
}
