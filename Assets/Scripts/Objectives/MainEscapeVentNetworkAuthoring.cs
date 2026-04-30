using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeVentNetworkAuthoring : MonoBehaviour
{
    [SerializeField] private string networkId = "SampleVentNetwork";
    [SerializeField] private Transform nodesRoot;
    [SerializeField] private Transform linksRoot;
    [SerializeField] private Transform exitsRoot;

    public string NetworkId => string.IsNullOrWhiteSpace(networkId) ? name : networkId;
    public Transform NodesRoot => nodesRoot;
    public Transform LinksRoot => linksRoot;
    public Transform ExitsRoot => exitsRoot;

    public void Configure(
        string configuredNetworkId,
        Transform configuredNodesRoot,
        Transform configuredLinksRoot,
        Transform configuredExitsRoot)
    {
        networkId = configuredNetworkId;
        nodesRoot = configuredNodesRoot;
        linksRoot = configuredLinksRoot;
        exitsRoot = configuredExitsRoot;
    }

    public MainEscapeVentNodeAuthoring[] GetNodes()
    {
        return nodesRoot != null
            ? nodesRoot.GetComponentsInChildren<MainEscapeVentNodeAuthoring>(true)
            : Array.Empty<MainEscapeVentNodeAuthoring>();
    }

    public MainEscapeVentLinkAuthoring[] GetLinks()
    {
        return linksRoot != null
            ? linksRoot.GetComponentsInChildren<MainEscapeVentLinkAuthoring>(true)
            : Array.Empty<MainEscapeVentLinkAuthoring>();
    }

    public MainEscapeVentExitAuthoring[] GetExits()
    {
        return exitsRoot != null
            ? exitsRoot.GetComponentsInChildren<MainEscapeVentExitAuthoring>(true)
            : Array.Empty<MainEscapeVentExitAuthoring>();
    }

#if UNITY_EDITOR
    private void Reset()
    {
        AutoAssignRoots();
    }

    private void OnValidate()
    {
        AutoAssignRoots();
    }

    private void AutoAssignRoots()
    {
        nodesRoot ??= transform.Find("Nodes");
        linksRoot ??= transform.Find("Links");
        exitsRoot ??= transform.Find("Exits");
    }
#endif
}
