using UnityEngine;

namespace PlaceableNodes;

internal sealed class PlaceableNodesPlacementColliderController : MonoBehaviour
{
    private BuildingObject? _buildingObject;

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
    }

    private void UpdatePlacementColliderState()
    {
        if (_buildingObject != null)
        {
            PlaceableNodesUtility.EnsurePlacementCollision(_buildingObject);
        }
    }
}
