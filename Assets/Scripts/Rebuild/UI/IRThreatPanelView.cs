using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class IRThreatPanelView : MonoBehaviour
{
    private const float CornerMargin = 12f;
    private const float CornerSpan = 148f;
    private const float EdgeThickness = 20f;
    private const string SpottedOverlayCanvasObjectName = "IRThreatSpottedOverlayCanvas";
    private const string SpottedOverlayObjectName = "IRThreatSpottedOverlay";
    private const float SpottedOverlayHeight = 150f;
    private const int SpottedOverlayTextureHeight = 96;
    private const int SpottedOverlaySortingBoost = 120;

    private static Texture2D spottedOverlayTexture;

    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RawImage topEdgeImage;
    [SerializeField] private RawImage bottomEdgeImage;
    [SerializeField] private RawImage leftEdgeImage;
    [SerializeField] private RawImage rightEdgeImage;

    private bool themeApplied;
    private bool canvasInteractionConfigured;
    private bool edgeLayoutsCached;
    private bool cornerLayoutApplied;
    private float lastCanvasAlpha = float.NaN;
    private Color lastSpottedOverlayColor = new(float.NaN, float.NaN, float.NaN, float.NaN);
    private Color lastTopEdgeColor = new(float.NaN, float.NaN, float.NaN, float.NaN);
    private Color lastBottomEdgeColor = new(float.NaN, float.NaN, float.NaN, float.NaN);
    private Color lastLeftEdgeColor = new(float.NaN, float.NaN, float.NaN, float.NaN);
    private Color lastRightEdgeColor = new(float.NaN, float.NaN, float.NaN, float.NaN);
    private Canvas spottedOverlayCanvas;
    private RectTransform spottedOverlayCanvasRect;
    private RawImage spottedOverlayImage;
    private RectTransform topEdgeRect;
    private RectTransform bottomEdgeRect;
    private RectTransform leftEdgeRect;
    private RectTransform rightEdgeRect;
    private RectLayoutSnapshot topEdgeLayout;
    private RectLayoutSnapshot bottomEdgeLayout;
    private RectLayoutSnapshot leftEdgeLayout;
    private RectLayoutSnapshot rightEdgeLayout;

    public bool HasRenderableEdges => topEdgeImage != null
        && bottomEdgeImage != null
        && leftEdgeImage != null
        && rightEdgeImage != null;

    private struct RectLayoutSnapshot
    {
        public RectLayoutSnapshot(RectTransform rectTransform)
        {
            AnchorMin = rectTransform != null ? rectTransform.anchorMin : Vector2.zero;
            AnchorMax = rectTransform != null ? rectTransform.anchorMax : Vector2.zero;
            AnchoredPosition = rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
            SizeDelta = rectTransform != null ? rectTransform.sizeDelta : Vector2.zero;
            Pivot = rectTransform != null ? rectTransform.pivot : new Vector2(0.5f, 0.5f);
        }

        public Vector2 AnchorMin { get; }
        public Vector2 AnchorMax { get; }
        public Vector2 AnchoredPosition { get; }
        public Vector2 SizeDelta { get; }
        public Vector2 Pivot { get; }
    }

    private void Awake()
    {
        ResetRenderCache();
        ValidateBindings();
    }

    private void OnValidate()
    {
        ResetRenderCache();
        ValidateBindings();
    }

    public void Configure(
        RectTransform configuredPanelRoot,
        CanvasGroup configuredCanvasGroup,
        RawImage configuredTopEdgeImage,
        RawImage configuredBottomEdgeImage,
        RawImage configuredLeftEdgeImage,
        RawImage configuredRightEdgeImage)
    {
        panelRoot = configuredPanelRoot;
        canvasGroup = configuredCanvasGroup;
        topEdgeImage = configuredTopEdgeImage;
        bottomEdgeImage = configuredBottomEdgeImage;
        leftEdgeImage = configuredLeftEdgeImage;
        rightEdgeImage = configuredRightEdgeImage;
        ResetRenderCache();
        ValidateBindings();
    }

    public void Render(in ThreatPanelPresentation presentation)
    {
        EnsureThreatHierarchyVisible();
        ApplyThemeIfNeeded();
        EnsureSpottedOverlayImage();
        EnsureEdgeLayoutsCached();

        float spottedPulseIntensity = Mathf.Clamp01(presentation.SpottedPulseIntensity);
        float pursuitOverlayIntensity = presentation.PursuitConfirmed ? Mathf.Clamp01(presentation.Intensity) : 0f;
        float overlayIntensity = Mathf.Max(spottedPulseIntensity, pursuitOverlayIntensity);
        bool showSpottedOverlay = overlayIntensity > 0.001f;

        if (panelRoot != null && !panelRoot.gameObject.activeSelf)
        {
            panelRoot.gameObject.SetActive(true);
        }

        if (canvasGroup != null)
        {
            float targetAlpha = 0f;

            if (showSpottedOverlay)
            {
                float panelIntensity = Mathf.SmoothStep(0f, 1f, overlayIntensity);
                targetAlpha = Mathf.Lerp(0.66f, 1f, panelIntensity);
            }

            if (!Mathf.Approximately(lastCanvasAlpha, targetAlpha))
            {
                canvasGroup.alpha = targetAlpha;
                lastCanvasAlpha = targetAlpha;
            }

            if (!canvasInteractionConfigured
                || canvasGroup.blocksRaycasts
                || canvasGroup.interactable)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                canvasInteractionConfigured = true;
            }
        }

        if (showSpottedOverlay)
        {
            RestoreEdgeLayout();
            ApplyEdgeColor(topEdgeImage, Color.clear, ref lastTopEdgeColor);
            ApplyEdgeColor(bottomEdgeImage, Color.clear, ref lastBottomEdgeColor);
            ApplyEdgeColor(leftEdgeImage, Color.clear, ref lastLeftEdgeColor);
            ApplyEdgeColor(rightEdgeImage, Color.clear, ref lastRightEdgeColor);
            float overlayAlpha = Mathf.Lerp(0.18f, 0.9f, overlayIntensity);
            ApplySpottedOverlayColor(new Color(1f, 1f, 1f, overlayAlpha));
            return;
        }

        ApplySpottedOverlayColor(Color.clear);
        RestoreEdgeLayout();
        ApplyEdgeColor(topEdgeImage, Color.clear, ref lastTopEdgeColor);
        ApplyEdgeColor(bottomEdgeImage, Color.clear, ref lastBottomEdgeColor);
        ApplyEdgeColor(leftEdgeImage, Color.clear, ref lastLeftEdgeColor);
        ApplyEdgeColor(rightEdgeImage, Color.clear, ref lastRightEdgeColor);
    }

    private void ValidateBindings()
    {
        if (panelRoot == null)
        {
            Debug.LogError($"{nameof(IRThreatPanelView)} is missing its panel root reference.", this);
        }
    }

    private void EnsureThreatHierarchyVisible()
    {
        if (panelRoot == null)
        {
            return;
        }

        Transform current = panelRoot.parent;

        while (current != null)
        {
            if (current.TryGetComponent(out Canvas _))
            {
                break;
            }

            if (!current.gameObject.activeSelf)
            {
                current.gameObject.SetActive(true);
            }

            current = current.parent;
        }

        if (!panelRoot.gameObject.activeSelf)
        {
            panelRoot.gameObject.SetActive(true);
        }
    }

    private void ApplyThemeIfNeeded()
    {
        if (themeApplied)
        {
            return;
        }

        IRAnalogNoiseUiTheme.ApplyThreatPanelTheme(panelRoot, canvasGroup, topEdgeImage, bottomEdgeImage, leftEdgeImage, rightEdgeImage);
        themeApplied = true;
    }

    private void ResetRenderCache()
    {
        themeApplied = false;
        canvasInteractionConfigured = false;
        edgeLayoutsCached = false;
        cornerLayoutApplied = false;
        spottedOverlayCanvas = null;
        spottedOverlayCanvasRect = null;
        spottedOverlayImage = null;
        lastCanvasAlpha = float.NaN;
        lastSpottedOverlayColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
        lastTopEdgeColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
        lastBottomEdgeColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
        lastLeftEdgeColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
        lastRightEdgeColor = new Color(float.NaN, float.NaN, float.NaN, float.NaN);
    }

    private static void ApplyEdgeColor(RawImage edgeImage, Color color, ref Color lastColor)
    {
        if (edgeImage == null || edgeImage.color == color)
        {
            return;
        }

        if (lastColor == color)
        {
            return;
        }

        edgeImage.color = color;
        lastColor = color;
    }

    private void ApplySpottedOverlayColor(Color color)
    {
        if (spottedOverlayImage == null || spottedOverlayImage.color == color)
        {
            return;
        }

        if (lastSpottedOverlayColor == color)
        {
            return;
        }

        spottedOverlayImage.color = color;
        lastSpottedOverlayColor = color;
    }

    private void EnsureEdgeLayoutsCached()
    {
        if (edgeLayoutsCached)
        {
            return;
        }

        topEdgeRect = topEdgeImage != null ? topEdgeImage.rectTransform : null;
        bottomEdgeRect = bottomEdgeImage != null ? bottomEdgeImage.rectTransform : null;
        leftEdgeRect = leftEdgeImage != null ? leftEdgeImage.rectTransform : null;
        rightEdgeRect = rightEdgeImage != null ? rightEdgeImage.rectTransform : null;
        topEdgeLayout = new RectLayoutSnapshot(topEdgeRect);
        bottomEdgeLayout = new RectLayoutSnapshot(bottomEdgeRect);
        leftEdgeLayout = new RectLayoutSnapshot(leftEdgeRect);
        rightEdgeLayout = new RectLayoutSnapshot(rightEdgeRect);
        edgeLayoutsCached = true;
    }

    private void EnsureSpottedOverlayImage()
    {
        if (panelRoot == null)
        {
            return;
        }

        Canvas parentCanvas = panelRoot.GetComponentInParent<Canvas>();

        if (parentCanvas == null)
        {
            return;
        }

        if (spottedOverlayCanvas == null || spottedOverlayCanvas.transform.parent != panelRoot)
        {
            Transform existingCanvasRoot = panelRoot.Find(SpottedOverlayCanvasObjectName);
            GameObject overlayCanvasObject = existingCanvasRoot != null
                ? existingCanvasRoot.gameObject
                : new GameObject(SpottedOverlayCanvasObjectName, typeof(RectTransform), typeof(Canvas));

            if (existingCanvasRoot == null)
            {
                overlayCanvasObject.transform.SetParent(panelRoot, false);
            }

            spottedOverlayCanvas = overlayCanvasObject.GetComponent<Canvas>();
            spottedOverlayCanvasRect = overlayCanvasObject.GetComponent<RectTransform>();
        }

        if (spottedOverlayCanvasRect != null)
        {
            spottedOverlayCanvasRect.anchorMin = Vector2.zero;
            spottedOverlayCanvasRect.anchorMax = Vector2.one;
            spottedOverlayCanvasRect.pivot = new Vector2(0.5f, 0.5f);
            spottedOverlayCanvasRect.anchoredPosition = Vector2.zero;
            spottedOverlayCanvasRect.sizeDelta = Vector2.zero;
            spottedOverlayCanvasRect.localScale = Vector3.one;
            spottedOverlayCanvasRect.localRotation = Quaternion.identity;
            spottedOverlayCanvasRect.SetAsLastSibling();
        }

        if (spottedOverlayCanvas != null)
        {
            spottedOverlayCanvas.renderMode = parentCanvas.renderMode;
            spottedOverlayCanvas.worldCamera = parentCanvas.worldCamera;
            spottedOverlayCanvas.planeDistance = parentCanvas.planeDistance;
            spottedOverlayCanvas.pixelPerfect = parentCanvas.pixelPerfect;
            spottedOverlayCanvas.overrideSorting = true;
            spottedOverlayCanvas.sortingLayerID = parentCanvas.sortingLayerID;
            spottedOverlayCanvas.sortingOrder = parentCanvas.sortingOrder + SpottedOverlaySortingBoost;
            spottedOverlayCanvas.targetDisplay = parentCanvas.targetDisplay;
        }

        if (spottedOverlayImage == null || spottedOverlayImage.transform.parent != spottedOverlayCanvasRect)
        {
            Transform existing = spottedOverlayCanvasRect != null ? spottedOverlayCanvasRect.Find(SpottedOverlayObjectName) : null;
            GameObject overlayObject = existing != null
                ? existing.gameObject
                : new GameObject(SpottedOverlayObjectName, typeof(RectTransform), typeof(RawImage));

            if (existing == null)
            {
                overlayObject.transform.SetParent(spottedOverlayCanvasRect, false);
            }

            spottedOverlayImage = overlayObject.GetComponent<RawImage>();
        }

        if (spottedOverlayImage == null)
        {
            return;
        }

        RectTransform overlayRect = spottedOverlayImage.rectTransform;
        overlayRect.anchorMin = new Vector2(0f, 0f);
        overlayRect.anchorMax = new Vector2(1f, 0f);
        overlayRect.pivot = new Vector2(0.5f, 0f);
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.sizeDelta = new Vector2(0f, SpottedOverlayHeight);
        overlayRect.localScale = Vector3.one;
        overlayRect.localRotation = Quaternion.identity;
        spottedOverlayImage.texture = GetOrCreateSpottedOverlayTexture();
        spottedOverlayImage.raycastTarget = false;
        overlayRect.SetAsLastSibling();
    }

    private static Texture2D GetOrCreateSpottedOverlayTexture()
    {
        if (spottedOverlayTexture != null)
        {
            return spottedOverlayTexture;
        }

        Texture2D generatedTexture = new(2, SpottedOverlayTextureHeight, TextureFormat.RGBA32, false)
        {
            name = "IRThreatSpottedOverlayGradient",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < SpottedOverlayTextureHeight; y++)
        {
            float normalizedHeight = SpottedOverlayTextureHeight <= 1
                ? 0f
                : y / (SpottedOverlayTextureHeight - 1f);
            float alpha = Mathf.Pow(1f - normalizedHeight, 1.85f);
            Color gradientColor = new(0.96f, 0.08f, 0.06f, alpha);

            for (int x = 0; x < generatedTexture.width; x++)
            {
                generatedTexture.SetPixel(x, y, gradientColor);
            }
        }

        generatedTexture.Apply(false, true);
        spottedOverlayTexture = generatedTexture;
        return spottedOverlayTexture;
    }

    private void ApplyCornerLayout()
    {
        if (cornerLayoutApplied)
        {
            return;
        }

        EnsureEdgeLayoutsCached();
        ApplyLayout(topEdgeRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(CornerMargin, -CornerMargin), new Vector2(CornerSpan, EdgeThickness), new Vector2(0f, 1f));
        ApplyLayout(rightEdgeRect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-CornerMargin, -CornerMargin), new Vector2(EdgeThickness, CornerSpan), new Vector2(1f, 1f));
        ApplyLayout(bottomEdgeRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-CornerMargin, CornerMargin), new Vector2(CornerSpan, EdgeThickness), new Vector2(1f, 0f));
        ApplyLayout(leftEdgeRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(CornerMargin, CornerMargin), new Vector2(EdgeThickness, CornerSpan), new Vector2(0f, 0f));
        cornerLayoutApplied = true;
    }

    private void RestoreEdgeLayout()
    {
        if (!cornerLayoutApplied)
        {
            return;
        }

        EnsureEdgeLayoutsCached();
        ApplyLayout(topEdgeRect, topEdgeLayout);
        ApplyLayout(bottomEdgeRect, bottomEdgeLayout);
        ApplyLayout(leftEdgeRect, leftEdgeLayout);
        ApplyLayout(rightEdgeRect, rightEdgeLayout);
        cornerLayoutApplied = false;
    }

    private static void ApplyLayout(RectTransform rectTransform, RectLayoutSnapshot snapshot)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = snapshot.AnchorMin;
        rectTransform.anchorMax = snapshot.AnchorMax;
        rectTransform.anchoredPosition = snapshot.AnchoredPosition;
        rectTransform.sizeDelta = snapshot.SizeDelta;
        rectTransform.pivot = snapshot.Pivot;
    }

    private static void ApplyLayout(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.pivot = pivot;
    }
}
