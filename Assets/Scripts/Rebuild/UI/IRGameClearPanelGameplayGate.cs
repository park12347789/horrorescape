using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class IRGameClearPanelGameplayGate
{
    public static bool Suspend(
        bool inputSuspended,
        WasdPlayerController playerController,
        RPlayerRuntimeReferences playerRuntime,
        ref float cachedTimeScale)
    {
        if (inputSuspended)
        {
            return true;
        }

        cachedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        PlayerInput playerInput = playerRuntime != null ? playerRuntime.PlayerInput : null;
        Rigidbody2D playerBody = playerRuntime != null ? playerRuntime.PlayerBody : null;

        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector2.zero;
        }

        playerInput?.DeactivateInput();

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        return true;
    }

    public static bool Resume(
        bool inputSuspended,
        WasdPlayerController playerController,
        RPlayerRuntimeReferences playerRuntime,
        float cachedTimeScale)
    {
        if (!inputSuspended)
        {
            return false;
        }

        Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;

        if (playerController != null)
        {
            playerController.enabled = true;
        }

        PlayerInput playerInput = playerRuntime != null ? playerRuntime.PlayerInput : null;
        playerInput?.ActivateInput();
        return false;
    }
}

public static class IRGameClearPanelSessionActions
{
    public static RRunSessionController ResolveSessionController(RRunSessionController assignedSessionController)
    {
        return ResolveSessionControllerForScene(assignedSessionController, default);
    }

    public static RRunSessionController ResolveSessionControllerForScene(
        RRunSessionController assignedSessionController,
        Scene scene)
    {
        if (assignedSessionController != null)
        {
            return assignedSessionController;
        }

        return RRunSessionResolver.ResolveForScene(scene);
    }

    public static bool TryRetryCurrentRun(MonoBehaviour context, RRunSessionController assignedSessionController)
    {
        RRunSessionController sessionController = ResolveSessionControllerForScene(
            assignedSessionController,
            context != null ? context.gameObject.scene : default);

        if (sessionController == null)
        {
            Debug.LogError($"{nameof(IRGameClearPanelView)} cannot retry because no RRunSessionController is assigned.", context);
            return false;
        }

        sessionController.RetryCurrentRun();
        return true;
    }

    public static bool TryReturnToLobby(MonoBehaviour context, RRunSessionController assignedSessionController)
    {
        RRunSessionController sessionController = ResolveSessionControllerForScene(
            assignedSessionController,
            context != null ? context.gameObject.scene : default);

        if (sessionController == null)
        {
            Debug.LogError($"{nameof(IRGameClearPanelView)} cannot return to the lobby because no RRunSessionController is assigned.", context);
            return false;
        }

        sessionController.ReturnToLobby();
        return true;
    }
}
