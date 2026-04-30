using System;
using UnityEngine;

public static class AudioClipExcerptUtility
{
    public static AudioClip CreateLoudestExcerptClip(
        AudioClip source,
        float durationSeconds,
        string clipName,
        float searchStartNormalized = 0f,
        float searchEndNormalized = 1f,
        int strideFrames = 1024,
        float edgeFadeSeconds = 0f)
    {
        if (source == null || source.samples <= 0 || source.channels <= 0)
        {
            return null;
        }

        int totalFrames = source.samples;
        int channels = source.channels;
        int excerptFrames = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Max(0.01f, durationSeconds) * source.frequency),
            1,
            totalFrames);

        if (excerptFrames >= totalFrames)
        {
            return source;
        }

        int maxStartFrame = totalFrames - excerptFrames;
        int startFrameMin = Mathf.Clamp(
            Mathf.FloorToInt(Mathf.Clamp01(searchStartNormalized) * maxStartFrame),
            0,
            maxStartFrame);
        int startFrameMax = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Clamp01(searchEndNormalized) * maxStartFrame),
            startFrameMin,
            maxStartFrame);

        float[] analysisBuffer = new float[excerptFrames * channels];
        int bestStartFrame = startFrameMin;
        double bestRms = double.NegativeInfinity;
        int frameStep = Mathf.Max(1, strideFrames);

        for (int startFrame = startFrameMin; startFrame <= startFrameMax; startFrame += frameStep)
        {
            if (!TryGetData(source, analysisBuffer, startFrame))
            {
                return null;
            }

            double rms = ComputeRms(analysisBuffer);
            if (rms > bestRms)
            {
                bestRms = rms;
                bestStartFrame = startFrame;
            }
        }

        if (bestStartFrame != startFrameMax)
        {
            if (!TryGetData(source, analysisBuffer, startFrameMax))
            {
                return null;
            }

            double rms = ComputeRms(analysisBuffer);
            if (rms > bestRms)
            {
                bestStartFrame = startFrameMax;
            }
        }

        float[] excerptSamples = new float[excerptFrames * channels];
        if (!TryGetData(source, excerptSamples, bestStartFrame))
        {
            return null;
        }

        ApplyEdgeFade(excerptSamples, channels, source.frequency, edgeFadeSeconds);

        AudioClip excerpt = AudioClip.Create(
            clipName,
            excerptFrames,
            channels,
            source.frequency,
            false);
        excerpt.SetData(excerptSamples, 0);
        return excerpt;
    }

    private static bool TryGetData(AudioClip source, float[] samples, int offsetSamples)
    {
        if (source.GetData(samples, offsetSamples))
        {
            return true;
        }

        if (source.loadState == AudioDataLoadState.Unloaded && source.LoadAudioData())
        {
            return source.GetData(samples, offsetSamples);
        }

        return false;
    }

    private static double ComputeRms(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return 0d;
        }

        double sumSquares = 0d;

        for (int index = 0; index < samples.Length; index++)
        {
            double sample = samples[index];
            sumSquares += sample * sample;
        }

        return Math.Sqrt(sumSquares / samples.Length);
    }

    private static void ApplyEdgeFade(float[] samples, int channels, int sampleRate, float edgeFadeSeconds)
    {
        if (samples == null || samples.Length == 0 || channels <= 0 || sampleRate <= 0 || edgeFadeSeconds <= 0f)
        {
            return;
        }

        int totalFrames = samples.Length / channels;
        int fadeFrames = Mathf.Min(
            Mathf.RoundToInt(edgeFadeSeconds * sampleRate),
            totalFrames / 2);

        if (fadeFrames <= 0)
        {
            return;
        }

        for (int frame = 0; frame < fadeFrames; frame++)
        {
            float startGain = frame / Mathf.Max(1f, fadeFrames);
            float endGain = (fadeFrames - frame) / Mathf.Max(1f, fadeFrames);
            int startSampleOffset = frame * channels;
            int endSampleOffset = (totalFrames - fadeFrames + frame) * channels;

            for (int channel = 0; channel < channels; channel++)
            {
                samples[startSampleOffset + channel] *= startGain;
                samples[endSampleOffset + channel] *= endGain;
            }
        }
    }
}
