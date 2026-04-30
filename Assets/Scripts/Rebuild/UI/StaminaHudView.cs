using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StaminaHudView : MonoBehaviour
{
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image gaugeBackgroundImage;
    [SerializeField] private Image[] segmentImages = new Image[0];
    [SerializeField] private Sprite highSegmentSprite;
    [SerializeField] private Sprite midSegmentSprite;
    [SerializeField] private Sprite lowSegmentSprite;
    [SerializeField] private Sprite exhaustedSegmentSprite;
    [SerializeField, Min(0f)] private float visibleHoldSeconds = 0.8f;
    [SerializeField, Min(0f)] private float fadeInSpeed = 10f;
    [SerializeField, Min(0f)] private float fadeOutSpeed = 4.8f;

    private IStaminaSource staminaSource;
    private IStaminaSource subscribedStaminaSource;
    private float targetAlpha;
    private float visibleUntilTime = float.NegativeInfinity;
    private bool configurationErrorLogged;
    private bool hasRenderedSegmentGauge;
    private int lastRenderedVisibleSegmentCount = int.MinValue;
    private Sprite lastRenderedSegmentSprite;

    public RectTransform PanelRoot => panelRoot;
    public Image FrameImage => frameImage;

    private void Awake()
    {
        EnsureReferences();
        ConfigureGaugeImages();
        RenderImmediate();
    }

    private void OnEnable()
    {
        Subscribe();
        RenderImmediate();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnValidate()
    {
        EnsureReferences();
        ConfigureGaugeImages();
        PrepareEditorPreview();
        visibleHoldSeconds = Mathf.Max(0f, visibleHoldSeconds);
        fadeInSpeed = Mathf.Max(0f, fadeInSpeed);
        fadeOutSpeed = Mathf.Max(0f, fadeOutSpeed);
    }

    private void Update()
    {
        RefreshVisibilityIntent();
        Render(Time.unscaledDeltaTime);
    }

    public void Bind(IStaminaSource source)
    {
        if (ReferenceEquals(staminaSource, source))
        {
            return;
        }

        Unsubscribe();
        staminaSource = source;
        Subscribe();
        RefreshVisibilityIntent(forceVisible: staminaSource != null && staminaSource.ShouldShowStaminaHud);
        RenderImmediate();
    }

    public void ApplyLayout()
    {
        EnsureReferences();
        ConfigureGaugeImages();
    }

    private void RefreshVisibilityIntent(bool forceVisible = false)
    {
        if (staminaSource != null && (forceVisible || staminaSource.ShouldShowStaminaHud))
        {
            visibleUntilTime = Time.unscaledTime + visibleHoldSeconds;
        }

        targetAlpha = staminaSource != null && Time.unscaledTime <= visibleUntilTime ? 1f : 0f;
    }

    private void RenderImmediate()
    {
        targetAlpha = staminaSource != null && staminaSource.ShouldShowStaminaHud ? 1f : 0f;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = targetAlpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        Render(0f);
    }

    private void Render(float deltaTime)
    {
        EnsureReferences();
        float normalized = staminaSource != null ? staminaSource.StaminaNormalized : 1f;

        RenderSegmentGauge(normalized);

        if (frameImage != null && frameImage.raycastTarget)
        {
            frameImage.raycastTarget = false;
        }

        if (gaugeBackgroundImage != null && gaugeBackgroundImage.raycastTarget)
        {
            gaugeBackgroundImage.raycastTarget = false;
        }

        if (canvasGroup == null)
        {
            return;
        }

        float speed = targetAlpha > canvasGroup.alpha ? fadeInSpeed : fadeOutSpeed;
        canvasGroup.alpha = speed <= 0f
            ? targetAlpha
            : Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, deltaTime * speed);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void EnsureReferences()
    {
        panelRoot ??= transform as RectTransform;
        canvasGroup ??= GetComponent<CanvasGroup>();
    }

    private void ConfigureGaugeImages()
    {
        if (gaugeBackgroundImage != null)
        {
            gaugeBackgroundImage.raycastTarget = false;
        }

        if (segmentImages == null)
        {
            return;
        }

        for (int index = 0; index < segmentImages.Length; index++)
        {
            Image segmentImage = segmentImages[index];
            if (segmentImage == null)
            {
                continue;
            }

            segmentImage.type = Image.Type.Simple;
            segmentImage.fillAmount = 1f;
            segmentImage.preserveAspect = true;
            segmentImage.raycastTarget = false;
        }

        InvalidateSegmentGaugeCache();
    }

    private bool ValidateGaugeConfiguration()
    {
        if (gaugeBackgroundImage == null || gaugeBackgroundImage.sprite == null)
        {
            LogConfigurationError("Stamina gauge background Image or Sprite is not assigned.");
            return false;
        }

        if (segmentImages == null || segmentImages.Length != 10)
        {
            LogConfigurationError("Stamina gauge must have exactly 10 segment Image references.");
            return false;
        }

        for (int index = 0; index < segmentImages.Length; index++)
        {
            Image segmentImage = segmentImages[index];
            if (segmentImage == null || segmentImage.sprite == null)
            {
                LogConfigurationError($"Stamina segment {index + 1:00} Image or Sprite is not assigned.");
                return false;
            }
        }

        if (highSegmentSprite == null || midSegmentSprite == null || lowSegmentSprite == null || exhaustedSegmentSprite == null)
        {
            LogConfigurationError("Stamina segment state sprites are not fully assigned.");
            return false;
        }

        return true;
    }

    private void RenderSegmentGauge(float normalized)
    {
        if (!ValidateGaugeConfiguration())
        {
            return;
        }

        int visibleCount = ResolveVisibleSegmentCount(normalized, segmentImages.Length);
        Sprite segmentSprite = ResolveSegmentSprite(normalized);

        if (hasRenderedSegmentGauge
            && visibleCount == lastRenderedVisibleSegmentCount
            && segmentSprite == lastRenderedSegmentSprite)
        {
            return;
        }

        for (int index = 0; index < segmentImages.Length; index++)
        {
            Image segmentImage = segmentImages[index];
            bool visible = index < visibleCount;
            SetImageActive(segmentImage, visible);

            if (!visible)
            {
                continue;
            }

            segmentImage.sprite = segmentSprite;
            segmentImage.color = Color.white;
            segmentImage.preserveAspect = true;
            segmentImage.raycastTarget = false;
        }

        hasRenderedSegmentGauge = true;
        lastRenderedVisibleSegmentCount = visibleCount;
        lastRenderedSegmentSprite = segmentSprite;
    }

    private int ResolveVisibleSegmentCount(float normalized, int segmentCount)
    {
        if (segmentCount <= 0 || normalized <= 0f)
        {
            return 0;
        }

        if (normalized >= 1f)
        {
            return segmentCount;
        }

        return Mathf.Clamp(Mathf.CeilToInt((Mathf.Clamp01(normalized) * segmentCount) - 0.0001f), 0, segmentCount);
    }

    private Sprite ResolveSegmentSprite(float normalized)
    {
        if (staminaSource != null && staminaSource.IsExhausted)
        {
            return exhaustedSegmentSprite;
        }

        if (normalized <= 0.35f)
        {
            return lowSegmentSprite;
        }

        if (normalized <= 0.65f)
        {
            return midSegmentSprite;
        }

        return highSegmentSprite;
    }

    private void LogConfigurationError(string message)
    {
        if (configurationErrorLogged)
        {
            return;
        }

        configurationErrorLogged = true;
        Debug.LogError($"{nameof(StaminaHudView)}: {message}", this);
    }

    private static void SetImageActive(Image image, bool active)
    {
        if (image != null && image.gameObject.activeSelf != active)
        {
            image.gameObject.SetActive(active);
        }
    }

    private void PrepareEditorPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        RenderSegmentGauge(1f);
    }

    private void InvalidateSegmentGaugeCache()
    {
        hasRenderedSegmentGauge = false;
        lastRenderedVisibleSegmentCount = int.MinValue;
        lastRenderedSegmentSprite = null;
    }

    private void Subscribe()
    {
        if (staminaSource == null || ReferenceEquals(subscribedStaminaSource, staminaSource))
        {
            return;
        }

        staminaSource.Changed += HandleStaminaChanged;
        subscribedStaminaSource = staminaSource;
    }

    private void Unsubscribe()
    {
        if (subscribedStaminaSource == null)
        {
            return;
        }

        subscribedStaminaSource.Changed -= HandleStaminaChanged;
        subscribedStaminaSource = null;
    }

    private void HandleStaminaChanged()
    {
        RefreshVisibilityIntent(forceVisible: staminaSource != null && staminaSource.ShouldShowStaminaHud);
    }
}
