using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public sealed class ProfileProgressSnapshotEditModeTests
{
    [Test]
    public void ApplyEvents_DuplicatePermanentEvents_AreIdempotent()
    {
        ScriptableObject definition = MainEscapeReflectionTestHelper.CreateScriptableObject("PersistentProfileDefinition");
        SerializedObject definitionObject = new(definition);
        definitionObject.FindProperty("schemaVersion").intValue = 2;
        SetStringArray(definitionObject.FindProperty("noteCollectionIds"), "note_lobby", "note_5f");
        SetStringArray(definitionObject.FindProperty("achievementIds"), "clear_5f");
        SetStringArray(definitionObject.FindProperty("chapterUnlockIds"), "chapter_backrooms");
        SetStringArray(definitionObject.FindProperty("tutorialIds"), "inventory_intro");
        definitionObject.ApplyModifiedPropertiesWithoutUndo();

        Array progressEvents = CreateProgressEvents(
            Event("CollectedNote", "note_lobby"),
            Event("CollectedNote", "note_lobby"),
            Event("AchievementUnlocked", "clear_5f"),
            Event("AchievementUnlocked", "clear_5f"),
            Event("ChapterUnlocked", "chapter_backrooms"),
            Event("ChapterUnlocked", "chapter_backrooms"),
            Event("TutorialSeen", "inventory_intro"),
            Event("TutorialSeen", "inventory_intro"),
            Event("CollectedNote", "unknown_note"));

        Type utilityType = MainEscapeReflectionTestHelper.RequireType("ProfileProgressSnapshotUtility");
        MethodInfo createFromEvents = utilityType.GetMethod(
            "CreateFromEvents",
            MainEscapeReflectionTestHelper.StaticMemberFlags);
        MethodInfo applyEvents = utilityType.GetMethod(
            "ApplyEvents",
            MainEscapeReflectionTestHelper.StaticMemberFlags);
        Assert.That(createFromEvents, Is.Not.Null, "ProfileProgressSnapshotUtility.CreateFromEvents is missing.");
        Assert.That(applyEvents, Is.Not.Null, "ProfileProgressSnapshotUtility.ApplyEvents is missing.");

        object firstApply = createFromEvents.Invoke(null, new object[] { definition, progressEvents });
        object secondApply = applyEvents.Invoke(null, new[] { firstApply, definition, progressEvents });

        Assert.That(GetIntProperty(firstApply, "SchemaVersion"), Is.EqualTo(2));
        Assert.That(GetIntProperty(firstApply, "CollectedNoteCount"), Is.EqualTo(1));
        Assert.That(GetIntProperty(firstApply, "AchievementCount"), Is.EqualTo(1));
        Assert.That(GetIntProperty(firstApply, "ChapterUnlockCount"), Is.EqualTo(1));
        Assert.That(GetIntProperty(firstApply, "SeenTutorialCount"), Is.EqualTo(1));
        Assert.That(InvokeBool(firstApply, "HasCollectedNote", "note_lobby"), Is.True);
        Assert.That(InvokeBool(firstApply, "HasAchievement", "clear_5f"), Is.True);
        Assert.That(InvokeBool(firstApply, "HasChapterUnlock", "chapter_backrooms"), Is.True);
        Assert.That(InvokeBool(firstApply, "HasSeenTutorial", "inventory_intro"), Is.True);
        Assert.That(InvokeBool(firstApply, "HasCollectedNote", "unknown_note"), Is.False);
        CollectionAssert.AreEqual(
            GetStringArrayProperty(firstApply, "CollectedNoteIds"),
            GetStringArrayProperty(secondApply, "CollectedNoteIds"));
        CollectionAssert.AreEqual(
            GetStringArrayProperty(firstApply, "AchievementIds"),
            GetStringArrayProperty(secondApply, "AchievementIds"));
        CollectionAssert.AreEqual(
            GetStringArrayProperty(firstApply, "ChapterUnlockIds"),
            GetStringArrayProperty(secondApply, "ChapterUnlockIds"));
        CollectionAssert.AreEqual(
            GetStringArrayProperty(firstApply, "SeenTutorialIds"),
            GetStringArrayProperty(secondApply, "SeenTutorialIds"));

        UnityEngine.Object.DestroyImmediate(definition);
    }

    private static (string Kind, string Id) Event(string kind, string id)
    {
        return (kind, id);
    }

    private static Array CreateProgressEvents(params (string Kind, string Id)[] events)
    {
        Type eventType = MainEscapeReflectionTestHelper.RequireType("ProfileProgressEvent");
        Type eventKindType = MainEscapeReflectionTestHelper.RequireType("ProfileEventKind");
        FieldInfo kindField = eventType.GetField("kind", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        FieldInfo idField = eventType.GetField("id", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        FieldInfo quantityField = eventType.GetField("quantity", MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(kindField, Is.Not.Null, "ProfileProgressEvent.kind is missing.");
        Assert.That(idField, Is.Not.Null, "ProfileProgressEvent.id is missing.");
        Assert.That(quantityField, Is.Not.Null, "ProfileProgressEvent.quantity is missing.");

        Array progressEvents = Array.CreateInstance(eventType, events.Length);

        for (int index = 0; index < events.Length; index++)
        {
            object progressEvent = Activator.CreateInstance(eventType);
            kindField.SetValue(progressEvent, Enum.Parse(eventKindType, events[index].Kind));
            idField.SetValue(progressEvent, events[index].Id);
            quantityField.SetValue(progressEvent, 1);
            progressEvents.SetValue(progressEvent, index);
        }

        return progressEvents;
    }

    private static int GetIntProperty(object owner, string propertyName)
    {
        object value = MainEscapeReflectionTestHelper.GetPropertyValue(owner, propertyName);
        return value is int intValue ? intValue : 0;
    }

    private static string[] GetStringArrayProperty(object owner, string propertyName)
    {
        object value = MainEscapeReflectionTestHelper.GetPropertyValue(owner, propertyName);
        return value as string[] ?? Array.Empty<string>();
    }

    private static bool InvokeBool(object owner, string methodName, string id)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, MainEscapeReflectionTestHelper.InstanceMemberFlags);
        Assert.That(method, Is.Not.Null, $"{owner.GetType().Name}.{methodName} is missing.");
        object value = method.Invoke(owner, new object[] { id });
        return value is bool boolValue && boolValue;
    }

    private static void SetStringArray(SerializedProperty property, params string[] values)
    {
        property.arraySize = values.Length;

        for (int index = 0; index < values.Length; index++)
        {
            property.GetArrayElementAtIndex(index).stringValue = values[index];
        }
    }
}
