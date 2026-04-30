using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(WasdPlayerController))]
[RequireComponent(typeof(PlayerInventory))]
public sealed class PlayerQuickItemController : MonoBehaviour
{
    private const float MissingReferenceRetryInterval = 0.25f;

    [Serializable]
    public sealed class QuickSlotBinding
    {
        [Range(1, 8)] public int slotNumber = 1;
        public string itemId = PrototypeItemCatalog.GlassBottleItemId;
    }

    public readonly struct QuickSlotView
    {
        public QuickSlotView(int slotNumber, string itemId, string displayName, PrototypeItemUseKind useKind, int quantity, bool equipped)
        {
            SlotNumber = slotNumber;
            ItemId = itemId ?? string.Empty;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName;
            UseKind = useKind;
            Quantity = Mathf.Max(0, quantity);
            Equipped = equipped;
        }

        public int SlotNumber { get; }
        public string ItemId { get; }
        public string DisplayName { get; }
        public PrototypeItemUseKind UseKind { get; }
        public int Quantity { get; }
        public bool Equipped { get; }
    }

    [SerializeField] private List<QuickSlotBinding> quickSlots = new();
    [SerializeField, Min(0f)] private float throwSpawnDistance = 0.55f;
    [SerializeField, Min(0f)] private float throwableUseCooldown = 1f;
    [SerializeField] private string equippedThrowableItemId = PrototypeItemCatalog.GlassBottleItemId;

    private WasdPlayerController playerController;
    private PlayerInventory inventory;
    private PlayerHealth playerHealth;
    private PlayerFlashlightBattery flashlightBattery;
    private Collider2D playerCollider;
    private float nextRuntimeReferenceRetryTime;
    private float nextThrowableUseTime;

    public string EquippedThrowableItemId => equippedThrowableItemId;
    public bool IsThrowableUseOnCooldown => Time.time < nextThrowableUseTime;
    public event Action Changed;

    private void Reset()
    {
        quickSlots = new List<QuickSlotBinding>();
        AddRecommendedDefaultSlots();
        NormalizeSlotConfiguration();
        EnsureEquippedThrowableAssigned();
    }

    private void Awake()
    {
        quickSlots ??= new List<QuickSlotBinding>();
        EnsureDefaultSlotsIfNeeded();
        NormalizeSlotConfiguration();
        ResolveRuntimeReferences(force: true);
        EnsureEquippedThrowableAssigned();
    }

    private void OnValidate()
    {
        quickSlots ??= new List<QuickSlotBinding>();
        EnsureDefaultSlotsIfNeeded();
        NormalizeSlotConfiguration();
        throwableUseCooldown = Mathf.Max(0f, throwableUseCooldown);
        EnsureEquippedThrowableAssigned();
    }

    private void Update()
    {
        EnsureDefaultSlotsIfNeeded();

        if (!HasRuntimeReferences() && Time.unscaledTime >= nextRuntimeReferenceRetryTime)
        {
            ResolveRuntimeReferences();
        }

        if (playerController == null)
        {
            return;
        }

        for (int index = 0; index < quickSlots.Count; index++)
        {
            QuickSlotBinding slot = quickSlots[index];

            if (slot == null || !WasSlotPressedThisFrame(slot.slotNumber))
            {
                continue;
            }

            HandleSlotPressed(slot);
        }

        if (playerController.ConsumeThrowablePressedThisFrame())
        {
            TryThrowEquippedItem();
        }
    }

    public string GetHudSummary()
    {
        StringBuilder builder = new();

        for (int index = 0; index < quickSlots.Count; index++)
        {
            if (!TryGetSlotViewAt(index, out QuickSlotView slotView))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("  ");
            }

            builder.Append(slotView.SlotNumber)
                .Append(' ')
                .Append(slotView.DisplayName)
                .Append(" x")
                .Append(slotView.Quantity);

            if (slotView.Equipped)
            {
                builder.Append(" [Equipped]");
            }
        }

        if (builder.Length == 0)
        {
            builder.Append("No quick slots");
        }

