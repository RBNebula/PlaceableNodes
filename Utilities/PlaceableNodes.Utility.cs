using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace PlaceableNodes;

internal static class PlaceableNodesUtility
{
    private static readonly BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void ClearHideFlagsRecursively(GameObject? root)
    {
        if (!IsUsableGameObject(root))
        {
            return;
        }

        root!.hideFlags = HideFlags.None;

        foreach (Transform child in root.transform.GetComponentsInChildren<Transform>(true))
        {
            if (child == null)
            {
                continue;
            }

            try
            {
                if (IsUsableGameObject(child.gameObject))
                {
                    child.gameObject.hideFlags = HideFlags.None;
                }
            }
            catch
            {
                // Ignore stale Unity object references while normalizing runtime clone flags.
            }
        }
    }

    internal static T? GetPrivateField<T>(object instance, string fieldName)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, AnyInstance);
        if (field == null)
        {
            return default;
        }

        object? value = field.GetValue(instance);
        if (value is T typed)
        {
            return typed;
        }

        return default;
    }

    internal static void SetPrivateField(object instance, string fieldName, object? value)
    {
        FieldInfo? field = instance.GetType().GetField(fieldName, AnyInstance);
        field?.SetValue(instance, value);
    }

    internal static void ResetShell(BuildingObject shell)
    {
        Transform? placement = shell.BuildingPlacementColliderObject != null ? shell.BuildingPlacementColliderObject.transform : null;
        Transform? spawn = shell.BuildingCrateSpawnPoint;

        foreach (Transform child in shell.transform.Cast<Transform>().ToArray())
        {
            if (child == placement || child == spawn)
            {
                continue;
            }

            Object.DestroyImmediate(child.gameObject);
        }

        foreach (MeshRenderer renderer in shell.GetComponents<MeshRenderer>().ToArray())
        {
            Object.DestroyImmediate(renderer);
        }

        foreach (MeshFilter filter in shell.GetComponents<MeshFilter>().ToArray())
        {
            Object.DestroyImmediate(filter);
        }

        foreach (Collider collider in shell.GetComponents<Collider>().ToArray())
        {
            Object.DestroyImmediate(collider);
        }

        foreach (PlaceableNodesOreMarker marker in shell.GetComponents<PlaceableNodesOreMarker>().ToArray())
        {
            Object.DestroyImmediate(marker);
        }

        foreach (PlaceableNodesGhostFallbackMarker marker in shell.GetComponents<PlaceableNodesGhostFallbackMarker>().ToArray())
        {
            Object.DestroyImmediate(marker);
        }

        foreach (PlaceableNodesOreRuntimeInitializer initializer in shell.GetComponents<PlaceableNodesOreRuntimeInitializer>().ToArray())
        {
            Object.DestroyImmediate(initializer);
        }

        foreach (PlaceableNodesPlacementNodeBootstrap bootstrap in shell.GetComponents<PlaceableNodesPlacementNodeBootstrap>().ToArray())
        {
            Object.DestroyImmediate(bootstrap);
        }

        foreach (BuildingPlacementNode placementNode in shell.GetComponents<BuildingPlacementNode>().ToArray())
        {
            Object.DestroyImmediate(placementNode);
        }

        OreNode? oreNode = shell.GetComponent<OreNode>();
        if (oreNode != null)
        {
            Object.DestroyImmediate(oreNode);
        }

        if (placement != null)
        {
            foreach (Transform child in placement.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }

            foreach (Collider collider in placement.GetComponents<Collider>().ToArray())
            {
                Object.DestroyImmediate(collider);
            }

            placement.localPosition = Vector3.zero;
            placement.localRotation = Quaternion.identity;
            placement.localScale = Vector3.one;
        }

        if (spawn != null)
        {
            spawn.localPosition = Vector3.zero;
            spawn.localRotation = Quaternion.identity;
            spawn.localScale = Vector3.one;
        }
    }

    internal static bool CopyRootMeshTemplateToShell(GameObject templateRoot, GameObject shellRoot, bool includeMeshCollider)
    {
        Transform? sourceTransform = FindFirstMeshRoot(templateRoot.transform);
        if (sourceTransform == null)
        {
            return false;
        }

        MeshFilter? sourceFilter = sourceTransform.GetComponent<MeshFilter>();
        MeshRenderer? sourceRenderer = sourceTransform.GetComponent<MeshRenderer>();
        if (sourceFilter == null || sourceRenderer == null)
        {
            return false;
        }

        MeshFilter targetFilter = shellRoot.GetComponent<MeshFilter>() ?? shellRoot.AddComponent<MeshFilter>();
        targetFilter.sharedMesh = sourceFilter.sharedMesh;

        MeshRenderer targetRenderer = shellRoot.GetComponent<MeshRenderer>() ?? shellRoot.AddComponent<MeshRenderer>();
        targetRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
        targetRenderer.enabled = sourceRenderer.enabled;
        targetRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        targetRenderer.receiveShadows = sourceRenderer.receiveShadows;
        targetRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
        targetRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
        targetRenderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;

        MeshCollider? sourceCollider = sourceTransform.GetComponent<MeshCollider>();
        if (includeMeshCollider && sourceCollider != null)
        {
            MeshCollider targetCollider = shellRoot.GetComponent<MeshCollider>() ?? shellRoot.AddComponent<MeshCollider>();
            targetCollider.sharedMesh = sourceCollider.sharedMesh != null ? sourceCollider.sharedMesh : sourceFilter.sharedMesh;
            targetCollider.convex = sourceCollider.convex;
            targetCollider.isTrigger = sourceCollider.isTrigger;
            targetCollider.sharedMaterial = sourceCollider.sharedMaterial;
            targetCollider.cookingOptions = sourceCollider.cookingOptions;
        }
        else
        {
            MeshCollider? existingCollider = shellRoot.GetComponent<MeshCollider>();
            if (existingCollider != null)
            {
                Object.DestroyImmediate(existingCollider);
            }
        }

        return true;
    }

    internal static GameObject? AttachVisibleTemplateChild(BuildingObject shell, GameObject templateRoot, string childName)
    {
        return AttachTemplateChild(shell, templateRoot, childName, keepRenderersEnabled: true);
    }

    internal static GameObject? AttachTemplateChild(BuildingObject shell, GameObject templateRoot, string childName, bool keepRenderersEnabled)
    {
        if (shell == null || !IsUsableGameObject(templateRoot))
        {
            return null;
        }

        Transform? sourceTransform;
        try
        {
            sourceTransform = templateRoot.transform;
        }
        catch
        {
            return null;
        }

        if (sourceTransform == null || !IsUsableGameObject(sourceTransform.gameObject))
        {
            return null;
        }

        Transform? existing = shell.transform.Find(childName);
        if (existing != null)
        {
            try
            {
                if (IsUsableGameObject(existing.gameObject))
                {
                    Object.DestroyImmediate(existing.gameObject);
                }
            }
            catch
            {
                return null;
            }
        }

        GameObject clone;
        try
        {
            if (!IsUsableGameObject(sourceTransform.gameObject))
            {
                return null;
            }

            clone = Object.Instantiate(sourceTransform.gameObject, shell.transform);
            if (!IsUsableGameObject(clone))
            {
                return null;
            }

            clone.name = childName;
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one;
            ClearHideFlagsRecursively(clone);
            SetLayerRecursively(clone, shell.gameObject.layer);
        }
        catch
        {
            return null;
        }

        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true).ToArray())
        {
            Object.DestroyImmediate(behaviour);
        }

        if (!keepRenderersEnabled)
        {
            foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = true;
            }
        }

        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = true;
            collider.isTrigger = false;
        }

        return clone;
    }

    internal static bool IsUsableGameObject(GameObject? gameObject)
    {
        if (gameObject == null)
        {
            return false;
        }

        try
        {
            Transform? transform = gameObject.transform;
            return transform != null;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsCustomPlacementNode(BuildingPlacementNode? node)
    {
        return node != null && node.GetComponent<PlaceableNodesPlacementItemMarker>() != null;
    }

    internal static void SanitizePlacementNodeGhostPrefab(BuildingPlacementNode? node)
    {
        if (node == null)
        {
            return;
        }

        try
        {
            if (!IsUsableGameObject(node.GhostPrefab))
            {
                node.GhostPrefab = null;
            }
        }
        catch
        {
            node.GhostPrefab = null;
        }
    }

    internal static bool AttachTemplateColliderClone(BuildingObject shell, GameObject templateRoot, IDamageable? damageTarget)
    {
        Transform? sourceTransform = FindFirstMeshRoot(templateRoot.transform);
        if (sourceTransform == null)
        {
            return false;
        }

        Collider[] sourceColliders = sourceTransform.GetComponentsInChildren<Collider>(true);
        if (sourceColliders.Length == 0)
        {
            return false;
        }

        Transform? existing = shell.transform.Find("__PlaceableNodesCollider");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject clone = Object.Instantiate(sourceTransform.gameObject, shell.transform);
        clone.name = "__PlaceableNodesCollider";
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;
        clone.transform.localScale = Vector3.one;
        ClearHideFlagsRecursively(clone);
        SetLayerRecursively(clone, shell.gameObject.layer);

        foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = false;
        }

        foreach (MonoBehaviour behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true).ToArray())
        {
            if (behaviour is PlaceableNodesDamageProxy)
            {
                continue;
            }

            Object.DestroyImmediate(behaviour);
        }

        foreach (Component component in clone.GetComponentsInChildren<Component>(true).ToArray())
        {
            if (component is Transform or Collider or MeshFilter or Renderer or PlaceableNodesDamageProxy)
            {
                continue;
            }

            if (component is MonoBehaviour)
            {
                continue;
            }

            Object.DestroyImmediate(component);
        }

        if (damageTarget != null)
        {
            PlaceableNodesDamageProxy rootProxy = clone.GetComponent<PlaceableNodesDamageProxy>() ?? clone.AddComponent<PlaceableNodesDamageProxy>();
            rootProxy.SetTarget(damageTarget);
        }

        foreach (Collider collider in clone.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = true;
            collider.isTrigger = false;
            if (damageTarget == null)
            {
                continue;
            }

            PlaceableNodesDamageProxy proxy = collider.GetComponent<PlaceableNodesDamageProxy>() ?? collider.gameObject.AddComponent<PlaceableNodesDamageProxy>();
            proxy.SetTarget(damageTarget);
        }

        shell.PhysicalColliderObject = clone;

        return true;
    }

    private static Transform? FindFirstMeshRoot(Transform root)
    {
        if (root.TryGetComponent(out MeshFilter _) && root.TryGetComponent(out MeshRenderer _))
        {
            return root;
        }

        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            Transform candidate = renderer.transform;
            if (candidate.TryGetComponent(out MeshFilter _))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        if (!IsUsableGameObject(root))
        {
            return;
        }

        root.layer = layer;
        foreach (Transform child in root.transform)
        {
            if (child == null)
            {
                continue;
            }

            try
            {
                SetLayerRecursively(child.gameObject, layer);
            }
            catch
            {
                // Unity can yield destroyed children while iterating hierarchy changes.
            }
        }
    }

    internal static void DestroyColliders(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true).ToArray())
        {
            Object.DestroyImmediate(collider);
        }
    }

    internal static void HideDefaultTemplateCube(BuildingObject shell)
    {
        MeshRenderer? rootRenderer = shell.GetComponent<MeshRenderer>();
        if (rootRenderer != null)
        {
            rootRenderer.enabled = false;
        }

        foreach (MeshRenderer renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer == null)
            {
                continue;
            }

            if (renderer.transform.name.IndexOf("cube", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                renderer.enabled = false;
            }
        }
    }

    internal static Bounds BuildColliderBounds(Transform targetTransform, IEnumerable<Renderer> renderers)
    {
        Renderer[] rendererArray = renderers.Where(renderer => renderer != null && renderer.enabled).ToArray();
        if (rendererArray.Length == 0)
        {
            return new Bounds(Vector3.up * 0.5f, Vector3.one);
        }

        Matrix4x4 worldToLocal = targetTransform.worldToLocalMatrix;
        bool initialized = false;
        Bounds localBounds = default;

        foreach (Renderer renderer in rendererArray)
        {
            Bounds worldBounds = renderer.bounds;
            Vector3[] corners =
            {
                new(worldBounds.min.x, worldBounds.min.y, worldBounds.min.z),
                new(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z),
                new(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z),
                new(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z),
                new(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z),
                new(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z),
                new(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z),
                new(worldBounds.max.x, worldBounds.max.y, worldBounds.max.z),
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 localCorner = worldToLocal.MultiplyPoint3x4(corner);
                if (!initialized)
                {
                    localBounds = new Bounds(localCorner, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    localBounds.Encapsulate(localCorner);
                }
            }
        }

        return initialized ? localBounds : new Bounds(Vector3.up * 0.5f, Vector3.one);
    }

    internal static void ApplyColliderLayout(BuildingObject shell, Bounds bounds, bool hasTemplateCollider)
    {
        ApplyColliderLayout(shell, bounds, hasTemplateCollider, 0f);
    }

    internal static void ApplyColliderLayout(BuildingObject shell, Bounds bounds, bool hasTemplateCollider, float additionalYOffset)
    {
        if (shell.BuildingPlacementColliderObject == null)
        {
            GameObject placementObject = new("BuildingPlacementColliderObject");
            placementObject.transform.SetParent(shell.transform, false);
            shell.BuildingPlacementColliderObject = placementObject;
        }

        BoxCollider? worldCollider = shell.GetComponent<BoxCollider>();
        if (worldCollider != null)
        {
            Object.DestroyImmediate(worldCollider);
        }

        BoxCollider placementCollider = shell.BuildingPlacementColliderObject.GetComponent<BoxCollider>() ?? shell.BuildingPlacementColliderObject.AddComponent<BoxCollider>();
        placementCollider.center = bounds.center;
        placementCollider.size = EnsureMinimumSize(bounds.size);
        placementCollider.enabled = true;
        placementCollider.isTrigger = false;

        RemovePlacementDamageProxy(shell.BuildingPlacementColliderObject);
        SetLayerRecursively(shell.BuildingPlacementColliderObject, shell.gameObject.layer);
        shell.BuildingPlacementColliderObject.SetActive(!hasTemplateCollider);
        if (!hasTemplateCollider)
        {
            shell.PhysicalColliderObject = shell.BuildingPlacementColliderObject;
        }
        shell.BuildModePlacementOffset = new Vector3(0f, -bounds.min.y + additionalYOffset, 0f);
    }

    internal static void ApplyOrePlacementLayout(BuildingObject shell, Bounds bounds, IDamageable? damageTarget, bool hasTemplateCollider)
    {
        if (shell.BuildingPlacementColliderObject == null)
        {
            GameObject placementObject = new("BuildingPlacementColliderObject");
            placementObject.transform.SetParent(shell.transform, false);
            shell.BuildingPlacementColliderObject = placementObject;
        }

        BoxCollider? worldCollider = shell.GetComponent<BoxCollider>();
        if (worldCollider != null)
        {
            Object.DestroyImmediate(worldCollider);
        }

        BoxCollider placementCollider = shell.BuildingPlacementColliderObject.GetComponent<BoxCollider>() ?? shell.BuildingPlacementColliderObject.AddComponent<BoxCollider>();
        placementCollider.center = bounds.center;
        placementCollider.size = EnsureMinimumSize(bounds.size);
        placementCollider.enabled = true;
        placementCollider.isTrigger = false;

        SetLayerRecursively(shell.BuildingPlacementColliderObject, shell.gameObject.layer);
        if (hasTemplateCollider)
        {
            RemovePlacementDamageProxy(shell.BuildingPlacementColliderObject);
            shell.BuildingPlacementColliderObject.SetActive(false);
        }
        else
        {
            AttachPlacementDamageProxy(shell.BuildingPlacementColliderObject, damageTarget);
            shell.BuildingPlacementColliderObject.SetActive(true);
            shell.PhysicalColliderObject = shell.BuildingPlacementColliderObject;
        }

        shell.BuildModePlacementOffset = new Vector3(0f, -bounds.min.y, 0f);
    }

    internal static void EnsurePlacementCollision(BuildingObject buildingObject)
    {
        if (buildingObject == null || buildingObject.IsGhost || buildingObject.GetComponent<PlaceableNodesPlacementItemMarker>() == null)
        {
            return;
        }

        GameObject? physicalObject = buildingObject.PhysicalColliderObject;
        if (physicalObject != null)
        {
            if (!physicalObject.activeSelf)
            {
                physicalObject.SetActive(true);
            }

            SetLayerRecursively(physicalObject, buildingObject.gameObject.layer);
            foreach (Collider collider in physicalObject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }
        }

        GameObject? placementObject = buildingObject.BuildingPlacementColliderObject;
        if (placementObject == null)
        {
            return;
        }

        if (physicalObject == null || physicalObject == placementObject)
        {
            if (!placementObject.activeSelf)
            {
                placementObject.SetActive(true);
            }

            SetLayerRecursively(placementObject, buildingObject.gameObject.layer);
            foreach (Collider collider in placementObject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }
        }
        else if (placementObject.activeSelf)
        {
            placementObject.SetActive(false);
        }
    }

    internal static bool TryRepairPlacementNodeAttachment(BuildingPlacementNode? node, float maxDistance = 1.25f)
    {
        if (node == null)
        {
            return false;
        }

        BuildingObject? attachedBuildingObject = node.AttachedBuildingObject;
        if (attachedBuildingObject != null)
        {
            if (!attachedBuildingObject.IsGhost)
            {
                return true;
            }

            SetPrivateField(node, "<AttachedBuildingObject>k__BackingField", null);
        }

        AutoMiner[] miners = Object.FindObjectsByType<AutoMiner>(FindObjectsSortMode.None);
        float closestDistance = float.MaxValue;
        BuildingObject? closestMatch = null;

        foreach (AutoMiner miner in miners)
        {
            if (miner == null)
            {
                continue;
            }

            BuildingObject buildingObject = miner.GetComponentInParent<BuildingObject>();
            if (buildingObject == null || buildingObject.IsGhost || buildingObject.PlacementNodeRequirement != node.RequirementType)
            {
                continue;
            }

            bool alreadyAttached = BuildingPlacementNode.All.Any(other => other != null && other.AttachedBuildingObject == buildingObject);
            if (alreadyAttached)
            {
                continue;
            }

            float distance = Vector3.Distance(buildingObject.transform.position, node.transform.position);
            if (distance > maxDistance || distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestMatch = buildingObject;
        }

        if (closestMatch == null)
        {
            return false;
        }

        node.AttachBuilding(closestMatch);
        return true;
    }

    private static void AttachPlacementDamageProxy(GameObject placementObject, IDamageable? damageTarget)
    {
        PlaceableNodesDamageProxy? proxy = placementObject.GetComponent<PlaceableNodesDamageProxy>();
        if (damageTarget == null)
        {
            if (proxy != null)
            {
                Object.DestroyImmediate(proxy);
            }

            return;
        }

        proxy ??= placementObject.AddComponent<PlaceableNodesDamageProxy>();
        proxy.SetTarget(damageTarget);
    }

    private static void RemovePlacementDamageProxy(GameObject placementObject)
    {
        PlaceableNodesDamageProxy? proxy = placementObject.GetComponent<PlaceableNodesDamageProxy>();
        if (proxy != null)
        {
            Object.DestroyImmediate(proxy);
        }
    }

    private static Vector3 EnsureMinimumSize(Vector3 size)
    {
        return new Vector3(
            Mathf.Max(size.x, 0.2f),
            Mathf.Max(size.y, 0.2f),
            Mathf.Max(size.z, 0.2f));
    }
}
