using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeVentLinkAuthoring : MonoBehaviour
{
    [SerializeField] private MainEscapeVentNodeAuthoring fromNode;
    [SerializeField] private MainEscapeVentNodeAuthoring toNode;

    public MainEscapeVentNodeAuthoring FromNode => fromNode;
    public MainEscapeVentNodeAuthoring ToNode => toNode;
    public bool IsValid => fromNode != null && toNode != null && fromNode != toNode;

    public void Configure(MainEscapeVentNodeAuthoring configuredFromNode, MainEscapeVentNodeAuthoring configuredToNode)
    {
        fromNode = configuredFromNode;
        toNode = configuredToNode;
    }
}
