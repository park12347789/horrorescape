using System.Collections;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RElevatorTransitionController : MonoBehaviour
{
    private const float SceneLoadReadyProgress = 0.9f;

    [Header("Authored Presentation")]
    [SerializeField] private RectTransform presentationRoot;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private Image flickerOverlay;
    [FormerlySerializedAs("fallbackScenePath")]
    [SerializeField, Tooltip("Legacy direct-play compatibility fallback. Used only when there is no pending elevator request and no resolvable run session.")]
    private string directPlayFallbackScenePath;

    [Header("Timing")]
    [SerializeField, Min(0.5f)] private float minimumRideSeconds = 2.35f;
    [SerializeField, Min(0.05f)] private float fadeInSeconds = 0.22f;
    [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.34f;

    [Header("Motion")]
    [SerializeField, Min(0f)] private float shakePixels = 2.6f;
    [SerializeField, Min(0.1f)] private float shakeFrequency = 34f;

    [Header("Temporary Light Overlay")]
    [SerializeField, Range(0f, 1f)] private float flickerBaseAlpha = 0.28f;
    [SerializeField, Range(0f, 1f)] private float flickerPulseAlpha = 0.1f;
    [SerializeField, Min(0.1f)] private float flickerFrequency = 8f;
    [SerializeField, Range(0f, 1f)] private float arrivalFlashAlpha = 0.44f;

    private Vector2 initialPresentationPosition;
    private Color initialFlickerOverlayColor = Color.white;
    private PrototypeAudioManager audioManager;

    private void OnDestroy()
    {
        ResolveAudioManager()?.StopElevatorRideNoise();
        RElevatorTransitionRequestStore.Clear();
    }

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        CacheInitialPresentationState();

        RElevatorTransitionRequest request = ResolveTransitionRequest();

        if (!request.IsValid)
        {
            Debug.LogError($"{nameof(RElevatorTransitionController)} could not resolve a target floor scene.", this);
            yield break;
        }

        audioManager = PrototypeAudioManager.EnsureExists();
        audioManager.PlayElevatorDing();
        audioManager.PlayElevatorRideNoise();

        AsyncOperation loadOperation = SceneLoadUtility.LoadSceneAsyncByPathOrName(
            request.TargetScenePath,
            nameof(RElevatorTransitionController),
            $"{nameof(RElevatorTransitionController)} received an empty target scene path.");

        if (loadOperation == null)
        {
            audioManager.StopElevatorRideNoise();
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        ResolveSessionController()?.PrepareHiddenElevatorTransitionWindow();

        float elapsed = 0f;

        while (elapsed < minimumRideSeconds || loadOperation.progress < SceneLoadReadyProgress)
        {
            UpdatePresentation(elapsed);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (request.DestinationFloorNumber > 0)
        {
            audioManager.PlayElevatorArrival(request.DestinationFloorNumber);
            yield return new WaitForSecondsRealtime(PrototypeAudioManager.ElevatorArrivalDoorOpenDelaySeconds);
        }

        audioManager.PlayDoorOpen();
        yield return FadeOut();

        audioManager.StopElevatorRideNoise();
        ResetPresentation();
        loadOperation.allowSceneActivation = true;
    }

    private PrototypeAudioManager ResolveAudioManager()
    {
        if (audioManager != null)
        {
            return audioManager;
        }

        _ = PrototypeAudioManager.TryGetCachedInstance(out audioManager);
        return audioManager;
    }

    private void CacheInitialPresentationState()
    {
        initialPresentationPosition = presentationRoot != null ? presentationRoot.anchoredPosition : Vector2.zero;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
        }

        if (flickerOverlay != null)
        {
            initialFlickerOverlayColor = flickerOverlay.color;
            SetFlickerOverlayAlpha(flickerBaseAlpha);
        }
    }

    private RElevatorTransitionRequest ResolveTransitionRequest()
    {
        if (RElevatorTransitionRequestStore.TryConsume(out RElevatorTransitionRequest request))
        {
            return request;
        }

        RRunSessionController sessionController = ResolveSessionController();
        string targetScenePath = ResolveFallbackTargetScenePath(sessionController);

        return new RElevatorTransitionRequest(
            targetScenePath,
            0,
            sessionController != null ? sessionController.Snapshot.CurrentFloorNumber : 0,
            "Fallback");
    }

    private string ResolveFallbackTargetScenePath(RRunSessionController sessionController)
    {
        if (sessionController != null)
        {
            return sessionController.GetCurrentFloorScenePath();
        }

        return directPlayFallbackScenePath?.Trim() ?? string.Empty;
    }

    private RRunSessionController ResolveSessionController()
    {
        return RRunSessionResolver.ResolveForContext(this);
    }

    private void UpdatePresentation(float elapsed)
    {
        if (presentationRoot != null && shakePixels > 0f)
        {
            float x = Mathf.Sin(elapsed * shakeFrequency) * shakePixels;
            float y = Mathf.Sin((elapsed * shakeFrequency * 1.37f) + 0.41f) * shakePixels * 0.45f;
            presentationRoot.anchoredPosition = initialPresentationPosition + new Vector2(x, y);
        }

        if (fadeCanvasGroup != null && fadeInSeconds > 0f)
        {
            fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeInSeconds));
        }

        UpdateFlickerOverlay(elapsed);
    }

    private void UpdateFlickerOverlay(float elapsed)
    {
        if (flickerOverlay == null)
        {
            return;
        }

        float slowNoise = Mathf.PerlinNoise(0.37f, elapsed * flickerFrequency);
        float snapPulse = Mathf.Sin(elapsed * flickerFrequency * 7.3f) > 0.74f ? 1f : 0f;
        float alpha = flickerBaseAlpha
            + ((slowNoise - 0.5f) * flickerPulseAlpha)
            + (snapPulse * flickerPulseAlpha * 0.35f);
        SetFlickerOverlayAlpha(alpha);
    }

    private IEnumerator FadeOut()
    {
        SetFlickerOverlayAlpha(arrivalFlashAlpha);

        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < fadeOutSeconds)
        {
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeOutSeconds);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    private void ResetPresentation()
    {
        if (presentationRoot != null)
        {
            presentationRoot.anchoredPosition = initialPresentationPosition;
        }

        SetFlickerOverlayAlpha(flickerBaseAlpha);
    }

    private void SetFlickerOverlayAlpha(float alpha)
    {
        if (flickerOverlay == null)
        {
            return;
        }

        Color color = initialFlickerOverlayColor;
        color.a = Mathf.Clamp01(alpha);
        flickerOverlay.color = color;
    }
}
