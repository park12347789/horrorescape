#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

[CustomEditor(typeof(MainEscapeVentRouteAuthoring))]
public sealed class MainEscapeVentRouteAuthoringEditor : Editor
{
    private SerializedProperty loopPathProperty;

    private void OnEnable()
    {
        loopPathProperty = serializedObject.FindProperty("loopPath");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        MainEscapeVentRouteAuthoring route = (MainEscapeVentRouteAuthoring)target;
        bool hasExplicitConnections = MainEscapeVentRouteSceneTools.HasExplicitConnections(route);
        bool sequentialPreviewEnabled = MainEscapeVentRouteSceneTools.IsSequentialPreviewEnabled(route.transform);
        string routeModeDescription = hasExplicitConnections
            ? "Runtime preview is active. Yellow lines are explicit links, green lines are inferred from Upper/Lower/Corridor node names and positions. Loop Path is ignored while explicit links exist."
            : sequentialPreviewEnabled
                ? "Sequential mode is active. Child order creates a one-stroke route, and Loop Path optionally closes it back to the first node."
                : "Runtime preview is active. Green lines are inferred from Upper/Lower/Corridor node names and positions; child order will not auto-rebuild the route.";

        EditorGUILayout.PropertyField(loopPathProperty);
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Select VentRoute to edit directly in Scene view. Drag node handles to move them, use Link Mode to click two nodes and connect/disconnect them, and use the buttons below to add or tidy nodes.",
            MessageType.Info);
        EditorGUILayout.HelpBox(routeModeDescription, hasExplicitConnections ? MessageType.Warning : MessageType.None);

