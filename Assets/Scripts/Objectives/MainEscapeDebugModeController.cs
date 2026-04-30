using System;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class MainEscapeDebugModeController : MonoBehaviour
{
    private const float PerformanceOverlaySnapshotInterval = 0.25f;
    private const float ReferenceRefreshRetryInterval = 0.5f;

    private static readonly string[] PerformanceOverlayRowLabels =
    {
        "FPS",
        "Frame Avg",
        "Frame Peak",
        "Fog Total",
        "Fog Update",
        "Fog Cleanup",
        "Fog Upload",
        "Fog Touched",
        "Enemy Vision",
        "Enemy Reveal",
        "Fluorescent",
        "Lights"
    };

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private PlayerRuntimeReferencesBase playerRuntime;
    [SerializeField, FormerlySerializedAs("floorDirector"), FormerlySerializedAs("rebuildFloorDirector")]
    private MonoBehaviour debugPresentationControllerSource;
    [SerializeField] private NoiseSystem noiseSystem;
    [SerializeField] private bool syncNoisePulsesWithDebugMode;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField, FormerlySerializedAs("fogOfWarOverlay")] private MonoBehaviour fogVisibilityServiceSource;
    [SerializeField] private MonoBehaviour[] debugModeApplierSources = Array.Empty<MonoBehaviour>();
    [SerializeField] private bool debugModeEnabled;
    [SerializeField] private bool invincibilityOnlyModeEnabled;
    [SerializeField] private bool performanceOverlayEnabled;

    private bool initialized;
    private bool appliedDebugModeEnabled;
    private bool appliedInvincibilityOnlyModeEnabled;
    private bool appliedEffectiveInvincibilityEnabled;
    private bool debugPresentationStateApplied;
    private bool debugModeAppliersDirty = true;
    private float statusMessageUntilTime;
    private string statusMessage = string.Empty;
    private GUIStyle overlayStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private Texture2D overlayTexture;
    private readonly string[] performanceOverlayRowValues = new string[PerformanceOverlayRowLabels.Length];
    private float nextReferenceRefreshTime;
    private float nextPerformanceOverlaySnapshotTime;
    private string performanceOverlayHeader = string.Empty;
    private IMainEscapeDebugPresentationController debugPresentationController;
    private IFogVisibilityService fogVisibilityService;
    private IInvincibilityDebugApplier invincibilityDebugApplier;
    private IFogBypassDebugApplier fogBypassDebugApplier;
    private IDebugPresentationApplier debugPresentationApplier;
    private INoiseDebugPulseApplier noiseDebugPulseApplier;
    private IMainEscapeDebugModeApplier[] debugModeAppliers = Array.Empty<IMainEscapeDebugModeApplier>();

    private static MainEscapeRuntimeSettings RuntimeSettings => MainEscapeRuntimeSettings.Load();
    public bool DebugModeEnabled => debugModeEnabled;
    public bool InvincibilityOnlyModeEnabled => invincibilityOnlyModeEnabled;

    private void Awake()
    {
        CacheReferences();
        MainEscapePerformanceTracker.SetEnabled(performanceOverlayEnabled);
    }

    private void Start()
    {
        CacheReferences();
        MainEscapePerformanceTracker.SetEnabled(performanceOverlayEnabled);
        ApplyDebugMode(force: true);
    }

    private void Update()
    {
        RefreshReferencesFromUpdateIfNeeded();

        if (!Application.isPlaying)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        Key toggleKey = RuntimeSettings.DebugToggleKey;
        Key invincibilityToggleKey = RuntimeSettings.InvincibilityOnlyToggleKey;
        Key performanceToggleKey = RuntimeSettings.PerformanceOverlayToggleKey;

        if (WasPressedThisFrame(keyboard, toggleKey))
        {
            SetDebugModeEnabled(!debugModeEnabled);
        }

        if (WasPressedThisFrame(keyboard, invincibilityToggleKey) && invincibilityToggleKey != toggleKey)
        {
            SetInvincibilityOnlyModeEnabled(!invincibilityOnlyModeEnabled);
        }

        if (WasPressedThisFrame(keyboard, performanceToggleKey)
            && performanceToggleKey != toggleKey
            && performanceToggleKey != invincibilityToggleKey)
        {
            SetPerformanceOverlayEnabled(!performanceOverlayEnabled);
        }

        if (debugModeEnabled != appliedDebugModeEnabled
            || invincibilityOnlyModeEnabled != appliedInvincibilityOnlyModeEnabled)
        {
            ApplyDebugMode(force: true);
        }

        MainEscapePerformanceTracker.TickFrame(Time.unscaledDeltaTime);
    }

    private void OnGUI()
    {
        if (Event.current != null && Event.current.type != EventType.Repaint)
        {
            return;
        }

        bool showStatusOverlay = RuntimeSettings.DebugShowsStatusOverlay
            && (debugModeEnabled || invincibilityOnlyModeEnabled || Time.unscaledTime < statusMessageUntilTime);
        bool showPerformanceOverlay = performanceOverlayEnabled;

        if (!showStatusOverlay && !showPerformanceOverlay)
        {
            return;
        }

        EnsureOverlayStyles();

        if (showStatusOverlay)
        {
            DrawStatusOverlay();
        }

        if (showPerformanceOverlay)
        {
            DrawPerformanceOverlay();
        }
    }

    public void Initialize(
        WasdPlayerController configuredPlayerController,
        PlayerRuntimeReferencesBase configuredPlayerRuntime,
        MonoBehaviour configuredDebugPresentationControllerSource,
        NoiseSystem configuredNoiseSystem,
        FlashlightFogOfWarOverlay configuredFogOfWarOverlay,
        bool startEnabled)
    {
        playerController = configuredPlayerController;
        playerRuntime = configuredPlayerRuntime != null
            ? configuredPlayerRuntime
            : configuredPlayerController != null
                ? configuredPlayerController.GetComponent<PlayerRuntimeReferencesBase>()
                : null;
        debugPresentationControllerSource = configuredDebugPresentationControllerSource;
        noiseSystem = configuredNoiseSystem;
        fogVisibilityServiceSource = configuredFogOfWarOverlay;
        debugModeAppliersDirty = true;

        if (!initialized)
        {
            debugModeEnabled = startEnabled;
            initialized = true;
        }

        CacheReferences();
        ApplyDebugMode(force: true);
    }

    public void BindDebugModeAppliers(params MonoBehaviour[] applierSources)
    {
        debugModeApplierSources = applierSources ?? Array.Empty<MonoBehaviour>();
        debugModeAppliersDirty = true;
        CacheReferences();
        ApplyDebugMode(force: true);
    }

    public void SetDebugModeEnabled(bool enabled)
    {
        debugModeEnabled = enabled;
        ApplyDebugMode(force: true);
    }

    public void SetInvincibilityOnlyModeEnabled(bool enabled)
    {
        invincibilityOnlyModeEnabled = enabled;
        ApplyDebugMode(force: true);
    }

    public void SetPerformanceOverlayEnabled(bool enabled)
    {
        performanceOverlayEnabled = enabled;
        MainEscapePerformanceTracker.SetEnabled(enabled);
        nextPerformanceOverlaySnapshotTime = 0f;
        statusMessage = enabled ? "PERF OVERLAY ON" : "PERF OVERLAY OFF";
        statusMessageUntilTime = Time.unscaledTime + 1.6f;
    }

    private void CacheReferences()
    {
        WasdPlayerController previousPlayerController = playerController;
        PlayerHealth previousPlayerHealth = playerHealth;
        IMainEscapeDebugPresentationController previousDebugPresentationController = debugPresentationController;
        IFogVisibilityService previousFogVisibilityService = fogVisibilityService;
        IInvincibilityDebugApplier previousInvincibilityDebugApplier = invincibilityDebugApplier;
        IFogBypassDebugApplier previousFogBypassDebugApplier = fogBypassDebugApplier;
        IDebugPresentationApplier previousDebugPresentationApplier = debugPresentationApplier;
        INoiseDebugPulseApplier previousNoiseDebugPulseApplier = noiseDebugPulseApplier;
        NoiseSystem previousNoiseSystem = noiseSystem;

        playerRuntime ??= playerController != null ? playerController.GetComponent<PlayerRuntimeReferencesBase>() : null;
        playerHealth ??= playerRuntime != null ? playerRuntime.PlayerHealth : null;
        debugPresentationController = debugPresentationControllerSource as IMainEscapeDebugPresentationController;
        fogVisibilityService = fogVisibilityServiceSource as IFogVisibilityService;
        invincibilityDebugApplier = playerHealth;
        fogBypassDebugApplier = fogVisibilityServiceSource as IFogBypassDebugApplier;
        debugPresentationApplier = debugPresentationControllerSource as IDebugPresentationApplier;
        noiseDebugPulseApplier = noiseSystem;

        if (!ReferenceEquals(previousPlayerController, playerController)
            || !ReferenceEquals(previousPlayerHealth, playerHealth)
            || !ReferenceEquals(previousDebugPresentationController, debugPresentationController)
            || !ReferenceEquals(previousFogVisibilityService, fogVisibilityService)
            || !ReferenceEquals(previousInvincibilityDebugApplier, invincibilityDebugApplier)
            || !ReferenceEquals(previousFogBypassDebugApplier, fogBypassDebugApplier)
            || !ReferenceEquals(previousDebugPresentationApplier, debugPresentationApplier)
            || !ReferenceEquals(previousNoiseDebugPulseApplier, noiseDebugPulseApplier)
            || !ReferenceEquals(previousNoiseSystem, noiseSystem))
        {
            debugModeAppliersDirty = true;
        }

        if (debugModeAppliersDirty)
        {
            RebuildDebugModeAppliers();
        }
    }

    private void ApplyDebugMode(bool force)
    {
        CacheReferences();

        bool effectiveInvincibilityEnabled = ResolveEffectiveInvincibilityEnabled();

        if (!force
            && debugPresentationStateApplied
            && appliedDebugModeEnabled == debugModeEnabled)
        {
            if (appliedInvincibilityOnlyModeEnabled == invincibilityOnlyModeEnabled
                && appliedEffectiveInvincibilityEnabled == effectiveInvincibilityEnabled)
            {
                return;
            }
        }

        bool fullDebugChanged = debugPresentationStateApplied && appliedDebugModeEnabled != debugModeEnabled;
        bool shouldRefreshStatusMessage = false;

        if (fullDebugChanged && debugModeEnabled)
        {
            statusMessage = "DEBUG MODE ON";
            Debug.Log("[MainEscapeDebugModeController] Debug mode enabled.");
            shouldRefreshStatusMessage = true;
        }
        else if (fullDebugChanged)
        {
            statusMessage = "DEBUG MODE OFF";
            Debug.Log("[MainEscapeDebugModeController] Debug mode disabled.");
            shouldRefreshStatusMessage = true;
        }

        ApplyResolvedDebugModeState(BuildDebugModeState(effectiveInvincibilityEnabled));

        if (!debugModeEnabled && appliedInvincibilityOnlyModeEnabled != invincibilityOnlyModeEnabled)
        {
            statusMessage = invincibilityOnlyModeEnabled
                ? "PLAYER INVINCIBLE"
                : "PLAYER INVINCIBILITY OFF";
            shouldRefreshStatusMessage = true;
        }

        appliedDebugModeEnabled = debugModeEnabled;
        appliedInvincibilityOnlyModeEnabled = invincibilityOnlyModeEnabled;
        appliedEffectiveInvincibilityEnabled = effectiveInvincibilityEnabled;
        debugPresentationStateApplied = true;

        if (shouldRefreshStatusMessage)
        {
            statusMessageUntilTime = Time.unscaledTime + 1.6f;
        }
        else if (!debugModeEnabled && !invincibilityOnlyModeEnabled)
        {
            statusMessage = string.Empty;
            statusMessageUntilTime = 0f;
        }
    }

    private bool ResolveEffectiveInvincibilityEnabled()
    {
        return debugModeEnabled
            ? RuntimeSettings.DebugMakesPlayerInvincible
            : invincibilityOnlyModeEnabled;
    }

    private MainEscapeDebugModeState BuildDebugModeState(bool effectiveInvincibilityEnabled)
    {
        bool? noiseDebugPulsesEnabled = syncNoisePulsesWithDebugMode
            ? debugModeEnabled && RuntimeSettings.DebugShowsNoisePulses
            : null;
        return new MainEscapeDebugModeState(
            debugModeEnabled,
            effectiveInvincibilityEnabled,
            debugModeEnabled && RuntimeSettings.DebugDisablesFogOfWar,
            debugModeEnabled && RuntimeSettings.DebugShowsVentMarkers,
            noiseDebugPulsesEnabled);
    }

    private void ApplyResolvedDebugModeState(MainEscapeDebugModeState state)
    {
        for (int index = 0; index < debugModeAppliers.Length; index++)
        {
            debugModeAppliers[index]?.ApplyDebugMode(state);
        }
    }

    private void RebuildDebugModeAppliers()
    {
        IMainEscapeDebugModeApplier[] internalAppliers = MainEscapeDebugModeTargetAppliers.Create(
            playerController,
            invincibilityDebugApplier,
            fogBypassDebugApplier,
            debugPresentationApplier,
            noiseDebugPulseApplier,
            syncNoisePulsesWithDebugMode);
        IMainEscapeDebugModeApplier[] externalAppliers = ResolveDebugModeAppliers(debugModeApplierSources);

        if (internalAppliers.Length == 0)
        {
            debugModeAppliers = externalAppliers;
            debugModeAppliersDirty = false;
            return;
        }

        if (externalAppliers.Length == 0)
        {
            debugModeAppliers = internalAppliers;
            debugModeAppliersDirty = false;
            return;
        }

        IMainEscapeDebugModeApplier[] mergedAppliers = new IMainEscapeDebugModeApplier[internalAppliers.Length + externalAppliers.Length];
        internalAppliers.CopyTo(mergedAppliers, 0);
        externalAppliers.CopyTo(mergedAppliers, internalAppliers.Length);
        debugModeAppliers = mergedAppliers;
        debugModeAppliersDirty = false;
    }

    private static IMainEscapeDebugModeApplier[] ResolveDebugModeAppliers(MonoBehaviour[] applierSources)
    {
        if (applierSources == null || applierSources.Length == 0)
        {
            return Array.Empty<IMainEscapeDebugModeApplier>();
        }

        IMainEscapeDebugModeApplier[] resolvedAppliers = new IMainEscapeDebugModeApplier[applierSources.Length];
        int resolvedCount = 0;

        for (int index = 0; index < applierSources.Length; index++)
        {
            if (!(applierSources[index] is IMainEscapeDebugModeApplier debugModeApplier))
            {
                continue;
            }

            resolvedAppliers[resolvedCount++] = debugModeApplier;
        }

        if (resolvedCount == 0)
        {
            return Array.Empty<IMainEscapeDebugModeApplier>();
        }

        if (resolvedCount == resolvedAppliers.Length)
        {
            return resolvedAppliers;
        }

        IMainEscapeDebugModeApplier[] trimmedAppliers = new IMainEscapeDebugModeApplier[resolvedCount];
        Array.Copy(resolvedAppliers, trimmedAppliers, resolvedCount);
        return trimmedAppliers;
    }

    private void EnsureOverlayStyles()
    {
        overlayTexture ??= CreateTexture(new Color(0.08f, 0.08f, 0.08f, 0.86f));

        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = overlayTexture },
                border = new RectOffset(6, 6, 6, 6)
            };
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.98f, 0.92f, 0.78f, 1f) }
            };
        }

        if (valueStyle == null)
        {
            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.82f, 0.96f, 1f, 1f) }
            };
        }
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static bool WasPressedThisFrame(Keyboard keyboard, Key key)
    {
        return keyboard != null
            && (int)key > 0
            && keyboard[key].wasPressedThisFrame;
    }

    private void DrawStatusOverlay()
    {
        float panelHeight = debugModeEnabled
            ? 104f
            : invincibilityOnlyModeEnabled
                ? 56f
                : 40f;
        Rect panelRect = new(16f, 16f, 230f, panelHeight);
        GUI.Box(panelRect, GUIContent.none, overlayStyle);

        string header = debugModeEnabled
            ? $"DEBUG MODE ON  [{RuntimeSettings.DebugToggleKey}]"
            : invincibilityOnlyModeEnabled
                ? $"INVINCIBLE ON  [{RuntimeSettings.InvincibilityOnlyToggleKey}]"
                : statusMessage;
        GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, 18f), header, labelStyle);

        if (!debugModeEnabled && !invincibilityOnlyModeEnabled)
        {
            return;
        }

        if (debugModeEnabled)
        {
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 32f, panelRect.width - 24f, 16f), "Invincible", labelStyle);
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 48f, panelRect.width - 24f, 16f), "Fog Bypass", labelStyle);
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 64f, panelRect.width - 24f, 16f), "Noise Pulses", labelStyle);
            GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 80f, panelRect.width - 24f, 16f), "Vent Markers", labelStyle);
            return;
        }

        GUI.Label(new Rect(panelRect.x + 12f, panelRect.y + 32f, panelRect.width - 24f, 16f), "Invincible Only", labelStyle);
    }

    private void DrawPerformanceOverlay()
    {
        const float panelWidth = 336f;
        const float lineHeight = 18f;
        Rect panelRect = new(Screen.width - panelWidth - 16f, 16f, panelWidth, 258f);
        GUI.Box(panelRect, GUIContent.none, overlayStyle);
        RefreshPerformanceOverlaySnapshot();

        GUI.Label(
            new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, 18f),
            performanceOverlayHeader,
            labelStyle);

        for (int index = 0; index < PerformanceOverlayRowLabels.Length; index++)
        {
            DrawPerformanceRow(panelRect, index, PerformanceOverlayRowLabels[index], performanceOverlayRowValues[index]);
        }

        void DrawPerformanceRow(Rect rect, int rowIndex, string label, string value)
        {
            float y = rect.y + 34f + (rowIndex * lineHeight);
            GUI.Label(new Rect(rect.x + 12f, y, 132f, 16f), label, labelStyle);
            GUI.Label(new Rect(rect.x + 148f, y, rect.width - 160f, 16f), value, valueStyle);
        }
    }

    private void RefreshReferencesFromUpdateIfNeeded()
    {
        if (!ShouldRefreshReferencesFromUpdate())
        {
            return;
        }

        if (Time.unscaledTime < nextReferenceRefreshTime)
        {
            return;
        }

        nextReferenceRefreshTime = Time.unscaledTime + ReferenceRefreshRetryInterval;
        CacheReferences();
    }

    private bool ShouldRefreshReferencesFromUpdate()
    {
        if (debugModeAppliersDirty)
        {
            return true;
        }

        return (playerController != null && playerRuntime == null)
            || (playerRuntime != null && playerHealth == null)
            || (debugPresentationControllerSource != null
                && (debugPresentationController == null || debugPresentationApplier == null))
            || (fogVisibilityServiceSource != null
                && (fogVisibilityService == null || fogBypassDebugApplier == null))
            || (noiseSystem != null && noiseDebugPulseApplier == null);
    }

    private void RefreshPerformanceOverlaySnapshot()
    {
        if (Time.unscaledTime < nextPerformanceOverlaySnapshotTime
            && !string.IsNullOrEmpty(performanceOverlayHeader))
        {
            return;
        }

        performanceOverlayHeader = $"PERF OVERLAY  [{RuntimeSettings.PerformanceOverlayToggleKey}]";
        performanceOverlayRowValues[0] = MainEscapePerformanceTracker.GetFramesPerSecondDisplay();
        performanceOverlayRowValues[1] = MainEscapePerformanceTracker.GetAverageFrameMsDisplay();
        performanceOverlayRowValues[2] = MainEscapePerformanceTracker.GetPeakFrameMsDisplay();
        performanceOverlayRowValues[3] = MainEscapePerformanceTracker.GetSampleDisplay(MainEscapePerformanceSampleId.FogOverlay);
        performanceOverlayRowValues[4] = MainEscapePerformanceTracker.GetSampleDisplay(MainEscapePerformanceSampleId.FogUpdatePixels);
        performanceOverlayRowValues[5] = MainEscapePerformanceTracker.GetSampleDisplay(MainEscapePerformanceSampleId.FogCleanup);
        performanceOverlayRowValues[6] = MainEscapePerformanceTracker.GetSampleDisplay(MainEscapePerformanceSampleId.FogUpload);
        performanceOverlayRowValues[7] = MainEscapePerformanceTracker.GetFogTouchedDisplay();
        performanceOverlayRowValues[8] = MainEscapePerformanceTracker.GetSampleDisplay(MainEscapePerformanceSampleId.EnemyVisionMesh);
        performanceOverlayRowValues[9] = MainEscapePerformanceTracker.GetSampleDisplay(MainEscapePerformanceSampleId.FogReactiveEnemyVisibility);
        performanceOverlayRowValues[10] = MainEscapePerformanceTracker.GetSampleDisplay(MainEscapePerformanceSampleId.FluorescentFlicker);
        performanceOverlayRowValues[11] = $"{AuthoredVisibilityLight2D.ActiveLights.Count} authored / {RuntimePointLight2DCache.CachedCount} point";
        nextPerformanceOverlaySnapshotTime = Time.unscaledTime + PerformanceOverlaySnapshotInterval;
    }
}

