using UnityEngine;

public static class RFogVisibilityServiceBindingUtility
{
    public static IFogVisibilityService Resolve(
        MonoBehaviour owner,
        MonoBehaviour currentSource,
        string ownerLabel,
        string sourceLabel)
    {
        if (currentSource is IFogVisibilityService boundFogVisibilityService
            && currentSource != null
            && owner != null
            && currentSource.gameObject.scene == owner.gameObject.scene)
        {
            return boundFogVisibilityService;
        }

        if (owner == null)
        {
            return null;
        }

        return RSceneReferenceLookup.FindUniqueComponentInScene<FlashlightFogOfWarOverlay>(
            owner.gameObject.scene,
            owner,
            ownerLabel,
            sourceLabel);
    }
}
