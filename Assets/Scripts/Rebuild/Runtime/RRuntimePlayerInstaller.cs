using UnityEngine;

public readonly struct RRuntimeHudBinderSet
{
    public RRuntimeHudBinderSet(
        IRPlayerInventoryHudBinder inventoryHudBinder,
        IRPlayerHealthHudBinder healthHudBinder,
        IRPlayerThreatHudBinder threatHudBinder,
        IRPlayerQuickSlotsHudBinder quickSlotsHudBinder,
        IRPlayerStaminaHudBinder staminaHudBinder)
    {
        InventoryHudBinder = inventoryHudBinder;
        HealthHudBinder = healthHudBinder;
        ThreatHudBinder = threatHudBinder;
        QuickSlotsHudBinder = quickSlotsHudBinder;
        StaminaHudBinder = staminaHudBinder;
    }

    public IRPlayerInventoryHudBinder InventoryHudBinder { get; }
    public IRPlayerHealthHudBinder HealthHudBinder { get; }
    public IRPlayerThreatHudBinder ThreatHudBinder { get; }
    public IRPlayerQuickSlotsHudBinder QuickSlotsHudBinder { get; }
    public IRPlayerStaminaHudBinder StaminaHudBinder { get; }
}

public static class RRuntimePlayerInstaller
{
    public static RRuntimeHudBinderSet ResolveHudBinders(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return default;
        }

        GameObject playerObject = playerController.gameObject;
        return new RRuntimeHudBinderSet(
            playerObject.GetComponent<IRPlayerInventoryHudBinder>(),
            playerObject.GetComponent<IRPlayerHealthHudBinder>(),
            playerObject.GetComponent<IRPlayerThreatHudBinder>(),
            playerObject.GetComponent<IRPlayerQuickSlotsHudBinder>(),
            playerObject.GetComponent<IRPlayerStaminaHudBinder>());
    }

    public static RPlayerRuntimeReferences PrepareRuntimePlayer(WasdPlayerController playerController, int sortingOrder)
    {
        if (playerController == null)
        {
            return null;
        }

        RPlayerRuntimeReferences playerRuntime = RPlayerRuntimeReferences.Resolve(playerController);
        playerRuntime?.EnsureRuntimeComponents();

        VisibilityTarget2D visibilityTarget = MainEscapeComponentUtility.GetOrAddComponent<VisibilityTarget2D>(playerController.gameObject);
        SpriteRenderer playerRenderer = playerController.GetComponent<SpriteRenderer>();

        if (playerRenderer != null)
        {
            playerRenderer.enabled = true;
            playerRenderer.forceRenderingOff = false;

            Color color = playerRenderer.color;

            if (color.a <= 0f)
            {
                color.a = 1f;
                playerRenderer.color = color;
            }
        }

        SpriteRenderer[] renderers = playerController.GetComponentsInChildren<SpriteRenderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            if (renderers[index] != null)
            {
                renderers[index].sortingOrder = sortingOrder;
            }
        }

        visibilityTarget?.BindDebugRenderers(playerRenderer, null, null);
        return playerRuntime;
    }

    public static RRuntimeHudBinderSet EnsureHudBinders(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return default;
        }

        GameObject playerObject = playerController.gameObject;
        return new RRuntimeHudBinderSet(
            MainEscapeComponentUtility.GetOrAddComponent<IRPlayerInventoryHudBinder>(playerObject),
            MainEscapeComponentUtility.GetOrAddComponent<IRPlayerHealthHudBinder>(playerObject),
            MainEscapeComponentUtility.GetOrAddComponent<IRPlayerThreatHudBinder>(playerObject),
            MainEscapeComponentUtility.GetOrAddComponent<IRPlayerQuickSlotsHudBinder>(playerObject),
            MainEscapeComponentUtility.GetOrAddComponent<IRPlayerStaminaHudBinder>(playerObject));
    }
}
