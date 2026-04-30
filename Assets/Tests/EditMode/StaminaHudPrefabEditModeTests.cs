using System.IO;

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class StaminaHudPrefabEditModeTests
{
    private const string AuthoredGameplayHudPrefabPath = "Assets/Prefabs/RAuthoredGameplayHudCanvas.prefab";
    private const string RuntimeHudPrefabPath = "Assets/Prefabs/IRHudCanvas.prefab";
    private const string StaminaHudViewSourcePath = "Assets/Scripts/Rebuild/UI/StaminaHudView.cs";

    [Test]
    public void RAuthoredGameplayHudCanvas_UsesAuthoredStaminaGaugeSprites()
    {
        AssertStaminaGaugeSprites(
            AuthoredGameplayHudPrefabPath,
            "RAuthoredStaminaPanel",
            "RAuthoredStaminaGaugeBackground",
            "RAuthoredStaminaSegment_");
    }

    [Test]
    public void IRHudCanvas_UsesAuthoredStaminaGaugeSprites()
    {
        AssertStaminaGaugeSprites(
            RuntimeHudPrefabPath,
            "IRPanelRoot/IRStaminaPanel",
            "IRStaminaGaugeBackground",
            "IRStaminaSegment_");
    }

    [Test]
    public void StaminaHudView_CachesSegmentGaugeRenderState()
    {
        string source = File.ReadAllText(StaminaHudViewSourcePath);

        Assert.That(source, Does.Contain("hasRenderedSegmentGauge"));
        Assert.That(source, Does.Contain("InvalidateSegmentGaugeCache()"));
    }

    private static void AssertStaminaGaugeSprites(
        string prefabPath,
        string panelPath,
        string backgroundName,
        string segmentNamePrefix)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, $"{prefabPath} must exist.");

        Transform panel = prefab.transform.Find(panelPath);
        Assert.That(panel, Is.Not.Null, "Stamina panel must be authored inside the HUD prefab.");

        AssertImageHasSprite(panel, backgroundName);

        for (int index = 1; index <= 10; index++)
        {
            AssertImageHasSprite(panel, $"{segmentNamePrefix}{index:00}");
        }

        Component staminaHudView = panel.GetComponent("StaminaHudView");
        Assert.That(staminaHudView, Is.Not.Null, "StaminaHudView must remain on the authored stamina panel.");

        SerializedObject serializedView = new(staminaHudView);
        AssertSpriteReference(serializedView, "gaugeBackgroundImage");
        AssertSpriteReference(serializedView, "highSegmentSprite");
        AssertSpriteReference(serializedView, "midSegmentSprite");
        AssertSpriteReference(serializedView, "lowSegmentSprite");
        AssertSpriteReference(serializedView, "exhaustedSegmentSprite");
    }

    private static void AssertImageHasSprite(Transform parent, string childName)
    {
        Transform child = parent.Find(childName);
        Assert.That(child, Is.Not.Null, $"{childName} must be a child of the stamina panel.");

        Image image = child.GetComponent<Image>();
        Assert.That(image, Is.Not.Null, $"{childName} must have an Image component.");
        Assert.That(image.sprite, Is.Not.Null, $"{childName} must reference its authored PNG sprite.");
    }

    private static void AssertSpriteReference(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"{propertyName} serialized field must exist.");
        Assert.That(property.objectReferenceValue, Is.Not.Null, $"{propertyName} must reference an authored sprite/image.");
    }
}
