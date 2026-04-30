using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(WasdPlayerController))]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PlayerSpriteAnimationDriver : MonoBehaviour
{
    private const string PlayerProfileResourcePath = "MainEscape/PlayerArt/PlayerSpriteProfile";

    [SerializeField] private WasdPlayerController playerController;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private PlayerSpriteProfile profile;
    [SerializeField, Min(0f)] private float movementThreshold = 0.02f;

    public void ResolveReferences()
    {
        playerController ??= GetComponent<WasdPlayerController>();
        playerBody ??= GetComponent<Rigidbody2D>();
        bodyRenderer ??= GetComponent<SpriteRenderer>();
        profile ??= Resources.Load<PlayerSpriteProfile>(PlayerProfileResourcePath);
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyDefaultSprite();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyDefaultSprite();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        if (playerController == null || bodyRenderer == null || profile == null)
        {
            return;
        }

        Vector2 movement = playerBody != null ? playerBody.linearVelocity : playerController.Velocity;
        bool isMoving = movement.sqrMagnitude > movementThreshold * movementThreshold;
        Vector2 facing = isMoving ? movement : playerController.AimDirection;
        EnemySpriteDirection direction = EnemySpriteDirectionUtility.FromFacing(facing);
        Sprite[] sequence = GetSequence(direction, isMoving);
        Sprite sprite = EnemySpriteDirectionUtility.ResolveFrame(sequence, profile.LoopFramesPerSecond, profile.DefaultSprite);

        if (sprite != null)
        {
            bodyRenderer.sprite = sprite;
        }

        bodyRenderer.flipX = profile.ShouldFlipX(direction);
    }

    private void ApplyDefaultSprite()
    {
        if (bodyRenderer == null || profile == null || profile.DefaultSprite == null)
        {
            return;
        }

        bodyRenderer.sprite = profile.DefaultSprite;
        bodyRenderer.flipX = false;
    }

    private Sprite[] GetSequence(EnemySpriteDirection direction, bool isMoving)
    {
        return isMoving
            ? profile.GetWalkSprites(direction)
            : profile.GetIdleSprites(direction);
    }
}
