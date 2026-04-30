using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class RShadowStartleDirector : MonoBehaviour
{
    private const string RuntimeCueRootName = "RuntimeShadowStartles";
    private const string FootstepCueName = "ShadowStartleCue_FootstepBehindPlayer";
    private const int DebugPreviewVisibleSortingOrder = 120;

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private PrototypePlayerAudio playerAudio;
    [SerializeField] private FlashlightFogOfWarOverlay fogOfWarOverlay;
    [SerializeField] private MainEscapeFloorAuthoring floorAuthoring;
    [SerializeField] private Transform presentationRoot;
    [SerializeField] private MainEscapeShadowStartleMarker[] markers = System.Array.Empty<MainEscapeShadowStartleMarker>();
    [Header("Footstep Random Startle")]
    [SerializeField] private bool enableFootstepBehindPlayerStartles = true;
    [SerializeField, Range(0f, 0.02f)] private float footstepBehindPlayerStartleChance = 0.001f;
    [SerializeField, Min(0.5f)] private float footstepBehindPlayerSpawnDistance = 1.35f;
    [SerializeField, Min(0f)] private float footstepBehindPlayerLateralJitter = 0.18f;
    [SerializeField] private Vector3 footstepBehindPlayerVisualScale = new(0.6f, 0.6f, 1f);
    [SerializeField, Min(0f)] private float footstepBehindPlayerCooldown = 0f;
    [SerializeField, Min(0.01f)] private float footstepBehindPlayerFadeInDuration = 0.05f;
    [SerializeField, Min(0.01f)] private float footstepBehindPlayerHoldDuration = 0.32f;
    [SerializeField, Min(0.01f)] private float footstepBehindPlayerFadeOutDuration = 0.12f;
    [SerializeField, Range(0.05f, 1f)] private float footstepBehindPlayerTargetAlpha = 0.68f;
    [SerializeField] private string footstepBehindPlayerSortingLayerName = "Default";
    [SerializeField, Range(8, 128)] private int footstepBehindPlayerSortingOrder = 30;
    [SerializeField] private AudioClip footstepBehindPlayerRevealClip;
    [SerializeField, Range(0f, 1f)] private float footstepBehindPlayerRevealClipVolume = 0.14f;
    [Header("Debug Preview")]
    [SerializeField] private bool enableDebugPreviewHotkey;
    [SerializeField] private KeyCode debugPreviewHotkey = KeyCode.F8;

    private Transform runtimeCueRoot;
    private RShadowStartleCue activeCue;
    private PrototypePlayerAudio subscribedPlayerAudio;
    private float nextFootstepBehindPlayerStartleTime;

    public void Initialize(
        WasdPlayerController configuredPlayerController,
        FlashlightFogOfWarOverlay configuredFogOfWarOverlay,
        MainEscapeFloorAuthoring configuredFloorAuthoring,
        Transform configuredPresentationRoot)
    {
        playerController = configuredPlayerController;
        playerAudio = playerController != null ? playerController.GetComponent<PrototypePlayerAudio>() : null;
        fogOfWarOverlay = configuredFogOfWarOverlay;
        floorAuthoring = configuredFloorAuthoring;
        presentationRoot = configuredPresentationRoot != null ? configuredPresentationRoot : transform;
        RefreshMarkers();
        ResetResidencyState();
        RefreshFootstepSubscription();
    }

    private void OnEnable()
    {
        RefreshFootstepSubscription();

        if (enableDebugPreviewHotkey && debugPreviewHotkey != KeyCode.None)
        {
            Debug.Log($"{nameof(RShadowStartleDirector)} debug preview armed on {debugPreviewHotkey}; waiting for input.", this);
        }
    }

    private void OnDisable()
    {
        UnsubscribeFootstepEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFootstepEvents();
    }

    private void Update()
    {
        if (playerController == null)
        {
            return;
        }

        if (activeCue == null && markers != null && markers.Length > 0)
        {
            TryTriggerNearestMarker();
        }

        TryHandleDebugPreviewHotkey();
    }

    public void RefreshMarkers()
    {
        if (floorAuthoring != null)
        {
            markers = floorAuthoring.GetShadowStartleMarkers();
        }

        if (markers == null || markers.Length == 0)
        {
            markers = RSceneReferenceLookup.FindComponentsInScene<MainEscapeShadowStartleMarker>(gameObject.scene);
        }
    }

    public void ResetResidencyState()
    {
        if (markers == null)
        {
            return;
        }

        for (int index = 0; index < markers.Length; index++)
        {
            markers[index]?.ResetSceneResidencyState();
        }
    }

    private void TryTriggerNearestMarker()
    {
        if (activeCue != null)
        {
            return;
        }

        MainEscapeShadowStartleMarker marker = ResolveNearestTriggerableMarker();

        if (marker == null)
        {
            return;
        }

        if (!SpawnCue(marker))
        {
            Debug.LogWarning(
                $"{nameof(RShadowStartleDirector)} could not spawn a marker-driven shadow startle cue for '{marker.name}'.",
                this);
            return;
        }

        marker.ConsumeTrigger();
    }

    private MainEscapeShadowStartleMarker ResolveNearestTriggerableMarker()
    {
        if (markers == null || playerController == null)
        {
            return null;
        }

        MainEscapeShadowStartleMarker bestMarker = null;
        float bestDistance = float.MaxValue;
        Vector3 playerPosition = playerController.transform.position;

        for (int index = 0; index < markers.Length; index++)
        {
            MainEscapeShadowStartleMarker marker = markers[index];

            if (marker == null || !marker.CanTrigger(playerController, fogOfWarOverlay))
            {
                continue;
            }

            float distance = (marker.transform.position - playerPosition).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestMarker = marker;
            }
        }

        return bestMarker;
    }

    private bool SpawnCue(MainEscapeShadowStartleMarker marker)
    {
        if (marker == null)
        {
            return false;
        }

        Transform cueRoot = EnsureRuntimeCueRoot();

        if (cueRoot == null)
        {
            Debug.LogWarning(
                $"{nameof(RShadowStartleDirector)} could not resolve the runtime cue root for a shadow startle cue.",
                this);
            return false;
        }

        GameObject cueObject = new($"ShadowStartleCue_{marker.name}");
        cueObject.transform.SetParent(cueRoot, false);
        activeCue = cueObject.AddComponent<RShadowStartleCue>();
        activeCue.Configure(marker, playerController);
        return ResolveCueSpawnState(cueObject);
    }

    private bool SpawnCue(ShadowStartleCueRequest request, string cueName)
    {
        Transform cueRoot = EnsureRuntimeCueRoot();

        if (cueRoot == null)
        {
            Debug.LogWarning(
                $"{nameof(RShadowStartleDirector)} could not resolve the runtime cue root for a shadow startle cue.",
                this);
            return false;
        }

        GameObject cueObject = new(string.IsNullOrWhiteSpace(cueName) ? "ShadowStartleCue_Runtime" : cueName);
        cueObject.transform.SetParent(cueRoot, false);
        activeCue = cueObject.AddComponent<RShadowStartleCue>();
        activeCue.Configure(request);
        return ResolveCueSpawnState(cueObject);
    }

    private void HandlePlayerFootstep(bool sprinting, float strength)
    {
        if (!enableFootstepBehindPlayerStartles || !isActiveAndEnabled || playerController == null || sprinting)
        {
            return;
        }

        if (activeCue != null || Time.time < nextFootstepBehindPlayerStartleTime)
        {
            return;
        }

        if (Random.value > footstepBehindPlayerStartleChance)
        {
            return;
        }

        if (!TrySpawnBehindPlayerCue(
                footstepBehindPlayerSortingOrder,
                "footstep-driven shadow startle",
                enableVerboseLogging: false))
        {
            return;
        }

        nextFootstepBehindPlayerStartleTime = Time.time + Mathf.Max(0f, footstepBehindPlayerCooldown);
    }

    private bool TrySpawnBehindPlayerCue(int sortingOrder, string spawnContext, bool enableVerboseLogging)
    {
        if (playerController == null)
        {
            if (enableVerboseLogging)
            {
                Debug.LogWarning(
                    $"{nameof(RShadowStartleDirector)} could not spawn the {spawnContext} because no player controller is assigned.",
                    this);
            }

            return false;
        }

        Vector2 forward = ResolvePlayerForward();

        if (forward.sqrMagnitude <= 0.0001f)
        {
            if (enableVerboseLogging)
            {
                Debug.LogWarning(
                    $"{nameof(RShadowStartleDirector)} could not spawn the {spawnContext} because the player forward vector could not be resolved.",
                    this);
            }

            return false;
        }

        Vector2 behind = -forward.normalized;
        Vector2 lateral = new(-behind.y, behind.x);
        float jitter = footstepBehindPlayerLateralJitter > 0f
            ? Random.Range(-footstepBehindPlayerLateralJitter, footstepBehindPlayerLateralJitter)
            : 0f;
        Vector3 worldPosition = playerController.transform.position
            + (Vector3)(behind * Mathf.Max(0.5f, footstepBehindPlayerSpawnDistance))
            + (Vector3)(lateral * jitter);

        if (enableVerboseLogging)
        {
            Debug.Log(
                $"{nameof(RShadowStartleDirector)} attempting {spawnContext} spawn at {worldPosition}.",
                this);
        }

        if (!SpawnCue(new ShadowStartleCueRequest
        {
            WorldPosition = worldPosition,
            VisualScale = footstepBehindPlayerVisualScale,
            FacingDirection = forward,
            DriftDirection = Vector2.zero,
            UseWalkAnimation = false,
            MovementDistance = 0f,
            FadeInDuration = footstepBehindPlayerFadeInDuration,
            HoldDuration = footstepBehindPlayerHoldDuration,
            FadeOutDuration = footstepBehindPlayerFadeOutDuration,
            TargetAlpha = footstepBehindPlayerTargetAlpha,
            SortingLayerName = footstepBehindPlayerSortingLayerName,
            SortingOrder = sortingOrder,
            RevealClip = footstepBehindPlayerRevealClip,
            RevealClipVolume = footstepBehindPlayerRevealClipVolume
        }, FootstepCueName))
        {
            if (enableVerboseLogging)
            {
                Debug.LogWarning(
                    $"{nameof(RShadowStartleDirector)} {spawnContext} spawn was attempted, but the cue was hidden or misconfigured. Check the cue warnings for the specific failure.",
                    this);
            }

            return false;
        }

        if (enableVerboseLogging)
        {
            Debug.Log(
                $"{nameof(RShadowStartleDirector)} {spawnContext} spawned successfully above the fog overlay at sorting order {sortingOrder}.",
                this);
        }

        return true;
    }

    private void TryHandleDebugPreviewHotkey()
    {
        if (!enableDebugPreviewHotkey || debugPreviewHotkey == KeyCode.None || activeCue != null)
        {
            return;
        }

        bool hotkeyPressed = false;
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && TryResolveInputSystemKey(debugPreviewHotkey, out Key resolvedKey))
        {
            hotkeyPressed = keyboard[resolvedKey].wasPressedThisFrame;
        }

        if (!hotkeyPressed)
        {
            hotkeyPressed = TryGetLegacyHotkeyDown(debugPreviewHotkey);
        }

        if (hotkeyPressed)
        {
            Debug.Log(
                $"{nameof(RShadowStartleDirector)} debug preview input received on {debugPreviewHotkey}; attempting spawn.",
                this);
            _ = TrySpawnBehindPlayerCue(
                DebugPreviewVisibleSortingOrder,
                "F8 debug preview",
                enableVerboseLogging: true);
        }
    }

    private Vector2 ResolvePlayerForward()
    {
        if (playerController == null)
        {
            return Vector2.zero;
        }

        Vector2 aimDirection = playerController.AimDirection;

        if (aimDirection.sqrMagnitude > 0.0001f)
        {
            return aimDirection.normalized;
        }

        Vector2 velocity = playerController.Velocity;
        return velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.up;
    }

    private static bool TryResolveInputSystemKey(KeyCode hotkey, out Key resolvedKey)
    {
        resolvedKey = hotkey switch
        {
            KeyCode.Space => Key.Space,
            KeyCode.Tab => Key.Tab,
            KeyCode.Return => Key.Enter,
            KeyCode.KeypadEnter => Key.NumpadEnter,
            KeyCode.Escape => Key.Escape,
            KeyCode.Alpha0 => Key.Digit0,
            KeyCode.Alpha1 => Key.Digit1,
            KeyCode.Alpha2 => Key.Digit2,
            KeyCode.Alpha3 => Key.Digit3,
            KeyCode.Alpha4 => Key.Digit4,
            KeyCode.Alpha5 => Key.Digit5,
            KeyCode.Alpha6 => Key.Digit6,
            KeyCode.Alpha7 => Key.Digit7,
            KeyCode.Alpha8 => Key.Digit8,
            KeyCode.Alpha9 => Key.Digit9,
            KeyCode.A => Key.A,
            KeyCode.B => Key.B,
            KeyCode.C => Key.C,
            KeyCode.D => Key.D,
            KeyCode.E => Key.E,
            KeyCode.F => Key.F,
            KeyCode.G => Key.G,
            KeyCode.H => Key.H,
            KeyCode.I => Key.I,
            KeyCode.J => Key.J,
            KeyCode.K => Key.K,
            KeyCode.L => Key.L,
            KeyCode.M => Key.M,
            KeyCode.N => Key.N,
            KeyCode.O => Key.O,
            KeyCode.P => Key.P,
            KeyCode.Q => Key.Q,
            KeyCode.R => Key.R,
            KeyCode.S => Key.S,
            KeyCode.T => Key.T,
            KeyCode.U => Key.U,
            KeyCode.V => Key.V,
            KeyCode.W => Key.W,
            KeyCode.X => Key.X,
            KeyCode.Y => Key.Y,
            KeyCode.Z => Key.Z,
            KeyCode.F1 => Key.F1,
            KeyCode.F2 => Key.F2,
            KeyCode.F3 => Key.F3,
            KeyCode.F4 => Key.F4,
            KeyCode.F5 => Key.F5,
            KeyCode.F6 => Key.F6,
            KeyCode.F7 => Key.F7,
            KeyCode.F8 => Key.F8,
            KeyCode.F9 => Key.F9,
            KeyCode.F10 => Key.F10,
            KeyCode.F11 => Key.F11,
            KeyCode.F12 => Key.F12,
            _ => Key.None
        };

        return resolvedKey != Key.None;
    }

    private static bool TryGetLegacyHotkeyDown(KeyCode hotkey)
    {
        try
        {
            return Input.GetKeyDown(hotkey);
        }
        catch (System.InvalidOperationException)
        {
            return false;
        }
    }

    private void RefreshFootstepSubscription()
    {
        PrototypePlayerAudio resolvedPlayerAudio = playerAudio != null
            ? playerAudio
            : playerController != null
                ? playerController.GetComponent<PrototypePlayerAudio>()
                : null;

        if (resolvedPlayerAudio == subscribedPlayerAudio)
        {
            return;
        }

        UnsubscribeFootstepEvents();
        subscribedPlayerAudio = resolvedPlayerAudio;

        if (subscribedPlayerAudio != null)
        {
            subscribedPlayerAudio.FootstepPlayed += HandlePlayerFootstep;
        }
    }

    private void UnsubscribeFootstepEvents()
    {
        if (subscribedPlayerAudio == null)
        {
            return;
        }

        subscribedPlayerAudio.FootstepPlayed -= HandlePlayerFootstep;
        subscribedPlayerAudio = null;
    }

    private Transform EnsureRuntimeCueRoot()
    {
        if (runtimeCueRoot != null && runtimeCueRoot.parent == presentationRoot)
        {
            return runtimeCueRoot;
        }

        runtimeCueRoot = null;
        Transform root = presentationRoot != null ? presentationRoot : transform;

        if (root == null)
        {
            return null;
        }

        runtimeCueRoot = root.Find(RuntimeCueRootName);

        if (runtimeCueRoot != null)
        {
            return runtimeCueRoot;
        }

        GameObject cueRootObject = new(RuntimeCueRootName);
        cueRootObject.transform.SetParent(root, false);
        cueRootObject.transform.localPosition = Vector3.zero;
        cueRootObject.transform.localRotation = Quaternion.identity;
        cueRootObject.transform.localScale = Vector3.one;
        runtimeCueRoot = cueRootObject.transform;
        return runtimeCueRoot;
    }

    private bool ResolveCueSpawnState(GameObject cueObject)
    {
        if (activeCue == null)
        {
            if (cueObject != null)
            {
                Destroy(cueObject);
            }

            return false;
        }

        if (activeCue.HasRenderablePresentation)
        {
            return true;
        }

        if (cueObject != null)
        {
            Destroy(cueObject);
        }

        activeCue = null;
        return false;
    }
}
