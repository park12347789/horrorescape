using System;
using System.Reflection;
using NUnit.Framework;

public sealed class MainEscapeSceneIdentityUtilityTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [Test]
    public void CanonicalFloorHelpers_ResolveKnownSceneNames()
    {
        Assert.That(InvokeTryGetCanonicalFloorNumber("RMainScene_5F", out int startFloorNumber), Is.True);
        Assert.That(startFloorNumber, Is.EqualTo(InvokeGetCanonicalStartFloorNumber()));

        Assert.That(InvokeTryGetCanonicalFloorNumber("RMainScene_2F", out int floorNumber), Is.True);
        Assert.That(floorNumber, Is.EqualTo(2));

        Assert.That(InvokeTryGetCanonicalFloorNumber("RMainScene_6F", out _), Is.False);
    }

    [Test]
    public void AuthoredSceneRecognition_AlwaysIncludesCanonicalLiveLoopScenes()
    {
        Assert.That(InvokeStaticBool("IsAuthoredSceneName", "RMainEscape_Lobby"), Is.True);
        Assert.That(InvokeStaticBool("IsAuthoredSceneName", "Assets/Scenes/RMainEscape_Lobby.unity"), Is.True);
        Assert.That(InvokeStaticBool("IsAuthoredSceneName", "RMainScene_5F"), Is.True);
        Assert.That(InvokeTryGetCanonicalFloorNumber("Assets/Scenes/RMainScene_5F.unity", out int floorNumber), Is.True);
        Assert.That(floorNumber, Is.EqualTo(5));
        Assert.That(InvokeStaticBool("IsProtectedPrefabOverrideSceneName", "RMainScene_1F"), Is.True);
        Assert.That(InvokeStaticBool("IsProtectedPrefabOverrideSceneName", "Assets/Scenes/RMainScene_1F.unity"), Is.True);
    }

    [Test]
    public void CanonicalTutorialHelpers_ResolveTutorialSceneName()
    {
        Assert.That(InvokeStaticString("GetCanonicalTutorialScenePath"), Is.EqualTo("Assets/Scenes/RMainEscape_tuto.unity"));
        Assert.That(InvokeStaticString("GetCanonicalElevatorTransitionScenePath"), Is.EqualTo("Assets/Scenes/RMainEscape_ElevatorTransition.unity"));
        Assert.That(InvokeStaticBool("MatchesCanonicalTutorialSceneName", "RMainEscape_tuto"), Is.True);
        Assert.That(InvokeStaticBool("MatchesCanonicalTutorialSceneName", "RMainScene_5F"), Is.False);
    }

    [Test]
    public void CanonicalRouteDefaults_ExposeSingleHospitalSceneChain()
    {
        Assert.That(InvokeStaticString("GetCanonicalStartFloorScenePath"), Is.EqualTo("Assets/Scenes/RMainScene_5F.unity"));
        Assert.That(InvokeStaticStringArray("GetCanonicalGameplayScenePaths"), Is.EqualTo(new[]
        {
            "Assets/Scenes/RMainScene_5F.unity",
            "Assets/Scenes/RMainScene_4F.unity",
            "Assets/Scenes/RMainScene_3F.unity",
            "Assets/Scenes/RMainScene_2F.unity",
            "Assets/Scenes/RMainScene_1F.unity"
        }));
        Assert.That(InvokeStaticStringArray("GetCanonicalAuthoredSceneNames"), Is.EqualTo(new[]
        {
            "RMainScene_5F",
            "RMainScene_4F",
            "RMainScene_3F",
            "RMainScene_2F",
            "RMainScene_1F"
        }));
    }

    [Test]
    public void ScenePathMatching_AcceptsFullUnityAssetPaths()
    {
        Assert.That(
            InvokeStaticBool("MatchesScenePath", "Assets/Scenes/RMainScene_5F.unity", "Assets/Scenes/RMainScene_5F.unity"),
            Is.True);
        Assert.That(
            InvokeStaticBool("MatchesScenePath", "Assets/Scenes/RMainScene_5F.unity", "Assets/Scenes/RMainScene_4F.unity"),
            Is.False);
    }

    private static bool InvokeTryGetCanonicalFloorNumber(string sceneName, out int floorNumber)
    {
        MethodInfo method = ResolveType().GetMethod("TryGetCanonicalFloorNumber", StaticFlags, null, new[] { typeof(string), typeof(int).MakeByRefType() }, null);
        Assert.That(method, Is.Not.Null, "MainEscapeSceneIdentityUtility.TryGetCanonicalFloorNumber(string, out int) is missing.");

        object[] arguments = { sceneName, 0 };
        bool result = method.Invoke(null, arguments) is bool value && value;
        floorNumber = Convert.ToInt32(arguments[1]);
        return result;
    }

    private static int InvokeGetCanonicalStartFloorNumber()
    {
        MethodInfo method = ResolveType().GetMethod("GetCanonicalStartFloorNumber", StaticFlags);
        Assert.That(method, Is.Not.Null, "MainEscapeSceneIdentityUtility.GetCanonicalStartFloorNumber is missing.");
        return Convert.ToInt32(method.Invoke(null, null));
    }

    private static bool InvokeStaticBool(string methodName, string argument)
    {
        MethodInfo method = ResolveType().GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"MainEscapeSceneIdentityUtility.{methodName} is missing.");
        return method.Invoke(null, new object[] { argument }) is bool value && value;
    }

    private static bool InvokeStaticBool(string methodName, string firstArgument, string secondArgument)
    {
        MethodInfo method = ResolveType().GetMethod(
            methodName,
            StaticFlags,
            null,
            new[] { typeof(string), typeof(string) },
            null);
        Assert.That(method, Is.Not.Null, $"MainEscapeSceneIdentityUtility.{methodName}(string, string) is missing.");
        return method.Invoke(null, new object[] { firstArgument, secondArgument }) is bool value && value;
    }

    private static string InvokeStaticString(string methodName)
    {
        MethodInfo method = ResolveType().GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"MainEscapeSceneIdentityUtility.{methodName} is missing.");
        return method.Invoke(null, null) as string;
    }

    private static string[] InvokeStaticStringArray(string methodName)
    {
        MethodInfo method = ResolveType().GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"MainEscapeSceneIdentityUtility.{methodName} is missing.");
        return method.Invoke(null, null) as string[];
    }

    private static Type ResolveType()
    {
        Type resolved = Type.GetType("MainEscapeSceneIdentityUtility, Assembly-CSharp");
        Assert.That(resolved, Is.Not.Null, "MainEscapeSceneIdentityUtility type is missing.");
        return resolved;
    }
}