internal enum MainEscapePerformanceSampleId
{
    FogOverlay = 0,
    EnemyVisionMesh = 1,
    FogReactiveEnemyVisibility = 2,
    FluorescentFlicker = 3,
    FogUpdatePixels = 4,
    FogCleanup = 5,
    FogUpload = 6
}

internal static class MainEscapePerformanceTracker
{
    private const int SampleCount = 7;

    private static readonly double[] WindowSampleTotalMs = new double[SampleCount];
    private static readonly double[] WindowSamplePeakMs = new double[SampleCount];
    private static readonly int[] WindowSampleCalls = new int[SampleCount];
    private static readonly double[] DisplaySampleMsPerFrame = new double[SampleCount];
    private static readonly double[] DisplaySamplePeakMs = new double[SampleCount];
    private static readonly double[] DisplaySampleCallsPerFrame = new double[SampleCount];

    private static bool enabled;
    private static double windowStartTime = -1d;
    private static int windowFrameCount;
    private static double windowFrameTotalMs;
    private static double windowFramePeakMs;
    private static double displayAverageFrameMs;
    private static double displayPeakFrameMs;
    private static double displayFramesPerSecond;
    private static string displayFogTouched = "--";

    public static void SetEnabled(bool value)
    {
        enabled = value;
        ResetWindow(Time.realtimeSinceStartupAsDouble);
    }

