using System;
using System.Reflection;

using NUnit.Framework;

using UnityEngine;

public sealed class EnemyScaleAndAnimationEditModeTests
{
    private const string AutoSelectProfileFieldName = "autoSelectProfile";
    private const string NurseProfileResourcePath = "MainEscape/EnemyArt/GroundEnemy_Nurse";
    private const string SentryProfileResourcePath = "MainEscape/EnemyArt/GroundEnemy_Sentry";
    private const string StalkerProfileFieldName = "stalkerProfile";
    private const string VisualRootFieldName = "visualRoot";

    private readonly System.Collections.Generic.List<UnityEngine.Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = createdObjects.Count - 1; index >= 0; index--)
        {
            UnityEngine.Object createdObject = createdObjects[index];

            if (createdObject != null)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void GroundEnemyIdleLoop_UsesSingleIdleSprite_InsteadOfWalkFallback()
    {
        GameObject enemyObject = CreateGameObject("Stalker");
        enemyObject.AddComponent(MainEscapeReflectionTestHelper.RequireType("EnemyStateMachine"));
        Component bindings = MainEscapeReflectionTestHelper.AddComponent(enemyObject, "EnemyPrefabBindings");
        Component animationDriver = MainEscapeReflectionTestHelper.AddComponent(enemyObject, "EnemySpriteAnimationDriver");

        GameObject visualRootObject = CreateGameObject("VisualRoot");
        visualRootObject.transform.SetParent(enemyObject.transform, false);

        GameObject bodyObject = CreateGameObject("Body");
        bodyObject.transform.SetParent(visualRootObject.transform, false);
        SpriteRenderer bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();

        InvokeInstanceMethod(bindings, "AutoAssign");

        Sprite idleBackSprite = CreateSprite("IdleBack");
        Sprite walkBackSpriteA = CreateSprite("WalkBack0");
        Sprite walkBackSpriteB = CreateSprite("WalkBack1");
        ScriptableObject profile = CreateProfile(
            idleBack: new[] { idleBackSprite },
            walkBack: new[] { walkBackSpriteA, walkBackSpriteB });

        MainEscapeReflectionTestHelper.SetFieldValue(animationDriver, AutoSelectProfileFieldName, false);
        MainEscapeReflectionTestHelper.SetFieldValue(animationDriver, StalkerProfileFieldName, profile);

        InvokeInstanceMethod(animationDriver, "LateUpdate");

        Assert.That(bodyRenderer.sprite, Is.EqualTo(idleBackSprite));
    }

    [Test]
    public void SentryProfile_HasStaticIdleSprite_ForEveryFacingDirection()
    {
        Type profileType = MainEscapeReflectionTestHelper.RequireType("GroundEnemySpriteProfile");
        Type directionType = MainEscapeReflectionTestHelper.RequireType("EnemySpriteDirection");
        UnityEngine.Object profile = Resources.Load(SentryProfileResourcePath, profileType);

        Assert.That(profile, Is.Not.Null);
        Assert.That(FirstAssignedSprite(ReadIdleSprites(profile, directionType, "Front")), Is.Not.Null);
        Assert.That(FirstAssignedSprite(ReadIdleSprites(profile, directionType, "Back")), Is.Not.Null);
        Assert.That(FirstAssignedSprite(ReadIdleSprites(profile, directionType, "Left")), Is.Not.Null);
        Assert.That(FirstAssignedSprite(ReadIdleSprites(profile, directionType, "Right")), Is.Not.Null);
    }

    [Test]
    public void NurseProfile_UsesSixWalkFrames_ForEveryFacingDirection()
    {
        Type profileType = MainEscapeReflectionTestHelper.RequireType("GroundEnemySpriteProfile");
        Type directionType = MainEscapeReflectionTestHelper.RequireType("EnemySpriteDirection");
        UnityEngine.Object profile = Resources.Load(NurseProfileResourcePath, profileType);

        Assert.That(profile, Is.Not.Null);
        Assert.That(CountAssignedSprites(ReadWalkSprites(profile, directionType, "Front")), Is.EqualTo(6));
        Assert.That(CountAssignedSprites(ReadWalkSprites(profile, directionType, "Back")), Is.EqualTo(6));
        Assert.That(CountAssignedSprites(ReadWalkSprites(profile, directionType, "Left")), Is.EqualTo(6));
        Assert.That(CountAssignedSprites(ReadWalkSprites(profile, directionType, "Right")), Is.EqualTo(6));
        Assert.That(ReadFloatProperty(profile, "LoopFramesPerSecond"), Is.EqualTo(6f).Within(0.001f));
    }

    [Test]
    public void EnemyAnimationMovementCheck_UsesSpeed_NotFrameDistance()
    {
        Type driverType = MainEscapeReflectionTestHelper.RequireType("EnemySpriteAnimationDriver");
        MethodInfo method = driverType.GetMethod("IsMovingBySpeed", MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, "EnemySpriteAnimationDriver.IsMovingBySpeed() is missing.");

        bool isMovingAtHighFrameRate = method.Invoke(null, new object[] { 0.012f, 1f / 144f, 0.02f }) is bool moving && moving;

        Assert.That(isMovingAtHighFrameRate, Is.True);
    }

    [Test]
    public void GroundEnemyRuntimeScale_UsesSharedScale_ForStalkerAndSentry()
    {
        (Component stalkerBindings, Transform stalkerVisualRoot) = CreateBindingsWithVisualRoot("StalkerEnemy");
        (Component sentryBindings, Transform sentryVisualRoot) = CreateBindingsWithVisualRoot("SentryEnemy");
        Type runtimeFactoryType = MainEscapeReflectionTestHelper.RequireType("EnemyRuntimeFactory");

        InvokeStaticMethod(runtimeFactoryType, "ApplyArchetypeVisualScale", stalkerBindings);
        InvokeStaticMethod(runtimeFactoryType, "ApplyArchetypeVisualScale", sentryBindings);

        Assert.That(stalkerVisualRoot.localScale.x, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(stalkerVisualRoot.localScale.y, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(stalkerVisualRoot.localScale.x, Is.EqualTo(sentryVisualRoot.localScale.x).Within(0.0001f));
        Assert.That(stalkerVisualRoot.localScale.y, Is.EqualTo(sentryVisualRoot.localScale.y).Within(0.0001f));
    }

    private (Component bindings, Transform visualRoot) CreateBindingsWithVisualRoot(string enemyName)
    {
        GameObject enemyObject = CreateGameObject(enemyName);
        Component bindings = MainEscapeReflectionTestHelper.AddComponent(enemyObject, "EnemyPrefabBindings");
        GameObject visualRootObject = CreateGameObject($"{enemyName}_VisualRoot");
        visualRootObject.transform.SetParent(enemyObject.transform, false);
        visualRootObject.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

        MainEscapeReflectionTestHelper.SetFieldValue(bindings, VisualRootFieldName, visualRootObject.transform);
        return (bindings, visualRootObject.transform);
    }

    private ScriptableObject CreateProfile(Sprite[] idleBack, Sprite[] walkBack)
    {
        ScriptableObject profile = MainEscapeReflectionTestHelper.CreateScriptableObject("GroundEnemySpriteProfile");
        createdObjects.Add(profile);

        MainEscapeReflectionTestHelper.SetFieldValue(profile, "idleBack", idleBack);
        MainEscapeReflectionTestHelper.SetFieldValue(profile, "walkBack", walkBack);
        return profile;
    }

    private Sprite CreateSprite(string spriteName)
    {
        Texture2D texture = new(8, 8, TextureFormat.RGBA32, mipChain: false);
        texture.name = $"{spriteName}_Texture";
        texture.SetPixels(new Color[64]);
        texture.Apply();
        createdObjects.Add(texture);

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = spriteName;
        createdObjects.Add(sprite);
        return sprite;
    }

    private static Sprite FirstAssignedSprite(Sprite[] sprites)
    {
        if (sprites == null)
        {
            return null;
        }

        for (int index = 0; index < sprites.Length; index++)
        {
            if (sprites[index] != null)
            {
                return sprites[index];
            }
        }

        return null;
    }

    private static int CountAssignedSprites(Sprite[] sprites)
    {
        if (sprites == null)
        {
            return 0;
        }

        int count = 0;

        for (int index = 0; index < sprites.Length; index++)
        {
            if (sprites[index] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static Sprite[] ReadIdleSprites(UnityEngine.Object profile, Type directionType, string directionName)
    {
        MethodInfo method = profile.GetType().GetMethod("GetIdleSprites", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"{profile.GetType().Name}.GetIdleSprites() is missing.");

        object direction = Enum.Parse(directionType, directionName);
        return method.Invoke(profile, new[] { direction }) as Sprite[];
    }

    private static Sprite[] ReadWalkSprites(UnityEngine.Object profile, Type directionType, string directionName)
    {
        MethodInfo method = profile.GetType().GetMethod("GetWalkSprites", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"{profile.GetType().Name}.GetWalkSprites() is missing.");

        object direction = Enum.Parse(directionType, directionName);
        return method.Invoke(profile, new[] { direction }) as Sprite[];
    }

    private static float ReadFloatProperty(UnityEngine.Object owner, string propertyName)
    {
        PropertyInfo property = owner.GetType().GetProperty(propertyName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(property, Is.Not.Null, $"{owner.GetType().Name}.{propertyName} is missing.");
        return property.GetValue(owner) is float value ? value : 0f;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void InvokeInstanceMethod(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"{instance.GetType().Name}.{methodName}() is missing.");
        method.Invoke(instance, null);
    }

    private static void InvokeStaticMethod(Type type, string methodName, params object[] arguments)
    {
        MethodInfo method = type.GetMethod(methodName, MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName}() is missing.");
        method.Invoke(null, arguments);
    }
}
