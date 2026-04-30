using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class MainEscapeDoorPassabilityController : MonoBehaviour
{
    [SerializeField] private BoxCollider2D physicalBlocker;
    [SerializeField] private BoxCollider2D lightBlocker;
    [SerializeField] private ShadowCaster2D lightBlockerShadowCaster;
    [SerializeField] private bool isPassable;

    public bool IsPassable => isPassable;

    public void Configure(
        BoxCollider2D configuredPhysicalBlocker,
        BoxCollider2D configuredLightBlocker,
        ShadowCaster2D configuredLightBlockerShadowCaster)
    {
        physicalBlocker = configuredPhysicalBlocker;
        lightBlocker = configuredLightBlocker;
        lightBlockerShadowCaster = configuredLightBlockerShadowCaster;
        ApplyState();
    }

    public void SetPassable(bool passable)
    {
        isPassable = passable;
        ApplyState();
    }

    private void Awake()
    {
        ApplyState();
    }

    private void OnEnable()
    {
        ApplyState();
    }

    private void ApplyState()
    {
        if (physicalBlocker != null)
        {
            physicalBlocker.enabled = !isPassable;
        }

        if (lightBlocker != null)
        {
            lightBlocker.enabled = !isPassable;
        }

        if (lightBlockerShadowCaster != null)
        {
            lightBlockerShadowCaster.enabled = !isPassable;
        }
    }
}
