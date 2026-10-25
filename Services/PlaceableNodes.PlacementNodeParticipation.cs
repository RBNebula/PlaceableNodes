using System.Linq;
using UnityEngine;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private void UpdatePlacementNodeParticipation()
    {
        if (IsHoldingCustomAutoMinerPlacementNode())
        {
            SuppressCustomPlacementNodes();
            return;
        }

        SuppressOccupiedCustomPlacementNodes();
        RestoreSuppressedPlacementNodes();
    }

    internal void SuppressPlacementNodeIfNeeded(BuildingPlacementNode? node)
    {
        if (!IsHoldingCustomAutoMinerPlacementNode())
        {
            return;
        }

        SuppressPlacementNode(node);
    }

    private void SuppressCustomPlacementNodes()
    {
        foreach (BuildingPlacementNode node in BuildingPlacementNode.All.ToArray())
        {
            if (!PlaceableNodesUtility.IsCustomPlacementNode(node))
            {
                continue;
            }

            SuppressPlacementNode(node);
        }

        _suppressedPlacementNodes.RemoveWhere(node => node == null);
    }

    private void SuppressOccupiedCustomPlacementNodes()
    {
        foreach (BuildingPlacementNode node in BuildingPlacementNode.All.ToArray())
        {
            if (!PlaceableNodesUtility.IsCustomPlacementNode(node) || !IsOccupiedPlacementNode(node))
            {
                continue;
            }

            SuppressPlacementNode(node);
        }
    }

    private void SuppressPlacementNode(BuildingPlacementNode? node)
    {
        if (node == null || !PlaceableNodesUtility.IsCustomPlacementNode(node))
        {
            return;
        }

        if (BuildingPlacementNode.All.Remove(node))
        {
            _suppressedPlacementNodes.Add(node);
        }

        if (PlaceableNodesUtility.IsUsableGameObject(node.GhostPrefab))
        {
            node.GhostPrefab.SetActive(false);
        }

        if (IsOccupiedPlacementNode(node))
        {
            DestroyPlacementNodeGhost(node);
        }
    }

    private void RestoreSuppressedPlacementNodes()
    {
        if (_suppressedPlacementNodes.Count == 0)
        {
            return;
        }

        foreach (BuildingPlacementNode node in _suppressedPlacementNodes.ToArray())
        {
            if (node == null)
            {
                continue;
            }

            if (IsOccupiedPlacementNode(node))
            {
                continue;
            }

            if (!BuildingPlacementNode.All.Contains(node))
            {
                BuildingPlacementNode.All.Add(node);
            }

            _suppressedPlacementNodes.Remove(node);
        }
    }

    private static bool IsOccupiedPlacementNode(BuildingPlacementNode node)
    {
        BuildingObject? attached = node.AttachedBuildingObject;
        return attached != null && !attached.IsGhost;
    }

    private static void DestroyPlacementNodeGhost(BuildingPlacementNode node)
    {
        GameObject? ghostPrefab = node.GhostPrefab;
        node.GhostPrefab = null;

        if (PlaceableNodesUtility.IsUsableGameObject(ghostPrefab))
        {
            Object.Destroy(ghostPrefab);
            return;
        }

        Transform? ghostChild = node.transform.Find("GhostMiner");
        if (ghostChild != null)
        {
            Object.Destroy(ghostChild.gameObject);
        }
    }
}
