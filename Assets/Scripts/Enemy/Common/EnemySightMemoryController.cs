using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemySightMemoryController : MonoBehaviour
{
    public readonly struct Observation
    {
        public Observation(bool canSeePlayer, bool justSpottedPlayer, bool justLostPlayer, Vector3Int lastSeenCell, float lastSeenTime, Vector2 facingDirection)
        {
            CanSeePlayer = canSeePlayer;
            JustSpottedPlayer = justSpottedPlayer;
            JustLostPlayer = justLostPlayer;
            LastSeenCell = lastSeenCell;
            LastSeenTime = lastSeenTime;
            FacingDirection = facingDirection;
        }

        public bool CanSeePlayer { get; }
        public bool JustSpottedPlayer { get; }
        public bool JustLostPlayer { get; }
        public Vector3Int LastSeenCell { get; }
        public float LastSeenTime { get; }
        public Vector2 FacingDirection { get; }
    }

    [SerializeField] private VisibilityTarget2D playerTarget;
    [SerializeField] private GridMapService mapService;
    [SerializeField] private bool hadVisualOnPlayer;
    [SerializeField] private float lastSeenTime = float.NegativeInfinity;
    [SerializeField] private Vector3Int lastSeenCell;

    public bool HasLastSeenTarget => lastSeenTime > float.NegativeInfinity;
    public float LastSeenTime => lastSeenTime;
    public Vector3Int LastSeenCell => lastSeenCell;

    public void Configure(VisibilityTarget2D configuredPlayerTarget, GridMapService configuredMapService)
    {
        playerTarget = configuredPlayerTarget;
        mapService = configuredMapService;
        hadVisualOnPlayer = false;
        lastSeenTime = float.NegativeInfinity;
        lastSeenCell = configuredMapService != null ? configuredMapService.ResolveNearestWalkableCell(transform.position, 1, true) : Vector3Int.zero;
    }

    public Observation Observe(VisionSensor2D.VisionReading reading, Vector3 observerWorldPosition, bool useAimPointDirection = false)
    {
        if (playerTarget == null || mapService == null)
        {
            return new Observation(false, false, false, lastSeenCell, lastSeenTime, Vector2.zero);
        }

        bool justSpottedPlayer = false;
        bool justLostPlayer = false;
        Vector2 facingDirection = Vector2.zero;

        if (reading.CanSee)
        {
            lastSeenTime = Time.time;
            lastSeenCell = mapService.ResolveNearestWalkableCell(playerTarget.transform.position, 4, true);
            justSpottedPlayer = !hadVisualOnPlayer;
            hadVisualOnPlayer = true;

            Vector2 targetPoint = useAimPointDirection
                ? playerTarget.AimPoint
                : (Vector2)playerTarget.transform.position;
            facingDirection = targetPoint - (Vector2)observerWorldPosition;

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                facingDirection.Normalize();
            }
            else
            {
                facingDirection = Vector2.zero;
            }
        }
        else
        {
            justLostPlayer = hadVisualOnPlayer;
            hadVisualOnPlayer = false;
        }

        return new Observation(reading.CanSee, justSpottedPlayer, justLostPlayer, lastSeenCell, lastSeenTime, facingDirection);
    }

    public void SetLastSeenCell(Vector3Int cell)
    {
        lastSeenCell = mapService != null ? mapService.ResolveNearestWalkableCell(cell, 2, true) : cell;
    }

    public void ClearLastSeenTarget()
    {
        hadVisualOnPlayer = false;
        lastSeenTime = float.NegativeInfinity;
    }
}
