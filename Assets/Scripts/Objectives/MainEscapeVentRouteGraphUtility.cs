using System;
using System.Collections.Generic;

public readonly struct MainEscapeVentRouteConnection
{
    public MainEscapeVentRouteConnection(int fromIndex, int toIndex)
    {
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }

    public int FromIndex { get; }
    public int ToIndex { get; }
}

public static class MainEscapeVentRouteGraphUtility
{
    public static MainEscapeVentRouteConnection[] BuildConnections(MainEscapeVentRouteDefinition route)
    {
        if (!route.IsValid || route.Nodes == null || route.Nodes.Length < 2)
        {
            return Array.Empty<MainEscapeVentRouteConnection>();
        }

        List<MainEscapeVentRouteConnection> resolvedConnections = new();
        HashSet<long> seenConnections = new();

        if (route.UsesExplicitConnections
            && route.Connections != null
            && route.Connections.Length > 0)
        {
            for (int index = 0; index < route.Connections.Length; index++)
            {
                MainEscapeVentConnectionDefinition connection = route.Connections[index];

                if (!TryAddConnection(
                        connection.FromIndex,
                        connection.ToIndex,
                        route.Nodes.Length,
                        resolvedConnections,
                        seenConnections))
                {
                    continue;
                }
            }

            return resolvedConnections.ToArray();
        }

        for (int index = 1; index < route.Nodes.Length; index++)
        {
            TryAddConnection(
                index - 1,
                index,
                route.Nodes.Length,
                resolvedConnections,
                seenConnections);
        }

        if (route.LoopPath && route.Nodes.Length > 2)
        {
            TryAddConnection(
                0,
                route.Nodes.Length - 1,
                route.Nodes.Length,
                resolvedConnections,
                seenConnections);
        }

        return resolvedConnections.ToArray();
    }

    private static bool TryAddConnection(
        int fromIndex,
        int toIndex,
        int nodeCount,
        ICollection<MainEscapeVentRouteConnection> resolvedConnections,
        ISet<long> seenConnections)
    {
        if (fromIndex < 0
            || toIndex < 0
            || fromIndex >= nodeCount
            || toIndex >= nodeCount
            || fromIndex == toIndex)
        {
            return false;
        }

        int low = Math.Min(fromIndex, toIndex);
        int high = Math.Max(fromIndex, toIndex);
        long key = ((long)low << 32) | (uint)high;

        if (!seenConnections.Add(key))
        {
            return false;
        }

        resolvedConnections.Add(new MainEscapeVentRouteConnection(low, high));
        return true;
    }
}
