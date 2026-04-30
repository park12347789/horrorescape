/*
 * File Role:
 * Builds simple runtime enemy visuals and components without requiring prefabs.
 *
 * Runtime Use:
 * Creates the sprite hierarchy, vision helper objects, and state machine in one place.
 *
 * Study Notes:
 * Read this when you want to see the minimum object graph needed for a working enemy.
 */

using UnityEngine;

public static class EnemyRuntimeFactory
{
    private const float GroundEnemyVisualScaleMultiplier = 1.5f;

    public static VisibilityTarget2D EnsurePlayerTarget(WasdPlayerController playerController)
    {
        if (playerController == null)
        {
            return null;
        }

        VisibilityTarget2D playerTarget = playerController.GetComponent<VisibilityTarget2D>();

        if (playerTarget == null)
        {
            Debug.LogError(
                $"{nameof(EnemyRuntimeFactory)} requires an authored {nameof(VisibilityTarget2D)} on player '{playerController.name}'.",
                playerController);
        }

        return playerTarget;
    }

    public static EnemyStateMachine CreateEnemy(
        Transform parent,
        string enemyName,
        Vector3 worldPosition,
        GridMapService mapService,
        VisibilityTarget2D playerTarget,
        EnemyArchetype archetype,
        PlayerTrailRecorder playerTrail = null,
        params Vector3Int[] patrolRoute)
    {
        return CreateEnemy(parent, enemyName, worldPosition, mapService, playerTarget, archetype, playerTrail, Vector2.zero, patrolRoute);
    }

    public static EnemyStateMachine CreateEnemy(
        Transform parent,
        string enemyName,
        Vector3 worldPosition,
        GridMapService mapService,
        VisibilityTarget2D playerTarget,
        EnemyArchetype archetype,
        PlayerTrailRecorder playerTrail,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog,
        params Vector3Int[] patrolRoute)
    {
        return CreateEnemy(
            parent,
            enemyName,
            worldPosition,
            mapService,
            playerTarget,
            archetype,
            playerTrail,
            Vector2.zero,
            runtimePrefabCatalog,
            patrolRoute);
    }

    public static EnemyStateMachine CreateEnemy(
        Transform parent,
        string enemyName,
        Vector3 worldPosition,
        GridMapService mapService,
        VisibilityTarget2D playerTarget,
        EnemyArchetype archetype,
        PlayerTrailRecorder playerTrail,
        Vector2 initialFacing,
        params Vector3Int[] patrolRoute)
    {
        return CreateEnemy(
            parent,
            enemyName,
            worldPosition,
            mapService,
            playerTarget,
            archetype,
            playerTrail,
            initialFacing,
            runtimePrefabCatalog: null,
            patrolRoute);
    }

    public static EnemyStateMachine CreateEnemy(
        Transform parent,
        string enemyName,
        Vector3 worldPosition,
        GridMapService mapService,
        VisibilityTarget2D playerTarget,
        EnemyArchetype archetype,
        PlayerTrailRecorder playerTrail,
        Vector2 initialFacing,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog,
        params Vector3Int[] patrolRoute)
    {
        if (TryInstantiatePrefabEnemy(parent, enemyName, worldPosition, runtimePrefabCatalog, out EnemyPrefabBindings bindings))
        {
            EnemyStateMachine prefabStateMachine = bindings.StateMachine ?? bindings.GetComponent<EnemyStateMachine>();

            if (prefabStateMachine != null)
            {
                prefabStateMachine.Configure(
                    archetype,
                    mapService,
                    playerTarget,
                    bindings.VisualRoot,
                    bindings.VisionOrigin,
                    bindings.BodyRenderer,
                    bindings.FacingMarkerRenderer,
                    bindings.VisionVisualizer,
                    playerTrail,
                    patrolRoute);
                prefabStateMachine.SetFacingDirection(initialFacing);
                EnsureAnimationDriver(prefabStateMachine);
                return prefabStateMachine;
            }
        }

        Debug.LogError(
            $"Enemy runtime fallback creation is disabled. Assign a valid ground enemy prefab in {nameof(MainEscapeRuntimePrefabCatalog)} before spawning '{enemyName}'.",
            parent);
        return null;
    }

    private static bool TryInstantiatePrefabEnemy(
        Transform parent,
        string enemyName,
        Vector3 worldPosition,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog,
        out EnemyPrefabBindings bindings)
    {
        bindings = null;
        MainEscapeRuntimePrefabCatalog catalog = ResolveRuntimePrefabCatalog(parent, runtimePrefabCatalog);
        EnemyPrefabBindings enemyPrefab = catalog != null ? catalog.GroundEnemyPrefab : null;

        if (enemyPrefab == null)
        {
            return false;
        }

        bindings = UnityEngine.Object.Instantiate(enemyPrefab, parent);
        bindings.name = string.IsNullOrWhiteSpace(enemyName) ? "RuntimeEnemy" : enemyName;
        bindings.transform.position = worldPosition;
        bindings.transform.localRotation = Quaternion.identity;
        bindings.transform.localScale = Vector3.one;
        bindings.AutoAssign();
        ApplyArchetypeVisualScale(bindings);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(bindings.BodyRenderer);
        MainEscapeRuntimeVisualDefaults.EnsureSpriteMaterial(bindings.FacingMarkerRenderer);

        CircleCollider2D hitbox = bindings.Hitbox ?? bindings.GetComponent<CircleCollider2D>();

        if (hitbox == null)
        {
            Debug.LogError(
                $"{nameof(EnemyRuntimeFactory)} requires an authored {nameof(CircleCollider2D)} on enemy prefab '{bindings.name}'.",
                bindings);
            return false;
        }

        hitbox.isTrigger = true;
        hitbox.radius = 0.42f;
        hitbox.offset = Vector2.zero;

        return true;
    }

    private static MainEscapeRuntimePrefabCatalog ResolveRuntimePrefabCatalog(
        Transform parent,
        MainEscapeRuntimePrefabCatalog runtimePrefabCatalog)
    {
        if (runtimePrefabCatalog != null)
        {
            return runtimePrefabCatalog;
        }

        return parent != null && parent.gameObject.scene.IsValid()
            ? MainEscapeRuntimePrefabCatalog.LoadForScene(parent.gameObject.scene)
            : MainEscapeRuntimePrefabCatalog.LoadDefault();
    }

    private static void ApplyArchetypeVisualScale(EnemyPrefabBindings bindings)
    {
        if (bindings == null || bindings.VisualRoot == null)
        {
            return;
        }

        Vector3 visualScale = bindings.VisualRoot.localScale;
        // Ground enemy sprite sheets now share the same authored dimensions, so
        // keep all runtime instances on the common visual scale.
        float scaleMultiplier = GroundEnemyVisualScaleMultiplier;
        bindings.VisualRoot.localScale = new Vector3(
            visualScale.x * scaleMultiplier,
            visualScale.y * scaleMultiplier,
            visualScale.z);
    }

    private static void EnsureAnimationDriver(EnemyStateMachine stateMachine)
    {
        if (stateMachine == null)
        {
            return;
        }

        EnemySpriteAnimationDriver animationDriver = stateMachine.GetComponent<EnemySpriteAnimationDriver>();

        if (animationDriver == null)
        {
            Debug.LogError(
                $"{nameof(EnemyRuntimeFactory)} requires an authored {nameof(EnemySpriteAnimationDriver)} on enemy prefab '{stateMachine.name}'.",
                stateMachine);
            return;
        }

        animationDriver.ResolveReferences();
    }
}

