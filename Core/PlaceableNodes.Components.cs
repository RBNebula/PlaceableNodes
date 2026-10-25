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
    private const float RepairRetryIntervalSeconds = 0.25f;
    private const float RepairRetryDurationSeconds = 6f;

    private float _stopRepairingAt;
    private float _nextRepairAttemptAt;

    private void Awake()
    {
        _stopRepairingAt = Time.unscaledTime + RepairRetryDurationSeconds;
    }

    private void Start()
    {
        EnsureVisualState();
        TryAttachNearbyMiner();
    }

    private void LateUpdate()
    {
        EnsureVisualState();
        if (Time.unscaledTime < _nextRepairAttemptAt)
        {
            return;
        }

        _nextRepairAttemptAt = Time.unscaledTime + RepairRetryIntervalSeconds;
        if (TryAttachNearbyMiner() || Time.unscaledTime >= _stopRepairingAt)
        {
            enabled = false;
        }
    }

    private bool TryAttachNearbyMiner()
    {
        BuildingPlacementNode node = GetComponent<BuildingPlacementNode>();
        return PlaceableNodesUtility.TryRepairPlacementNodeAttachment(node);
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
