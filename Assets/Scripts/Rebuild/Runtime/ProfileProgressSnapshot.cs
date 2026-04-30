using System;
using System.Collections.Generic;

[Serializable]
public sealed class ProfileProgressSnapshot
{
    private readonly int schemaVersion;
    private readonly string[] collectedNoteIds;
    private readonly string[] achievementIds;
    private readonly string[] chapterUnlockIds;
    private readonly string[] seenTutorialIds;

    public ProfileProgressSnapshot(
        int schemaVersion,
        IEnumerable<string> collectedNoteIds,
        IEnumerable<string> achievementIds,
        IEnumerable<string> chapterUnlockIds,
        IEnumerable<string> seenTutorialIds)
    {
        this.schemaVersion = Math.Max(1, schemaVersion);
        this.collectedNoteIds = NormalizeUnique(collectedNoteIds);
        this.achievementIds = NormalizeUnique(achievementIds);
        this.chapterUnlockIds = NormalizeUnique(chapterUnlockIds);
        this.seenTutorialIds = NormalizeUnique(seenTutorialIds);
    }

    public int SchemaVersion => schemaVersion;
    public string[] CollectedNoteIds => Clone(collectedNoteIds);
    public string[] AchievementIds => Clone(achievementIds);
    public string[] ChapterUnlockIds => Clone(chapterUnlockIds);
    public string[] SeenTutorialIds => Clone(seenTutorialIds);
    public int CollectedNoteCount => collectedNoteIds.Length;
    public int AchievementCount => achievementIds.Length;
    public int ChapterUnlockCount => chapterUnlockIds.Length;
    public int SeenTutorialCount => seenTutorialIds.Length;

    public bool HasCollectedNote(string id)
    {
        return ContainsId(collectedNoteIds, id);
    }

    public bool HasAchievement(string id)
    {
        return ContainsId(achievementIds, id);
    }

    public bool HasChapterUnlock(string id)
    {
        return ContainsId(chapterUnlockIds, id);
    }

    public bool HasSeenTutorial(string id)
    {
        return ContainsId(seenTutorialIds, id);
    }

    internal IReadOnlyList<string> CollectedNoteIdsInternal => collectedNoteIds;
    internal IReadOnlyList<string> AchievementIdsInternal => achievementIds;
    internal IReadOnlyList<string> ChapterUnlockIdsInternal => chapterUnlockIds;
    internal IReadOnlyList<string> SeenTutorialIdsInternal => seenTutorialIds;

    private static string[] NormalizeUnique(IEnumerable<string> source)
    {
        if (source == null)
        {
            return Array.Empty<string>();
        }

        List<string> results = new();
        HashSet<string> seen = new(StringComparer.Ordinal);

        foreach (string sourceId in source)
        {
            string normalizedId = NormalizeId(sourceId);

            if (normalizedId.Length == 0 || !seen.Add(normalizedId))
            {
                continue;
            }

            results.Add(normalizedId);
        }

        return results.Count == 0 ? Array.Empty<string>() : results.ToArray();
    }

