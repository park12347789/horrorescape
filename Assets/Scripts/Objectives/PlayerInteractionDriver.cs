using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WasdPlayerController))]
public sealed class PlayerInteractionDriver : MonoBehaviour
{
    private static Sprite sharedInteractionKeyPromptSprite;

    [SerializeField] private LayerMask interactionBlockingMask;
    [SerializeField, Min(0f)] private float lineOfSightPadding = 0.05f;
    [SerializeField] private SpriteRenderer interactionKeyPromptRenderer;
    [SerializeField, Min(0f)] private float promptVerticalPadding = 0.17f;
    [SerializeField] private Vector2 promptFallbackOffset = new(0f, 0.52f);
    [SerializeField] private Vector3 promptScale = new(0.24f, 0.24f, 1f);
    [SerializeField, Min(0f)] private float promptBoundsRefreshInterval = 0.1f;
    [SerializeField, Min(0.02f)] private float promptTextRefreshInterval = 0.12f;
    [SerializeField, Min(0.01f)] private float candidateRefreshInterval = 0.04f;
    [SerializeField, Min(0f)] private float candidateMovementRefreshThreshold = 0.08f;

    private WasdPlayerController playerController;
    private NoiseEmitter noiseEmitter;
    private PlayerInteractable2D currentCandidate;
    private string currentPrompt = string.Empty;
    private PlayerInteractable2D cachedPromptTextCandidate;
    private PlayerInteractable2D cachedPromptBoundsCandidate;
    private Bounds cachedPromptBounds;
    private float nextPromptBoundsRefreshTime;
    private float nextPromptTextRefreshTime;
    private float nextCandidateRefreshTime;
    private Vector2 lastCandidateSamplePlayerPosition;
    private bool hasCachedPromptBounds;
    private bool hasCandidateSamplePosition;
    private bool interactionPromptRendererConfigured;
    private readonly List<SpriteRenderer> promptBoundsSpriteRenderers = new(4);
    private readonly List<Collider2D> promptBoundsColliders = new(4);

    public PlayerInteractable2D CurrentCandidate => currentCandidate;
    public string CurrentPrompt => currentPrompt;

    private void Reset()
    {
        EnsureBlockingMask();
    }

    private void Awake()
    {
        EnsureBlockingMask();
        playerController = GetComponent<WasdPlayerController>();
        noiseEmitter = GetComponent<NoiseEmitter>();
        EnsureInteractionPromptRenderer();
    }

    private void Update()
    {
        if (playerController == null)
        {
            SetPromptVisible(false);
            return;
        }

        if (ShouldRefreshCandidate())
        {
            currentCandidate = FindBestCandidate();
            StampCandidateRefresh();
        }

        RefreshCurrentPromptText();
        RefreshInteractionPrompt();

        if (!playerController.ConsumeInteractPressedThisFrame() || currentCandidate == null)
        {
            return;
        }

        if (!EnsureCurrentCandidateIsInteractableForInput())
        {
            return;
        }

        currentCandidate.Interact(playerController);
        noiseEmitter ??= GetComponent<NoiseEmitter>();
        noiseEmitter?.EmitInteractNoise();
    }

