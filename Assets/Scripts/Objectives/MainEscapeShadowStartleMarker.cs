using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeShadowStartleMarker : MonoBehaviour
{
    public enum RevealVariant
    {
        Peripheral,
        Threshold,
        Glass
    }

    public enum TriggerMode
    {
        Proximity,
        FlashlightReveal
    }

    [SerializeField] private RevealVariant revealVariant = RevealVariant.Peripheral;
    [SerializeField] private TriggerMode triggerMode = TriggerMode.Proximity;
    [SerializeField, Min(0.25f)] private float triggerRadius = 2.4f;
    [SerializeField, Min(0f)] private float minimumPlayerDistance = 0.65f;
    [SerializeField] private bool oneShotPerSceneResidency = true;
    [SerializeField] private bool requireFlashlightForReveal;
    [SerializeField] private bool facePlayerOnTrigger;
    [SerializeField] private bool useWalkAnimation = true;
    [SerializeField, Min(0f)] private float movementDistance = 0.24f;
    [SerializeField, Min(0f)] private float retriggerCooldown = 1.5f;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float holdDuration = 0.42f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.18f;
    [SerializeField, Range(0.05f, 1f)] private float targetAlpha = 0.66f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField, Range(8, 128)] private int sortingOrder = 30;
    [SerializeField] private AudioClip revealClip;
    [SerializeField, Range(0f, 1f)] private float revealClipVolume = 0.14f;

    private bool hasTriggeredThisResidency;
    private float nextEligibleTime;

    public RevealVariant Variant => revealVariant;
    public TriggerMode Mode => triggerMode;
    public float TriggerRadius => Mathf.Max(0.25f, triggerRadius);
    public bool OneShotPerSceneResidency => oneShotPerSceneResidency;
    public bool RequireFlashlightForReveal => requireFlashlightForReveal;
    public bool FacePlayerOnTrigger => facePlayerOnTrigger;
    public bool UseWalkAnimation => useWalkAnimation;
    public float MovementDistance => Mathf.Max(0f, movementDistance);
    public float RetriggerCooldown => Mathf.Max(0f, retriggerCooldown);
    public float FadeInDuration => Mathf.Max(0.01f, fadeInDuration);
    public float HoldDuration => Mathf.Max(0.01f, holdDuration);
    public float FadeOutDuration => Mathf.Max(0.01f, fadeOutDuration);
    public float TargetAlpha => Mathf.Clamp(targetAlpha, 0.05f, 1f);
    public string SortingLayerName => string.IsNullOrWhiteSpace(sortingLayerName) ? "Default" : sortingLayerName;
    public int SortingOrder => Mathf.Clamp(sortingOrder, 8, 128);
    public AudioClip RevealClip => revealClip;
    public float RevealClipVolume => Mathf.Clamp01(revealClipVolume);

    private void OnValidate()
    {
        triggerRadius = Mathf.Max(0.25f, triggerRadius);
        minimumPlayerDistance = Mathf.Max(0f, minimumPlayerDistance);
        movementDistance = Mathf.Max(0f, movementDistance);
        retriggerCooldown = Mathf.Max(0f, retriggerCooldown);
        fadeInDuration = Mathf.Max(0.01f, fadeInDuration);
        holdDuration = Mathf.Max(0.01f, holdDuration);
        fadeOutDuration = Mathf.Max(0.01f, fadeOutDuration);
        targetAlpha = Mathf.Clamp(targetAlpha, 0.05f, 1f);
        sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName) ? "Default" : sortingLayerName.Trim();
        sortingOrder = Mathf.Clamp(sortingOrder, 8, 128);
        revealClipVolume = Mathf.Clamp01(revealClipVolume);
    }

    public void ResetSceneResidencyState()
    {
        hasTriggeredThisResidency = false;
        nextEligibleTime = 0f;
    }

    public bool CanTrigger(WasdPlayerController playerController, FlashlightFogOfWarOverlay fogOfWarOverlay)
    {
        if (!isActiveAndEnabled || playerController == null)
        {
            return false;
        }

        if (hasTriggeredThisResidency && oneShotPerSceneResidency)
        {
            return false;
        }

        if (!oneShotPerSceneResidency && Time.time < nextEligibleTime)
        {
            return false;
        }

        Vector2 markerWorld = transform.position;
        Vector2 playerWorld = playerController.transform.position;
        float distance = Vector2.Distance(markerWorld, playerWorld);

        if (distance < minimumPlayerDistance || distance > TriggerRadius)
        {
            return false;
        }

        if (requireFlashlightForReveal && !playerController.IsFlashlightSwitchOn)
        {
            return false;
        }

        return triggerMode switch
        {
            TriggerMode.FlashlightReveal => fogOfWarOverlay != null && fogOfWarOverlay.IsWorldPointVisible(markerWorld),
            _ => true
        };
    }

    public void ConsumeTrigger()
    {
        hasTriggeredThisResidency = oneShotPerSceneResidency;
        nextEligibleTime = Time.time + RetriggerCooldown;
    }

    public Vector2 ResolveFacing(Vector3 playerWorldPosition)
    {
        if (facePlayerOnTrigger)
        {
            Vector2 toPlayer = playerWorldPosition - transform.position;

            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                return toPlayer.normalized;
            }
        }

        Vector2 facing = transform.up;
        return facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.down;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, TriggerRadius);

        Vector3 facing = ResolveFacing(transform.position + transform.up);
        Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + (facing * 0.75f));
    }
}
