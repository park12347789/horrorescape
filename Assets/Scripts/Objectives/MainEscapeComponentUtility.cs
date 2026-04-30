using UnityEngine;

public static class MainEscapeComponentUtility
{
    public static TComponent GetOrAddComponent<TComponent>(GameObject target)
        where TComponent : Component
    {
        if (target == null)
        {
            return null;
        }

        TComponent component = target.GetComponent<TComponent>();
        return component != null ? component : target.AddComponent<TComponent>();
    }
}