    public static long BeginSample(MainEscapePerformanceSampleId sampleId)
    {
        return enabled ? System.Diagnostics.Stopwatch.GetTimestamp() : 0L;
    }

    public static void EndSample(MainEscapePerformanceSampleId sampleId, long startTimestamp)
    {
        if (!enabled || startTimestamp == 0L)
        {
            return;
        }

        long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
        double elapsedMs = elapsedTicks * 1000d / System.Diagnostics.Stopwatch.Frequency;
        int index = (int)sampleId;

        WindowSampleTotalMs[index] += elapsedMs;
        WindowSamplePeakMs[index] = System.Math.Max(WindowSamplePeakMs[index], elapsedMs);
        WindowSampleCalls[index]++;
    }

    public static void TickFrame(float deltaTime)
    {
        if (!enabled)
        {
            return;
        }

        double now = Time.realtimeSinceStartupAsDouble;

        if (windowStartTime < 0d)
        {
            windowStartTime = now;
        }

        double frameMs = System.Math.Max(0.0001d, deltaTime * 1000d);
        windowFrameCount++;
        windowFrameTotalMs += frameMs;
        windowFramePeakMs = System.Math.Max(windowFramePeakMs, frameMs);

        if ((now - windowStartTime) >= 1d)
        {
            CommitWindow(now);
        }
    }

