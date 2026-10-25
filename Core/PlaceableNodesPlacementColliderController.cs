using UnityEngine;

namespace PlaceableNodes;

internal sealed class PlaceableNodesPlacementColliderController : MonoBehaviour
{
    private const int StabilizationFrames = 3;

    private BuildingObject? _buildingObject;
    private int _remainingStabilizationFrames = StabilizationFrames;

    private void Awake()
    {
        _buildingObject = GetComponent<BuildingObject>();
    }

    private void Start()
    {
        UpdatePlacementColliderState();
    }

    private void LateUpdate()
    {
        UpdatePlacementColliderState();
        _remainingStabilizationFrames--;
        if (_remainingStabilizationFrames <= 0)
        {
            enabled = false;
        }
    }

    private void UpdatePlacementColliderState()
    {
        if (_buildingObject != null)
        {
            PlaceableNodesUtility.EnsurePlacementCollision(_buildingObject);
        }
    }
}
