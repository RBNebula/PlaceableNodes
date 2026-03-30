using System.Collections.Generic;

namespace PlaceableNodes;

internal static class PlaceableNodesConstants
{
    public const string OreShopCategory = "OreNodes";
    public const string AutoMinerShopCategory = "AutoNodes";
    public const string ResourceFolderName = "Resources";
    public const string ImportedAssetsFolderName = "ImportedAssets";
    public const string AssetBundlesFolderName = "AssetBundles";
    public const string NodeAssetBundleFileName = "minemogul_nodes";
    public const string IconsFolderName = "Icons";
    public const string RipRootPath = @"C:\Users\main\Desktop\RIP 2.0.25";
    public const int OreNodePrice = 50;
    public const int AutoMinerNodePrice = 30;
    public const int MaxStackSize = 50;
    public const float AutoMinerNodeVerticalPlacementAdjustment = -0.32f;
    public const float DefaultOreHealth = 20f;
    public const int DefaultOreMinDrops = 2;
    public const int DefaultOreMaxDrops = 3;

    public static readonly ShopItemPlan[] Plans =
    {
        CreateOrePlan(ResourceType.Coal, "Coal", new[] { 6400, 6401, 6402 }),
        CreateOrePlan(ResourceType.Copper, "Copper", new[] { 6410, 6411, 6412 }),
        CreateOrePlan(ResourceType.Iron, "Iron", new[] { 6420, 6421, 6422 }),
        CreateOrePlan(ResourceType.Gold, "Gold", new[] { 6430, 6431, 6432 }),
        CreateAutoMinerNodePlan(ResourceType.Coal, "Coal", new[] { 6500, 6501 }),
        CreateAutoMinerNodePlan(ResourceType.Copper, "Copper", new[] { 6510, 6511 }),
        CreateAutoMinerNodePlan(ResourceType.Iron, "Iron", new[] { 6520, 6521 }),
        CreateAutoMinerNodePlan(ResourceType.Gold, "Gold", new[] { 6530, 6531 }),
    };

    public static IEnumerable<ImportedAssetRecord> ImportedAssets
    {
        get
        {
            return CreateImportedAssets();
        }
    }

