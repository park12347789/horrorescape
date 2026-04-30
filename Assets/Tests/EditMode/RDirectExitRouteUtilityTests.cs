using NUnit.Framework;
using System;
using System.Reflection;

public sealed class RDirectExitRouteUtilityTests
{
    [TestCase(1, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    [TestCase(5, true)]
    public void UsesElevatorPropDirectExit_MatchesAuthoredExitRoute(int floorNumber, bool expected)
    {
        Type utilityType = Type.GetType("RDirectExitRouteUtility, Assembly-CSharp");
        Assert.That(utilityType, Is.Not.Null, "RDirectExitRouteUtility type is missing.");

        MethodInfo method = utilityType.GetMethod(
            "UsesElevatorPropDirectExit",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "RDirectExitRouteUtility.UsesElevatorPropDirectExit is missing.");

        object result = method.Invoke(null, new object[] { floorNumber });
        Assert.That(result, Is.EqualTo(expected));
    }
}
