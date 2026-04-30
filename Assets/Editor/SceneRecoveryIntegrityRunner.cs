using UnityEditor;

[InitializeOnLoad]
public static class SceneRecoveryIntegrityRunner
{
    private const string MenuPath = "Tools/Main Escape/Run Integrity + Recovery Prep";
    private const string EnableAutoRunMenuPath = "Tools/Main Escape/Recovery Prep Auto Run/Enable";
    private const string DisableAutoRunMenuPath = "Tools/Main Escape/Recovery Prep Auto Run/Disable";
    private const string AutoRunEnabledKey = "HorrorStealth.SceneRecoveryIntegrityRunner.AutoRunEnabled";
    private const string ExecutedThisSessionKey = "HorrorStealth.SceneRecoveryIntegrityRunner.ExecutedThisSession";
    private const string PhaseKey = "HorrorStealth.SceneRecoveryIntegrityRunner.Phase";
    private const string NextActionTimeKey = "HorrorStealth.SceneRecoveryIntegrityRunner.NextActionTime";

    static SceneRecoveryIntegrityRunner()
    {
        if (IsAutomaticRunEnabled())
        {
            EditorApplication.delayCall += TryScheduleAutomaticRun;
        }
    }

    [MenuItem(MenuPath)]
    private static void RunFromMenu()
    {
        BeginRun(force: true);
    }

    [MenuItem(EnableAutoRunMenuPath)]
    private static void EnableAutoRun()
    {
        EditorPrefs.SetBool(AutoRunEnabledKey, true);
        UnityEngine.Debug.Log("[SceneRecoveryIntegrityRunner] Enabled automatic integrity prep for future Unity sessions.");
        TryScheduleAutomaticRun();
    }

    [MenuItem(EnableAutoRunMenuPath, true)]
    private static bool ValidateEnableAutoRun()
    {
        return !IsAutomaticRunEnabled();
    }

    [MenuItem(DisableAutoRunMenuPath)]
    private static void DisableAutoRun()
    {
        EditorPrefs.SetBool(AutoRunEnabledKey, false);
        ClearQueuedRunState();
        UnityEngine.Debug.Log("[SceneRecoveryIntegrityRunner] Disabled automatic integrity prep. Use the manual menu item when validation is needed.");
    }

    [MenuItem(DisableAutoRunMenuPath, true)]
    private static bool ValidateDisableAutoRun()
    {
        return IsAutomaticRunEnabled();
    }

    private static void TryScheduleAutomaticRun()
    {
        if (!IsAutomaticRunEnabled())
        {
            return;
        }

        if (SessionState.GetBool(ExecutedThisSessionKey, false))
        {
            return;
        }

        BeginRun(force: false);
    }

    private static void BeginRun(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        if (!force && !IsAutomaticRunEnabled())
        {
            return;
        }

        if (!force && SessionState.GetBool(ExecutedThisSessionKey, false))
        {
            return;
        }

        SessionState.SetBool(ExecutedThisSessionKey, true);
        SessionState.SetInt(PhaseKey, 0);
        SessionState.SetFloat(NextActionTimeKey, 0f);
        EditorApplication.update -= HandleUpdate;
        EditorApplication.update += HandleUpdate;
        UnityEngine.Debug.Log("[SceneRecoveryIntegrityRunner] Queued integrity prep for the current Unity session.");
    }

    private static void HandleUpdate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        int phase = SessionState.GetInt(PhaseKey, 0);
        float nextActionTime = SessionState.GetFloat(NextActionTimeKey, 0f);

        if (EditorApplication.timeSinceStartup < nextActionTime)
        {
            return;
        }

        switch (phase)
        {
            case 0:
                UnityEngine.Debug.Log("[SceneRecoveryIntegrityRunner] Caching open authored floor references and running lobby preflight.");
                EditorApplication.ExecuteMenuItem("Tools/Main Escape Rebuild/Cache Authored Floor References");
                EditorApplication.ExecuteMenuItem("Tools/Main Escape/Validate Lobby Scene Preflight");
                SessionState.SetInt(PhaseKey, 1);
                SessionState.SetFloat(NextActionTimeKey, (float)EditorApplication.timeSinceStartup + 2f);
                break;

            case 1:
                UnityEngine.Debug.Log("[SceneRecoveryIntegrityRunner] Starting start-floor runtime validation.");
                EditorApplication.ExecuteMenuItem("Tools/Main Escape/Validate Start Floor Runtime");
                SessionState.SetInt(PhaseKey, 2);
                SessionState.SetFloat(NextActionTimeKey, 0f);
                EditorApplication.update -= HandleUpdate;
                break;
        }
    }

    private static bool IsAutomaticRunEnabled()
    {
        return EditorPrefs.GetBool(AutoRunEnabledKey, false);
    }

    private static void ClearQueuedRunState()
    {
        EditorApplication.update -= HandleUpdate;
        SessionState.EraseBool(ExecutedThisSessionKey);
        SessionState.EraseInt(PhaseKey);
        SessionState.EraseFloat(NextActionTimeKey);
    }
}
