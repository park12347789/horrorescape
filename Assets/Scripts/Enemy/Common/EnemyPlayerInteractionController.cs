using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPlayerInteractionController : MonoBehaviour
{
    [SerializeField] private VisibilityTarget2D playerTarget;
    [SerializeField] private PlayerCaughtState playerCaughtState;
    [SerializeField] private PlayerHealth playerHealth;

    private Collider2D playerCollider;

    public VisibilityTarget2D PlayerTarget => playerTarget;
    public bool IsPlayerCaught
    {
        get
        {
            RefreshBindings();
            return playerCaughtState != null && playerCaughtState.IsCaught;
        }
    }

    public void Configure(VisibilityTarget2D configuredPlayerTarget)
    {
        playerTarget = configuredPlayerTarget;
        playerCaughtState = null;
        playerHealth = null;
        playerCollider = null;
        RefreshBindings();
    }

    public bool TryStrikePlayer(Transform attackerTransform, float attackDistance, string attackerName, int damageAmount = 1)
    {
        RefreshBindings();

        if (attackerTransform == null || playerTarget == null)
        {
            return false;
        }

        if (playerCaughtState != null && playerCaughtState.IsCaught)
        {
            return false;
        }

        float distanceToPlayer = CalculateDistanceToPlayer(attackerTransform);

        if (distanceToPlayer > attackDistance)
        {
            return false;
        }

        if (playerHealth != null)
        {
            return playerHealth.TryApplyDamage(damageAmount, attackerName, attackerTransform.position);
        }

        return playerCaughtState != null && playerCaughtState.TryCapture(attackerName);
    }

    private void RefreshBindings()
    {
        if (playerTarget == null)
        {
            return;
        }

        playerCaughtState ??= playerTarget.GetComponent<PlayerCaughtState>()
            ?? playerTarget.GetComponentInParent<PlayerCaughtState>()
            ?? playerTarget.GetComponentInChildren<PlayerCaughtState>(true);
        playerHealth ??= playerTarget.GetComponent<PlayerHealth>()
            ?? playerTarget.GetComponentInParent<PlayerHealth>()
            ?? playerTarget.GetComponentInChildren<PlayerHealth>(true);
        playerCollider ??= playerTarget.GetComponent<Collider2D>()
            ?? playerTarget.GetComponentInParent<Collider2D>()
            ?? playerTarget.GetComponentInChildren<Collider2D>(true);
    }

    private float CalculateDistanceToPlayer(Transform attackerTransform)
    {
        Vector2 attackOrigin = attackerTransform.position;

        if (playerCollider != null && playerCollider.enabled)
        {
            Vector2 closestPoint = playerCollider.ClosestPoint(attackOrigin);
            return Vector2.Distance(attackOrigin, closestPoint);
        }

        return Vector2.Distance(attackOrigin, playerTarget.transform.position);
    }
}