    public static string GetFramesPerSecondDisplay()
    {
        return displayFramesPerSecond <= 0d ? "--" : $"{displayFramesPerSecond:0.0}";
    }

    public static string GetAverageFrameMsDisplay()
    {
        return displayAverageFrameMs <= 0d ? "--" : $"{displayAverageFrameMs:0.00} ms";
    }

    public static string GetPeakFrameMsDisplay()
    {
        return displayPeakFrameMs <= 0d ? "--" : $"{displayPeakFrameMs:0.00} ms";
    }

    public static string GetSampleDisplay(MainEscapePerformanceSampleId sampleId)
    {
        int index = (int)sampleId;
        double averageMsPerFrame = DisplaySampleMsPerFrame[index];
        double peakMs = DisplaySamplePeakMs[index];
        double callsPerFrame = DisplaySampleCallsPerFrame[index];

        if (averageMsPerFrame <= 0d && peakMs <= 0d)
        {
            return "--";
        }

        return $"{averageMsPerFrame:0.00} ms/f  peak {peakMs:0.00}  x{callsPerFrame:0.0}";
    }

    public static void ReportFogTouchedCounts(int touchedPixels, int touchedBlocks)
    {
        displayFogTouched = $"{Mathf.Max(0, touchedPixels)} px  /  {Mathf.Max(0, touchedBlocks)} blk";
    }

