using UnityEngine;
using UnityEngine.InputSystem;

public abstract class PlayerRuntimeReferencesBase : MonoBehaviour
{
    [SerializeField] protected WasdPlayerController playerController;
    [SerializeField] protected Rigidbody2D playerBody;
    [SerializeField] protected PlayerInput playerInput;
    [SerializeField] protected PlayerInteractionDriver interactionDriver;
    [SerializeField] protected NoiseEmitter noiseEmitter;
    [SerializeField] protected PrototypePlayerAudio playerAudio;
    [SerializeField] protected PlayerInventory inventory;
    [SerializeField] protected PlayerCaughtState playerCaughtState;
    [SerializeField] protected PlayerHealth playerHealth;
    [SerializeField] protected FlashlightStateOwner flashlightStateOwner;
    [SerializeField] protected PlayerFlashlightEquipment flashlightEquipment;
    [SerializeField] protected PlayerFlashlightBattery flashlightBattery;
    [SerializeField] protected PlayerQuickItemController quickItems;
    [SerializeField] protected PlayerStamina stamina;
    [SerializeField] protected PlayerTrailRecorder trailRecorder;
    [SerializeField] protected PlayerSpriteAnimationDriver spriteAnimationDriver;

    public WasdPlayerController PlayerController => playerController;
    public Rigidbody2D PlayerBody => playerBody;
    public PlayerInput PlayerInput => playerInput;
    public PlayerInteractionDriver InteractionDriver => interactionDriver;
    public NoiseEmitter NoiseEmitter => noiseEmitter;
    public PrototypePlayerAudio PlayerAudio => playerAudio;
    public PlayerInventory Inventory => inventory;
    public PlayerCaughtState CaughtState => playerCaughtState;
    public PlayerHealth PlayerHealth => playerHealth;
    public FlashlightStateOwner FlashlightStateOwner => flashlightStateOwner;
    public IFlashlightStateReadModel FlashlightState => flashlightStateOwner;
    public PlayerFlashlightEquipment FlashlightEquipment => flashlightEquipment;
    public PlayerFlashlightBattery FlashlightBattery => flashlightBattery;
    public PlayerQuickItemController QuickItems => quickItems;
    public PlayerStamina Stamina => stamina;
    public PlayerTrailRecorder TrailRecorder => trailRecorder;
    public PlayerSpriteAnimationDriver SpriteAnimationDriver => spriteAnimationDriver;
    public IStaminaSource StaminaSource => stamina;

    public void CacheExistingReferences()
    {
        playerController = GetComponent<WasdPlayerController>();
        playerBody = GetComponent<Rigidbody2D>();
        playerInput = GetComponent<PlayerInput>();
        interactionDriver = GetComponent<PlayerInteractionDriver>();
        noiseEmitter = GetComponent<NoiseEmitter>();
        playerAudio = GetComponent<PrototypePlayerAudio>();
        inventory = GetComponent<PlayerInventory>();
        playerCaughtState = GetComponent<PlayerCaughtState>();
        playerHealth = GetComponent<PlayerHealth>();
        flashlightStateOwner = GetComponent<FlashlightStateOwner>();
        flashlightEquipment = GetComponent<PlayerFlashlightEquipment>();
        flashlightBattery = GetComponent<PlayerFlashlightBattery>();
        quickItems = GetComponent<PlayerQuickItemController>();
        stamina = GetComponent<PlayerStamina>();
        trailRecorder = GetComponent<PlayerTrailRecorder>();
        spriteAnimationDriver = GetComponent<PlayerSpriteAnimationDriver>();
    }

    public void EnsureRuntimeComponents()
    {
        CacheExistingReferences();
        interactionDriver ??= gameObject.AddComponent<PlayerInteractionDriver>();
        noiseEmitter ??= gameObject.AddComponent<NoiseEmitter>();
        playerAudio ??= gameObject.AddComponent<PrototypePlayerAudio>();
        inventory ??= gameObject.AddComponent<PlayerInventory>();
        playerCaughtState ??= gameObject.AddComponent<PlayerCaughtState>();
        playerHealth ??= gameObject.AddComponent<PlayerHealth>();
        flashlightStateOwner ??= gameObject.AddComponent<FlashlightStateOwner>();
        flashlightEquipment ??= gameObject.AddComponent<PlayerFlashlightEquipment>();
        flashlightBattery ??= gameObject.AddComponent<PlayerFlashlightBattery>();
        quickItems ??= gameObject.AddComponent<PlayerQuickItemController>();
        stamina ??= gameObject.AddComponent<PlayerStamina>();
        trailRecorder ??= gameObject.AddComponent<PlayerTrailRecorder>();
        spriteAnimationDriver ??= gameObject.AddComponent<PlayerSpriteAnimationDriver>();
        CacheExistingReferences();
    }

    protected static TReferences ResolveReference<TReferences>(WasdPlayerController controller)
        where TReferences : PlayerRuntimeReferencesBase
    {
        if (controller == null)
        {
            return null;
        }

        TReferences references = controller.GetComponent(typeof(TReferences)) as TReferences;

        if (references == null)
        {
            references = controller.gameObject.AddComponent(typeof(TReferences)) as TReferences;
        }

        references?.CacheExistingReferences();
        return references;
    }
}
