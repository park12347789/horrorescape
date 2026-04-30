using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeVentNetworkDebugOverlay : MonoBehaviour
{
    private static readonly Color CorridorNodeColor = new(0.2f, 0.96f, 1f, 0.92f);
    private static readonly Color RoomNodeColor = new(1f, 0.82f, 0.28f, 0.92f);
    private static readonly Color CorridorLinkColor = new(0.22f, 0.92f, 1f, 0.7f);
    private static readonly Color RoomLinkColor = new(1f, 0.88f, 0.34f, 0.64f);
    private static readonly Color MixedLinkColor = new(0.66f, 0.96f, 0.62f, 0.7f);

    private static Sprite sharedSprite;
    private static Material sharedLineMaterial;

    [SerializeField] private GeneratedFloorLayout layout;
    [SerializeField] private GridMapService mapService;
    [SerializeField] private bool visible;

    private MainEscapeVentRouteDefinition ventRoute = MainEscapeVentRouteDefinition.Empty;
    private Transform markerRoot;
    private Transform connectionRoot;

    public void Configure(
        GeneratedFloorLayout configuredLayout,
        GridMapService configuredMapService,
        MainEscapeVentRouteDefinition configuredVentRoute,
        bool showNetwork)
    {
        layout = configuredLayout;
        mapService = configuredMapService;
        ventRoute = configuredVentRoute.IsValid ? configuredVentRoute : MainEscapeVentRouteDefinition.Empty;
        visible = showNetwork;
        Rebuild();
    }

    public void SetVisible(bool showNetwork)
    {
        if (visible == showNetwork)
        {
            return;
        }

        visible = showNetwork;
        Rebuild();
    }

    public void Clear()
    {
        ventRoute = MainEscapeVentRouteDefinition.Empty;
        layout = null;
        mapService = null;
        DestroyDebugRoots();
    }

    private void OnDisable()
    {
        DestroyDebugRoots();
    }

    private void Rebuild()
    {
        DestroyDebugRoots();

        if (!visible
            || layout == null
            || mapService == null
            || !ventRoute.IsValid
            || ventRoute.Nodes == null
            || ventRoute.Nodes.Length == 0)
        {
            return;
        }

        EnsureSharedSprite();

        markerRoot = CreateRoot("VentDebugNodes");
        connectionRoot = CreateRoot("VentDebugConnections");

        for (int index = 0; index < ventRoute.Nodes.Length; index++)
        {
            CreateVentMarker(index, ventRoute.Nodes[index]);
        }

        MainEscapeVentRouteConnection[] connections = MainEscapeVentRouteGraphUtility.BuildConnections(ventRoute);

        for (int index = 0; index < connections.Length; index++)
        {
            MainEscapeVentRouteConnection connection = connections[index];
            CreateVentConnection(connection.FromIndex, connection.ToIndex);
        }
    }

    private Transform CreateRoot(string rootName)
    {
        GameObject rootObject = new(rootName);
        rootObject.transform.SetParent(layout.transform, false);
        return rootObject.transform;
    }

    private void CreateVentMarker(int nodeIndex, MainEscapeVentNodeDefinition node)
    {
        GameObject markerObject = new($"VentNode_{nodeIndex:00}");
        markerObject.transform.SetParent(markerRoot, false);
        markerObject.transform.position = mapService.CellToWorldCenter(node.Cell);
        markerObject.transform.localScale = node.IsCorridor
            ? new Vector3(0.46f, 0.12f, 1f)
            : new Vector3(0.26f, 0.26f, 1f);

        SpriteRenderer renderer = markerObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sharedSprite;
        renderer.color = node.IsCorridor ? CorridorNodeColor : RoomNodeColor;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 32;
    }

    private void CreateVentConnection(int fromIndex, int toIndex)
    {
        if (fromIndex < 0
            || toIndex < 0
            || ventRoute.Nodes == null
            || fromIndex >= ventRoute.Nodes.Length
            || toIndex >= ventRoute.Nodes.Length)
        {
            return;
        }

        MainEscapeVentNodeDefinition fromNode = ventRoute.Nodes[fromIndex];
        MainEscapeVentNodeDefinition toNode = ventRoute.Nodes[toIndex];

        GameObject lineObject = new($"VentLink_{fromIndex:00}_{toIndex:00}");
        lineObject.transform.SetParent(connectionRoot, false);

        LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.positionCount = 2;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.numCornerVertices = 4;
        lineRenderer.numCapVertices = 2;
        lineRenderer.widthMultiplier = fromNode.IsCorridor && toNode.IsCorridor ? 0.11f : 0.085f;
        lineRenderer.sharedMaterial = GetSharedLineMaterial();
        lineRenderer.sortingOrder = 31;

        Color linkColor = ResolveConnectionColor(fromNode, toNode);
        lineRenderer.startColor = linkColor;
        lineRenderer.endColor = linkColor;
        lineRenderer.SetPosition(0, mapService.CellToWorldCenter(fromNode.Cell));
        lineRenderer.SetPosition(1, mapService.CellToWorldCenter(toNode.Cell));
    }

    private static Color ResolveConnectionColor(
        MainEscapeVentNodeDefinition fromNode,
        MainEscapeVentNodeDefinition toNode)
    {
        if (fromNode.IsCorridor && toNode.IsCorridor)
        {
            return CorridorLinkColor;
        }

        if (!fromNode.IsCorridor && !toNode.IsCorridor)
        {
            return RoomLinkColor;
        }

        return MixedLinkColor;
    }

    private void DestroyDebugRoots()
    {
        DestroyTransform(ref markerRoot);
        DestroyTransform(ref connectionRoot);
    }

    private static void DestroyTransform(ref Transform target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target.gameObject);
        }
        else
        {
            DestroyImmediate(target.gameObject);
        }

        target = null;
    }

    private static void EnsureSharedSprite()
    {
        if (sharedSprite != null)
        {
            return;
        }

        Texture2D texture = new(1, 1, TextureFormat.RGBA32, mipChain: false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

        sharedSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sharedSprite.hideFlags = HideFlags.HideAndDontSave;
    }

    private static Material GetSharedLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        sharedLineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        return sharedLineMaterial;
    }
}