    public static string GetFogTouchedDisplay()
    {
        return displayFogTouched;
    }

    private static void CommitWindow(double now)
    {
        int safeFrameCount = System.Math.Max(1, windowFrameCount);
        displayAverageFrameMs = windowFrameTotalMs / safeFrameCount;
        displayPeakFrameMs = windowFramePeakMs;
        displayFramesPerSecond = 1000d / System.Math.Max(0.0001d, displayAverageFrameMs);

        for (int index = 0; index < SampleCount; index++)
        {
            DisplaySampleMsPerFrame[index] = WindowSampleTotalMs[index] / safeFrameCount;
            DisplaySamplePeakMs[index] = WindowSamplePeakMs[index];
            DisplaySampleCallsPerFrame[index] = WindowSampleCalls[index] / (double)safeFrameCount;
        }

        ResetWindow(now);
    }

    private static void ResetWindow(double now)
    {
        windowStartTime = enabled ? now : -1d;
        windowFrameCount = 0;
        windowFrameTotalMs = 0d;
        windowFramePeakMs = 0d;

        for (int index = 0; index < SampleCount; index++)
        {
            WindowSampleTotalMs[index] = 0d;
            WindowSamplePeakMs[index] = 0d;
            WindowSampleCalls[index] = 0;
        }
    }
}
