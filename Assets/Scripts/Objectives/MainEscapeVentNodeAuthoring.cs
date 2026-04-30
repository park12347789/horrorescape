using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MainEscapeVentNodeAuthoring : MonoBehaviour
{
    [SerializeField] private MainEscapeVentNodeType nodeType = MainEscapeVentNodeType.Auto;
    [SerializeField] private List<MainEscapeVentNodeAuthoring> connectedNodes = new();

    public MainEscapeVentNodeType NodeType => nodeType;
    public IReadOnlyList<MainEscapeVentNodeAuthoring> ConnectedNodes => connectedNodes;
    public bool HasExplicitConnections => connectedNodes != null && connectedNodes.Count > 0;

    public void Configure(MainEscapeVentNodeType configuredNodeType)
    {
        nodeType = configuredNodeType;
    }

    public bool ContainsConnection(MainEscapeVentNodeAuthoring other)
    {
        if (other == null || connectedNodes == null)
        {
            return false;
        }

        return connectedNodes.Contains(other);
    }

    public void AddConnection(MainEscapeVentNodeAuthoring other)
    {
        if (other == null || other == this)
        {
            return;
        }

        connectedNodes ??= new List<MainEscapeVentNodeAuthoring>();

        if (!connectedNodes.Contains(other))
        {
            connectedNodes.Add(other);
        }
    }

    public void RemoveConnection(MainEscapeVentNodeAuthoring other)
    {
        if (other == null || connectedNodes == null)
        {
            return;
        }

        connectedNodes.Remove(other);
    }

    public void ClearConnections()
    {
        connectedNodes?.Clear();
    }

    public void RemoveInvalidConnections(Transform expectedParent = null)
    {
        if (connectedNodes == null)
        {
            return;
        }

        for (int index = connectedNodes.Count - 1; index >= 0; index--)
        {
            MainEscapeVentNodeAuthoring connectedNode = connectedNodes[index];

            if (connectedNode == null || connectedNode == this)
            {
                connectedNodes.RemoveAt(index);
                continue;
            }

            if (expectedParent != null && connectedNode.transform.parent != expectedParent)
            {
                connectedNodes.RemoveAt(index);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (transform != null
            && transform.gameObject.scene.IsValid()
            && MainEscapeSceneIdentityUtility.IsAuthoredSceneName(
                MainEscapeSceneIdentityUtility.GetScenePathOrName(transform.gameObject.scene)))
        {
            return;
        }

        RemoveInvalidConnections(transform.parent);
    }
#endif
}