        builder.Append("  Space Throw");
        return builder.ToString();
    }

    public int GetConfiguredSlotCount()
    {
        return quickSlots.Count;
    }

    public bool TryGetSlotViewAt(int index, out QuickSlotView slotView)
    {
        slotView = default;

        if (index < 0 || index >= quickSlots.Count)
        {
            return false;
        }

        QuickSlotBinding slot = quickSlots[index];

        if (slot == null)
        {
            return false;
        }

        return TryCreateSlotView(slot, out slotView);
    }

    public bool TryGetSlotView(int slotNumber, out QuickSlotView slotView)
    {
        slotView = default;

        for (int index = 0; index < quickSlots.Count; index++)
        {
            QuickSlotBinding slot = quickSlots[index];

            if (slot == null || slot.slotNumber != slotNumber)
            {
                continue;
            }

            return TryCreateSlotView(slot, out slotView);
        }

        return false;
    }

    private void HandleSlotPressed(QuickSlotBinding slot)
    {
        if (slot == null || !PrototypeItemCatalog.TryGetDefinition(slot.itemId, out PrototypeItemDefinition definition))
        {
            return;
        }

        switch (definition.UseKind)
        {
            case PrototypeItemUseKind.Throwable:
                if (!string.Equals(equippedThrowableItemId, definition.ItemId, StringComparison.Ordinal))
                {
                    equippedThrowableItemId = definition.ItemId;
                    Changed?.Invoke();
                }
                break;
            case PrototypeItemUseKind.Instant:
                TryUseInstantItem(definition);
                break;
            case PrototypeItemUseKind.Passive:
                TryUsePassiveItem(definition);
                break;
        }
    }

    private void TryUseInstantItem(PrototypeItemDefinition definition)
    {
        if (inventory == null || definition.HealAmount <= 0 || playerHealth == null)
        {
            return;
        }

        if (!playerHealth.TryHeal(definition.HealAmount))
        {
            return;
        }

        if (inventory.RemoveItem(definition.ItemId))
        {
            PrototypeAudioManager.TryPlayPickup();
        }
    }

    private void TryUsePassiveItem(PrototypeItemDefinition definition)
    {
        if (definition.FlashlightChargeAmount <= 0f || flashlightBattery == null)
        {
            return;
        }

        flashlightBattery.TryUseStoredBattery();
    }

    private void TryThrowEquippedItem()
    {
        if (inventory == null)
        {
            return;
        }

        if (IsThrowableUseOnCooldown)
        {
            return;
        }

        if (!TryResolveThrowableDefinition(out PrototypeItemDefinition definition))
        {
            return;
        }

        if (!inventory.RemoveItem(definition.ItemId))
        {
            return;
        }

        Vector2 throwDirection = playerController.AimDirection;

        if (throwDirection.sqrMagnitude <= 0.0001f)
        {
            throwDirection = Vector2.up;
        }

        Vector2 spawnPosition = (Vector2)transform.position + (throwDirection.normalized * throwSpawnDistance);
        ThrowableBottleProjectile.Spawn(
            spawnPosition,
            throwDirection,
            playerCollider,
            definition.ThrowSpeed,
            definition.ThrowDistance,
            definition.ThrowNoiseRadius,
            definition.StunDurationMin,
            definition.StunDurationMax,
            gameObject.GetInstanceID());
        nextThrowableUseTime = Time.time + Mathf.Max(0f, throwableUseCooldown);
    }

    private bool TryResolveThrowableDefinition(out PrototypeItemDefinition definition)
    {
        definition = default;
        EnsureEquippedThrowableAssigned();

        if (!string.IsNullOrWhiteSpace(equippedThrowableItemId)
            && PrototypeItemCatalog.TryGetDefinition(equippedThrowableItemId, out definition)
            && definition.UseKind == PrototypeItemUseKind.Throwable
            && inventory != null
            && inventory.HasItem(definition.ItemId))
        {
            return true;
        }

        for (int index = 0; index < quickSlots.Count; index++)
        {
            QuickSlotBinding slot = quickSlots[index];

            if (slot == null
                || string.IsNullOrWhiteSpace(slot.itemId)
                || !PrototypeItemCatalog.TryGetDefinition(slot.itemId, out definition)
                || definition.UseKind != PrototypeItemUseKind.Throwable
                || inventory == null
                || !inventory.HasItem(definition.ItemId))
            {
                continue;
            }

            equippedThrowableItemId = definition.ItemId;
            return true;
        }

        if (inventory != null)
        {
            for (int index = 0; index < inventory.Items.Count; index++)
            {
                PlayerInventory.ItemStack stack = inventory.Items[index];

                if (stack == null
                    || stack.quantity <= 0
                    || !PrototypeItemCatalog.TryGetDefinition(stack.itemId, out definition)
                    || definition.UseKind != PrototypeItemUseKind.Throwable)
                {
                    continue;
                }

                equippedThrowableItemId = definition.ItemId;
                return true;
            }
        }

        definition = default;
        return false;
    }

    private bool TryCreateSlotView(QuickSlotBinding slot, out QuickSlotView slotView)
    {
        slotView = default;

        if (slot == null || string.IsNullOrWhiteSpace(slot.itemId))
        {
            return false;
        }

        PrototypeItemCatalog.TryGetDefinition(slot.itemId, out PrototypeItemDefinition definition);
        int quantity = inventory != null ? inventory.GetQuantity(slot.itemId) : 0;
        bool equipped = definition.UseKind == PrototypeItemUseKind.Throwable
            && quantity > 0
            && string.Equals(equippedThrowableItemId, slot.itemId, StringComparison.Ordinal);

        slotView = new QuickSlotView(
            slot.slotNumber,
            slot.itemId,
            PrototypeItemCatalog.GetDisplayName(slot.itemId, definition.DisplayName),
            definition.UseKind,
            quantity,
            equipped);
        return true;
    }

    private bool WasSlotPressedThisFrame(int slotNumber)
    {
        switch (slotNumber)
        {
            case 1:
                return playerController != null && playerController.ConsumeQuickSlot1PressedThisFrame();
            case 2:
                return playerController != null && playerController.ConsumeQuickSlot2PressedThisFrame();
            default:
                return WasExtendedKeyboardSlotPressedThisFrame(slotNumber);
        }
    }

    private static bool WasExtendedKeyboardSlotPressedThisFrame(int slotNumber)
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return false;
        }

        switch (slotNumber)
        {
            case 3:
                return keyboard.digit3Key.wasPressedThisFrame;
            case 4:
                return keyboard.digit4Key.wasPressedThisFrame;
            case 5:
                return keyboard.digit5Key.wasPressedThisFrame;
            case 6:
                return keyboard.digit6Key.wasPressedThisFrame;
            case 7:
                return keyboard.digit7Key.wasPressedThisFrame;
            case 8:
                return keyboard.digit8Key.wasPressedThisFrame;
            default:
                return false;
        }
    }

    private void AddRecommendedDefaultSlots()
    {
        quickSlots.Add(new QuickSlotBinding
        {
            slotNumber = 1,
            itemId = PrototypeItemCatalog.MedkitItemId
        });
        quickSlots.Add(new QuickSlotBinding
        {
            slotNumber = 2,
            itemId = PrototypeItemCatalog.FlashlightBatteryItemId
        });
        quickSlots.Add(new QuickSlotBinding
        {
            slotNumber = 3,
            itemId = PrototypeItemCatalog.GlassBottleItemId
        });
    }

    private void EnsureDefaultSlotsIfNeeded()
    {
        if (quickSlots == null)
        {
            quickSlots = new List<QuickSlotBinding>();
        }

        if (quickSlots.Count > 0)
        {
            return;
        }

        AddRecommendedDefaultSlots();
    }

    private void ResolveRuntimeReferences(bool force = false)
    {
        if (force || playerController == null)
        {
            playerController = GetComponent<WasdPlayerController>();
        }

        if (force || inventory == null)
        {
            inventory = GetComponent<PlayerInventory>();
        }

        if (force || playerHealth == null)
        {
            playerHealth = GetComponent<PlayerHealth>();
        }

        if (force || flashlightBattery == null)
        {
            flashlightBattery = GetComponent<PlayerFlashlightBattery>();
        }

        if (force || playerCollider == null)
        {
            playerCollider = GetComponent<Collider2D>();
        }

        if (!HasRuntimeReferences())
        {
            nextRuntimeReferenceRetryTime = Time.unscaledTime + MissingReferenceRetryInterval;
        }
    }

    private bool HasRuntimeReferences()
    {
        return playerController != null
            && inventory != null
            && playerHealth != null
            && flashlightBattery != null
            && playerCollider != null;
    }

    private void NormalizeSlotConfiguration()
    {
        if (quickSlots == null)
        {
            return;
        }

        HashSet<int> usedSlots = new();

        for (int index = 0; index < quickSlots.Count; index++)
        {
            QuickSlotBinding slot = quickSlots[index];

            if (slot == null)
            {
                continue;
            }

            slot.slotNumber = Mathf.Clamp(slot.slotNumber, 1, 8);

            if (usedSlots.Contains(slot.slotNumber))
            {
                for (int candidate = 1; candidate <= 8; candidate++)
                {
                    if (usedSlots.Contains(candidate))
                    {
                        continue;
                    }

                    slot.slotNumber = candidate;
                    break;
                }
            }

            usedSlots.Add(slot.slotNumber);
        }

        quickSlots.Sort((left, right) =>
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.slotNumber.CompareTo(right.slotNumber);
        });
    }

    private void EnsureEquippedThrowableAssigned()
    {
        if (!string.IsNullOrWhiteSpace(equippedThrowableItemId))
        {
            for (int index = 0; index < quickSlots.Count; index++)
            {
                QuickSlotBinding slot = quickSlots[index];

                if (slot == null || !string.Equals(slot.itemId, equippedThrowableItemId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (PrototypeItemCatalog.TryGetDefinition(slot.itemId, out PrototypeItemDefinition equippedDefinition)
                    && equippedDefinition.UseKind == PrototypeItemUseKind.Throwable)
                {
                    return;
                }
            }
        }

        equippedThrowableItemId = string.Empty;

        for (int index = 0; index < quickSlots.Count; index++)
        {
            QuickSlotBinding slot = quickSlots[index];

            if (slot == null || !PrototypeItemCatalog.TryGetDefinition(slot.itemId, out PrototypeItemDefinition definition))
            {
                continue;
            }

            if (definition.UseKind != PrototypeItemUseKind.Throwable)
            {
                continue;
            }

            equippedThrowableItemId = definition.ItemId;
            Changed?.Invoke();
            return;
        }
    }
}
