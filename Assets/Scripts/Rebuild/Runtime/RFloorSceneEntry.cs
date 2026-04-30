using System;

using UnityEngine;

[Serializable]
public struct RFloorSceneEntry
{
    [Min(1)]
    public int floorNumber;

    public string scenePath;

    public RFloorSceneEntry(int floorNumber, string scenePath)
    {
        this.floorNumber = Mathf.Max(1, floorNumber);
        this.scenePath = scenePath;
    }
}
