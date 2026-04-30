using System.IO;

using NUnit.Framework;

public sealed class VisionSensor2DOptimizationEditModeTests
{
    private const string VisionSensorSourcePath = "Assets/Scripts/Perception/VisionSensor2D.cs";

    [Test]
    public void GetReading_SamplesExposureOnlyAfterRangeAngleAndOcclusionPass()
    {
        string source = File.ReadAllText(VisionSensorSourcePath);
        int exposureDefaultIndex = source.IndexOf("float exposureMultiplier = 1f;", System.StringComparison.Ordinal);
        int visibilityGateIndex = source.IndexOf("if (inRange && inAngle && !isOccluded)", System.StringComparison.Ordinal);
        int exposureSampleIndex = source.IndexOf("exposureMultiplier = useExposureMultiplier ? target.GetExposureMultiplier() : 1f;", System.StringComparison.Ordinal);

        Assert.That(exposureDefaultIndex, Is.GreaterThanOrEqualTo(0), "VisionSensor2D should default exposure without sampling.");
        Assert.That(visibilityGateIndex, Is.GreaterThan(exposureDefaultIndex), "Visibility gate should follow the cheap distance/angle/occlusion checks.");
        Assert.That(exposureSampleIndex, Is.GreaterThan(visibilityGateIndex), "Exposure sampling should happen only inside the visibility gate.");
    }
}
