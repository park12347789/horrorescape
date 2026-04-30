using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class MainEscapeAudioLoudnessAuditTools
{
    private const string AudioRoot = "Assets/Resources/Audio";
    private const string ReportPath = "Docs/AudioLoudnessAudit.md";
    private const int AnalysisBlockFrames = 16384;
    private const double SilenceFloorDb = -80d;
    private const double OutlierThresholdDb = 4d;
    private const string GlassShatterResourcePath = "Audio/Sfx/GlassShatter3_GregSurr_CC0";
    private const string ZombieScreamResourcePath = "Audio/Sfx/alex_jauk-zombie-screaming-207590";
    private const string VentMovementResourcePath = "Audio/Sfx/MetalStairsFootSteps_sagamusix_CC0";
    private const string VentEmergeResourcePath = "Audio/Sfx/mechanical1_BMacZero_CC0";
    private const string LiveGroundEnemyPrefabPath = "Assets/Prefabs/Enemies/MainEscape/Ground/Enemy_GroundRuntime.prefab";
    private const string LiveVentEnemyPrefabPath = "Assets/Prefabs/Enemies/MainEscape/Vent/Enemy_CeilingVent.prefab";

    [MenuItem("Tools/Main Escape/Audio/Write Loudness Audit Report")]
    public static void WriteLoudnessAuditReport()
    {
        string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot });

        if (clipGuids.Length == 0)
        {
            Debug.LogWarning($"[MainEscapeAudioLoudnessAudit] No AudioClip assets were found under '{AudioRoot}'.");
            return;
        }

        List<AudioClipAuditRecord> records = new(clipGuids.Length);

        try
        {
            for (int index = 0; index < clipGuids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(clipGuids[index]);
                EditorUtility.DisplayProgressBar(
                    "Audio Loudness Audit",
                    $"Analyzing {assetPath}",
                    (index + 1f) / clipGuids.Length);

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;

                if (clip == null || importer == null)
                {
                    continue;
                }

                records.Add(AnalyzeClip(assetPath, clip, importer));
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        List<ConfiguredPlaybackRecord> playbackRecords = AnalyzeConfiguredPlayback();
        string reportContent = BuildReport(records, playbackRecords);
        string absoluteReportPath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteReportPath) ?? ".");
        File.WriteAllText(absoluteReportPath, reportContent, new UTF8Encoding(false));
        AssetDatabase.Refresh();

        Debug.Log(
            $"[MainEscapeAudioLoudnessAudit] Wrote {records.Count} clip measurements to '{absoluteReportPath}'.");
    }

    private static AudioClipAuditRecord AnalyzeClip(string assetPath, AudioClip clip, AudioImporter importer)
    {
        AudioImporterSampleSettings settings = importer.defaultSampleSettings;
        bool requestedLoad = false;

        if (!clip.preloadAudioData && clip.loadState != AudioDataLoadState.Loaded)
        {
            requestedLoad = clip.LoadAudioData();
        }

        AudioClipAuditRecord record = new()
        {
            AssetPath = assetPath,
            Category = ResolveCategory(assetPath),
            DurationSeconds = clip.length,
            ChannelCount = clip.channels,
            Frequency = clip.frequency,
            PeakDbFs = SilenceFloorDb,
            RmsDbFs = SilenceFloorDb,
            CrestDb = 0d,
            LoadType = settings.loadType.ToString(),
            CompressionFormat = settings.compressionFormat.ToString(),
            ForceToMono = importer.forceToMono,
            Normalize = ResolveNormalize(importer),
            PreloadAudioData = clip.preloadAudioData,
            LoadState = clip.loadState.ToString()
        };

        if (!TryMeasureClip(clip, out double peakDb, out double rmsDb, out string notes))
        {
            record.Notes = string.IsNullOrWhiteSpace(notes)
                ? "Sample analysis unavailable."
                : notes;
            return record;
        }

        record.PeakDbFs = peakDb;
        record.RmsDbFs = rmsDb;
        record.CrestDb = peakDb - rmsDb;
        record.MeasurementSucceeded = true;
        record.Notes = string.IsNullOrWhiteSpace(notes)
            ? (requestedLoad ? "Loaded on demand for analysis." : string.Empty)
            : notes;
        return record;
    }

    private static bool TryMeasureClip(AudioClip clip, out double peakDb, out double rmsDb, out string notes)
    {
        peakDb = SilenceFloorDb;
        rmsDb = SilenceFloorDb;
        notes = string.Empty;

        if (clip == null || clip.samples <= 0 || clip.channels <= 0)
        {
            notes = "Clip has no readable samples.";
            return false;
        }

        int channels = clip.channels;
        int totalFrames = clip.samples;
        float[] buffer = new float[Mathf.Min(AnalysisBlockFrames, totalFrames) * channels];
        double peak = 0d;
        double sumSquares = 0d;
        int analyzedSamples = 0;

        for (int frameOffset = 0; frameOffset < totalFrames; frameOffset += AnalysisBlockFrames)
        {
            int framesToRead = Mathf.Min(AnalysisBlockFrames, totalFrames - frameOffset);
            int sampleCount = framesToRead * channels;

            if (buffer.Length != sampleCount)
            {
                buffer = new float[sampleCount];
            }

            if (!clip.GetData(buffer, frameOffset))
            {
                notes = "Unity could not read sample data from this clip with the current import settings.";
                return false;
            }

            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                double absolute = Math.Abs(buffer[sampleIndex]);
                if (absolute > peak)
                {
                    peak = absolute;
                }

                sumSquares += absolute * absolute;
            }

            analyzedSamples += sampleCount;
        }

        if (analyzedSamples <= 0)
        {
            notes = "Clip returned zero readable samples.";
            return false;
        }

        double rms = Math.Sqrt(sumSquares / analyzedSamples);
        peakDb = ToDbFs(peak);
        rmsDb = ToDbFs(rms);
        return true;
    }

    private static List<ConfiguredPlaybackRecord> AnalyzeConfiguredPlayback()
    {
        List<ConfiguredPlaybackRecord> records = new();
        AudioClip zombieScreamClip = Resources.Load<AudioClip>(ZombieScreamResourcePath);
        AudioClip ventMovementClip = Resources.Load<AudioClip>(VentMovementResourcePath);
        AudioClip ventEmergeClip = Resources.Load<AudioClip>(VentEmergeResourcePath);
        AudioClip glassShatterClip = Resources.Load<AudioClip>(GlassShatterResourcePath);

        AddConfiguredPlaybackRecord(
            records,
            "Thrown Bottle Shatter",
            AudioClipExcerptUtility.CreateLoudestExcerptClip(
                glassShatterClip,
                1f,
                "Audit_GlassShatter_LoudestOneSecond"),
            0.5f,
            "PrototypeAudioManager bottle shatter loudest 1.00s excerpt",
            glassShatterClip == null ? "Glass shatter source clip is missing." : string.Empty);

        AddConfiguredPlaybackRecord(
            records,
            "Enemy Spotted Scream (Ground)",
            zombieScreamClip,
            ReadSerializedFloatFromPrefab<EnemyPlayerSpottedScreamAudio>(
                LiveGroundEnemyPrefabPath,
                "spottedVolume",
                0.32f),
            $"Prefab `{LiveGroundEnemyPrefabPath}`",
            zombieScreamClip == null ? "Spotted scream clip is missing." : string.Empty);

        AddConfiguredPlaybackRecord(
            records,
            "Enemy Spotted Scream (Vent)",
            zombieScreamClip,
            ReadSerializedFloatFromPrefab<EnemyPlayerSpottedScreamAudio>(
                LiveVentEnemyPrefabPath,
                "spottedVolume",
                0.32f),
            $"Prefab `{LiveVentEnemyPrefabPath}`",
            zombieScreamClip == null ? "Spotted scream clip is missing." : string.Empty);

        AddConfiguredPlaybackRecord(
            records,
            "Vent Crawl Loop",
            AudioClipExcerptUtility.CreateLoudestExcerptClip(
                ventMovementClip,
                1.15f,
                "Audit_Vent_CrawlLoop",
                searchStartNormalized: 0f,
                searchEndNormalized: 0.8f,
                strideFrames: 2048,
                edgeFadeSeconds: 0.01f),
            ReadSerializedFloatFromPrefab<VentEnemyAudioDriver>(
                LiveVentEnemyPrefabPath,
                "crawlPulseVolume",
                0.16f),
            $"Prefab `{LiveVentEnemyPrefabPath}` + loudest 1.15s loop excerpt from vent movement source",
            ventMovementClip == null ? "Vent movement source clip is missing." : string.Empty);

        AddConfiguredPlaybackRecord(
            records,
            "Vent Node Step",
            AudioClipExcerptUtility.CreateLoudestExcerptClip(
                ventMovementClip,
                0.34f,
                "Audit_Vent_NodeStep",
                searchStartNormalized: 0.35f,
                searchEndNormalized: 1f,
                strideFrames: 2048),
            ReadSerializedFloatFromPrefab<VentEnemyAudioDriver>(
                LiveVentEnemyPrefabPath,
                "nodeStepVolume",
                0.2f),
            $"Prefab `{LiveVentEnemyPrefabPath}` + loudest 0.34s excerpt from vent movement source",
            ventMovementClip == null ? "Vent movement source clip is missing." : string.Empty);

        AddConfiguredPlaybackRecord(
            records,
            "Vent Emerge",
            AudioClipExcerptUtility.CreateLoudestExcerptClip(
                ventEmergeClip,
                0.52f,
                "Audit_Vent_Emerge",
                searchStartNormalized: 0f,
                searchEndNormalized: 1f,
                strideFrames: 512),
            ReadSerializedFloatFromPrefab<VentEnemyAudioDriver>(
                LiveVentEnemyPrefabPath,
                "transitionVolume",
                0.3f),
            $"Prefab `{LiveVentEnemyPrefabPath}` + loudest 0.52s excerpt from vent emerge source",
            ventEmergeClip == null ? "Vent emerge source clip is missing." : string.Empty);

        return records;
    }

    private static void AddConfiguredPlaybackRecord(
        List<ConfiguredPlaybackRecord> records,
        string eventName,
        AudioClip clip,
        float baseVolume,
        string sourceDescription,
        string extraNotes)
    {
        ConfiguredPlaybackRecord record = new()
        {
            EventName = eventName,
            SourceDescription = sourceDescription,
            ClipName = clip != null ? clip.name : string.Empty,
            DurationSeconds = clip != null ? clip.length : 0f,
            BaseVolume = Mathf.Clamp01(baseVolume),
            Notes = extraNotes ?? string.Empty
        };

        if (clip == null)
        {
            if (string.IsNullOrWhiteSpace(record.Notes))
            {
                record.Notes = "Configured clip could not be created.";
            }

            records.Add(record);
            return;
        }

        if (!TryMeasureClip(clip, out double peakDb, out double rmsDb, out string analysisNotes))
        {
            record.Notes = CombineNotes(record.Notes, analysisNotes);
            records.Add(record);
            return;
        }

        double baseGainDb = ToGainDb(record.BaseVolume);
        record.ClipPeakDbFs = peakDb;
        record.ClipRmsDbFs = rmsDb;
        record.EffectivePeakDbFs = peakDb + baseGainDb;
        record.EffectiveRmsDbFs = rmsDb + baseGainDb;
        record.MeasurementSucceeded = true;
        record.Notes = CombineNotes(record.Notes, $"Base volume x{record.BaseVolume:0.00}.", analysisNotes);
        records.Add(record);
    }

    private static string BuildReport(List<AudioClipAuditRecord> records, List<ConfiguredPlaybackRecord> playbackRecords)
    {
        List<AudioClipAuditRecord> measuredRecords = records
            .Where(record => record.HasMeasurement)
            .OrderBy(record => record.Category)
            .ThenByDescending(record => record.RmsDbFs)
            .ThenBy(record => record.AssetPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        StringBuilder builder = new();
        builder.AppendLine("# Audio Loudness Audit");
        builder.AppendLine();
        builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();
        builder.AppendLine($"Audio root: `{AudioRoot}`");
        builder.AppendLine();
        builder.AppendLine("Use this report to compare imported clip loudness before tuning runtime volume multipliers.");
        builder.AppendLine("RMS is the best quick comparison for perceived level. Peak shows headroom. Crest is the difference between them.");
        builder.AppendLine("Configured playback snapshots below apply the current excerpt rules and base per-event volume fields, but they still exclude distance attenuation and shared global master/SFX mix.");
        builder.AppendLine();
        builder.AppendLine("Suggested workflow:");
        builder.AppendLine("- Compare clips within the same category first, especially `Sfx` against `Sfx` and `Music` against `Music`.");
        builder.AppendLine("- Treat clips more than about 4 dB away from their category median RMS as loudness outliers worth checking.");
        builder.AppendLine("- Use the configured playback section to balance authored event volumes after clip selection or excerpt logic changes.");
        builder.AppendLine();

        AppendSummary(builder, measuredRecords);
        AppendOutliers(builder, measuredRecords);
        AppendConfiguredPlaybackSummary(builder, playbackRecords);
        AppendConfiguredPlaybackOutliers(builder, playbackRecords);
        AppendConfiguredPlaybackTable(builder, playbackRecords);
        AppendTable(builder, records);

        return builder.ToString();
    }

    private static void AppendSummary(StringBuilder builder, List<AudioClipAuditRecord> records)
    {
        builder.AppendLine("## Summary");
        builder.AppendLine();

        if (records.Count == 0)
        {
            builder.AppendLine("No clips produced readable sample data.");
            builder.AppendLine();
            return;
        }

        foreach (IGrouping<string, AudioClipAuditRecord> categoryGroup in records.GroupBy(record => record.Category))
        {
            List<AudioClipAuditRecord> categoryRecords = categoryGroup
                .OrderBy(record => record.RmsDbFs)
                .ToList();
            AudioClipAuditRecord loudest = categoryRecords[^1];
            AudioClipAuditRecord quietest = categoryRecords[0];
            double medianRms = Median(categoryRecords.Select(record => record.RmsDbFs));

            builder.AppendLine($"### {categoryGroup.Key}");
            builder.AppendLine();
            builder.AppendLine($"- Count: {categoryRecords.Count}");
            builder.AppendLine($"- Median RMS: {FormatDb(medianRms)}");
            builder.AppendLine($"- Loudest RMS: {FormatDb(loudest.RmsDbFs)} at `{loudest.AssetPath}`");
            builder.AppendLine($"- Quietest RMS: {FormatDb(quietest.RmsDbFs)} at `{quietest.AssetPath}`");
            builder.AppendLine();
        }
    }

    private static void AppendOutliers(StringBuilder builder, List<AudioClipAuditRecord> records)
    {
        builder.AppendLine("## RMS Outliers");
        builder.AppendLine();

        bool wroteCategory = false;

        foreach (IGrouping<string, AudioClipAuditRecord> categoryGroup in records.GroupBy(record => record.Category))
        {
            List<AudioClipAuditRecord> categoryRecords = categoryGroup.ToList();
            double medianRms = Median(categoryRecords.Select(record => record.RmsDbFs));
            List<AudioClipAuditRecord> outliers = categoryRecords
                .Where(record => Math.Abs(record.RmsDbFs - medianRms) >= OutlierThresholdDb)
                .OrderByDescending(record => Math.Abs(record.RmsDbFs - medianRms))
                .ToList();

            if (outliers.Count == 0)
            {
                continue;
            }

            wroteCategory = true;
            builder.AppendLine($"### {categoryGroup.Key}");
            builder.AppendLine();

            foreach (AudioClipAuditRecord outlier in outliers)
            {
                double delta = outlier.RmsDbFs - medianRms;
                builder.AppendLine($"- `{outlier.AssetPath}`: RMS {FormatDb(outlier.RmsDbFs)} ({FormatSignedDb(delta)} vs median)");
            }

            builder.AppendLine();
        }

        if (!wroteCategory)
        {
            builder.AppendLine("No category outliers exceeded the default 4 dB RMS threshold.");
            builder.AppendLine();
        }
    }

    private static void AppendConfiguredPlaybackSummary(StringBuilder builder, List<ConfiguredPlaybackRecord> records)
    {
        builder.AppendLine("## Configured Playback Summary");
        builder.AppendLine();

        List<ConfiguredPlaybackRecord> measuredRecords = records
            .Where(record => record.HasMeasurement)
            .OrderBy(record => record.EffectiveRmsDbFs)
            .ToList();

        if (measuredRecords.Count == 0)
        {
            builder.AppendLine("No configured playback snapshots produced readable sample data.");
            builder.AppendLine();
            return;
        }

        ConfiguredPlaybackRecord quietest = measuredRecords[0];
        ConfiguredPlaybackRecord loudest = measuredRecords[^1];
        double medianRms = Median(measuredRecords.Select(record => record.EffectiveRmsDbFs));

        builder.AppendLine($"- Count: {measuredRecords.Count}");
        builder.AppendLine($"- Median effective RMS: {FormatDb(medianRms)}");
        builder.AppendLine($"- Loudest effective RMS: {FormatDb(loudest.EffectiveRmsDbFs)} at `{loudest.EventName}`");
        builder.AppendLine($"- Quietest effective RMS: {FormatDb(quietest.EffectiveRmsDbFs)} at `{quietest.EventName}`");
        builder.AppendLine();
    }

    private static void AppendConfiguredPlaybackOutliers(StringBuilder builder, List<ConfiguredPlaybackRecord> records)
    {
        builder.AppendLine("## Configured Playback Outliers");
        builder.AppendLine();

        List<ConfiguredPlaybackRecord> measuredRecords = records
            .Where(record => record.HasMeasurement)
            .ToList();

        if (measuredRecords.Count == 0)
        {
            builder.AppendLine("No configured playback snapshots were measurable.");
            builder.AppendLine();
            return;
        }

        double medianRms = Median(measuredRecords.Select(record => record.EffectiveRmsDbFs));
        List<ConfiguredPlaybackRecord> outliers = measuredRecords
            .Where(record => Math.Abs(record.EffectiveRmsDbFs - medianRms) >= OutlierThresholdDb)
            .OrderByDescending(record => Math.Abs(record.EffectiveRmsDbFs - medianRms))
            .ToList();

        if (outliers.Count == 0)
        {
            builder.AppendLine("No configured playback outliers exceeded the default 4 dB RMS threshold.");
            builder.AppendLine();
            return;
        }

        foreach (ConfiguredPlaybackRecord outlier in outliers)
        {
            double delta = outlier.EffectiveRmsDbFs - medianRms;
            builder.AppendLine($"- `{outlier.EventName}`: effective RMS {FormatDb(outlier.EffectiveRmsDbFs)} ({FormatSignedDb(delta)} vs median)");
        }

        builder.AppendLine();
    }

    private static void AppendConfiguredPlaybackTable(StringBuilder builder, List<ConfiguredPlaybackRecord> records)
    {
        builder.AppendLine("## Configured Playback Table");
        builder.AppendLine();
        builder.AppendLine("| Event | Source | Clip | Dur (s) | Base Vol | Clip RMS dBFS | Effective RMS dBFS | Clip Peak dBFS | Effective Peak dBFS | Notes |");
        builder.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |");

        foreach (ConfiguredPlaybackRecord record in records
                     .OrderByDescending(record => record.HasMeasurement)
                     .ThenByDescending(record => record.EffectiveRmsDbFs)
                     .ThenBy(record => record.EventName, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("| ");
            builder.Append(EscapeTable(record.EventName));
            builder.Append(" | ");
            builder.Append(EscapeTable(record.SourceDescription));
            builder.Append(" | ");
            builder.Append(EscapeTable(record.ClipName));
            builder.Append(" | ");
            builder.Append(FormatNumber(record.DurationSeconds));
            builder.Append(" | ");
            builder.Append(record.BaseVolume.ToString("0.00", CultureInfo.InvariantCulture));
            builder.Append(" | ");
            builder.Append(record.HasMeasurement ? FormatDb(record.ClipRmsDbFs) : "n/a");
            builder.Append(" | ");
            builder.Append(record.HasMeasurement ? FormatDb(record.EffectiveRmsDbFs) : "n/a");
            builder.Append(" | ");
            builder.Append(record.HasMeasurement ? FormatDb(record.ClipPeakDbFs) : "n/a");
            builder.Append(" | ");
            builder.Append(record.HasMeasurement ? FormatDb(record.EffectivePeakDbFs) : "n/a");
            builder.Append(" | ");
            builder.Append(EscapeTable(record.Notes));
            builder.AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static void AppendTable(StringBuilder builder, List<AudioClipAuditRecord> records)
    {
        builder.AppendLine("## Clip Table");
        builder.AppendLine();
        builder.AppendLine("| Category | Clip | Dur (s) | Ch | Peak dBFS | RMS dBFS | Crest dB | Load | Compression | Mono | Normalize | Preload | Notes |");
        builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | --- | --- | --- | --- | --- |");

        foreach (AudioClipAuditRecord record in records
                     .OrderBy(record => record.Category)
                     .ThenByDescending(record => record.HasMeasurement)
                     .ThenByDescending(record => record.RmsDbFs)
                     .ThenBy(record => record.AssetPath, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("| ");
            builder.Append(EscapeTable(record.Category));
            builder.Append(" | ");
            builder.Append(EscapeTable(record.AssetPath));
            builder.Append(" | ");
            builder.Append(FormatNumber(record.DurationSeconds));
            builder.Append(" | ");
            builder.Append(record.ChannelCount.ToString(CultureInfo.InvariantCulture));
            builder.Append(" | ");
            builder.Append(record.HasMeasurement ? FormatDb(record.PeakDbFs) : "n/a");
            builder.Append(" | ");
            builder.Append(record.HasMeasurement ? FormatDb(record.RmsDbFs) : "n/a");
            builder.Append(" | ");
            builder.Append(record.HasMeasurement ? FormatDb(record.CrestDb) : "n/a");
            builder.Append(" | ");
            builder.Append(EscapeTable(record.LoadType));
            builder.Append(" | ");
            builder.Append(EscapeTable(record.CompressionFormat));
            builder.Append(" | ");
            builder.Append(record.ForceToMono ? "yes" : "no");
            builder.Append(" | ");
            builder.Append(record.Normalize ? "yes" : "no");
            builder.Append(" | ");
            builder.Append(record.PreloadAudioData ? "yes" : "no");
            builder.Append(" | ");
            builder.Append(EscapeTable(record.Notes));
            builder.AppendLine(" |");
        }

        builder.AppendLine();
    }

    private static string ResolveCategory(string assetPath)
    {
        if (assetPath.IndexOf("/Music/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Music";
        }

        if (assetPath.IndexOf("/Sfx/", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "Sfx";
        }

        return "Other";
    }

    private static double Median(IEnumerable<double> values)
    {
        double[] ordered = values.OrderBy(value => value).ToArray();

        if (ordered.Length == 0)
        {
            return SilenceFloorDb;
        }

        int middleIndex = ordered.Length / 2;
        return ordered.Length % 2 == 0
            ? (ordered[middleIndex - 1] + ordered[middleIndex]) * 0.5d
            : ordered[middleIndex];
    }

    private static double ToDbFs(double value)
    {
        if (value <= 0d)
        {
            return SilenceFloorDb;
        }

        return Math.Max(SilenceFloorDb, 20d * Math.Log10(value));
    }

    private static double ToGainDb(float scalar)
    {
        return scalar <= 0f
            ? SilenceFloorDb
            : 20d * Math.Log10(Mathf.Clamp01(scalar));
    }

    private static bool ResolveNormalize(AudioImporter importer)
    {
        SerializedObject serializedImporter = new(importer);
        SerializedProperty normalizeProperty = serializedImporter.FindProperty("m_Normalize")
            ?? serializedImporter.FindProperty("normalize");

        return normalizeProperty != null
            && normalizeProperty.propertyType == SerializedPropertyType.Boolean
            && normalizeProperty.boolValue;
    }

    private static float ReadSerializedFloatFromPrefab<TComponent>(string prefabPath, string fieldName, float fallback)
        where TComponent : Component
    {
        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        if (prefab == null)
        {
            return fallback;
        }

        TComponent component = prefab.GetComponent<TComponent>();
        if (component == null)
        {
            return fallback;
        }

        FieldInfo field = typeof(TComponent).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        return field != null && field.GetValue(component) is float value
            ? value
            : fallback;
    }

    private static string CombineNotes(params string[] notes)
    {
        return string.Join(
            " ",
            notes.Where(note => !string.IsNullOrWhiteSpace(note)).Select(note => note.Trim()));
    }

    private static string FormatDb(double value)
    {
        return $"{value.ToString("0.0", CultureInfo.InvariantCulture)} dB";
    }

    private static string FormatSignedDb(double value)
    {
        return $"{value.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)} dB";
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    private static string EscapeTable(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }

    private struct AudioClipAuditRecord
    {
        public string AssetPath;
        public string Category;
        public float DurationSeconds;
        public int ChannelCount;
        public int Frequency;
        public double PeakDbFs;
        public double RmsDbFs;
        public double CrestDb;
        public string LoadType;
        public string CompressionFormat;
        public bool ForceToMono;
        public bool Normalize;
        public bool PreloadAudioData;
        public string LoadState;
        public string Notes;
        public bool MeasurementSucceeded;

        public bool HasMeasurement => MeasurementSucceeded;
    }

    private struct ConfiguredPlaybackRecord
    {
        public string EventName;
        public string SourceDescription;
        public string ClipName;
        public float DurationSeconds;
        public float BaseVolume;
        public double ClipPeakDbFs;
        public double ClipRmsDbFs;
        public double EffectivePeakDbFs;
        public double EffectiveRmsDbFs;
        public string Notes;
        public bool MeasurementSucceeded;

        public bool HasMeasurement => MeasurementSucceeded;
    }
}
