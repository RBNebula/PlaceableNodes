using System.Collections.Generic;

namespace PlaceableNodes;

internal enum PlaceableNodeKind
{
    OreNode,
    AutoMinerPlacementNode
}

internal sealed class ShopItemPlan
{
    public ShopItemPlan(
        PlaceableNodeKind kind,
        ResourceType resourceType,
        string resourceKey,
        string displayName,
        string internalName,
        string description,
        string shopCategory,
        int price,
        int[] variantBlockIds)
    {
        Kind = kind;
        ResourceType = resourceType;
        ResourceKey = resourceKey;
        DisplayName = displayName;
        InternalName = internalName;
        Description = description;
        ShopCategory = shopCategory;
        Price = price;
        VariantBlockIds = variantBlockIds;
    }

    public PlaceableNodeKind Kind { get; }
    public ResourceType ResourceType { get; }
    public string ResourceKey { get; }
    public string DisplayName { get; }
    public string InternalName { get; }
    public string Description { get; }
    public string ShopCategory { get; }
    public int Price { get; }
    public int[] VariantBlockIds { get; }
}

internal sealed class ShopItemRuntime
{
    public ShopItemRuntime(
        ShopItemPlan plan,
        BuildingObject primaryPrefab,
        BuildingInventoryDefinition definition,
        ShopItemDefinition shopDefinition)
    {
        Plan = plan;
        PrimaryPrefab = primaryPrefab;
        Definition = definition;
        ShopDefinition = shopDefinition;
    }

    public ShopItemPlan Plan { get; }
    public BuildingObject PrimaryPrefab { get; }
    public BuildingInventoryDefinition Definition { get; }
    public ShopItemDefinition ShopDefinition { get; }
    public List<BuildingObject> VariantPrefabs { get; } = new();
    public bool IsConfigured { get; set; }
    public bool SavablePrefabsRegistered { get; set; }
}

internal sealed class ImportedAssetRecord
{
    public ImportedAssetRecord(string kind, string resourceKey, string sourceRelativePath, string targetRelativePath)
    {
        Kind = kind;
        ResourceKey = resourceKey;
        SourceRelativePath = sourceRelativePath;
        TargetRelativePath = targetRelativePath;
    }

    public string Kind { get; }
    public string ResourceKey { get; }
    public string SourceRelativePath { get; }
    public string TargetRelativePath { get; }
    public bool ExistsInRip { get; set; }
    public bool ExistsInProject { get; set; }
}