    public static IReadOnlyList<string> GetCuratedAutoMinerDefinitionNames(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Coal => new[]
            {
                "HeavyMinerDef-Coal-Standard",
                "MinerDef-Coal-Standard",
                "MinerDef-Coal-HighDiamond"
            },
            ResourceType.Copper => new[]
            {
                "MinerDef-Copper-Basic",
                "MinerDef-CopperAndCoal-LowCopper",
                "HeavyMinerDef-Copper-Standard",
                "MinerDef-CopperAndCoal-Standard"
            },
            ResourceType.Gold => new[]
            {
                "MinerDef-GoldAndCoal-Standard",
                "HeavyMinerDef-Gold-Standard",
                "MinerDef-Gold-Pure",
                "MinerDef-Gold-Standard"
            },
            ResourceType.Iron => new[]
            {
                "HeavyMinerDef-Iron-HighGeode",
                "HeavyMinerDef-Iron-Standard",
                "MinerDef-Iron-Basic",
                "MinerDef-Iron-HighEmerald",
                "MinerDef-Iron-Standard",
                "MinerDef-IronAndCoal-Standard"
            },
            _ => System.Array.Empty<string>()
        };
    }

    private static ShopItemPlan CreateOrePlan(ResourceType resourceType, string resourceKey, int[] variantBlockIds)
    {
        return new ShopItemPlan(
            PlaceableNodeKind.OreNode,
            resourceType,
            resourceKey,
            resourceKey + " Ore Node",
            "PlaceableNodes_" + resourceKey + "OreNode",
            "Place a mineable " + resourceKey + " node. Press Q to cycle the node variants.",
            OreShopCategory,
            OreNodePrice,
            variantBlockIds);
    }

    private static ShopItemPlan CreateAutoMinerNodePlan(ResourceType resourceType, string resourceKey, int[] variantBlockIds)
    {
        return new ShopItemPlan(
            PlaceableNodeKind.AutoMinerPlacementNode,
            resourceType,
            resourceKey,
            resourceKey + " Auto-Miner Node",
            "PlaceableNodes_" + resourceKey + "AutoMinerNode",
            "Place a " + resourceKey + " auto-miner ore node. Press Q to swap between OreNode1 and OreNode2.",
            AutoMinerShopCategory,
            AutoMinerNodePrice,
            variantBlockIds);
    }

    private static IEnumerable<ImportedAssetRecord> CreateImportedAssets()
    {
        yield return CreateAsset("OreNodeMesh", "Coal", @"UnityRIP\ExportedProject\Assets\Mesh\CoalNode1.asset", @"Resources\ImportedAssets\OreNodes\Coal\Meshes\CoalNode1.asset");
        yield return CreateAsset("OreNodeMesh", "Coal", @"UnityRIP\ExportedProject\Assets\Mesh\CoalNode2.asset", @"Resources\ImportedAssets\OreNodes\Coal\Meshes\CoalNode2.asset");
        yield return CreateAsset("OreNodeMesh", "Coal", @"UnityRIP\ExportedProject\Assets\Mesh\CoalNode3.asset", @"Resources\ImportedAssets\OreNodes\Coal\Meshes\CoalNode3.asset");
        yield return CreateAsset("OreNodeTexture", "Coal", @"UnityRIP\ExportedProject\Assets\Texture2D\CoalNodes_Albedo.png", @"Resources\ImportedAssets\OreNodes\Coal\Textures\CoalNodes_Albedo.png");

        yield return CreateAsset("OreNodeMesh", "Copper", @"UnityRIP\ExportedProject\Assets\Mesh\CopperNode1.asset", @"Resources\ImportedAssets\OreNodes\Copper\Meshes\CopperNode1.asset");
        yield return CreateAsset("OreNodeMesh", "Copper", @"UnityRIP\ExportedProject\Assets\Mesh\CopperNode2.asset", @"Resources\ImportedAssets\OreNodes\Copper\Meshes\CopperNode2.asset");
        yield return CreateAsset("OreNodeMesh", "Copper", @"UnityRIP\ExportedProject\Assets\Mesh\CopperNode3.asset", @"Resources\ImportedAssets\OreNodes\Copper\Meshes\CopperNode3.asset");
        yield return CreateAsset("OreNodeTexture", "Copper", @"UnityRIP\ExportedProject\Assets\Texture2D\CopperNodes_Albedo.png", @"Resources\ImportedAssets\OreNodes\Copper\Textures\CopperNodes_Albedo.png");

        yield return CreateAsset("OreNodeMesh", "Iron", @"UnityRIP\ExportedProject\Assets\Mesh\IronNode1.asset", @"Resources\ImportedAssets\OreNodes\Iron\Meshes\IronNode1.asset");
        yield return CreateAsset("OreNodeMesh", "Iron", @"UnityRIP\ExportedProject\Assets\Mesh\IronNode2.asset", @"Resources\ImportedAssets\OreNodes\Iron\Meshes\IronNode2.asset");
        yield return CreateAsset("OreNodeMesh", "Iron", @"UnityRIP\ExportedProject\Assets\Mesh\IronNode3.asset", @"Resources\ImportedAssets\OreNodes\Iron\Meshes\IronNode3.asset");
        yield return CreateAsset("OreNodeTexture", "Iron", @"UnityRIP\ExportedProject\Assets\Texture2D\IronNodes_Albedo.png", @"Resources\ImportedAssets\OreNodes\Iron\Textures\IronNodes_Albedo.png");

        yield return CreateAsset("OreNodeMesh", "Gold", @"UnityRIP\ExportedProject\Assets\Mesh\GoldNode1.asset", @"Resources\ImportedAssets\OreNodes\Gold\Meshes\GoldNode1.asset");
        yield return CreateAsset("OreNodeMesh", "Gold", @"UnityRIP\ExportedProject\Assets\Mesh\GoldNode2.asset", @"Resources\ImportedAssets\OreNodes\Gold\Meshes\GoldNode2.asset");
        yield return CreateAsset("OreNodeMesh", "Gold", @"UnityRIP\ExportedProject\Assets\Mesh\GoldNode3.asset", @"Resources\ImportedAssets\OreNodes\Gold\Meshes\GoldNode3.asset");
        yield return CreateAsset("OreNodeMaterial", "Gold", @"UnityRIP\ExportedProject\Assets\Material\GoldOreNode.mat", @"Resources\ImportedAssets\OreNodes\Gold\Materials\GoldOreNode.mat");

        yield return CreateAsset("AutoMinerMesh", "Shared", @"UnityRIP\ExportedProject\Assets\Mesh\OreNode1.asset", @"Resources\ImportedAssets\AutoMinerNodes\Shared\Meshes\OreNode1.asset");
        yield return CreateAsset("AutoMinerMesh", "Shared", @"UnityRIP\ExportedProject\Assets\Mesh\OreNode2.asset", @"Resources\ImportedAssets\AutoMinerNodes\Shared\Meshes\OreNode2.asset");

        yield return CreateAsset("AutoMinerMaterial", "Coal", @"UnityRIP\ExportedProject\Assets\Material\Coal_Ore_Cluster.mat", @"Resources\ImportedAssets\AutoMinerNodes\Coal\Materials\Coal_Ore_Cluster.mat");
        yield return CreateAsset("AutoMinerMaterial", "Copper", @"UnityRIP\ExportedProject\Assets\Material\Copper_Ore.mat", @"Resources\ImportedAssets\AutoMinerNodes\Copper\Materials\Copper_Ore.mat");
        yield return CreateAsset("AutoMinerMaterial", "Iron", @"UnityRIP\ExportedProject\Assets\Material\Iron_Ore.mat", @"Resources\ImportedAssets\AutoMinerNodes\Iron\Materials\Iron_Ore.mat");
        yield return CreateAsset("AutoMinerMaterial", "Gold", @"UnityRIP\ExportedProject\Assets\Material\Gold_Ore.mat", @"Resources\ImportedAssets\AutoMinerNodes\Gold\Materials\Gold_Ore.mat");
    }

    private static ImportedAssetRecord CreateAsset(string kind, string resourceKey, string sourceRelativePath, string targetRelativePath)
    {
        return new ImportedAssetRecord(kind, resourceKey, sourceRelativePath, targetRelativePath);
    }
}
