using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private void TryConfigurePrefabs()
    {
        if (!_shopItemsRegistered || _allPrefabsConfigured)
        {
            return;
        }

        bool allConfigured = true;
        foreach (ShopItemRuntime runtime in _itemsByKey.Values)
        {
            if (runtime.IsConfigured)
            {
                continue;
            }

            bool configured = runtime.Plan.Kind == PlaceableNodeKind.OreNode
                ? TryConfigureOreNodeItem(runtime)
                : TryConfigureAutoMinerNodeItem(runtime);

            if (!configured)
            {
                allConfigured = false;
            }
        }

        _allPrefabsConfigured = allConfigured && _itemsByKey.Values.All(runtime => runtime.IsConfigured);
        if (_allPrefabsConfigured)
        {
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Prefab configuration complete.");
        }
    }

    private bool TryConfigureOreNodeItem(ShopItemRuntime runtime)
    {
        runtime.VariantPrefabs.Clear();

        for (int i = 0; i < runtime.Plan.VariantBlockIds.Length; i++)
        {
            OreNode? gameplayTemplate = FindOreTemplate(runtime.Plan.ResourceType, i);
            BuildingObject shell = i == 0
                ? runtime.PrimaryPrefab
                : CreateAdditionalVariantShell(runtime, runtime.Plan.VariantBlockIds[i], runtime.Plan.InternalName + "_Variant" + (i + 1));

            ConfigureShellDefaults(shell, runtime.Definition, runtime.Plan.VariantBlockIds[i]);
            ConfigureOreNodeVariant(shell, runtime.Plan, gameplayTemplate, i);
            runtime.VariantPrefabs.Add(shell);
        }

        runtime.Definition.BuildingPrefabs = new List<BuildingObject>(runtime.VariantPrefabs);
        runtime.Definition.QButtonFunction = "Cycle Variant";
        runtime.Definition.CanBePlacedInTerrain = true;
        runtime.IsConfigured = runtime.VariantPrefabs.Count == runtime.Plan.VariantBlockIds.Length;
        return runtime.IsConfigured;
    }

    private bool TryConfigureAutoMinerNodeItem(ShopItemRuntime runtime)
    {
        runtime.VariantPrefabs.Clear();

        BuildingPlacementNode? nodeTemplate = FindPlacementNodeTemplate(runtime.Plan.ResourceType);
        AutoMinerResourceDefinition? resourceDefinition = nodeTemplate?.AutoMinerResourceDefinition ?? FindAutoMinerResourceDefinition(runtime.Plan.ResourceType);
        if (resourceDefinition == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Missing auto-miner resource definition for {runtime.Plan.DisplayName}.");
            return false;
        }

        for (int i = 0; i < runtime.Plan.VariantBlockIds.Length; i++)
        {
            BuildingObject shell = i == 0
                ? runtime.PrimaryPrefab
                : CreateAdditionalVariantShell(runtime, runtime.Plan.VariantBlockIds[i], runtime.Plan.InternalName + "_Variant" + (i + 1));

            ConfigureShellDefaults(shell, runtime.Definition, runtime.Plan.VariantBlockIds[i]);
            bool variantConfigured = ConfigureAutoMinerPlacementNodeVariant(shell, runtime.Plan, nodeTemplate, resourceDefinition, i);
            if (!variantConfigured)
            {
                return false;
            }

            runtime.VariantPrefabs.Add(shell);
        }

        runtime.Definition.BuildingPrefabs = new List<BuildingObject>(runtime.VariantPrefabs);
        runtime.Definition.QButtonFunction = "Cycle Variant";
        runtime.Definition.CanBePlacedInTerrain = true;
        runtime.IsConfigured = runtime.VariantPrefabs.Count == runtime.Plan.VariantBlockIds.Length;
        return runtime.IsConfigured;
    }

    private void ConfigureOreNodeVariant(BuildingObject shell, ShopItemPlan plan, OreNode? gameplayTemplate, int variantIndex)
    {
        PlaceableNodesUtility.ResetShell(shell);
        PlaceableNodesUtility.HideDefaultTemplateCube(shell);

        OreNode oreNode = shell.GetComponent<OreNode>() ?? shell.gameObject.AddComponent<OreNode>();
        ConfigureOreNodeGameplay(oreNode, plan.ResourceType, gameplayTemplate);

        if (shell.GetComponent<PlaceableNodesOreMarker>() == null)
        {
            shell.gameObject.AddComponent<PlaceableNodesOreMarker>();
        }

        if (shell.GetComponent<PlaceableNodesOreRuntimeInitializer>() == null)
        {
            shell.gameObject.AddComponent<PlaceableNodesOreRuntimeInitializer>();
        }

        string prefabName = GetOreBundlePrefabName(plan.ResourceType, variantIndex);
        GameObject? bundlePrefab = FindBundledPrefab(prefabName);
        GameObject? visualTemplate = bundlePrefab ?? gameplayTemplate?.gameObject;
        GameObject? collisionTemplate = gameplayTemplate != null ? gameplayTemplate.gameObject : visualTemplate;
        if (!TryApplyVisualTemplate(shell, oreNode, visualTemplate, collisionTemplate, useOrePlacementLayout: true))
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Missing ore visual for {plan.DisplayName} variant {variantIndex + 1}.");
        }
    }

    private void ConfigureOreNodeGameplay(OreNode oreNode, ResourceType resourceType, OreNode? template)
    {
        oreNode.ResourceType = resourceType;

        if (template != null)
        {
            oreNode.Health = template.Health > 0f ? template.Health : GetDefaultOreHealth(resourceType);
            oreNode.MinDrops = template.MinDrops > 0 ? template.MinDrops : PlaceableNodesConstants.DefaultOreMinDrops;
            oreNode.MaxDrops = template.MaxDrops >= oreNode.MinDrops ? template.MaxDrops : PlaceableNodesConstants.DefaultOreMaxDrops;

            List<WeightedNodeDrop> templateDrops = PlaceableNodesUtility.GetPrivateField<List<WeightedNodeDrop>>(template, "_possibleDrops") ?? new List<WeightedNodeDrop>();
            SoundDefinition? takeDamageSound = PlaceableNodesUtility.GetPrivateField<SoundDefinition>(template, "_takeDamageSoundDefinition") ?? FindFallbackOreTakeDamageSound(resourceType);

            PlaceableNodesUtility.SetPrivateField(oreNode, "_possibleDrops", new List<WeightedNodeDrop>(templateDrops));
            PlaceableNodesUtility.SetPrivateField(oreNode, "_takeDamageSoundDefinition", takeDamageSound);
            PlaceableNodesUtility.SetPrivateField(oreNode, "_models", Array.Empty<GameObject>());
            return;
        }

        oreNode.Health = GetDefaultOreHealth(resourceType);
        oreNode.MinDrops = PlaceableNodesConstants.DefaultOreMinDrops;
        oreNode.MaxDrops = PlaceableNodesConstants.DefaultOreMaxDrops;

        List<WeightedNodeDrop> fallbackDrops = new();
        OrePiece? orePrefab = FindOrePiecePrefab(resourceType);
        if (orePrefab != null)
        {
            fallbackDrops.Add(new WeightedNodeDrop
            {
                OrePrefab = orePrefab,
                Weight = 100f
            });
        }

        PlaceableNodesUtility.SetPrivateField(oreNode, "_possibleDrops", fallbackDrops);
        PlaceableNodesUtility.SetPrivateField(oreNode, "_takeDamageSoundDefinition", FindFallbackOreTakeDamageSound(resourceType));
        PlaceableNodesUtility.SetPrivateField(oreNode, "_models", Array.Empty<GameObject>());
    }

    private static float GetDefaultOreHealth(ResourceType resourceType)
    {
        return resourceType == ResourceType.Gold ? 30f : PlaceableNodesConstants.DefaultOreHealth;
    }

    private bool ConfigureAutoMinerPlacementNodeVariant(BuildingObject shell, ShopItemPlan plan, BuildingPlacementNode? nodeTemplate, AutoMinerResourceDefinition resourceDefinition, int variantIndex)
    {
        PlaceableNodesUtility.ResetShell(shell);
        PlaceableNodesUtility.HideDefaultTemplateCube(shell);

        string prefabName = GetAutoMinerBundlePrefabName(plan.ResourceType, variantIndex);
        GameObject? bundlePrefab = FindBundledPrefab(prefabName);
        if (bundlePrefab == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Missing assetbundle prefab '{prefabName}' for {plan.DisplayName}.");
            return false;
        }

        GameObject? visibleChild = PlaceableNodesUtility.AttachVisibleTemplateChild(shell, bundlePrefab, "__PlaceableNodesNodeVisual");
        if (visibleChild == null)
        {
            return false;
        }

        GameObject? ghostTemplate = ResolveAutoMinerGhostTemplate(nodeTemplate, visibleChild, out bool usedFallbackVisual);
        if (ghostTemplate == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Missing auto-miner ghost template for {plan.DisplayName}.");
            return false;
        }

        GameObject? ghostChild = PlaceableNodesUtility.AttachTemplateChild(shell, ghostTemplate, "GhostMiner", keepRenderersEnabled: true);
        if (ghostChild == null)
        {
            return false;
        }

        PlaceableNodesUtility.DestroyColliders(ghostChild);
        ghostChild.SetActive(false);
        if (usedFallbackVisual && ghostChild.GetComponent<PlaceableNodesGhostFallbackMarker>() == null)
        {
            ghostChild.AddComponent<PlaceableNodesGhostFallbackMarker>();
        }

        BuildingPlacementNode node = shell.GetComponent<BuildingPlacementNode>() ?? shell.gameObject.AddComponent<BuildingPlacementNode>();
        node.RequirementType = nodeTemplate?.RequirementType ?? PlacementNodeRequirement.AutoMiner;
        node.AutoMinerResourceDefinition = resourceDefinition;
        node.GhostPrefab = ghostChild;

        if (shell.GetComponent<PlaceableNodesPlacementNodeBootstrap>() == null)
        {
            shell.gameObject.AddComponent<PlaceableNodesPlacementNodeBootstrap>();
        }

        shell.PhysicalColliderObject = visibleChild;

        Renderer[] renderers = visibleChild.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0
            ? PlaceableNodesUtility.BuildColliderBounds(shell.transform, renderers)
            : new Bounds(Vector3.up * 0.5f, Vector3.one);

        PlaceableNodesUtility.ApplyColliderLayout(
            shell,
            bounds,
            hasTemplateCollider: true,
            PlaceableNodesConstants.AutoMinerNodeVerticalPlacementAdjustment);
        return true;
    }

    internal void RefreshPlacementNodeGhostIfNeeded(BuildingPlacementNode? node)
    {
        if (node == null || node.GetComponent<PlaceableNodesPlacementItemMarker>() == null)
        {
            return;
        }

        PlaceableNodesUtility.SanitizePlacementNodeGhostPrefab(node);

        if (PlaceableNodesUtility.IsUsableGameObject(node.GhostPrefab))
        {
            PlaceableNodesGhostFallbackMarker? fallbackMarker = null;
            try
            {
                fallbackMarker = node.GhostPrefab.GetComponent<PlaceableNodesGhostFallbackMarker>();
            }
            catch
            {
                node.GhostPrefab = null;
            }

            if (fallbackMarker == null && PlaceableNodesUtility.IsUsableGameObject(node.GhostPrefab))
            {
                return;
            }
        }
        else if (node.GhostPrefab != null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Cleared stale GhostPrefab reference on node '{node.name}' before refreshing ghost.");
            node.GhostPrefab = null;
        }

        BuildingObject? shell = node.GetComponent<BuildingObject>();
        GameObject? visibleChild = shell?.transform.Find("__PlaceableNodesNodeVisual")?.gameObject;
        GameObject? ghostTemplate = ResolveAutoMinerGhostTemplate(node, visibleChild, out bool usedFallbackVisual);
        if (ghostTemplate == null || shell == null)
        {
            return;
        }

        GameObject? ghostChild = PlaceableNodesUtility.AttachTemplateChild(shell, ghostTemplate, "GhostMiner", keepRenderersEnabled: true);
        if (ghostChild == null)
        {
            node.GhostPrefab = null;
            return;
        }

        PlaceableNodesUtility.DestroyColliders(ghostChild);
        ghostChild.SetActive(false);
        if (usedFallbackVisual && ghostChild.GetComponent<PlaceableNodesGhostFallbackMarker>() == null)
        {
            ghostChild.AddComponent<PlaceableNodesGhostFallbackMarker>();
        }
        node.GhostPrefab = ghostChild;
        PlaceableNodesUtility.SanitizePlacementNodeGhostPrefab(node);
    }

    private GameObject? ResolveAutoMinerGhostTemplate(BuildingPlacementNode? nodeTemplate, GameObject? fallbackVisual, out bool usedFallbackVisual)
    {
        usedFallbackVisual = false;

        if (PlaceableNodesUtility.IsUsableGameObject(_cachedAutoMinerGhostTemplate))
        {
            return _cachedAutoMinerGhostTemplate;
        }

        if (_cachedAutoMinerGhostTemplate != null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Discarded stale cached auto-miner ghost template reference.");
            _cachedAutoMinerGhostTemplate = null;
        }

        GameObject? template = FindAutoMinerGhostFromMiner()
            ?? FindGhostPrefabFromNode(nodeTemplate)
            ?? FindAutoMinerGhostTemplate();

        if (PlaceableNodesUtility.IsUsableGameObject(template))
        {
            _cachedAutoMinerGhostTemplate = template;
            return template;
        }

        if (template != null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Resolved auto-miner ghost template was stale; falling back to visual template.");
        }

        usedFallbackVisual = PlaceableNodesUtility.IsUsableGameObject(fallbackVisual);
        return usedFallbackVisual ? fallbackVisual : null;
    }

    private GameObject? FindAutoMinerGhostTemplate()
    {
        BuildingPlacementNode[] nodes = Resources.FindObjectsOfTypeAll<BuildingPlacementNode>()
            .Where(node =>
                node != null &&
                node.GetComponent<PlaceableNodesPlacementItemMarker>() == null &&
                node.RequirementType == PlacementNodeRequirement.AutoMiner)
            .ToArray();

        BuildingPlacementNode? exactTemplate = nodes.FirstOrDefault(node =>
            string.Equals(node.gameObject.name, "AutoMinerNodeIron", StringComparison.OrdinalIgnoreCase));
        GameObject? exactGhost = FindGhostPrefabFromNode(exactTemplate);
        if (exactGhost != null)
        {
            return exactGhost;
        }

        foreach (BuildingPlacementNode node in nodes)
        {
            GameObject? ghost = FindGhostPrefabFromNode(node);
            if (ghost != null)
            {
                return ghost;
            }
        }

        return null;
    }

    private static GameObject? FindGhostPrefabFromNode(BuildingPlacementNode? node)
    {
        if (node == null)
        {
            return null;
        }

        if (PlaceableNodesUtility.IsUsableGameObject(node.GhostPrefab))
        {
            return node.GhostPrefab;
        }

        Transform? ghostChild = node.transform
            .GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child =>
                child != null &&
                string.Equals(child.name, "GhostMiner", StringComparison.OrdinalIgnoreCase));

        return PlaceableNodesUtility.IsUsableGameObject(ghostChild?.gameObject)
            ? ghostChild!.gameObject
            : null;
    }

    private static GameObject? FindAutoMinerGhostFromMiner()
    {
        AutoMiner[] miners = Resources.FindObjectsOfTypeAll<AutoMiner>()
            .Where(miner => miner != null)
            .ToArray();

        BuildingObject? heldGhostTemplate = miners
            .Select(miner => miner.GetComponentInParent<BuildingObject>())
            .FirstOrDefault(buildingObject =>
                buildingObject != null &&
                buildingObject.IsGhost &&
                buildingObject.GetComponentsInChildren<Renderer>(true).Length > 0);

        if (heldGhostTemplate != null)
        {
            return PlaceableNodesUtility.IsUsableGameObject(heldGhostTemplate.gameObject)
                ? heldGhostTemplate.gameObject
                : null;
        }

        BuildingObject? nonGhostTemplate = miners
            .Select(miner => miner.GetComponentInParent<BuildingObject>())
            .FirstOrDefault(buildingObject =>
                buildingObject != null &&
                !buildingObject.IsGhost &&
                buildingObject.GetComponentsInChildren<Renderer>(true).Length > 0);

        return PlaceableNodesUtility.IsUsableGameObject(nonGhostTemplate?.gameObject)
            ? nonGhostTemplate!.gameObject
            : null;
    }

    internal void InitializePlacedOreNode(OreNode oreNode)
    {
        if (oreNode == null || oreNode.GetComponent<PlaceableNodesOreMarker>() == null)
        {
            return;
        }

        OreNode? template = FindOreTemplate(oreNode.ResourceType, 0);
        ConfigureOreNodeGameplay(oreNode, oreNode.ResourceType, template);
    }

    private bool TryApplyVisualTemplate(
        BuildingObject shell,
        OreNode? oreNode,
        GameObject? visualTemplate,
        GameObject? collisionTemplate,
        bool useOrePlacementLayout)
    {
        if (visualTemplate == null)
        {
            return false;
        }

        if (!PlaceableNodesUtility.CopyRootMeshTemplateToShell(visualTemplate, shell.gameObject, includeMeshCollider: false))
        {
            return false;
        }

        bool attachedTemplateCollider = PlaceableNodesUtility.AttachTemplateColliderClone(shell, collisionTemplate ?? visualTemplate, oreNode);

        MeshRenderer? renderer = shell.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
        }

        if (oreNode != null)
        {
            PlaceableNodesUtility.SetPrivateField(oreNode, "_models", Array.Empty<GameObject>());
        }

        Renderer[] renderers = shell.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers.Length > 0
            ? PlaceableNodesUtility.BuildColliderBounds(shell.transform, renderers)
            : new Bounds(Vector3.up * 0.5f, Vector3.one);

        if (useOrePlacementLayout)
        {
            PlaceableNodesUtility.ApplyOrePlacementLayout(shell, bounds, oreNode, attachedTemplateCollider);
        }
        else
        {
            PlaceableNodesUtility.ApplyColliderLayout(shell, bounds, attachedTemplateCollider);
        }

        return true;
    }

    private OreNode? FindOreTemplate(ResourceType resourceType, int variantIndex)
    {
        string exactName = GetOreTemplateName(resourceType, variantIndex);
        OreNode[] nodes = Resources.FindObjectsOfTypeAll<OreNode>()
            .Where(node => node != null && node.GetComponent<PlaceableNodesOreMarker>() == null)
            .ToArray();

        OreNode? exactMatch = nodes.FirstOrDefault(node => string.Equals(node.gameObject.name, exactName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch;
        }

        string prefix = GetOreTemplatePrefix(resourceType);
        return nodes.FirstOrDefault(node => node.gameObject.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private BuildingPlacementNode? FindPlacementNodeTemplate(ResourceType resourceType)
    {
        string exactName = "AutoMinerNode" + GetResourceKey(resourceType);
        BuildingPlacementNode[] nodes = Resources.FindObjectsOfTypeAll<BuildingPlacementNode>()
            .Where(node =>
                node != null &&
                node.GetComponent<PlaceableNodesPlacementItemMarker>() == null)
            .ToArray();

        BuildingPlacementNode? exactMatch = nodes.FirstOrDefault(node => string.Equals(node.gameObject.name, exactName, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch;
        }

        BuildingPlacementNode? resourceMatch = nodes.FirstOrDefault(node =>
            node.AutoMinerResourceDefinition != null &&
            node.AutoMinerResourceDefinition.GetPrimaryResourceType() == resourceType &&
            node.RequirementType == PlacementNodeRequirement.AutoMiner);

        if (resourceMatch != null)
        {
            return resourceMatch;
        }

        return nodes.FirstOrDefault(node =>
            node.AutoMinerResourceDefinition != null &&
            node.AutoMinerResourceDefinition.GetPrimaryResourceType() == resourceType);
    }

    private AutoMinerResourceDefinition? FindAutoMinerResourceDefinition(ResourceType resourceType)
    {
        AutoMinerResourceDefinition[] definitions = Resources.FindObjectsOfTypeAll<AutoMinerResourceDefinition>()
            .Where(definition => definition != null && definition.GetPrimaryResourceType() == resourceType)
            .ToArray();

        AutoMinerResourceDefinition? standardDefinition = definitions.FirstOrDefault(MatchesStandardAutoMinerDefinition);
        if (standardDefinition != null)
        {
            return standardDefinition;
        }

        AutoMinerResourceDefinition? normalOreDefinition = definitions.FirstOrDefault(ProducesNormalOre);
        if (normalOreDefinition != null)
        {
            return normalOreDefinition;
        }

        return definitions.FirstOrDefault() ?? GetOrCreateRuntimeStandardAutoMinerResourceDefinition(resourceType);
    }

    private bool ProducesNormalOre(AutoMinerResourceDefinition definition)
    {
        List<WeightedOreChance>? chances = PlaceableNodesUtility.GetPrivateField<List<WeightedOreChance>>(definition, "_possibleOrePrefabs");
        if (chances == null || chances.Count == 0)
        {
            return false;
        }

        bool hasNormalOre = chances.Any(chance =>
            chance?.OrePrefab != null &&
            chance.OrePrefab.ResourceType == definition.GetPrimaryResourceType() &&
            chance.OrePrefab.PieceType == PieceType.Ore);

        bool hasOreCluster = chances.Any(chance =>
            chance?.OrePrefab != null &&
            chance.OrePrefab.ResourceType == definition.GetPrimaryResourceType() &&
            chance.OrePrefab.PieceType == PieceType.OreCluster);

        return hasNormalOre && !hasOreCluster;
    }

    private bool MatchesStandardAutoMinerDefinition(AutoMinerResourceDefinition definition)
    {
        List<WeightedOreChance>? chances = PlaceableNodesUtility.GetPrivateField<List<WeightedOreChance>>(definition, "_possibleOrePrefabs");
        if (chances == null || chances.Count == 0)
        {
            return false;
        }

        ResourceType primaryResourceType = definition.GetPrimaryResourceType();
        if (primaryResourceType == ResourceType.INVALID)
        {
            return false;
        }

        bool hasNormalOre = chances.Any(chance =>
            chance?.OrePrefab != null &&
            chance.OrePrefab.ResourceType == primaryResourceType &&
            chance.OrePrefab.PieceType == PieceType.Ore);

        bool hasCrushedOre = chances.Any(chance =>
            chance?.OrePrefab != null &&
            chance.OrePrefab.ResourceType == primaryResourceType &&
            chance.OrePrefab.PieceType == PieceType.Crushed);

        bool hasOreCluster = chances.Any(chance =>
            chance?.OrePrefab != null &&
            chance.OrePrefab.ResourceType == primaryResourceType &&
            chance.OrePrefab.PieceType == PieceType.OreCluster);

        return hasNormalOre && hasCrushedOre && !hasOreCluster;
    }

    private AutoMinerResourceDefinition? GetOrCreateRuntimeStandardAutoMinerResourceDefinition(ResourceType resourceType)
    {
        if (_runtimeAutoMinerDefinitions.TryGetValue(resourceType, out AutoMinerResourceDefinition existing))
        {
            return existing;
        }

        AutoMinerDefinitionSpec? spec = GetStandardAutoMinerDefinitionSpec(resourceType);
        if (spec == null)
        {
            return null;
        }

        List<WeightedOreChance>? chances = BuildStandardAutoMinerChanceTable(resourceType);
        if (chances == null || chances.Count == 0)
        {
            return null;
        }

        AutoMinerResourceDefinition definition = ScriptableObject.CreateInstance<AutoMinerResourceDefinition>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = $"PlaceableNodes_{GetResourceKey(resourceType)}AutoMinerResourceDefinition";
        definition.SpawnProbability = spec.SpawnProbability;
        definition.SpawnRate = spec.SpawnRate;
        PlaceableNodesUtility.SetPrivateField(definition, "_possibleOrePrefabs", chances);
        _runtimeAutoMinerDefinitions[resourceType] = definition;
        return definition;
    }

    private List<WeightedOreChance>? BuildStandardAutoMinerChanceTable(ResourceType resourceType)
    {
        AutoMinerDefinitionSpec? spec = GetStandardAutoMinerDefinitionSpec(resourceType);
        if (spec == null)
        {
            return null;
        }

        OrePiece? normalOrePrefab = FindOrePiecePrefab(resourceType, PieceType.Ore);
        OrePiece? crushedOrePrefab = FindOrePiecePrefab(resourceType, PieceType.Crushed);
        OrePiece? diamondPrefab = FindOrePiecePrefab(ResourceType.Diamond, PieceType.Gem);
        OrePiece? emeraldPrefab = FindOrePiecePrefab(ResourceType.Emerald, PieceType.Gem);
        if (normalOrePrefab == null || crushedOrePrefab == null)
        {
            return null;
        }

        List<WeightedOreChance> chances = new()
        {
            new WeightedOreChance
            {
                OrePrefab = normalOrePrefab,
                Weight = spec.NormalOreWeight
            },
            new WeightedOreChance
            {
                OrePrefab = crushedOrePrefab,
                Weight = spec.CrushedOreWeight
            }
        };

        if (spec.DiamondWeight > 0f && diamondPrefab != null)
        {
            chances.Add(new WeightedOreChance
            {
                OrePrefab = diamondPrefab,
                Weight = spec.DiamondWeight
            });
        }

        if (spec.EmeraldWeight > 0f && emeraldPrefab != null)
        {
            chances.Add(new WeightedOreChance
            {
                OrePrefab = emeraldPrefab,
                Weight = spec.EmeraldWeight
            });
        }

        return chances;
    }

    private static AutoMinerDefinitionSpec? GetStandardAutoMinerDefinitionSpec(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Coal => new AutoMinerDefinitionSpec(85f, 3.3f, 94f, 5.5f, 0.1f, 0.4f),
            ResourceType.Copper => new AutoMinerDefinitionSpec(88f, 3.6f, 92f, 7.8f, 0f, 0.2f),
            ResourceType.Iron => new AutoMinerDefinitionSpec(85f, 4f, 94f, 5.2f, 0.3f, 0.5f),
            ResourceType.Gold => new AutoMinerDefinitionSpec(75f, 5.6f, 93f, 5.5f, 1f, 0.5f),
            _ => null
        };
    }

    private static string GetOreTemplatePrefix(ResourceType resourceType)
    {
        return GetResourceKey(resourceType) + "Node";
    }

    private static string GetOreTemplateName(ResourceType resourceType, int variantIndex)
    {
        return GetOreTemplatePrefix(resourceType) + (variantIndex + 1);
    }

    private static string GetOreBundlePrefabName(ResourceType resourceType, int variantIndex)
    {
        return GetResourceKey(resourceType) + "Node" + (variantIndex + 1);
    }

    private static string GetAutoMinerBundlePrefabName(ResourceType resourceType, int variantIndex)
    {
        return "AutoMiner_" + GetResourceKey(resourceType) + "_OreNode" + (variantIndex + 1);
    }

    private static string GetResourceKey(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Coal => "Coal",
            ResourceType.Copper => "Copper",
            ResourceType.Iron => "Iron",
            ResourceType.Gold => "Gold",
            _ => resourceType.ToString()
        };
    }

    private OrePiece? FindOrePiecePrefab(ResourceType resourceType)
    {
        return FindOrePiecePrefab(resourceType, PieceType.Ore);
    }

    private OrePiece? FindOrePiecePrefab(ResourceType resourceType, PieceType pieceType)
    {
        SavingLoadingManager? savingLoadingManager = Object.FindFirstObjectByType<SavingLoadingManager>();
        OrePiece? prefab = savingLoadingManager?.GetOrePiecePrefab(resourceType, pieceType, false);
        if (prefab != null)
        {
            return prefab;
        }

        return Resources.FindObjectsOfTypeAll<OrePiece>()
            .FirstOrDefault(orePiece =>
                orePiece != null &&
                orePiece.ResourceType == resourceType &&
                orePiece.PieceType == pieceType &&
                orePiece.GetComponent<PlaceableNodesPlacementItemMarker>() == null);
    }

    private SoundDefinition? FindFallbackOreTakeDamageSound(ResourceType resourceType)
    {
        OreNode? firstTemplate = Resources.FindObjectsOfTypeAll<OreNode>()
            .FirstOrDefault(node =>
                node != null &&
                node.GetComponent<PlaceableNodesOreMarker>() == null &&
                node.ResourceType == resourceType);

        firstTemplate ??= Resources.FindObjectsOfTypeAll<OreNode>()
            .FirstOrDefault(node => node != null && node.GetComponent<PlaceableNodesOreMarker>() == null);

        return firstTemplate == null
            ? null
            : PlaceableNodesUtility.GetPrivateField<SoundDefinition>(firstTemplate, "_takeDamageSoundDefinition");
    }

    private void ConfigureShellDefaults(BuildingObject shell, BuildingInventoryDefinition definition, int blockId)
    {
        shell.Definition = definition;
        shell.SavableObjectID = (SavableObjectID)blockId;
        shell.RequiresFlatGround = false;
        shell.PlacementNodeRequirement = PlacementNodeRequirement.None;
        shell.SupportType = SupportType.None;
        shell.BuildingSupportsEnabled = false;
        shell.RotatingShouldMirrorWhenSnapped = false;
        definition.CanBePlacedInTerrain = true;

        if (shell.PhysicalColliderObject == null)
        {
            shell.PhysicalColliderObject = shell.gameObject;
        }

        if (shell.BuildingPlacementColliderObject == null)
        {
            GameObject placementObject = new("BuildingPlacementColliderObject");
            placementObject.transform.SetParent(shell.transform, false);
            SetBuildingLayer(placementObject);
            shell.BuildingPlacementColliderObject = placementObject;
        }

        if (shell.BuildingCrateSpawnPoint == null)
        {
            GameObject spawnPoint = new("BuildingCrateSpawnPoint");
            spawnPoint.transform.SetParent(shell.transform, false);
            shell.BuildingCrateSpawnPoint = spawnPoint.transform;
        }

        if (shell.GetComponent<PlaceableNodesPlacementColliderController>() == null)
        {
            shell.gameObject.AddComponent<PlaceableNodesPlacementColliderController>();
        }

        if (shell.GetComponent<PlaceableNodesPlacementItemMarker>() == null)
        {
            shell.gameObject.AddComponent<PlaceableNodesPlacementItemMarker>();
        }
    }

    private BuildingObject CreateAdditionalVariantShell(ShopItemRuntime runtime, int blockId, string name)
    {
        BuildingObject shell = CreatePrimaryShell(GetOrCreateRuntimePrefabRoot(), name);
        ConfigureShellDefaults(shell, runtime.Definition, blockId);
        return shell;
    }

    private void EnsureSavablePrefabRegistration()
    {
        if (_savablePrefabsRegistered || !_shopItemsRegistered)
        {
            return;
        }

        SavingLoadingManager? savingLoadingManager = Object.FindFirstObjectByType<SavingLoadingManager>();
        if (savingLoadingManager == null)
        {
            return;
        }

        foreach (ShopItemRuntime runtime in _itemsByKey.Values)
        {
            foreach (BuildingObject prefab in runtime.Definition.BuildingPrefabs ?? runtime.VariantPrefabs)
            {
                RegisterAdditionalSavablePrefab(savingLoadingManager, prefab);
            }

            runtime.SavablePrefabsRegistered = true;
        }

        _savablePrefabsRegistered = _itemsByKey.Values.All(runtime => runtime.SavablePrefabsRegistered);
    }

    private static void RegisterAdditionalSavablePrefab(SavingLoadingManager savingLoadingManager, BuildingObject shell)
    {
        if (!savingLoadingManager.AllSavableObjectPrefabs.Contains(shell.gameObject))
        {
            savingLoadingManager.AllSavableObjectPrefabs.Add(shell.gameObject);
        }

        Dictionary<SavableObjectID, GameObject>? lookup =
            AccessTools.Field(typeof(SavingLoadingManager), "_lookup")?.GetValue(savingLoadingManager) as Dictionary<SavableObjectID, GameObject>;

        if (lookup != null)
        {
            lookup[shell.SavableObjectID] = shell.gameObject;
        }
    }

    private sealed class AutoMinerDefinitionSpec
    {
        public AutoMinerDefinitionSpec(
            float spawnProbability,
            float spawnRate,
            float normalOreWeight,
            float crushedOreWeight,
            float diamondWeight,
            float emeraldWeight)
        {
            SpawnProbability = spawnProbability;
            SpawnRate = spawnRate;
            NormalOreWeight = normalOreWeight;
            CrushedOreWeight = crushedOreWeight;
            DiamondWeight = diamondWeight;
            EmeraldWeight = emeraldWeight;
        }

        public float SpawnProbability { get; }

        public float SpawnRate { get; }

        public float NormalOreWeight { get; }

        public float CrushedOreWeight { get; }

        public float DiamondWeight { get; }

        public float EmeraldWeight { get; }
    }
}
