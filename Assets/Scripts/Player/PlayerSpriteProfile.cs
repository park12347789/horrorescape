using System;
using UnityEngine;

[CreateAssetMenu(menuName = "HorrorStealth/Player/Sprite Profile", fileName = "PlayerSpriteProfile")]
public sealed class PlayerSpriteProfile : ScriptableObject
{
    [SerializeField] private Sprite[] idleFront = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] idleBack = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] idleSide = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] idleLeft = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] idleRight = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkFront = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkBack = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkSide = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkLeft = Array.Empty<Sprite>();
    [SerializeField] private Sprite[] walkRight = Array.Empty<Sprite>();
    [SerializeField, Min(1f)] private float loopFramesPerSecond = 8f;

    public float LoopFramesPerSecond => loopFramesPerSecond;
    public Sprite DefaultSprite => FirstSprite(idleFront)
        ?? FirstSprite(idleSide)
        ?? FirstSprite(idleLeft)
        ?? FirstSprite(idleRight)
        ?? FirstSprite(idleBack)
        ?? FirstSprite(walkFront)
        ?? FirstSprite(walkSide)
        ?? FirstSprite(walkLeft)
        ?? FirstSprite(walkRight)
        ?? FirstSprite(walkBack);

    public Sprite[] GetIdleSprites(EnemySpriteDirection direction)
    {
        return direction switch
        {
            EnemySpriteDirection.Front => idleFront,
            EnemySpriteDirection.Back => idleBack,
            EnemySpriteDirection.Left => FirstPopulated(idleLeft, idleSide),
            EnemySpriteDirection.Right => FirstPopulated(idleRight, idleSide),
            _ => idleFront
        };
    }

    public Sprite[] GetWalkSprites(EnemySpriteDirection direction)
    {
        return direction switch
        {
            EnemySpriteDirection.Front => walkFront,
            EnemySpriteDirection.Back => walkBack,
            EnemySpriteDirection.Left => FirstPopulated(walkLeft, walkSide),
            EnemySpriteDirection.Right => FirstPopulated(walkRight, walkSide),
            _ => walkFront
        };
    }

    public bool ShouldFlipX(EnemySpriteDirection direction)
    {
        return direction == EnemySpriteDirection.Right
            && !HasAnySprite(idleRight)
            && !HasAnySprite(walkRight);
    }

    private static Sprite FirstSprite(Sprite[] sprites)
    {
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private static Sprite[] FirstPopulated(Sprite[] primary, Sprite[] fallback)
    {
        return HasAnySprite(primary) ? primary : fallback;
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
