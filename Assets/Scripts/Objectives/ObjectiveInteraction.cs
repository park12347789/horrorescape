/*
 * File Role:
 * Defines the shared player interaction framework for nearby interactables.
 *
 * Runtime Use:
 * Tracks active interactables, finds the closest valid one, and triggers it when the player presses interact.
 *
 * Study Notes:
 * A small but important file because many pickups and doors rely on this common pattern.
 */

using System.Collections.Generic;
using UnityEngine;

public abstract class PlayerInteractable2D : MonoBehaviour
{
    private static readonly List<PlayerInteractable2D> ActiveInstances = new();
    [SerializeField, Min(0.1f)] private float interactionRadius = 0.9f;

    public static IReadOnlyList<PlayerInteractable2D> Active => ActiveInstances;
    public float InteractionRadius => MaxInteractionDistance;
    public virtual Vector2 InteractionPoint => transform.position;
    protected virtual float MaxInteractionDistance => interactionRadius;

    protected virtual void OnEnable()
    {
        if (!ActiveInstances.Contains(this))
        {
            ActiveInstances.Add(this);
        }
    }

    protected virtual void OnDisable()
    {
        ActiveInstances.Remove(this);
    }

    public virtual bool CanInteract(WasdPlayerController playerController)
    {
        float interactionDistance = GetInteractionDistance(playerController);
        return CanInteractAtDistance(playerController, interactionDistance);
    }

    public virtual bool CanInteractAtDistance(WasdPlayerController playerController, float interactionDistance)
    {
        if (playerController == null || !enabled || !gameObject.activeInHierarchy)
        {
            return false;
        }

        return interactionDistance <= MaxInteractionDistance;
    }

    public virtual float GetInteractionDistance(WasdPlayerController playerController)
    {
        return playerController == null
            ? float.MaxValue
            : Vector2.Distance(playerController.transform.position, InteractionPoint);
    }

    public virtual Vector2 GetInteractionLineOfSightPoint(WasdPlayerController playerController)
    {
        return InteractionPoint;
    }

    public virtual bool TryGetPromptWorldPosition(WasdPlayerController playerController, out Vector3 worldPosition)
    {
        worldPosition = default;
        return false;
    }

    public virtual bool AllowsLineOfSightBlocker(Collider2D blocker, Vector2 hitPoint, WasdPlayerController playerController)
    {
        return false;
    }

    public virtual string GetInteractionPrompt(WasdPlayerController playerController)
    {
        return "E Interact";
    }

    public abstract void Interact(WasdPlayerController playerController);
}


