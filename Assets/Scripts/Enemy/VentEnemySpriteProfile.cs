using UnityEngine;

[CreateAssetMenu(menuName = "HorrorStealth/Enemy/Vent Sprite Profile", fileName = "VentEnemySpriteProfile")]
public sealed class VentEnemySpriteProfile : ScriptableObject
{
    [SerializeField] private Sprite[] emerge = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] crawlFront = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] crawlBack = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] crawlLeft = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] crawlRight = System.Array.Empty<Sprite>();
    [SerializeField] private Sprite[] attack = System.Array.Empty<Sprite>();
    [SerializeField, Min(1f)] private float emergeFramesPerSecond = 12f;
    [SerializeField, Min(1f)] private float crawlFramesPerSecond = 10f;
    [SerializeField, Min(1f)] private float attackFramesPerSecond = 12f;

    public float EmergeFramesPerSecond => emergeFramesPerSecond;
    public float CrawlFramesPerSecond => crawlFramesPerSecond;
    public float AttackFramesPerSecond => attackFramesPerSecond;
    public Sprite SettleSprite => FirstSprite(crawlFront)
        ?? FirstSprite(crawlLeft)
        ?? FirstSprite(crawlRight)
        ?? FirstSprite(crawlBack)
        ?? LastSprite(emerge);

    public Sprite[] EmergeSprites => emerge;
    public Sprite[] AttackSprites => attack;

    public Sprite[] GetCrawlSprites(EnemySpriteDirection direction)
    {
        return direction switch
        {
            EnemySpriteDirection.Front => crawlFront,
            EnemySpriteDirection.Back => crawlBack,
            EnemySpriteDirection.Left => crawlLeft,
            EnemySpriteDirection.Right => crawlRight,
            _ => crawlFront
        };
    }

    private static Sprite FirstSprite(Sprite[] sprites)
    {
        return sprites != null && sprites.Length > 0 ? sprites[0] : null;
    }

    private static Sprite LastSprite(Sprite[] sprites)
    {
        return sprites != null && sprites.Length > 0 ? sprites[sprites.Length - 1] : null;
    }
}
