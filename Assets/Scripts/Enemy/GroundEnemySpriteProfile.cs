using UnityEngine;

[CreateAssetMenu(menuName = "HorrorStealth/Enemy/Ground Sprite Profile", fileName = "GroundEnemySpriteProfile")]
public sealed class GroundEnemySpriteProfile : ScriptableObject
{
    [SerializeField] private Sprite[] idleFront = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] idleBack = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] idleLeft = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] idleRight = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkFront = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkBack = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkLeft = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkRight = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] attack = System.Array.Empty<Sprite>();
    [SerializeField] private bool preserveSpriteColors;
    [SerializeField, Min(1f)] private float loopFramesPerSecond = 8f;
    [SerializeField, Min(1f)] private float attackFramesPerSecond = 12f;

    public float LoopFramesPerSecond => loopFramesPerSecond;
    public float AttackFramesPerSecond => attackFramesPerSecond;
    public bool PreserveSpriteColors => preserveSpriteColors;
    public Sprite DefaultSprite => FirstSprite(idleFront)
        ?? FirstSprite(idleBack)
        ?? FirstSprite(idleLeft)
        ?? FirstSprite(idleRight)
        ?? FirstSprite(walkFront)
        ?? FirstSprite(walkBack)
        ?? FirstSprite(walkLeft)
        ?? FirstSprite(walkRight)
        ?? FirstSprite(attack);

    public Sprite[] GetIdleSprites(EnemySpriteDirection direction)
    {
        return direction switch
        {
            EnemySpriteDirection.Front => idleFront,
            EnemySpriteDirection.Back => idleBack,
            EnemySpriteDirection.Left => idleLeft,
            EnemySpriteDirection.Right => idleRight,
            _ => idleFront
        };
    }

    public Sprite[] GetIdleSpritesOrWalkFallback(EnemySpriteDirection direction)
    {
        Sprite[] idleSprites = GetIdleSprites(direction);

        if (HasAnySprite(idleSprites))
        {
            return idleSprites;
        }

        Sprite[] walkSprites = GetWalkSprites(direction);
        return HasAnySprite(walkSprites) ? walkSprites : idleSprites;
    }

    public Sprite[] GetWalkSprites(EnemySpriteDirection direction)
    {
        return direction switch
        {
            EnemySpriteDirection.Front => walkFront,
            EnemySpriteDirection.Back => walkBack,
            EnemySpriteDirection.Left => walkLeft,
            EnemySpriteDirection.Right => walkRight,
            _ => walkFront
        };
    }

    public Sprite[] GetAttackSprites() => attack;

    private static Sprite FirstSprite(Sprite[] sprites)
    {
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private static bool HasAnySprite(Sprite[] sprites)
    {
        if (sprites == null)
        {
            return false;
        }

        for (int index = 0; index < sprites.Length; index++)
        {
            if (sprites[index] != null)
            {
                return true;
            }
        }

        return false;
    }
}
