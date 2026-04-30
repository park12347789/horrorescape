using System;
using System.Reflection;

using NUnit.Framework;

public sealed class RRunHudPresenterTests
{
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    [Test]
    public void BuildHeader_UsesFloorNumberDuringRun()
    {
        object context = CreateContext(
            currentFloorNumber: 4,
            escaped: false,
            currentFloorGateUnlocked: false,
            hasAuthoredGateKey: false,
            statusMessage: "Dropped to 4F.",
            healthSummary: "HP 3/3",
            flashlightChargeNormalized: 0.5f,
            storedBatteryCount: 2,
            quickSummary: "1 Bottle x2",
            interactionSummary: "Open gate");

        Assert.That(InvokePresenter("BuildHeader", context), Is.EqualTo("Emergency Route - 4F"));
    }

    [Test]
    public void BuildBody_ShowsKeyGateObjectiveBeforeUnlock()
    {
        object context = CreateContext(
            currentFloorNumber: 3,
            escaped: false,
            currentFloorGateUnlocked: false,
            hasAuthoredGateKey: true,
            statusMessage: "Iron gate ahead.",
            healthSummary: "HP 2/3",
            flashlightChargeNormalized: 0.75f,
            storedBatteryCount: 1,
            quickSummary: "1 Bottle x1",
            interactionSummary: "Use key");

        string body = InvokePresenter("BuildBody", context);

        Assert.That(body, Does.Contain("Use the key on the iron gate."));
        Assert.That(body, Does.Contain("Battery 75%  Cells 1"));
        Assert.That(body, Does.Contain("Use key"));
    }

    [Test]
    public void BuildBody_ShowsEscapeSummaryAfterClear()
    {
        object context = CreateContext(
            currentFloorNumber: 1,
            escaped: true,
            currentFloorGateUnlocked: false,
            hasAuthoredGateKey: false,
            statusMessage: "Escape successful.",
            healthSummary: "HP 3/3",
            flashlightChargeNormalized: 1f,
            storedBatteryCount: 0,
            quickSummary: "1 Bottle x0",
            interactionSummary: "No interactable in reach");

        string body = InvokePresenter("BuildBody", context);

        Assert.That(body, Does.Contain("Street exit reached."));
        Assert.That(body, Does.Not.Contain("Objective"));
    }

    private static object CreateContext(
        int currentFloorNumber,
        bool escaped,
        bool currentFloorGateUnlocked,
        bool hasAuthoredGateKey,
        string statusMessage,
        string healthSummary,
        float flashlightChargeNormalized,
        int storedBatteryCount,
        string quickSummary,
        string interactionSummary)
    {
        Type contextType = FindTypeByName("RRunHudContext");
        Assert.That(contextType, Is.Not.Null, "RRunHudContext type is missing.");

        return Activator.CreateInstance(
            contextType,
            currentFloorNumber,
            escaped,
            currentFloorGateUnlocked,
            hasAuthoredGateKey,
            statusMessage,
            healthSummary,
            flashlightChargeNormalized,
            storedBatteryCount,
            quickSummary,
            interactionSummary);
    }

    private static string InvokePresenter(string methodName, object context)
    {
        Type presenterType = FindTypeByName("RRunHudPresenter");
        Assert.That(presenterType, Is.Not.Null, "RRunHudPresenter type is missing.");

        MethodInfo method = presenterType.GetMethod(methodName, StaticFlags);
        Assert.That(method, Is.Not.Null, $"{presenterType.Name}.{methodName} is missing.");
        return method.Invoke(null, new[] { context }) as string;
    }

    private static Type FindTypeByName(string typeName)
    {
        Type resolved = Type.GetType(typeName + ", Assembly-CSharp");

        if (resolved != null)
        {
            return resolved;
        }

        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int index = 0; index < assemblies.Length; index++)
        {
            Type found = assemblies[index].GetType(typeName, false);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
