using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPrefabBindings : MonoBehaviour
{
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private CircleCollider2D hitbox;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform visionOrigin;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer facingMarkerRenderer;
    [SerializeField] private EnemyVisionVisualizer visionVisualizer;

    public EnemyStateMachine StateMachine => stateMachine;
    public CircleCollider2D Hitbox => hitbox;
    public Transform VisualRoot => visualRoot;
    public Transform VisionOrigin => visionOrigin;
    public SpriteRenderer BodyRenderer => bodyRenderer;
    public SpriteRenderer FacingMarkerRenderer => facingMarkerRenderer;
    public EnemyVisionVisualizer VisionVisualizer => visionVisualizer;

    private void Reset()
    {
        AutoAssign();
    }

    private void OnValidate()
    {
        AutoAssign();
    }

    public void AutoAssign()
    {
        stateMachine ??= GetComponent<EnemyStateMachine>();
        hitbox ??= GetComponent<CircleCollider2D>();
        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        bodyRenderer ??= ResolvePrimaryRenderer(spriteRenderers);
        facingMarkerRenderer ??= ResolveSecondaryRenderer(spriteRenderers, bodyRenderer);
        visualRoot ??= bodyRenderer != null ? bodyRenderer.transform.parent : null;
        visionVisualizer ??= GetComponentInChildren<EnemyVisionVisualizer>(true);
        visionOrigin ??= visionVisualizer != null ? visionVisualizer.transform.parent : null;
    }

    private static SpriteRenderer ResolvePrimaryRenderer(SpriteRenderer[] spriteRenderers)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return null;
        }

        SpriteRenderer bestRenderer = null;

        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            SpriteRenderer candidate = spriteRenderers[index];

            if (candidate == null)
            {
                continue;
            }

            if (bestRenderer == null || candidate.sortingOrder < bestRenderer.sortingOrder)
            {
                bestRenderer = candidate;
            }
        }

        return bestRenderer;
    }

    private static SpriteRenderer ResolveSecondaryRenderer(SpriteRenderer[] spriteRenderers, SpriteRenderer primaryRenderer)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return null;
        }

        SpriteRenderer bestRenderer = null;

        for (int index = 0; index < spriteRenderers.Length; index++)
        {
            SpriteRenderer candidate = spriteRenderers[index];

            if (candidate == null || candidate == primaryRenderer)
            {
                continue;
            }

            if (bestRenderer == null || candidate.sortingOrder > bestRenderer.sortingOrder)
            {
                bestRenderer = candidate;
            }
        }

        return bestRenderer;
    }
}
