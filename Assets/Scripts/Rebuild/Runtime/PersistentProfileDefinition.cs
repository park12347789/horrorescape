using System;

using UnityEngine;

[CreateAssetMenu(
    fileName = "PersistentProfileDefinition",
    menuName = "Main Escape/Contracts/Persistent Profile Definition")]
public sealed class PersistentProfileDefinition : ScriptableObject
{
    [SerializeField, Min(1)] private int schemaVersion = 1;
    [SerializeField] private string[] noteCollectionIds = Array.Empty<string>();
    [SerializeField] private string[] achievementIds = Array.Empty<string>();
    [SerializeField] private string[] chapterUnlockIds = Array.Empty<string>();
    [SerializeField] private string[] tutorialIds = Array.Empty<string>();

    public int SchemaVersion => Mathf.Max(1, schemaVersion);
    public string[] NoteCollectionIds => noteCollectionIds ?? Array.Empty<string>();
    public string[] AchievementIds => achievementIds ?? Array.Empty<string>();
    public string[] ChapterUnlockIds => chapterUnlockIds ?? Array.Empty<string>();
    public string[] TutorialIds => tutorialIds ?? Array.Empty<string>();
}
