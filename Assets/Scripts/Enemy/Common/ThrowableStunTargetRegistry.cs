using System.Collections.Generic;

public static class ThrowableStunTargetRegistry
{
    private static readonly List<IThrowableStunTarget> targets = new();

    public static IReadOnlyList<IThrowableStunTarget> Targets => targets;

    public static void Register(IThrowableStunTarget target)
    {
        if (target == null || targets.Contains(target))
        {
            return;
        }

        targets.Add(target);
    }

    public static void Unregister(IThrowableStunTarget target)
    {
        if (target == null)
        {
            return;
        }

        targets.Remove(target);
    }
}
