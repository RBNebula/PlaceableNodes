using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private static readonly System.Reflection.FieldInfo AllShopCategoriesField =
        AccessTools.Field(typeof(EconomyManager), "_allShopCategories");

    private void TryRegisterShopItems()
    {
        if (_shopItemsRegistered)
        {
            return;
        }

        Transform runtimeRoot = GetOrCreateRuntimePrefabRoot();

        foreach (ShopItemPlan plan in PlaceableNodesConstants.Plans)
        {
            Sprite? icon = LoadIconSprite(plan);
            BuildingObject primaryPrefab = CreatePrimaryShell(runtimeRoot, plan.InternalName + "_BuildingPrefab");
            BuildingInventoryDefinition definition = CreateInventoryDefinition(plan, primaryPrefab, icon);
            ShopItemDefinition shopDefinition = CreateShopItemDefinition(plan, definition);

            ConfigureShellDefaults(primaryPrefab, definition, plan.VariantBlockIds[0]);

            ShopItemRuntime runtime = new(plan, primaryPrefab, definition, shopDefinition);
            runtime.VariantPrefabs.Add(primaryPrefab);

            _itemsByKey[plan.InternalName] = runtime;
        }

        _shopItemsRegistered = _itemsByKey.Count == PlaceableNodesConstants.Plans.Length;
        if (_shopItemsRegistered)
        {
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Registered {_itemsByKey.Count} vanilla shop items.");
        }
    }

    private Transform GetOrCreateRuntimePrefabRoot()
    {
        if (_runtimePrefabRoot != null)
        {
            return _runtimePrefabRoot;
        }

        GameObject runtimeRoot = new("__PlaceableNodesRuntime");
        runtimeRoot.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        runtimeRoot.transform.SetParent(transform, false);
        runtimeRoot.SetActive(false);
        DontDestroyOnLoad(runtimeRoot);
        _runtimePrefabRoot = runtimeRoot.transform;
        return _runtimePrefabRoot;
    }

    private BuildingObject CreatePrimaryShell(Transform runtimeRoot, string name)
    {
        GameObject shellRoot = new(name);
        shellRoot.transform.SetParent(runtimeRoot, false);
        PlaceableNodesUtility.ClearHideFlagsRecursively(shellRoot);
        SetBuildingLayer(shellRoot);

        GameObject placementObject = new("BuildingPlacementColliderObject");
        placementObject.transform.SetParent(shellRoot.transform, false);
        PlaceableNodesUtility.ClearHideFlagsRecursively(placementObject);
        SetBuildingLayer(placementObject);

        GameObject spawnPoint = new("BuildingCrateSpawnPoint");
        spawnPoint.transform.SetParent(shellRoot.transform, false);
        PlaceableNodesUtility.ClearHideFlagsRecursively(spawnPoint);

        BuildingObject shell = shellRoot.AddComponent<BuildingObject>();
        shell.BuildingPlacementColliderObject = placementObject;
        shell.BuildingCrateSpawnPoint = spawnPoint.transform;
        shell.PhysicalColliderObject = shellRoot;
        return shell;
    }

    private static void SetBuildingLayer(GameObject target)
    {
        int buildingLayer = LayerMask.NameToLayer("BuildingObject");
        if (buildingLayer >= 0)
        {
            target.layer = buildingLayer;
        }
    }

    private BuildingInventoryDefinition CreateInventoryDefinition(ShopItemPlan plan, BuildingObject primaryPrefab, Sprite? icon)
    {
        BuildingInventoryDefinition definition = ScriptableObject.CreateInstance<BuildingInventoryDefinition>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = plan.InternalName + "_Definition";
        definition.Name = plan.DisplayName;
        definition.Description = plan.Description;
        definition.InventoryIcon = icon;
        definition.ProgrammerInventoryIcon = icon;
        definition.QButtonFunction = "Cycle Variant";
        definition.MaxInventoryStackSize = PlaceableNodesConstants.MaxStackSize;
        definition.BuildingPrefabs = new List<BuildingObject> { primaryPrefab };
        definition.CanBePlacedInTerrain = true;
        definition.PackedPrefab = null;
        definition.UseReverseRotationDirection = false;
        return definition;
    }

    private static ShopItemDefinition CreateShopItemDefinition(ShopItemPlan plan, BuildingInventoryDefinition definition)
    {
        ShopItemDefinition shopDefinition = ScriptableObject.CreateInstance<ShopItemDefinition>();
        shopDefinition.hideFlags = HideFlags.HideAndDontSave;
        shopDefinition.name = plan.InternalName + "_ShopItem";
        shopDefinition.Name = plan.DisplayName;
        shopDefinition.Description = plan.Description;
        shopDefinition.UseNameAndDescriptionOfBuildingDefinition = true;
        shopDefinition.Price = plan.Price;
        shopDefinition.IsLockedByDefault = false;
        shopDefinition.IsDummyItem = false;
        shopDefinition.BuildingInventoryDefinition = definition;
        shopDefinition.PrefabToSpawn = null;
        return shopDefinition;
    }

    private Sprite? LoadIconSprite(ShopItemPlan plan)
    {
        string cacheKey = $"{plan.Kind}:{plan.ResourceKey}";
        if (_iconsByRelativePath.TryGetValue(cacheKey, out Sprite existing))
        {
            return existing;
        }

        OrePiece? orePiece = FindOrePiecePrefab(plan.ResourceType);
        Sprite? icon = orePiece != null ? orePiece.InventoryIcon : null;
        if (icon == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Missing live ore piece icon for {plan.DisplayName}.");
            return null;
        }

        Sprite resolvedIcon = plan.Kind == PlaceableNodeKind.AutoMinerPlacementNode
            ? CreateOutlinedIconSprite(icon, Color.red, 2) ?? icon
            : icon;

        _iconsByRelativePath[cacheKey] = resolvedIcon;
        return resolvedIcon;
    }

    private Sprite? CreateOutlinedIconSprite(Sprite sourceIcon, Color outlineColor, int outlineThickness)
    {
        if (outlineThickness <= 0)
        {
            return sourceIcon;
        }

        Texture2D? sourceTexture = ExtractSpriteTexture(sourceIcon);
        if (sourceTexture == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to create outlined icon for '{sourceIcon.name}' because the source texture could not be read.");
            return null;
        }

        int width = sourceTexture.width;
        int height = sourceTexture.height;
        int paddedWidth = width + (outlineThickness * 2);
        int paddedHeight = height + (outlineThickness * 2);

        Color[] sourcePixels = sourceTexture.GetPixels();
        Color[] outlinedPixels = new Color[paddedWidth * paddedHeight];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color sourcePixel = sourcePixels[(y * width) + x];
                if (sourcePixel.a <= 0.01f)
                {
                    continue;
                }

                int paddedX = x + outlineThickness;
                int paddedY = y + outlineThickness;

                for (int offsetY = -outlineThickness; offsetY <= outlineThickness; offsetY++)
                {
                    for (int offsetX = -outlineThickness; offsetX <= outlineThickness; offsetX++)
                    {
                        if (offsetX == 0 && offsetY == 0)
                        {
                            continue;
                        }

                        int targetX = paddedX + offsetX;
                        int targetY = paddedY + offsetY;
                        if (targetX < 0 || targetX >= paddedWidth || targetY < 0 || targetY >= paddedHeight)
                        {
                            continue;
                        }

                        int targetIndex = (targetY * paddedWidth) + targetX;
                        if (outlinedPixels[targetIndex].a <= 0.01f)
                        {
                            outlinedPixels[targetIndex] = outlineColor;
                        }
                    }
                }

                outlinedPixels[(paddedY * paddedWidth) + paddedX] = sourcePixel;
            }
        }

        Texture2D outlinedTexture = new(paddedWidth, paddedHeight, TextureFormat.RGBA32, false)
        {
            name = sourceIcon.name + "_Outlined",
            filterMode = sourceIcon.texture.filterMode,
            wrapMode = TextureWrapMode.Clamp
        };
        outlinedTexture.SetPixels(outlinedPixels);
        outlinedTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        Vector2 pivot = new(
            (outlineThickness + sourceIcon.pivot.x) / paddedWidth,
            (outlineThickness + sourceIcon.pivot.y) / paddedHeight);
        Sprite outlinedSprite = Sprite.Create(
            outlinedTexture,
            new Rect(0f, 0f, paddedWidth, paddedHeight),
            pivot,
            sourceIcon.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        outlinedSprite.name = sourceIcon.name + "_Outlined";
        return outlinedSprite;
    }

    private static Texture2D? ExtractSpriteTexture(Sprite sourceIcon)
    {
        Rect rect = sourceIcon.rect;
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);

        RenderTexture renderTexture = RenderTexture.GetTemporary(
            sourceIcon.texture.width,
            sourceIcon.texture.height,
            0,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.Linear);

        RenderTexture previous = RenderTexture.active;
        try
        {
            Graphics.Blit(sourceIcon.texture, renderTexture);
            RenderTexture.active = renderTexture;

            Texture2D fullTexture = new(sourceIcon.texture.width, sourceIcon.texture.height, TextureFormat.RGBA32, false);
            fullTexture.ReadPixels(new Rect(0f, 0f, sourceIcon.texture.width, sourceIcon.texture.height), 0, 0);
            fullTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            Texture2D spriteTexture = new(width, height, TextureFormat.RGBA32, false)
            {
                name = sourceIcon.name + "_Readable"
            };
            spriteTexture.SetPixels(fullTexture.GetPixels(Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y), width, height));
            spriteTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            Object.Destroy(fullTexture);
            return spriteTexture;
        }
        catch
        {
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    internal void InjectShopDefinitions(EconomyManager economyManager)
    {
        TryLoadNodeBundle();
        TryRegisterShopItems();
        TryConfigurePrefabs();
        EnsureSavablePrefabRegistration();

        List<ShopCategory>? allShopCategories = AllShopCategoriesField.GetValue(economyManager) as List<ShopCategory>;
        if (allShopCategories == null)
        {
            allShopCategories = new List<ShopCategory>();
            AllShopCategoriesField.SetValue(economyManager, allShopCategories);
        }

        EnsureCategoryContainsPlans(allShopCategories, PlaceableNodesConstants.OreShopCategory, PlaceableNodeKind.OreNode);
        EnsureCategoryContainsPlans(allShopCategories, PlaceableNodesConstants.AutoMinerShopCategory, PlaceableNodeKind.AutoMinerPlacementNode);
    }

    private void EnsureCategoryContainsPlans(List<ShopCategory> categories, string categoryName, PlaceableNodeKind kind)
    {
        ShopCategory? category = categories.Find(existing =>
            existing != null &&
            string.Equals(existing.CategoryName, categoryName, System.StringComparison.OrdinalIgnoreCase));

        if (category == null)
        {
            category = new ShopCategory
            {
                CategoryName = categoryName,
                ShopItemDefinitions = new List<ShopItemDefinition>(),
                ShopItems = new List<ShopItem>(),
                DontShowIfAllItemsAreLocked = false,
                HolidayType = HolidayType.None
            };

            categories.Add(category);
        }
        else
        {
            category.ShopItemDefinitions ??= new List<ShopItemDefinition>();
            category.ShopItems ??= new List<ShopItem>();
        }

        foreach (ShopItemRuntime runtime in _itemsByKey.Values)
        {
            if (runtime.Plan.Kind != kind)
            {
                continue;
            }

            if (!category.ShopItemDefinitions.Contains(runtime.ShopDefinition))
            {
                category.ShopItemDefinitions.Add(runtime.ShopDefinition);
            }
        }
    }
}
