using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class MedicalCabinetMedkitInteractable : PlayerInteractable2D
{
    [SerializeField] private SpriteRenderer cabinetRenderer;
    [SerializeField] private BoxCollider2D interactionTrigger;
    [SerializeField] private string itemId = PrototypeItemCatalog.MedkitItemId;
    [SerializeField] private string inventoryDisplayName = "Medkit";
    [SerializeField, Min(1)] private int quantity = 1;
    [SerializeField, Min(0.5f)] private float shatterNoiseRadius = 8.5f;
    [SerializeField] private Color lootedTint = new(0.58f, 0.62f, 0.68f, 0.55f);
    [SerializeField] private Vector2 colliderPadding = new(0.08f, 0.06f);
    [SerializeField] private NoiseSystem noiseSystem;

    private bool looted;
    private INoiseEventBus noiseEventBus;

    private void Awake()
    {
        CacheReferences();
        SyncColliderToSprite();
    }

    private void Reset()
    {
        CacheReferences();
        SyncColliderToSprite();
    }

    private void OnValidate()
    {
        CacheReferences();
        SyncColliderToSprite();
    }

    public override bool CanInteract(WasdPlayerController playerController)
    {
        return CanInteractAtDistance(playerController, GetInteractionDistance(playerController));
    }

    public override bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        return !looted && base.CanInteractAtDistance(playerController, interactionDistance);
    }

    public override string GetInteractionPrompt(WasdPlayerController playerController)
    {
        return looted
            ? string.Empty
            : $"E Break Cabinet ({inventoryDisplayName})";
    }

    public override float GetInteractionDistance(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return float.MaxValue;
        }

        Vector2 playerPosition = playerController.transform.position;
        Vector2 surfacePoint = ResolveSurfacePoint(playerPosition);
        return Vector2.Distance(playerPosition, surfacePoint);
    }

    public override Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        return playerController != null
            ? ResolveSurfacePoint(playerController.transform.position)
            : InteractionPoint;
    }

    public void ConfigureNoiseEventBus(INoiseEventBus configuredNoiseEventBus)
    {
        noiseEventBus = configuredNoiseEventBus;
    }

    public override void Interact(WasdPlayerController playerController)
    {
        if (looted)
        {
            return;
        }

        PlayerInventory inventory = playerController != null ? playerController.GetComponent<PlayerInventory>() : null;

        if (inventory == null || !inventory.AddItem(itemId, inventoryDisplayName, quantity))
        {
            return;
        }

        looted = true;
        PrototypeAudioManager.TryPlayBottleShatter();
        PrototypeAudioManager.TryPlayPickup();
        ResolveNoiseEventBus()?.TryEmitNoise(
            transform.position,
            shatterNoiseRadius,
            NoiseSourceType.Collision,
            playerController != null ? playerController.GetInstanceID() : 0,
            NoiseEmitterAffiliation.Player);

        if (cabinetRenderer != null)
        {
            cabinetRenderer.color = lootedTint;
        }

        if (interactionTrigger != null)
        {
            interactionTrigger.enabled = false;
        }

        enabled = false;
    }

    private INoiseEventBus ResolveNoiseEventBus()
    {
        noiseEventBus ??= NoiseEventBusResolver.Resolve(gameObject.scene, noiseSystem);
        return noiseEventBus;
    }

    private void CacheReferences()
    {
        cabinetRenderer ??= GetComponent<SpriteRenderer>();
        interactionTrigger ??= GetComponent<BoxCollider2D>();
    }

    private void SyncColliderToSprite()
    {
        if (interactionTrigger == null)
        {
            return;
        }

        interactionTrigger.isTrigger = true;
        interactionTrigger.offset = Vector2.zero;

        if (cabinetRenderer == null || cabinetRenderer.sprite == null)
        {
            interactionTrigger.size = Vector2.one;
            return;
        }

        Vector2 spriteSize = cabinetRenderer.sprite.bounds.size;
        interactionTrigger.size = new Vector2(
            Mathf.Max(0.2f, spriteSize.x + colliderPadding.x),
            Mathf.Max(0.2f, spriteSize.y + colliderPadding.y));
    }

    private Vector2 ResolveSurfacePoint(Vector2 playerPosition)
    {
        if (interactionTrigger != null && interactionTrigger.enabled)
        {
            return interactionTrigger.ClosestPoint(playerPosition);
        }

        if (cabinetRenderer != null)
        {
            return cabinetRenderer.bounds.ClosestPoint(playerPosition);
        }

        return InteractionPoint;
    }
}