    private static bool ContainsId(IReadOnlyList<string> ids, string id)
    {
        string normalizedId = NormalizeId(id);

        if (normalizedId.Length == 0)
        {
            return false;
        }

        for (int index = 0; index < ids.Count; index++)
        {
            if (string.Equals(ids[index], normalizedId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Clone(string[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<string>();
        }

        string[] clone = new string[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }

    internal static string NormalizeId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}

public static class ProfileProgressSnapshotUtility
{
    public static ProfileProgressSnapshot CreateEmpty(PersistentProfileDefinition definition)
    {
        int schemaVersion = definition?.SchemaVersion ?? 1;

        return new ProfileProgressSnapshot(
            schemaVersion,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    public static ProfileProgressSnapshot CreateFromEvents(
        PersistentProfileDefinition definition,
        ProfileProgressEvent[] events)
    {
        return ApplyEvents(CreateEmpty(definition), definition, events);
    }

    public static ProfileProgressSnapshot ApplyEvents(
        ProfileProgressSnapshot snapshot,
        PersistentProfileDefinition definition,
        ProfileProgressEvent[] events)
    {
        ProfileProgressSnapshot sourceSnapshot = snapshot ?? CreateEmpty(definition);

        if (events == null || events.Length == 0)
        {
            return sourceSnapshot;
        }

        ProfileProgressAccumulator accumulator = new(sourceSnapshot, definition);

        for (int index = 0; index < events.Length; index++)
        {
            accumulator.Apply(events[index]);
        }

        return accumulator.ToSnapshot();
    }

    private sealed class ProfileProgressAccumulator
    {
        private readonly int schemaVersion;
        private readonly List<string> collectedNoteIds;
        private readonly List<string> achievementIds;
        private readonly List<string> chapterUnlockIds;
        private readonly List<string> seenTutorialIds;
        private readonly HashSet<string> collectedNoteSet;
        private readonly HashSet<string> achievementSet;
        private readonly HashSet<string> chapterUnlockSet;
        private readonly HashSet<string> seenTutorialSet;
        private readonly HashSet<string> allowedNoteIds;
        private readonly HashSet<string> allowedAchievementIds;
        private readonly HashSet<string> allowedChapterUnlockIds;
        private readonly HashSet<string> allowedTutorialIds;

        public ProfileProgressAccumulator(
            ProfileProgressSnapshot snapshot,
            PersistentProfileDefinition definition)
        {
            schemaVersion = definition?.SchemaVersion ?? snapshot.SchemaVersion;
            collectedNoteIds = new List<string>(snapshot.CollectedNoteIdsInternal);
            achievementIds = new List<string>(snapshot.AchievementIdsInternal);
            chapterUnlockIds = new List<string>(snapshot.ChapterUnlockIdsInternal);
            seenTutorialIds = new List<string>(snapshot.SeenTutorialIdsInternal);
            collectedNoteSet = new HashSet<string>(collectedNoteIds, StringComparer.Ordinal);
            achievementSet = new HashSet<string>(achievementIds, StringComparer.Ordinal);
            chapterUnlockSet = new HashSet<string>(chapterUnlockIds, StringComparer.Ordinal);
            seenTutorialSet = new HashSet<string>(seenTutorialIds, StringComparer.Ordinal);
            allowedNoteIds = CreateAllowedSet(definition?.NoteCollectionIds);
            allowedAchievementIds = CreateAllowedSet(definition?.AchievementIds);
            allowedChapterUnlockIds = CreateAllowedSet(definition?.ChapterUnlockIds);
            allowedTutorialIds = CreateAllowedSet(definition?.TutorialIds);
        }

        public void Apply(ProfileProgressEvent progressEvent)
        {
            string id = ProfileProgressSnapshot.NormalizeId(progressEvent.id);

            if (id.Length == 0)
            {
                return;
            }

            switch (progressEvent.kind)
            {
                case ProfileEventKind.CollectedNote:
                    AddIfAllowed(id, allowedNoteIds, collectedNoteSet, collectedNoteIds);
                    break;
                case ProfileEventKind.AchievementUnlocked:
                    AddIfAllowed(id, allowedAchievementIds, achievementSet, achievementIds);
                    break;
                case ProfileEventKind.ChapterUnlocked:
                    AddIfAllowed(id, allowedChapterUnlockIds, chapterUnlockSet, chapterUnlockIds);
                    break;
                case ProfileEventKind.TutorialSeen:
                    AddIfAllowed(id, allowedTutorialIds, seenTutorialSet, seenTutorialIds);
                    break;
            }
        }

        public ProfileProgressSnapshot ToSnapshot()
        {
            return new ProfileProgressSnapshot(
                schemaVersion,
                collectedNoteIds,
                achievementIds,
                chapterUnlockIds,
                seenTutorialIds);
        }

        private static HashSet<string> CreateAllowedSet(IEnumerable<string> ids)
        {
            HashSet<string> allowedIds = new(StringComparer.Ordinal);

            if (ids == null)
            {
                return allowedIds;
            }

            foreach (string id in ids)
            {
                string normalizedId = ProfileProgressSnapshot.NormalizeId(id);

                if (normalizedId.Length > 0)
                {
                    allowedIds.Add(normalizedId);
                }
            }

            return allowedIds;
        }

        private static void AddIfAllowed(
            string id,
            HashSet<string> allowedIds,
            HashSet<string> existingIds,
            List<string> targetIds)
        {
            if (!allowedIds.Contains(id) || !existingIds.Add(id))
            {
                return;
            }

            targetIds.Add(id);
        }
    }
}
