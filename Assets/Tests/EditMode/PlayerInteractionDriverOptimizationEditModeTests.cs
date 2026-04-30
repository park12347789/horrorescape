using System;
using System.IO;
using System.Linq;
using System.Reflection;

using NUnit.Framework;

public sealed class PlayerInteractionDriverOptimizationEditModeTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string PlayerInteractionDriverSourcePath = "Assets/Scripts/Objectives/PlayerInteractionDriver.cs";

    [Test]
    public void PlayerInteractable_ExposesDistanceAwareCanInteractContract()
    {
        Type interactableType = FindTypeByName("PlayerInteractable2D");
        Assert.That(interactableType, Is.Not.Null, "PlayerInteractable2D type is missing.");

        MethodInfo method = interactableType.GetMethod("CanInteractAtDistance", InstanceFlags);
        Assert.That(method, Is.Not.Null, "PlayerInteractable2D should expose a distance-aware CanInteract hook.");
        Assert.That(method.IsVirtual, Is.True);
    }

    [Test]
    public void StatefulInteractables_OverrideDistanceAwareCanInteractContract()
    {
        string[] statefulInteractableTypeNames =
        {
            "DoorController",
            "FloorEscapeTransitionPoint",
            "FloorEscapeGoalPickup",
            "MainEscapeSelfContainedDoor",
            "MainEscapeEmergencyStairsPoint",
            "MainEscapeElevatorExitInteractable",
            "MainEscapeKeyGatePoint",
            "MedicalCabinetMedkitInteractable"
        };

        foreach (string typeName in statefulInteractableTypeNames)
        {
            Type type = FindTypeByName(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} type is missing.");

            MethodInfo canInteractAtDistance = type.GetMethod("CanInteractAtDistance", InstanceFlags);
            Assert.That(canInteractAtDistance, Is.Not.Null, $"{typeName} should expose CanInteractAtDistance.");
            Assert.That(canInteractAtDistance.DeclaringType, Is.EqualTo(type), $"{typeName} should preserve its state checks in the distance-aware path.");
        }
    }

    [Test]
    public void PlayerInteractionDriver_UsesDistanceAwareCanInteractDuringCandidateScan()
    {
        string source = File.ReadAllText(PlayerInteractionDriverSourcePath);

        Assert.That(source, Does.Contain("interactable.CanInteractAtDistance(playerController, distance)"));
        Assert.That(source, Does.Contain("currentCandidate.CanInteractAtDistance(playerController, distance)"));
        Assert.That(source, Does.Contain("private bool IsSceneLocalInteractable(PlayerInteractable2D interactable)"));
        Assert.That(source, Does.Contain("interactable.gameObject.scene == gameObject.scene"));
    }

    [Test]
    public void PlayerInteractionDriver_CachesPromptRendererConfiguration()
    {
        string source = File.ReadAllText(PlayerInteractionDriverSourcePath);

        Assert.That(source, Does.Contain("private bool interactionPromptRendererConfigured"));
        Assert.That(source, Does.Contain("&& interactionPromptRendererConfigured"));
        Assert.That(source, Does.Contain("interactionPromptRendererConfigured = true"));
        Assert.That(source, Does.Contain("interactionKeyPromptRenderer.enabled != visible"));
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