        if (GUILayout.Button(MainEscapeVentRouteSceneTools.LinkModeEnabled ? "Exit Link Mode" : "Enter Link Mode"))
        {
            MainEscapeVentRouteSceneTools.ToggleLinkMode(route);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Corridor Node"))
            {
                MainEscapeVentRouteSceneTools.CreateNode(route, MainEscapeVentNodeType.Corridor);
            }

            if (GUILayout.Button("Add Room Node"))
            {
                MainEscapeVentRouteSceneTools.CreateNode(route, MainEscapeVentNodeType.Room);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Convert Order To Links"))
            {
                MainEscapeVentRouteSceneTools.ConvertSequentialOrderToLinks(route);
            }

            if (GUILayout.Button("Clear Links"))
            {
                MainEscapeVentRouteSceneTools.ClearAllConnections(route);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save Preview Lines As Links"))
            {
                MainEscapeVentRouteSceneTools.SaveRuntimePreviewAsLinks(route);
            }

            if (GUILayout.Button("Check Vent Network"))
            {
                MainEscapeVentRouteSceneTools.ShowVentNetworkReport(route);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Snap All To Tiles"))
            {
                MainEscapeVentRouteSceneTools.SnapAllNodes(route);
            }

            if (GUILayout.Button("Rename Nodes"))
            {
                MainEscapeVentRouteSceneTools.RenameNodes(route.transform);
            }
        }

        if (GUILayout.Button("Select All Nodes"))
        {
            MainEscapeVentRouteSceneTools.SelectAllNodes(route);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        MainEscapeVentRouteSceneTools.DrawInteractiveSceneGui((MainEscapeVentRouteAuthoring)target);
    }

    [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
    private static void DrawVentRouteGizmos(MainEscapeVentRouteAuthoring route, GizmoType gizmoType)
    {
        MainEscapeVentRouteSceneTools.DrawRouteGizmos(route, gizmoType);
    }
}

[CustomEditor(typeof(MainEscapeVentNodeAuthoring))]
public sealed class MainEscapeVentNodeAuthoringEditor : Editor
{
    private SerializedProperty nodeTypeProperty;

    private void OnEnable()
    {
        nodeTypeProperty = serializedObject.FindProperty("nodeType");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        MainEscapeVentNodeAuthoring node = (MainEscapeVentNodeAuthoring)target;
        Transform nodeTransform = node.transform;
        Transform routeRoot = nodeTransform.parent;
        bool hasExplicitConnections = routeRoot != null && MainEscapeVentRouteSceneTools.HasExplicitConnections(routeRoot);
        bool sequentialPreviewEnabled = MainEscapeVentRouteSceneTools.IsSequentialPreviewEnabled(routeRoot);

        EditorGUILayout.PropertyField(nodeTypeProperty);
        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            hasExplicitConnections
                ? $"This node currently has {node.ConnectedNodes.Count} explicit link(s). Use Link Mode in the VentRoute inspector or the buttons below to connect and disconnect nodes."
                : sequentialPreviewEnabled
                    ? "This route is still using child order as a one-stroke path. Use Convert Order To Links on the VentRoute if you want branching connections."
                    : "This scene is in manual vent mode. Child order does not rebuild links here; create explicit links if you want this node connected.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = routeRoot != null && nodeTransform.GetSiblingIndex() > 0;

            if (GUILayout.Button("Move Earlier"))
            {
                MainEscapeVentRouteSceneTools.MoveNode(nodeTransform, -1);
            }

            GUI.enabled = routeRoot != null && nodeTransform.GetSiblingIndex() < routeRoot.childCount - 1;

            if (GUILayout.Button("Move Later"))
            {
                MainEscapeVentRouteSceneTools.MoveNode(nodeTransform, 1);
            }

            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Duplicate After"))
            {
                MainEscapeVentRouteSceneTools.DuplicateNode(nodeTransform);
            }

            if (GUILayout.Button("Snap To Tile"))
            {
                MainEscapeVentRouteSceneTools.SnapNode(nodeTransform);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Start Link From This"))
            {
                MainEscapeVentRouteSceneTools.BeginLinkFrom(node);
            }

            if (GUILayout.Button("Clear Links From This"))
            {
                MainEscapeVentRouteSceneTools.DisconnectAll(node);
            }
        }

        if (MainEscapeVentRouteSceneTools.TryGetSelectedNodePair(routeRoot, out MainEscapeVentNodeAuthoring first, out MainEscapeVentNodeAuthoring second))
        {
            bool connected = MainEscapeVentRouteSceneTools.AreConnected(first, second);

            if (GUILayout.Button(connected ? "Disconnect Selected Pair" : "Connect Selected Pair"))
            {
                MainEscapeVentRouteSceneTools.ToggleConnection(first, second);
            }
        }

        if (GUILayout.Button("Delete Node"))
        {
            MainEscapeVentRouteSceneTools.DeleteNode(nodeTransform);
        }

        serializedObject.ApplyModifiedProperties();
    }
}

internal readonly struct VentSceneConnection
{
    public VentSceneConnection(Transform from, Transform to, int fromIndex, int toIndex)
    {
        From = from;
        To = to;
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }

    public Transform From { get; }
    public Transform To { get; }
    public int FromIndex { get; }
    public int ToIndex { get; }
}

internal static class MainEscapeVentRouteSceneTools
{
    private static readonly Color CorridorColor = new(0.48f, 0.84f, 1f, 1f);
    private static readonly Color RoomColor = new(1f, 0.76f, 0.3f, 1f);
    private static readonly Color AutoColor = new(0.74f, 0.92f, 0.66f, 1f);
    private static readonly Color SequentialConnectionColor = new(0.46f, 0.86f, 1f, 0.9f);
    private static readonly Color ExplicitConnectionColor = new(1f, 0.9f, 0.45f, 0.95f);
    private static readonly Color InferredConnectionColor = new(0.54f, 1f, 0.58f, 0.82f);
    private static readonly Color PendingConnectionColor = new(1f, 0.45f, 0.45f, 1f);
    private static readonly GUIStyle LabelStyle = new(EditorStyles.boldLabel)
    {
        normal = { textColor = Color.white }
    };

    private static bool linkModeEnabled;
    private static MainEscapeVentNodeAuthoring pendingLinkStart;

    public static bool LinkModeEnabled => linkModeEnabled;

    public static bool HasExplicitConnections(MainEscapeVentRouteAuthoring route)
    {
        return route != null && HasExplicitConnections(route.transform);
    }

    public static bool HasExplicitConnections(Transform routeRoot)
    {
        if (routeRoot == null)
        {
            return false;
        }

        for (int index = 0; index < routeRoot.childCount; index++)
        {
            MainEscapeVentNodeAuthoring node = routeRoot.GetChild(index).GetComponent<MainEscapeVentNodeAuthoring>();

            if (node != null)
            {
                if (node.HasExplicitConnections)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static void ToggleLinkMode(MainEscapeVentRouteAuthoring route)
    {
        linkModeEnabled = !linkModeEnabled;

        if (!linkModeEnabled)
        {
            pendingLinkStart = null;
        }
        else if (route != null && pendingLinkStart != null && pendingLinkStart.transform.parent != route.transform)
        {
            pendingLinkStart = null;
        }

        if (route != null)
        {
            Selection.activeObject = route.gameObject;
        }

        SceneView.RepaintAll();
    }

    public static void BeginLinkFrom(MainEscapeVentNodeAuthoring node)
    {
        if (node == null)
        {
            return;
        }

        linkModeEnabled = true;
        pendingLinkStart = node;
        Selection.activeObject = node.gameObject;
        SceneView.RepaintAll();
    }

    public static void ConvertSequentialOrderToLinks(MainEscapeVentRouteAuthoring route)
    {
        if (route == null)
        {
            return;
        }

        MainEscapeVentNodeAuthoring[] nodes = GetNodeAuthorings(route.transform);

        if (nodes.Length == 0)
        {
            return;
        }

        ClearAllConnections(route, markDirty: false);
        Undo.RegisterCompleteObjectUndo(nodes, "Convert vent route to explicit links");

        for (int index = 1; index < nodes.Length; index++)
        {
            ConnectNodesInternal(nodes[index - 1], nodes[index]);
        }

        if (route.LoopPath && nodes.Length > 2)
        {
            ConnectNodesInternal(nodes[0], nodes[nodes.Length - 1]);
        }

        MarkNodesDirty(nodes);
        MarkSceneDirty();
        SceneView.RepaintAll();
    }

    public static void SaveRuntimePreviewAsLinks(MainEscapeVentRouteAuthoring route)
    {
        if (route == null)
        {
            return;
        }

        MainEscapeVentNodeAuthoring[] nodes = GetNodeAuthorings(route.transform);

        if (nodes.Length == 0)
        {
            return;
        }

        List<VentSceneConnection> previewConnections = CollectNamedColumnConnections(route.transform);

        if (previewConnections.Count == 0)
        {
            previewConnections = CollectExplicitConnections(route.transform);
        }

        if (previewConnections.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Vent Network",
                "No preview lines were found. Check that vent node names use Upper_, Lower_, and Corridor_ prefixes, or create explicit links first.",
                "OK");
            return;
        }

        Undo.RegisterCompleteObjectUndo(nodes, "Save vent preview lines as explicit links");

        for (int index = 0; index < nodes.Length; index++)
        {
            nodes[index].ClearConnections();
        }

        for (int index = 0; index < previewConnections.Count; index++)
        {
            VentSceneConnection connection = previewConnections[index];
            MainEscapeVentNodeAuthoring from = connection.From.GetComponent<MainEscapeVentNodeAuthoring>();
            MainEscapeVentNodeAuthoring to = connection.To.GetComponent<MainEscapeVentNodeAuthoring>();

            if (from != null && to != null)
            {
                ConnectNodesInternal(from, to);
            }
        }

        MarkNodesDirty(nodes);
        MarkSceneDirty();
        SceneView.RepaintAll();
    }

    public static void ShowVentNetworkReport(MainEscapeVentRouteAuthoring route)
    {
        if (route == null)
        {
            return;
        }

        int nodeCount = GetNodeAuthorings(route.transform).Length;
        List<VentSceneConnection> explicitConnections = CollectExplicitConnections(route.transform);
        List<VentSceneConnection> inferredConnections = CollectNamedColumnConnections(route.transform);
        int runtimeConnectionCount = CountMergedConnections(explicitConnections, inferredConnections);
        int isolatedNodeCount = CountIsolatedNodes(route.transform, explicitConnections, inferredConnections);

        EditorUtility.DisplayDialog(
            "Vent Network",
            $"Nodes: {nodeCount}\nExplicit links: {explicitConnections.Count}\nInferred preview links: {inferredConnections.Count}\nRuntime preview links: {runtimeConnectionCount}\nIsolated nodes: {isolatedNodeCount}",
            "OK");
    }

    public static void ClearAllConnections(MainEscapeVentRouteAuthoring route, bool markDirty = true)
    {
        if (route == null)
        {
            return;
        }

        MainEscapeVentNodeAuthoring[] nodes = GetNodeAuthorings(route.transform);

        if (nodes.Length == 0)
        {
            return;
        }

        Undo.RegisterCompleteObjectUndo(nodes, "Clear vent links");

        for (int index = 0; index < nodes.Length; index++)
        {
            nodes[index].ClearConnections();
        }

        pendingLinkStart = null;
        MarkNodesDirty(nodes);

        if (markDirty)
        {
            MarkSceneDirty();
            SceneView.RepaintAll();
        }
    }

    public static void DisconnectAll(MainEscapeVentNodeAuthoring node)
    {
        if (node == null || node.transform.parent == null)
        {
            return;
        }

        MainEscapeVentNodeAuthoring[] routeNodes = GetNodeAuthorings(node.transform.parent);
        Undo.RegisterCompleteObjectUndo(routeNodes, "Clear vent node links");

        for (int index = 0; index < routeNodes.Length; index++)
        {
            routeNodes[index].RemoveConnection(node);
        }

        node.ClearConnections();

        if (pendingLinkStart == node)
        {
            pendingLinkStart = null;
        }

        MarkNodesDirty(routeNodes);
        MarkSceneDirty();
        SceneView.RepaintAll();
    }

    public static bool TryGetSelectedNodePair(Transform routeRoot, out MainEscapeVentNodeAuthoring first, out MainEscapeVentNodeAuthoring second)
    {
        first = null;
        second = null;

        if (routeRoot == null)
        {
            return false;
        }

        MainEscapeVentNodeAuthoring[] selectedNodes = Selection.GetFiltered<MainEscapeVentNodeAuthoring>(SelectionMode.Editable | SelectionMode.TopLevel);

        for (int index = 0; index < selectedNodes.Length; index++)
        {
            MainEscapeVentNodeAuthoring node = selectedNodes[index];

            if (node == null || node.transform.parent != routeRoot)
            {
                continue;
            }

            if (first == null)
            {
                first = node;
            }
            else if (second == null && node != first)
            {
                second = node;
                break;
            }
        }

        return first != null && second != null;
    }

    public static bool AreConnected(MainEscapeVentNodeAuthoring a, MainEscapeVentNodeAuthoring b)
    {
        return a != null && b != null && (a.ContainsConnection(b) || b.ContainsConnection(a));
    }

    public static void ToggleConnection(MainEscapeVentNodeAuthoring a, MainEscapeVentNodeAuthoring b)
    {
        if (a == null || b == null || a == b || a.transform.parent == null || a.transform.parent != b.transform.parent)
        {
            return;
        }

        MainEscapeVentNodeAuthoring[] routeNodes = GetNodeAuthorings(a.transform.parent);
        Undo.RegisterCompleteObjectUndo(routeNodes, AreConnected(a, b) ? "Remove vent link" : "Add vent link");

        if (AreConnected(a, b))
        {
            DisconnectNodesInternal(a, b);
        }
        else
        {
            ConnectNodesInternal(a, b);
        }

        MarkNodesDirty(routeNodes);
        MarkSceneDirty();
        SceneView.RepaintAll();
    }

    public static void DrawRouteGizmos(MainEscapeVentRouteAuthoring route, GizmoType gizmoType)
    {
        if (route == null)
        {
            return;
        }

        bool emphasize = (gizmoType & (GizmoType.Selected | GizmoType.Active)) != 0;
        Transform root = route.transform;
        int childCount = root.childCount;

        if (childCount == 0)
        {
            return;
        }

        bool explicitMode = HasExplicitConnections(route);
        bool sequentialPreview = IsSequentialPreviewEnabled(route.transform);

        if (sequentialPreview && !explicitMode)
        {
            List<VentSceneConnection> sequentialConnections = CollectSequentialConnections(route);

            for (int index = 0; index < sequentialConnections.Count; index++)
            {
                DrawConnectionGizmo(
                    sequentialConnections[index].From.position,
                    sequentialConnections[index].To.position,
                    SequentialConnectionColor,
                    emphasize);
            }
        }
        else
        {
            DrawRuntimePreviewGizmos(root, emphasize);
        }

        if (!explicitMode && sequentialPreview && route.LoopPath && childCount > 2)
        {
            Transform last = root.GetChild(childCount - 1);
            Transform first = root.GetChild(0);
            DrawConnectionGizmo(last.position, first.position, SequentialConnectionColor, emphasize, dotted: true);
        }

        for (int index = 0; index < childCount; index++)
        {
            Transform node = root.GetChild(index);
            Vector3 position = node.position;
            DrawNodeGizmo(node, position, emphasize, pendingLinkStart != null && node.GetComponent<MainEscapeVentNodeAuthoring>() == pendingLinkStart);
        }
    }

    public static void DrawInteractiveSceneGui(MainEscapeVentRouteAuthoring route)
    {
        if (route == null)
        {
            return;
        }

        CleanupPendingLink(route.transform);

        Transform root = route.transform;
        int childCount = root.childCount;

        if (childCount == 0)
        {
            Handles.Label(route.transform.position + new Vector3(0.5f, 0.5f, 0f), "Add a vent node from the inspector to start the route.", LabelStyle);
            return;
        }

        bool explicitMode = HasExplicitConnections(route);
        bool sequentialPreview = IsSequentialPreviewEnabled(route.transform);

        if (sequentialPreview && !explicitMode)
        {
            List<VentSceneConnection> sequentialConnections = CollectSequentialConnections(route);

            for (int index = 0; index < sequentialConnections.Count; index++)
            {
                VentSceneConnection connection = sequentialConnections[index];
                DrawConnectionHandle(connection.From.position, connection.To.position, SequentialConnectionColor);
            }
        }
        else
        {
            DrawRuntimePreviewHandles(root);
        }

        if (!explicitMode && sequentialPreview)
        {
            for (int index = 0; index < childCount - 1; index++)
            {
                Transform from = root.GetChild(index);
                Transform to = root.GetChild(index + 1);
                DrawInsertButton(root, index + 1, from.position, to.position, GuessInsertedNodeType(from, to));
            }

            if (route.LoopPath && childCount > 2)
            {
                Transform last = root.GetChild(childCount - 1);
                Transform first = root.GetChild(0);
                Handles.color = SequentialConnectionColor;
                Handles.DrawDottedLine(last.position, first.position, 6f);
                DrawArrowHandle(last.position, first.position);
            }
        }

        for (int index = 0; index < childCount; index++)
        {
            Transform node = root.GetChild(index);
            MainEscapeVentNodeAuthoring nodeAuthoring = node.GetComponent<MainEscapeVentNodeAuthoring>();
            Color nodeColor = ResolveNodeColor(nodeAuthoring);
            Vector3 position = node.position;
            float handleSize = HandleUtility.GetHandleSize(position) * 0.12f;

            EditorGUI.BeginChangeCheck();
            Handles.color = nodeColor;
            var fmh_541_70_639108509185104152 = Quaternion.identity; Vector3 movedPosition = Handles.FreeMoveHandle(position, handleSize, Vector3.zero, Handles.CircleHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(node, "Move vent node");
                node.position = SnapPositionToTileCenter(node, movedPosition);
                EditorUtility.SetDirty(node);
                MarkSceneDirty();
            }

            bool isPending = pendingLinkStart != null && nodeAuthoring == pendingLinkStart;
            string label = $"{index:00} {ResolveNodeLabel(nodeAuthoring)}";

            if (isPending)
            {
                label += " [link]";
            }

            Handles.Label(position + new Vector3(0.28f, 0.3f, 0f), label, LabelStyle);

            if (linkModeEnabled)
            {
                DrawLinkButton(nodeAuthoring, position, isPending);
            }
        }

        if (linkModeEnabled)
        {
            string linkHint = pendingLinkStart != null
                ? $"Link Mode: click another node to connect/disconnect with {pendingLinkStart.name}. Click the same node again to cancel."
                : "Link Mode: click a node to choose a start point, then click another node to connect/disconnect them.";
            Handles.Label(root.position + new Vector3(0f, 1.5f, 0f), linkHint, LabelStyle);
        }
    }

    internal static bool IsSequentialPreviewEnabled(Transform routeRoot)
    {
        return routeRoot == null
            || !routeRoot.gameObject.scene.IsValid()
            || !MainEscapeSceneIdentityUtility.IsAuthoredSceneName(routeRoot.gameObject.scene.name);
    }

    public static void CreateNode(MainEscapeVentRouteAuthoring route, MainEscapeVentNodeType nodeType)
    {
        if (route == null)
        {
            return;
        }

        Vector3 placement = ResolveCreationPosition(route.transform);
        Transform node = CreateNode(route.transform, route.transform.childCount, placement, nodeType);
        Selection.activeTransform = node;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    public static void SnapAllNodes(MainEscapeVentRouteAuthoring route)
    {
        if (route == null)
        {
            return;
        }

        Transform root = route.transform;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Snap vent nodes to tile centers");

        for (int index = 0; index < root.childCount; index++)
        {
            Transform node = root.GetChild(index);
            node.position = SnapPositionToTileCenter(node, node.position);
            EditorUtility.SetDirty(node);
        }

        MarkSceneDirty();
    }

    public static void SelectAllNodes(MainEscapeVentRouteAuthoring route)
    {
        if (route == null || route.transform.childCount == 0)
        {
            return;
        }

        Object[] selection = new Object[route.transform.childCount];

        for (int index = 0; index < route.transform.childCount; index++)
        {
            selection[index] = route.transform.GetChild(index).gameObject;
        }

        Selection.objects = selection;
    }

    public static void RenameNodes(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Rename vent nodes");

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            child.name = $"Node_{index:00}";
            EditorUtility.SetDirty(child);
        }

        MarkSceneDirty();
    }

    public static void MoveNode(Transform node, int direction)
    {
        if (node == null || node.parent == null || direction == 0)
        {
            return;
        }

        Transform root = node.parent;
        int currentIndex = node.GetSiblingIndex();
        int targetIndex = Mathf.Clamp(currentIndex + direction, 0, root.childCount - 1);

        if (targetIndex == currentIndex)
        {
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Reorder vent node");
        node.SetSiblingIndex(targetIndex);
        RenameNodes(root);
        Selection.activeTransform = node;
    }

    public static void DuplicateNode(Transform node)
    {
        if (node == null || node.parent == null)
        {
            return;
        }

        MainEscapeVentNodeAuthoring authoring = node.GetComponent<MainEscapeVentNodeAuthoring>();
        MainEscapeVentNodeType nodeType = authoring != null ? authoring.NodeType : MainEscapeVentNodeType.Auto;
        Transform duplicate = CreateNode(node.parent, node.GetSiblingIndex() + 1, node.position, nodeType);
        Selection.activeTransform = duplicate;
    }

    public static void SnapNode(Transform node)
    {
        if (node == null)
        {
            return;
        }

        Undo.RecordObject(node, "Snap vent node to tile");
        node.position = SnapPositionToTileCenter(node, node.position);
        EditorUtility.SetDirty(node);
        MarkSceneDirty();
    }

    public static void DeleteNode(Transform node)
    {
        if (node == null)
        {
            return;
        }

        MainEscapeVentNodeAuthoring nodeAuthoring = node.GetComponent<MainEscapeVentNodeAuthoring>();

        if (nodeAuthoring != null)
        {
            DisconnectAll(nodeAuthoring);
        }

        Transform root = node.parent;
        Undo.DestroyObjectImmediate(node.gameObject);

        if (root != null)
        {
            RenameNodes(root);
        }
    }

    private static void DrawLinkButton(MainEscapeVentNodeAuthoring node, Vector3 position, bool isPending)
    {
        if (node == null)
        {
            return;
        }

        Vector3 buttonPosition = position + new Vector3(0f, 0.58f, 0f);
        float size = HandleUtility.GetHandleSize(buttonPosition) * 0.08f;
        Color previousColor = Handles.color;
        Handles.color = isPending ? PendingConnectionColor : Color.white;

        if (Handles.Button(buttonPosition, Quaternion.identity, size, size, Handles.RectangleHandleCap))
        {
            HandleLinkButtonClick(node);
        }

        Handles.Label(buttonPosition + new Vector3(0.12f, 0.08f, 0f), isPending ? "linking" : "link", LabelStyle);
        Handles.color = previousColor;
    }

    private static void HandleLinkButtonClick(MainEscapeVentNodeAuthoring clickedNode)
    {
        if (!linkModeEnabled || clickedNode == null)
        {
            return;
        }

        if (pendingLinkStart == null)
        {
            pendingLinkStart = clickedNode;
            Selection.activeObject = clickedNode.gameObject;
            SceneView.RepaintAll();
            return;
        }

        if (pendingLinkStart == clickedNode)
        {
            pendingLinkStart = null;
            SceneView.RepaintAll();
            return;
        }

        ToggleConnection(pendingLinkStart, clickedNode);
    }

    private static Transform CreateNode(Transform root, int siblingIndex, Vector3 worldPosition, MainEscapeVentNodeType nodeType)
    {
        GameObject nodeObject = new("VentNode");
        Undo.RegisterCreatedObjectUndo(nodeObject, "Create vent node");
        nodeObject.transform.SetParent(root, false);
        nodeObject.transform.position = SnapPositionToTileCenter(root, worldPosition);
        nodeObject.transform.rotation = Quaternion.identity;

        MainEscapeVentNodeAuthoring authoring = nodeObject.AddComponent<MainEscapeVentNodeAuthoring>();
        authoring.Configure(nodeType);
        nodeObject.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, root.childCount - 1));

        RenameNodes(root);
        EditorUtility.SetDirty(nodeObject);
        MarkSceneDirty();
        return nodeObject.transform;
    }

    private static void DrawInsertButton(Transform root, int siblingIndex, Vector3 from, Vector3 to, MainEscapeVentNodeType nodeType)
    {
        Vector3 midpoint = SnapPositionToTileCenter(root, Vector3.Lerp(from, to, 0.5f));
        float buttonSize = HandleUtility.GetHandleSize(midpoint) * 0.1f;
        Color previousColor = Handles.color;
        Handles.color = new Color(1f, 1f, 1f, 0.95f);

        if (Handles.Button(midpoint, Quaternion.identity, buttonSize, buttonSize, Handles.RectangleHandleCap))
        {
            Transform createdNode = CreateNode(root, siblingIndex, midpoint, nodeType);
            Selection.activeTransform = createdNode;
        }

        Handles.Label(midpoint + new Vector3(0.1f, 0.1f, 0f), "+", LabelStyle);
        Handles.color = previousColor;
    }

    private static void DrawNodeGizmo(Transform node, Vector3 position, bool emphasize, bool isPending)
    {
        MainEscapeVentNodeAuthoring nodeAuthoring = node != null ? node.GetComponent<MainEscapeVentNodeAuthoring>() : null;
        Color color = isPending ? PendingConnectionColor : ResolveNodeColor(nodeAuthoring);
        color.a = emphasize ? 0.95f : 0.7f;
        Gizmos.color = color;
        float radius = emphasize ? 0.22f : 0.16f;
        Gizmos.DrawSphere(position, radius);
    }

    private static void DrawConnectionHandle(Vector3 from, Vector3 to, Color color)
    {
        Color previousColor = Handles.color;
        Handles.color = color;
        Handles.DrawAAPolyLine(4f, from, to);
        DrawArrowHandle(from, to);
        Handles.color = previousColor;
    }

    private static void DrawConnectionGizmo(Vector3 from, Vector3 to, Color color, bool emphasize, bool dotted = false)
    {
        Color gizmoColor = color;
        gizmoColor.a = emphasize ? 0.95f : 0.55f;
        Gizmos.color = gizmoColor;

        if (dotted)
        {
            Handles.color = gizmoColor;
            Handles.DrawDottedLine(from, to, 6f);
        }
        else
        {
            Gizmos.DrawLine(from, to);
        }

        DrawArrowGizmo(from, to, emphasize ? 0.3f : 0.22f);
    }

    private static void DrawArrowHandle(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 tip = Vector3.Lerp(from, to, 0.82f);
        Vector3 side = Quaternion.Euler(0f, 0f, 90f) * direction * 0.18f;
        Vector3 back = -direction * 0.28f;
        Handles.DrawAAPolyLine(3f, tip, tip + back + side);
        Handles.DrawAAPolyLine(3f, tip, tip + back - side);
    }

    private static void DrawArrowGizmo(Vector3 from, Vector3 to, float length)
    {
        Vector3 direction = (to - from).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 tip = Vector3.Lerp(from, to, 0.82f);
        Vector3 side = Quaternion.Euler(0f, 0f, 90f) * direction * (length * 0.45f);
        Vector3 back = -direction * length;
        Gizmos.DrawLine(tip, tip + back + side);
        Gizmos.DrawLine(tip, tip + back - side);
    }

    private static MainEscapeVentNodeType GuessInsertedNodeType(Transform from, Transform to)
    {
        MainEscapeVentNodeAuthoring fromAuthoring = from != null ? from.GetComponent<MainEscapeVentNodeAuthoring>() : null;
        MainEscapeVentNodeAuthoring toAuthoring = to != null ? to.GetComponent<MainEscapeVentNodeAuthoring>() : null;

        if (fromAuthoring != null && fromAuthoring.NodeType == toAuthoring?.NodeType)
        {
            return fromAuthoring.NodeType;
        }

        return MainEscapeVentNodeType.Auto;
    }

    private static Color ResolveNodeColor(MainEscapeVentNodeAuthoring nodeAuthoring)
    {
        if (nodeAuthoring == null)
        {
            return AutoColor;
        }

        return nodeAuthoring.NodeType switch
        {
            MainEscapeVentNodeType.Corridor => CorridorColor,
            MainEscapeVentNodeType.Room => RoomColor,
            _ => AutoColor
        };
    }

    private static string ResolveNodeLabel(MainEscapeVentNodeAuthoring nodeAuthoring)
    {
        if (nodeAuthoring == null)
        {
            return "Auto";
        }

        return nodeAuthoring.NodeType switch
        {
            MainEscapeVentNodeType.Corridor => "Corridor",
            MainEscapeVentNodeType.Room => "Room",
            _ => "Auto"
        };
    }

    private static void ConnectNodesInternal(MainEscapeVentNodeAuthoring a, MainEscapeVentNodeAuthoring b)
    {
        a.AddConnection(b);
        b.AddConnection(a);
    }

    private static void DisconnectNodesInternal(MainEscapeVentNodeAuthoring a, MainEscapeVentNodeAuthoring b)
    {
        a.RemoveConnection(b);
        b.RemoveConnection(a);
    }

    private static void CleanupPendingLink(Transform routeRoot)
    {
        if (pendingLinkStart != null && pendingLinkStart.transform.parent != routeRoot)
        {
            pendingLinkStart = null;
        }
    }

    private static MainEscapeVentNodeAuthoring[] GetNodeAuthorings(Transform routeRoot)
    {
        List<MainEscapeVentNodeAuthoring> nodes = new(routeRoot.childCount);

        for (int index = 0; index < routeRoot.childCount; index++)
        {
            MainEscapeVentNodeAuthoring node = routeRoot.GetChild(index).GetComponent<MainEscapeVentNodeAuthoring>();

            if (node != null)
            {
                nodes.Add(node);
            }
        }

        return nodes.ToArray();
    }

    private static List<VentSceneConnection> CollectExplicitConnections(Transform routeRoot)
    {
        List<VentSceneConnection> connections = new();
        HashSet<long> seenConnections = new();

        for (int index = 0; index < routeRoot.childCount; index++)
        {
            Transform from = routeRoot.GetChild(index);
            MainEscapeVentNodeAuthoring fromAuthoring = from.GetComponent<MainEscapeVentNodeAuthoring>();

            if (fromAuthoring == null)
            {
                continue;
            }

            IReadOnlyList<MainEscapeVentNodeAuthoring> linkedNodes = fromAuthoring.ConnectedNodes;

            for (int linkIndex = 0; linkIndex < linkedNodes.Count; linkIndex++)
            {
                MainEscapeVentNodeAuthoring linkedNode = linkedNodes[linkIndex];

                if (linkedNode == null || linkedNode.transform.parent != routeRoot)
                {
                    continue;
                }

                int toIndex = linkedNode.transform.GetSiblingIndex();
                int minIndex = Mathf.Min(index, toIndex);
                int maxIndex = Mathf.Max(index, toIndex);
                long key = ((long)minIndex << 32) | (uint)maxIndex;

                if (minIndex == maxIndex || !seenConnections.Add(key))
                {
                    continue;
                }

                Transform orderedFrom = routeRoot.GetChild(minIndex);
                Transform orderedTo = routeRoot.GetChild(maxIndex);
                connections.Add(new VentSceneConnection(orderedFrom, orderedTo, minIndex, maxIndex));
            }
        }

        return connections;
    }

    private static void DrawRuntimePreviewGizmos(Transform routeRoot, bool emphasize)
    {
        List<VentSceneConnection> inferredConnections = CollectNamedColumnConnections(routeRoot);

        for (int index = 0; index < inferredConnections.Count; index++)
        {
            VentSceneConnection connection = inferredConnections[index];
            DrawConnectionGizmo(connection.From.position, connection.To.position, InferredConnectionColor, emphasize);
        }

        List<VentSceneConnection> explicitConnections = CollectExplicitConnections(routeRoot);

        for (int index = 0; index < explicitConnections.Count; index++)
        {
            VentSceneConnection connection = explicitConnections[index];
            DrawConnectionGizmo(connection.From.position, connection.To.position, ExplicitConnectionColor, emphasize);
        }
    }

    private static void DrawRuntimePreviewHandles(Transform routeRoot)
    {
        List<VentSceneConnection> inferredConnections = CollectNamedColumnConnections(routeRoot);

        for (int index = 0; index < inferredConnections.Count; index++)
        {
            VentSceneConnection connection = inferredConnections[index];
            DrawConnectionHandle(connection.From.position, connection.To.position, InferredConnectionColor);
        }

        List<VentSceneConnection> explicitConnections = CollectExplicitConnections(routeRoot);

        for (int index = 0; index < explicitConnections.Count; index++)
        {
            VentSceneConnection connection = explicitConnections[index];
            DrawConnectionHandle(connection.From.position, connection.To.position, ExplicitConnectionColor);
        }
    }

    private static int CountMergedConnections(
        List<VentSceneConnection> explicitConnections,
        List<VentSceneConnection> inferredConnections)
    {
        HashSet<long> connectionKeys = new();
        AddConnectionKeys(explicitConnections, connectionKeys);
        AddConnectionKeys(inferredConnections, connectionKeys);
        return connectionKeys.Count;
    }

    private static int CountIsolatedNodes(
        Transform routeRoot,
        List<VentSceneConnection> explicitConnections,
        List<VentSceneConnection> inferredConnections)
    {
        if (routeRoot == null)
        {
            return 0;
        }

        Dictionary<Transform, int> degreeByNode = new(routeRoot.childCount);

        for (int index = 0; index < routeRoot.childCount; index++)
        {
            Transform nodeTransform = routeRoot.GetChild(index);

            if (nodeTransform.GetComponent<MainEscapeVentNodeAuthoring>() != null)
            {
                degreeByNode[nodeTransform] = 0;
            }
        }

        AddConnectionDegrees(explicitConnections, degreeByNode);
        AddConnectionDegrees(inferredConnections, degreeByNode);

        int isolatedCount = 0;

        foreach (int degree in degreeByNode.Values)
        {
            if (degree == 0)
            {
                isolatedCount++;
            }
        }

        return isolatedCount;
    }

    private static void AddConnectionKeys(List<VentSceneConnection> connections, ISet<long> connectionKeys)
    {
        if (connections == null)
        {
            return;
        }

        for (int index = 0; index < connections.Count; index++)
        {
            VentSceneConnection connection = connections[index];
            int fromId = connection.From != null ? connection.From.GetInstanceID() : 0;
            int toId = connection.To != null ? connection.To.GetInstanceID() : 0;

            if (fromId == 0 || toId == 0 || fromId == toId)
            {
                continue;
            }

            int low = Mathf.Min(fromId, toId);
            int high = Mathf.Max(fromId, toId);
            long key = ((long)low << 32) | (uint)high;
            connectionKeys.Add(key);
        }
    }

    private static void AddConnectionDegrees(
        List<VentSceneConnection> connections,
        Dictionary<Transform, int> degreeByNode)
    {
        if (connections == null || degreeByNode == null)
        {
            return;
        }

        for (int index = 0; index < connections.Count; index++)
        {
            VentSceneConnection connection = connections[index];

            if (connection.From != null && degreeByNode.ContainsKey(connection.From))
            {
                degreeByNode[connection.From]++;
            }

            if (connection.To != null && degreeByNode.ContainsKey(connection.To))
            {
                degreeByNode[connection.To]++;
            }
        }
    }

    private static List<VentSceneConnection> CollectSequentialConnections(MainEscapeVentRouteAuthoring route)
    {
        List<VentSceneConnection> connections = new();
        Transform root = route.transform;

        for (int index = 1; index < root.childCount; index++)
        {
            connections.Add(new VentSceneConnection(root.GetChild(index - 1), root.GetChild(index), index - 1, index));
        }

        return connections;
    }

    private static List<VentSceneConnection> CollectNamedColumnConnections(Transform routeRoot)
    {
        List<VentSceneConnection> connections = new();

        if (routeRoot == null || routeRoot.childCount == 0)
        {
            return connections;
        }

        List<Transform> nodeTransforms = new(routeRoot.childCount);
        Dictionary<Transform, int> nodeIndexByTransform = new(routeRoot.childCount);

        for (int index = 0; index < routeRoot.childCount; index++)
        {
            Transform nodeTransform = routeRoot.GetChild(index);

            if (nodeTransform.GetComponent<MainEscapeVentNodeAuthoring>() == null)
            {
                continue;
            }

            nodeIndexByTransform[nodeTransform] = nodeTransforms.Count;
            nodeTransforms.Add(nodeTransform);
        }

        if (!MainEscapeVentRouteAuthoring.TryBuildNamedColumnConnections(
                nodeIndexByTransform,
                out MainEscapeVentConnectionDefinition[] inferredConnections))
        {
            return connections;
        }

        for (int index = 0; index < inferredConnections.Length; index++)
        {
            MainEscapeVentConnectionDefinition connection = inferredConnections[index];

            if (connection.FromIndex < 0
                || connection.ToIndex < 0
                || connection.FromIndex >= nodeTransforms.Count
                || connection.ToIndex >= nodeTransforms.Count)
            {
                continue;
            }

            connections.Add(new VentSceneConnection(
                nodeTransforms[connection.FromIndex],
                nodeTransforms[connection.ToIndex],
                connection.FromIndex,
                connection.ToIndex));
        }

        return connections;
    }

    private static void MarkNodesDirty(MainEscapeVentNodeAuthoring[] nodes)
    {
        for (int index = 0; index < nodes.Length; index++)
        {
            if (nodes[index] != null)
            {
                EditorUtility.SetDirty(nodes[index]);
            }
        }
    }

    private static Vector3 ResolveCreationPosition(Transform routeRoot)
    {
        SceneView sceneView = SceneView.lastActiveSceneView;

        if (sceneView != null)
        {
            return SnapPositionToTileCenter(routeRoot, sceneView.pivot);
        }

        if (routeRoot.childCount > 0)
        {
            return SnapPositionToTileCenter(routeRoot, routeRoot.GetChild(routeRoot.childCount - 1).position + Vector3.right);
        }

        return SnapPositionToTileCenter(routeRoot, routeRoot.position);
    }

    private static Vector3 SnapPositionToTileCenter(Object context, Vector3 worldPosition)
    {
        Tilemap tilemap = ResolveGroundTilemap(context);

        if (tilemap != null)
        {
            Vector3Int cell = tilemap.WorldToCell(worldPosition);
            Vector3 center = tilemap.GetCellCenterWorld(cell);
            center.z = 0f;
            return center;
        }

        return new Vector3(Mathf.Floor(worldPosition.x) + 0.5f, Mathf.Floor(worldPosition.y) + 0.5f, 0f);
    }

    private static Tilemap ResolveGroundTilemap(Object context)
    {
        Transform transform = context switch
        {
            Component component => component.transform,
            GameObject gameObject => gameObject.transform,
            _ => null
        };

        MainEscapeFloorAuthoring floorAuthoring = transform != null ? transform.GetComponentInParent<MainEscapeFloorAuthoring>() : null;

        if (floorAuthoring != null)
        {
            Transform groundRoot = floorAuthoring.transform.Find("Ground");

            if (groundRoot != null)
            {
                Tilemap tilemap = groundRoot.GetComponent<Tilemap>();

                if (tilemap != null)
                {
                    return tilemap;
                }
            }
        }

        Tilemap[] tilemaps = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int index = 0; index < tilemaps.Length; index++)
        {
            Tilemap tilemap = tilemaps[index];

            if (tilemap != null && tilemap.name == "Ground")
            {
                return tilemap;
            }
        }

        return null;
    }

    private static void MarkSceneDirty()
    {
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
#endif
