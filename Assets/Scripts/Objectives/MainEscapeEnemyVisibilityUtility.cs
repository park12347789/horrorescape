using UnityEngine;

public static class MainEscapeEnemyVisibilityUtility
{
    public static void ApplyFogReactiveVisibility(
        GameObject enemyObject,
        IFogVisibilityService fogVisibilityService,
        int? minimumSortingOrder = null)
    {
        if (enemyObject == null)
        {
            return;
        }

        FogReactiveEnemyVisibility fogReactiveVisibility = enemyObject.GetComponent<FogReactiveEnemyVisibility>();

        if (fogReactiveVisibility == null)
        {
            fogReactiveVisibility = enemyObject.AddComponent<FogReactiveEnemyVisibility>();
        }

        BindFogVisibilityConsumers(enemyObject, fogVisibilityService);
        fogReactiveVisibility.Initialize(fogVisibilityService);

        if (minimumSortingOrder.HasValue)
        {
            PromoteEnemyVisuals(enemyObject, minimumSortingOrder.Value);
        }
    }

    private static void BindFogVisibilityConsumers(GameObject enemyObject, IFogVisibilityService fogVisibilityService)
    {
        MonoBehaviour[] behaviours = enemyObject.GetComponentsInChildren<MonoBehaviour>(true);

        for (int index = 0; index < behaviours.Length; index++)
        {
            if (behaviours[index] is IFogOfWarOverlayConsumer fogConsumer)
            {
                fogConsumer.BindFogVisibilityService(fogVisibilityService);
            }
        }
    }

    private static void PromoteEnemyVisuals(GameObject enemyObject, int minimumSortingOrder)
    {
        SpriteRenderer[] renderers = enemyObject.GetComponentsInChildren<SpriteRenderer>(true);

        if (renderers == null || renderers.Length == 0)
        {
            return;
        }

        int lowestSortingOrder = int.MaxValue;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer != null && renderer.sortingOrder < lowestSortingOrder)
            {
                lowestSortingOrder = renderer.sortingOrder;
            }
        }

        if (lowestSortingOrder == int.MaxValue || lowestSortingOrder >= minimumSortingOrder)
        {
            return;
        }

        int sortingDelta = minimumSortingOrder - lowestSortingOrder;

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer renderer = renderers[index];

            if (renderer != null)
            {
                renderer.sortingOrder += sortingDelta;
            }
        }
    }
}
