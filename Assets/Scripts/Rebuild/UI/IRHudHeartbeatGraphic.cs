using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class IRHudHeartbeatGraphic : MaskableGraphic
{
    private enum AnimationMode
    {
        Pulse = 0,
        ScrollingGraph = 1
    }

    private static readonly Vector2[] WaveformPoints =
    {
        new(0f, 0f),
        new(0.36f, 0f),
        new(0.44f, 0.03f),
        new(0.48f, 0f),
        new(0.53f, -0.2f),
        new(0.57f, 1f),
        new(0.6f, -0.42f),
        new(0.66f, 0.06f),
        new(0.74f, 0f),
        new(1f, 0f)
    };

    [SerializeField] private AnimationMode animationMode = AnimationMode.Pulse;
    [SerializeField, Range(24, 256)] private int sampleCount = 120;
    [SerializeField, Min(1f)] private float lineThickness = 3f;
    [SerializeField, Range(0.1f, 1f)] private float amplitude = 0.7f;
    [SerializeField, Min(36f)] private float beatsPerMinute = 72f;
    [SerializeField, Min(0.8f)] private float visibleDuration = 3.2f;
    [SerializeField, Min(0.05f)] private float cycleSpeed = 0.5f;
    [SerializeField, Range(-2f, 2f)] private float flowSpeed = 1f;
    [SerializeField, Range(0f, 0.2f)] private float horizontalPadding = 0.01f;
    [SerializeField] private bool previewInEditMode;

    private Vector2[] positions = System.Array.Empty<Vector2>();
    private Vector2[] normals = System.Array.Empty<Vector2>();
    private CanvasRenderer cachedCanvasRenderer;

    protected override void Awake()
    {
        EnsureCanvasRenderer();
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnEnable()
    {
        EnsureCanvasRenderer();
        base.OnEnable();
        SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        sampleCount = Mathf.Clamp(sampleCount, 24, 256);
        lineThickness = Mathf.Max(1f, lineThickness);
        amplitude = Mathf.Clamp(amplitude, 0.1f, 1f);
        beatsPerMinute = Mathf.Max(36f, beatsPerMinute);
        visibleDuration = Mathf.Max(0.8f, visibleDuration);
        cycleSpeed = Mathf.Max(0.05f, cycleSpeed);
        flowSpeed = Mathf.Clamp(flowSpeed, -2f, 2f);
        horizontalPadding = Mathf.Clamp(horizontalPadding, 0f, 0.2f);
        SetVerticesDirty();
    }
#endif

    public void Configure(Color traceColor, float thickness, float normalizedAmplitude, float speed)
    {
        float resolvedThickness = Mathf.Max(1f, thickness);
        float resolvedAmplitude = Mathf.Clamp01(normalizedAmplitude);
        float resolvedCycleSpeed = Mathf.Max(0.05f, speed);
        float resolvedBeatsPerMinute = Mathf.Lerp(58f, 96f, Mathf.InverseLerp(0.48f, 0.95f, resolvedCycleSpeed));

        if (color == traceColor
            && Mathf.Approximately(lineThickness, resolvedThickness)
            && Mathf.Approximately(amplitude, resolvedAmplitude)
            && Mathf.Approximately(cycleSpeed, resolvedCycleSpeed)
            && Mathf.Approximately(beatsPerMinute, resolvedBeatsPerMinute))
        {
            return;
        }

        color = traceColor;
        lineThickness = resolvedThickness;
        amplitude = resolvedAmplitude;
        cycleSpeed = resolvedCycleSpeed;
        beatsPerMinute = resolvedBeatsPerMinute;
        SetVerticesDirty();
    }

    private void Update()
    {
        if (!ShouldAnimate())
        {
            return;
        }

        if (TryResolveCanvasRenderer(out CanvasRenderer renderer) && renderer.cull)
        {
            return;
        }

        SetVerticesDirty();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();

        if (rect.width <= 1f || rect.height <= 1f)
        {
            return;
        }

        if (animationMode == AnimationMode.ScrollingGraph)
        {
            PopulateScrollingGraph(vh, rect);
            return;
        }

        PopulatePulse(vh, rect);
    }

    private void PopulatePulse(VertexHelper vh, Rect rect)
    {
        int pointCount = Mathf.Max(24, sampleCount);
        EnsureBuffers(pointCount);

        float padding = rect.width * horizontalPadding;
        float left = rect.xMin + padding;
        float right = rect.xMax - padding;
        float usableWidth = Mathf.Max(1f, right - left);
        float centerY = rect.center.y;
        float animationTime = GetAnimationTime();
        float pulse = ShouldAnimate()
            ? 0.92f + (0.08f * Mathf.Sin(animationTime * cycleSpeed * Mathf.PI * 2f))
            : 1f;
        float amplitudePixels = rect.height * 0.5f * amplitude * pulse;

        for (int index = 0; index < pointCount; index++)
        {
            float normalizedX = index / (float)(pointCount - 1);
            float normalizedY = SampleWave(normalizedX);
            positions[index] = new Vector2(left + (usableWidth * normalizedX), centerY + (normalizedY * amplitudePixels));
        }

        BuildRibbonMesh(vh, pointCount, lineThickness * (0.48f + (0.08f * pulse)));
    }

    private void PopulateScrollingGraph(VertexHelper vh, Rect rect)
    {
        int pointCount = Mathf.Max(48, sampleCount);
        EnsureBuffers(pointCount);

        float padding = rect.width * horizontalPadding;
        float left = rect.xMin + padding;
        float right = rect.xMax - padding;
        float usableWidth = Mathf.Max(1f, right - left);
        float centerY = rect.center.y;
        float effectiveTime = ShouldAnimate()
            ? GetAnimationTime() * flowSpeed
            : 0f;
        float beatInterval = 60f / Mathf.Max(36f, beatsPerMinute);
        float window = Mathf.Max(visibleDuration, beatInterval * 1.5f);
        float amplitudePixels = rect.height * 0.5f * amplitude;

        for (int index = 0; index < pointCount; index++)
        {
            float normalizedX = index / (float)(pointCount - 1);
            float sampleTime = effectiveTime - ((1f - normalizedX) * window);
            float beatProgress = Mathf.Repeat(sampleTime / beatInterval, 1f);
            float normalizedY = SampleWave(beatProgress);
            positions[index] = new Vector2(left + (usableWidth * normalizedX), centerY + (normalizedY * amplitudePixels));
        }

        BuildRibbonMesh(vh, pointCount, lineThickness * 0.52f);
    }

    private void BuildRibbonMesh(VertexHelper vh, int pointCount, float halfThickness)
    {
        for (int index = 0; index < pointCount; index++)
        {
            Vector2 previous = positions[Mathf.Max(0, index - 1)];
            Vector2 next = positions[Mathf.Min(pointCount - 1, index + 1)];
            Vector2 tangent = next - previous;

            if (tangent.sqrMagnitude <= 0.0001f)
            {
                tangent = Vector2.right;
            }

            tangent.Normalize();
            normals[index] = new Vector2(-tangent.y, tangent.x) * halfThickness;
        }

        Color32 tint = color;

        for (int index = 0; index < pointCount; index++)
        {
            vh.AddVert(positions[index] - normals[index], tint, Vector2.zero);
            vh.AddVert(positions[index] + normals[index], tint, Vector2.one);
        }

        for (int index = 0; index < pointCount - 1; index++)
        {
            int baseIndex = index * 2;
            vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
            vh.AddTriangle(baseIndex + 1, baseIndex + 3, baseIndex + 2);
        }
    }

    private void EnsureBuffers(int pointCount)
    {
        if (positions.Length == pointCount && normals.Length == pointCount)
        {
            return;
        }

        positions = new Vector2[pointCount];
        normals = new Vector2[pointCount];
    }

    private bool ShouldAnimate()
    {
        return Application.isPlaying || previewInEditMode;
    }

    private void EnsureCanvasRenderer()
    {
        if (TryResolveCanvasRenderer(out _))
        {
            return;
        }

        cachedCanvasRenderer = gameObject.AddComponent<CanvasRenderer>();
    }

    private bool TryResolveCanvasRenderer(out CanvasRenderer resolvedCanvasRenderer)
    {
        if (cachedCanvasRenderer != null)
        {
            resolvedCanvasRenderer = cachedCanvasRenderer;
            return true;
        }

        if (TryGetComponent(out cachedCanvasRenderer))
        {
            resolvedCanvasRenderer = cachedCanvasRenderer;
            return true;
        }

        resolvedCanvasRenderer = null;
        return false;
    }

    private static float GetAnimationTime()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return (float)EditorApplication.timeSinceStartup;
        }
#endif

        return Time.unscaledTime;
    }

    private static float SampleWave(float time)
    {
        if (time <= WaveformPoints[0].x)
        {
            return WaveformPoints[0].y;
        }

        for (int index = 1; index < WaveformPoints.Length; index++)
        {
            if (time > WaveformPoints[index].x)
            {
                continue;
            }

            Vector2 start = WaveformPoints[index - 1];
            Vector2 end = WaveformPoints[index];
            float t = Mathf.InverseLerp(start.x, end.x, time);
            return Mathf.Lerp(start.y, end.y, t);
        }

        return WaveformPoints[WaveformPoints.Length - 1].y;
    }
}