    private PlayerInteractable2D FindBestCandidate()
    {
        PlayerInteractable2D bestCandidate = null;
        float bestDistance = float.MaxValue;
        foreach (PlayerInteractable2D interactable in PlayerInteractable2D.Active)
        {
            if (!IsSceneLocalInteractable(interactable)
                || !interactable.enabled
                || !interactable.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = interactable.GetInteractionDistance(playerController);
            if (distance >= bestDistance)
            {
                continue;
            }

            if (!interactable.CanInteractAtDistance(playerController, distance))
            {
                continue;
            }

            if (!HasLineOfSight(interactable))
            {
                continue;
            }
            bestDistance = distance;
            bestCandidate = interactable;
        }

        return bestCandidate;
    }

    private bool ShouldRefreshCandidate()
    {
        if (currentCandidate != null && !IsCurrentCandidateLocallyValid())
        {
            return true;
        }

        if (!hasCandidateSamplePosition)
        {
            return true;
        }

        float movementThreshold = candidateMovementRefreshThreshold;
        if (movementThreshold > 0f
            && ((Vector2)transform.position - lastCandidateSamplePlayerPosition).sqrMagnitude >= movementThreshold * movementThreshold)
        {
            return true;
        }

        return Time.unscaledTime >= nextCandidateRefreshTime;
    }

    private bool IsCurrentCandidateLocallyValid()
    {
        if (currentCandidate == null
            || !IsSceneLocalInteractable(currentCandidate)
            || !currentCandidate.enabled
            || !currentCandidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        float distance = currentCandidate.GetInteractionDistance(playerController);
        return currentCandidate.CanInteractAtDistance(playerController, distance);
    }

    private bool IsSceneLocalInteractable(PlayerInteractable2D interactable)
    {
        return interactable != null && interactable.gameObject.scene == gameObject.scene;
    }

    private void StampCandidateRefresh()
    {
        hasCandidateSamplePosition = true;
        lastCandidateSamplePlayerPosition = transform.position;
        nextCandidateRefreshTime = Time.unscaledTime + Mathf.Max(0.01f, candidateRefreshInterval);
    }

    private bool HasLineOfSight(PlayerInteractable2D interactable)
    {
        if (playerController == null || interactable == null)
        {
            return false;
        }

        EnsureBlockingMask();

        if (interactionBlockingMask.value == 0)
        {
            return true;
        }

        Vector2 origin = playerController.transform.position;
        Vector2 targetPoint = interactable.GetInteractionLineOfSightPoint(playerController);
        Vector2 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;

        if (distance <= 0.0001f)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(origin, toTarget / distance, Mathf.Max(0f, distance - lineOfSightPadding), interactionBlockingMask);

        if (hit.collider == null)
        {
            return true;
        }

        Transform hitTransform = hit.collider.transform;
        return hitTransform == interactable.transform
            || hitTransform.IsChildOf(interactable.transform)
            || interactable.AllowsLineOfSightBlocker(hit.collider, hit.point, playerController);
    }

    private bool EnsureCurrentCandidateIsInteractableForInput()
    {
        if (currentCandidate != null)
        {
            float distance = currentCandidate.GetInteractionDistance(playerController);

            if (currentCandidate.CanInteractAtDistance(playerController, distance)
                && HasLineOfSight(currentCandidate))
            {
                return true;
            }
        }

        currentCandidate = FindBestCandidate();
        StampCandidateRefresh();
        RefreshCurrentPromptText();
        RefreshInteractionPrompt();
        return currentCandidate != null;
    }

    private void RefreshCurrentPromptText()
    {
        if (currentCandidate == null)
        {
            currentPrompt = string.Empty;
            cachedPromptTextCandidate = null;
            nextPromptTextRefreshTime = 0f;
            return;
        }

        bool candidateChanged = cachedPromptTextCandidate != currentCandidate;
        if (!candidateChanged && Time.unscaledTime < nextPromptTextRefreshTime)
        {
            return;
        }

        currentPrompt = currentCandidate.GetInteractionPrompt(playerController) ?? string.Empty;
        cachedPromptTextCandidate = currentCandidate;
        nextPromptTextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, promptTextRefreshInterval);
    }

    private void EnsureBlockingMask()
    {
        if (interactionBlockingMask.value == 0)
        {
            interactionBlockingMask = GameLayers.VisionBlockingMask;
        }
    }

    private void EnsureInteractionPromptRenderer()
    {
        EnsureSharedInteractionKeyPromptSprite();

        if (interactionKeyPromptRenderer != null
            && interactionPromptRendererConfigured
            && interactionKeyPromptRenderer.sprite == sharedInteractionKeyPromptSprite)
        {
            return;
        }

        if (interactionKeyPromptRenderer == null)
        {
            Transform existingPrompt = transform.Find("InteractionKeyPrompt");
            GameObject promptObject = existingPrompt != null
                ? existingPrompt.gameObject
                : new GameObject("InteractionKeyPrompt");
            promptObject.transform.SetParent(transform, false);
            interactionKeyPromptRenderer = promptObject.GetComponent<SpriteRenderer>();

            if (interactionKeyPromptRenderer == null)
            {
                interactionKeyPromptRenderer = promptObject.AddComponent<SpriteRenderer>();
            }
        }

        interactionKeyPromptRenderer.sprite = sharedInteractionKeyPromptSprite;
        interactionKeyPromptRenderer.transform.localScale = ResolvePromptScale();
        interactionKeyPromptRenderer.enabled = false;
        interactionPromptRendererConfigured = true;

        SpriteRenderer playerRenderer = GetComponent<SpriteRenderer>();
        interactionKeyPromptRenderer.sortingLayerName = playerRenderer != null
            ? playerRenderer.sortingLayerName
            : "Default";
        interactionKeyPromptRenderer.sortingOrder = playerRenderer != null
            ? playerRenderer.sortingOrder + 24
            : 220;
    }

    private void RefreshInteractionPrompt()
    {
        EnsureInteractionPromptRenderer();

        if (interactionKeyPromptRenderer == null || currentCandidate == null)
        {
            SetPromptVisible(false);
            return;
        }

        Vector3 promptWorldPosition = ResolvePromptWorldPosition(currentCandidate);
        promptWorldPosition.y += Mathf.Sin(Time.time * 3.4f) * 0.03f;
        promptWorldPosition.z = interactionKeyPromptRenderer.transform.position.z;
        interactionKeyPromptRenderer.transform.position = promptWorldPosition;
        interactionKeyPromptRenderer.transform.localScale = ResolvePromptScale();
        SetPromptVisible(true);
    }

    private Vector3 ResolvePromptScale()
    {
        Vector3 targetScale = new(
            Mathf.Clamp(promptScale.x, 0.12f, 0.24f),
            Mathf.Clamp(promptScale.y, 0.12f, 0.24f),
            1f);

        Transform parent = interactionKeyPromptRenderer != null
            ? interactionKeyPromptRenderer.transform.parent
            : transform;

        if (parent == null)
        {
            return targetScale;
        }

        Vector3 parentScale = parent.lossyScale;
        return new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? targetScale.x : targetScale.x / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? targetScale.y : targetScale.y / parentScale.y,
            1f);
    }

