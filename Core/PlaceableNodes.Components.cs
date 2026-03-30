using System.Linq;
using UnityEngine;

namespace PlaceableNodes;

internal sealed class PlaceableNodesPlacementItemMarker : MonoBehaviour
{
}

internal sealed class PlaceableNodesOreMarker : MonoBehaviour
{
}

internal sealed class PlaceableNodesGhostFallbackMarker : MonoBehaviour
{
}

internal sealed class PlaceableNodesOreRuntimeInitializer : MonoBehaviour
{
    private bool _initialized;

    private void Start()
    {
        EnsureInitialized();
    }

    private void LateUpdate()
    {
        if (!_initialized)
        {
            EnsureInitialized();
        }
    }

    private void EnsureInitialized()
    {
        OreNode oreNode = GetComponent<OreNode>();
        if (oreNode == null)
        {
            return;
        }

        if (PlaceableNodes.Instance == null)
        {
            return;
        }

        PlaceableNodes.Instance.InitializePlacedOreNode(oreNode);
        _initialized = true;
        enabled = false;
    }
}

internal sealed class PlaceableNodesPlacementNodeBootstrap : MonoBehaviour
{
    private void Start()
    {
        EnsureVisualState();
    }

    private void LateUpdate()
    {
        EnsureVisualState();
        TryAttachNearbyMiner();
    }

    private void TryAttachNearbyMiner()
    {
        BuildingPlacementNode node = GetComponent<BuildingPlacementNode>();
        PlaceableNodesUtility.TryRepairPlacementNodeAttachment(node);
    }

    private void EnsureVisualState()
    {
        Transform? visible = transform.Find("__PlaceableNodesNodeVisual");
        if (visible != null && !visible.gameObject.activeSelf)
        {
            visible.gameObject.SetActive(true);
        }
    }
}
