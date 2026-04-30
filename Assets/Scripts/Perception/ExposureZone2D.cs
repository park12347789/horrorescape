/*
 * File Role:
 * Defines areas that modify visibility exposure for targets inside them.
 *
 * Runtime Use:
 * Used to simulate brighter or darker spaces without relying on actual light sampling.
 *
 * Study Notes:
 * Helpful when learning how the project separated visual lighting from gameplay visibility values.
 */

using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExposureZone2D : MonoBehaviour
{
    private static readonly List<ExposureZone2D> ActiveZonesInternal = new();

    [SerializeField] private Vector2 size = new(2f, 2f);
    [SerializeField, Min(0f)] private float exposureMultiplier = 1f;
    [SerializeField] private Color debugColor = new(1f, 1f, 1f, 0.2f);

    public static IReadOnlyList<ExposureZone2D> ActiveZones => ActiveZonesInternal;

    public Vector2 Size => size;
    public float ExposureMultiplier => exposureMultiplier;
    public Color DebugColor => debugColor;

    private void OnEnable()
    {
        if (!ActiveZonesInternal.Contains(this))
        {
            ActiveZonesInternal.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveZonesInternal.Remove(this);
    }

    public void Configure(Vector2 newSize, float newExposureMultiplier, Color newDebugColor)
    {
        size = newSize;
        exposureMultiplier = newExposureMultiplier;
        debugColor = newDebugColor;
    }

    public bool Contains(Vector2 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        Vector2 halfExtents = size * 0.5f;

        return Mathf.Abs(localPosition.x) <= halfExtents.x
            && Mathf.Abs(localPosition.y) <= halfExtents.y;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = debugColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, size.y, 0.02f));
    }
}