    private Vector3 ResolvePromptWorldPosition(PlayerInteractable2D interactable)
    {
        if (interactable == null)
        {
            return transform.position;
        }

        if (interactable.TryGetPromptWorldPosition(playerController, out Vector3 customPromptWorldPosition))
        {
            customPromptWorldPosition.z = transform.position.z;
            return customPromptWorldPosition;
        }

        if (TryGetCachedPromptBounds(interactable, out Bounds combinedBounds))
        {
            return new Vector3(
                combinedBounds.center.x,
                combinedBounds.max.y + promptVerticalPadding,
                transform.position.z);
        }

        Vector2 interactionPoint = interactable.InteractionPoint;
        return new Vector3(
            interactionPoint.x + promptFallbackOffset.x,
            interactionPoint.y + promptFallbackOffset.y,
            transform.position.z);
    }

    private bool TryGetCachedPromptBounds(PlayerInteractable2D interactable, out Bounds combinedBounds)
    {
        combinedBounds = default;

        if (interactable == null)
        {
            return false;
        }

        bool candidateChanged = cachedPromptBoundsCandidate != interactable;
        bool cacheExpired = Time.unscaledTime >= nextPromptBoundsRefreshTime;

        if (candidateChanged)
        {
            cachedPromptBoundsCandidate = interactable;
            CachePromptBoundsSources(interactable);
        }

        if (candidateChanged || !hasCachedPromptBounds || cacheExpired)
        {
            hasCachedPromptBounds = TryGetPromptBounds(interactable, out cachedPromptBounds);
            nextPromptBoundsRefreshTime = Time.unscaledTime + promptBoundsRefreshInterval;
        }

        if (!hasCachedPromptBounds)
        {
            return false;
        }

        combinedBounds = cachedPromptBounds;
        return true;
    }

    private void CachePromptBoundsSources(PlayerInteractable2D interactable)
    {
        promptBoundsSpriteRenderers.Clear();
        promptBoundsColliders.Clear();

        if (interactable == null)
        {
            return;
        }

        interactable.GetComponentsInChildren(true, promptBoundsSpriteRenderers);
        interactable.GetComponentsInChildren(true, promptBoundsColliders);
    }

    private bool TryGetPromptBounds(PlayerInteractable2D interactable, out Bounds combinedBounds)
    {
        combinedBounds = default;

        if (interactable == null)
        {
            return false;
        }

        if (TryGetPromptBoundsFromCachedSources(out combinedBounds))
        {
            return true;
        }

        // Rebuild once if the cached source list went stale because the interactable hierarchy changed.
        CachePromptBoundsSources(interactable);
        return TryGetPromptBoundsFromCachedSources(out combinedBounds);
    }

    private bool TryGetPromptBoundsFromCachedSources(out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;

        for (int index = 0; index < promptBoundsSpriteRenderers.Count; index++)
        {
            SpriteRenderer spriteRenderer = promptBoundsSpriteRenderers[index];

            if (spriteRenderer == null || spriteRenderer.sprite == null || !spriteRenderer.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = spriteRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(spriteRenderer.bounds.min);
                combinedBounds.Encapsulate(spriteRenderer.bounds.max);
            }
        }

        for (int index = 0; index < promptBoundsColliders.Count; index++)
        {
            Collider2D collider = promptBoundsColliders[index];

            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(collider.bounds.min);
                combinedBounds.Encapsulate(collider.bounds.max);
            }
        }

        return hasBounds;
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactionKeyPromptRenderer != null && interactionKeyPromptRenderer.enabled != visible)
        {
            interactionKeyPromptRenderer.enabled = visible;
        }
    }

    private static void EnsureSharedInteractionKeyPromptSprite()
    {
        if (sharedInteractionKeyPromptSprite != null)
        {
            return;
        }

        const int textureSize = 14;
        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color clear = Color.clear;
        Color body = new(0.08f, 0.1f, 0.12f, 0.96f);
        Color edge = new(0.86f, 0.92f, 0.98f, 1f);
        Color glyph = new(0.92f, 0.96f, 1f, 1f);

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                bool outerBody = x >= 1 && x <= 12 && y >= 1 && y <= 12;
                bool innerBody = x >= 2 && x <= 11 && y >= 2 && y <= 11;
                texture.SetPixel(x, y, outerBody ? (innerBody ? body : edge) : clear);
            }
        }

        for (int y = 4; y <= 9; y++)
        {
            texture.SetPixel(4, y, glyph);
            texture.SetPixel(5, y, glyph);
        }

        for (int x = 4; x <= 8; x++)
        {
            texture.SetPixel(x, 9, glyph);
            texture.SetPixel(x, 7, glyph);
            texture.SetPixel(x, 4, glyph);
        }

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.Apply();
        sharedInteractionKeyPromptSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width);
    }
}
