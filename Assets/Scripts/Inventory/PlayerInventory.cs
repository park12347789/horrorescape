/*
 * File Role:
 * Implements the current lightweight player inventory and its simple debug UI.
 *
 * Runtime Use:
 * Tracks item ids, stack counts, and inventory toggling for temporary prototype items.
 *
 * Study Notes:
 * Useful for understanding how key and battery pickups currently persist in play.
 */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public sealed class ItemStack
    {
        public string itemId;
        public string displayName;
        public int quantity;
    }

    [SerializeField] private List<ItemStack> items = new();

    public IReadOnlyList<ItemStack> Items => items;
    public event Action Changed;

    public bool AddItem(string itemId, string displayName, int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        ItemStack stack = items.Find(entry => string.Equals(entry.itemId, itemId, StringComparison.Ordinal));
        int currentQuantity = stack != null ? Mathf.Max(0, stack.quantity) : 0;
        int acceptedQuantity = ResolveAcceptedQuantity(itemId, currentQuantity, quantity);

        if (acceptedQuantity <= 0)
        {
            return false;
        }

        if (stack == null)
        {
            stack = new ItemStack
            {
                itemId = itemId,
                displayName = string.IsNullOrWhiteSpace(displayName) ? itemId : displayName,
                quantity = 0
            };
            items.Add(stack);
        }
        else if (!string.IsNullOrWhiteSpace(displayName))
        {
            stack.displayName = displayName;
        }

        stack.quantity += acceptedQuantity;
        Debug.Log($"Inventory +{acceptedQuantity}: {stack.displayName} ({stack.quantity})", this);
        Changed?.Invoke();
        return true;
    }

    public bool HasItem(string itemId, int minimumQuantity = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || minimumQuantity <= 0)
        {
            return false;
        }

        ItemStack stack = items.Find(entry => string.Equals(entry.itemId, itemId, StringComparison.Ordinal));
        return stack != null && stack.quantity >= minimumQuantity;
    }

    public bool RemoveItem(string itemId, int quantity = 1)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        ItemStack stack = items.Find(entry => string.Equals(entry.itemId, itemId, StringComparison.Ordinal));

        if (stack == null || stack.quantity < quantity)
        {
            return false;
        }

        stack.quantity -= quantity;
        Debug.Log($"Inventory -{quantity}: {stack.displayName} ({stack.quantity})", this);

        if (stack.quantity <= 0)
        {
            items.Remove(stack);
        }

        Changed?.Invoke();
        return true;
    }

    public int GetQuantity(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        ItemStack stack = items.Find(entry => string.Equals(entry.itemId, itemId, StringComparison.Ordinal));
        return stack != null ? Mathf.Max(0, stack.quantity) : 0;
    }

    public void SetItems(IEnumerable<ItemStack> sourceItems)
    {
        items.Clear();

        if (sourceItems != null)
        {
            foreach (ItemStack source in sourceItems)
            {
                if (source == null || string.IsNullOrWhiteSpace(source.itemId) || source.quantity <= 0)
                {
                    continue;
                }

                int clampedQuantity = ClampQuantityToStackLimit(source.itemId, source.quantity);

                if (clampedQuantity <= 0)
                {
                    continue;
                }

                items.Add(new ItemStack
                {
                    itemId = source.itemId,
                    displayName = source.displayName,
                    quantity = clampedQuantity
                });
            }
        }

        Changed?.Invoke();
    }

    public string GetDebugSummary()
    {
        if (items.Count == 0)
        {
            return "Inventory\n- Empty";
        }

        StringBuilder builder = new("Inventory");

        foreach (ItemStack item in items)
        {
            builder.Append('\n')
                .Append("- ")
                .Append(PrototypeItemCatalog.GetDisplayName(item.itemId, item.displayName))
                .Append(" x")
                .Append(item.quantity);
        }

        return builder.ToString();
    }

    private static int ResolveAcceptedQuantity(string itemId, int currentQuantity, int requestedQuantity)
    {
        int stackLimit = ResolveMaxStackQuantity(itemId);

        if (stackLimit <= 0)
        {
            return requestedQuantity;
        }

        int remainingCapacity = Mathf.Max(0, stackLimit - Mathf.Max(0, currentQuantity));
        return Mathf.Min(requestedQuantity, remainingCapacity);
    }

    private static int ClampQuantityToStackLimit(string itemId, int quantity)
    {
        int stackLimit = ResolveMaxStackQuantity(itemId);
        return stackLimit > 0
            ? Mathf.Clamp(quantity, 0, stackLimit)
            : Mathf.Max(0, quantity);
    }

    private static int ResolveMaxStackQuantity(string itemId)
    {
        return PrototypeItemCatalog.TryGetDefinition(itemId, out PrototypeItemDefinition definition)
            ? definition.MaxStackQuantity
            : 0;
    }
}
