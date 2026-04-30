using System;

using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerDamageFeedback : MonoBehaviour
{
    private const string ImpactSfxResourcePath = "Audio/Sfx/qubodupImpactMeat02";

    [SerializeField, Min(0.05f)] private float overlayDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] private float overlayAlpha = 0.22f;
    [SerializeField, Range(0f, 1f)] private float impactVolume = 0.35f;
    [SerializeField] private Color overlayColor = new(0.78f, 0.06f, 0.08f, 1f);
    [SerializeField] private Color burstColor = new(1f, 0.58f, 0.28f, 1f);
    [SerializeField] private Sprite[] hitFxFrames = Array.Empty<Sprite>();
    [SerializeField] private Color hitFxTint = Color.white;
    [SerializeField, Min(0.05f)] private float hitFxDuration = 0.28f;
    [SerializeField] private Vector2 hitFxStartScale = new(0.035f, 0.035f);
    [SerializeField] private Vector2 hitFxEndScale = new(0.075f, 0.075f);
    [SerializeField, Min(0f)] private float hitFxDriftDistance = 0.18f;
    [SerializeField] private int hitFxSortingOrder = 168;

    private AudioSource audioSource;
    [SerializeField] private Transform audioRoot;
    private AudioClip impactClip;
    private Texture2D overlayTexture;
    private float flashUntilUnscaledTime;

    public void PlayHit(Vector2 sourceWorldPosition)
    {
        Vector2 direction = (Vector2)transform.position - sourceWorldPosition;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.up;
        }
        else
        {
            direction.Normalize();
        }

        GameObject burstObject = new("PlayerDamageImpactBurst");
        burstObject.transform.SetParent(transform, false);
        burstObject.transform.localPosition = direction * hitFxDriftDistance;

        PlayerDamageImpactBurst burst = burstObject.AddComponent<PlayerDamageImpactBurst>();

        if (HasAnyHitFxFrame())
        {
            burst.Configure(
                direction,
                hitFxTint,
                hitFxFrames,
                hitFxDuration,
                hitFxStartScale,
                hitFxEndScale,
                hitFxDriftDistance,
                hitFxSortingOrder);
        }
        else
        {
            burst.Configure(direction, burstColor);
        }

        EnsureAudio();
        audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
        audioSource.PlayOneShot(impactClip, impactVolume);
        flashUntilUnscaledTime = Mathf.Max(flashUntilUnscaledTime, Time.unscaledTime + overlayDuration);
    }

    private void OnGUI()
    {
        if (Time.unscaledTime >= flashUntilUnscaledTime)
        {
            return;
        }

        overlayTexture ??= CreateTexture(new Color(1f, 1f, 1f, 1f));
        float remaining = Mathf.Clamp01((flashUntilUnscaledTime - Time.unscaledTime) / overlayDuration);
        GUI.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, overlayAlpha * remaining);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), overlayTexture, ScaleMode.StretchToFill);
        GUI.color = Color.white;
    }

    private void OnValidate()
    {
        overlayDuration = Mathf.Max(0.05f, overlayDuration);
        hitFxDuration = Mathf.Max(0.05f, hitFxDuration);
        hitFxStartScale = SanitizeScale(hitFxStartScale, new Vector2(0.035f, 0.035f));
        hitFxEndScale = SanitizeScale(hitFxEndScale, new Vector2(0.075f, 0.075f));
    }

    private void EnsureAudio()
    {
        if (audioSource == null)
        {
            if (audioRoot == null)
            {
                GameObject audioObject = new("DamageFeedbackAudio");
                audioObject.transform.SetParent(transform, false);
                audioRoot = audioObject.transform;
            }

            audioSource = audioRoot.GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = audioRoot.gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.dopplerLevel = 0f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.reverbZoneMix = 0f;
        }

        impactClip ??= Resources.Load<AudioClip>(ImpactSfxResourcePath) ?? CreateImpactClip();
    }

    private bool HasAnyHitFxFrame()
    {
        if (hitFxFrames == null)
        {
            return false;
        }

        for (int index = 0; index < hitFxFrames.Length; index++)
        {
            if (hitFxFrames[index] != null)
            {
                return true;
            }
        }

        return false;
    }

    private static AudioClip CreateImpactClip()
    {
        const int sampleRate = 22050;
        float duration = 0.2f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int index = 0; index < sampleCount; index++)
        {
            float time = index / (float)sampleRate;
            float normalizedTime = index / Mathf.Max(1f, sampleCount - 1f);
            float envelope = Mathf.Pow(1f - normalizedTime, 2.8f);
            float thump = Mathf.Sin(Mathf.PI * 2f * 86f * time) * 0.48f;
            float crack = HashNoise(index, 11) * Mathf.Exp(-13f * normalizedTime) * 0.52f;
            float sting = Mathf.Sin(Mathf.PI * 2f * 512f * time + 0.3f) * Mathf.Exp(-9f * normalizedTime) * 0.14f;
            samples[index] = Mathf.Clamp((thump + crack + sting) * envelope * 0.7f, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("Prototype_PlayerHit", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    private static Vector2 SanitizeScale(Vector2 value, Vector2 fallback)
    {
        if (value.x <= 0f || value.y <= 0f)
        {
            return fallback;
        }

        return value;
    }

    private static float HashNoise(int index, int seed)
    {
        float value = Mathf.Sin((index + 1) * 12.9898f + seed * 78.233f) * 43758.5453f;
        return Mathf.Repeat(value, 1f) * 2f - 1f;
    }
}
