using System.Collections.Generic;
using System.Linq;
using ModMogul;
using UnityEngine;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private void TryRegisterShopItems()
    {
        if (_shopItemsRegistered)
        {
            return;
        }

        foreach (ShopItemPlan plan in PlaceableNodesConstants.Plans)
        {
            Itemizer.ItemSpec itemSpec = CreateItemSpec(plan);
            BuildingObject primaryPrefab = Itemizer.RegisterItem(itemSpec);

            ConfigureShellDefaults(primaryPrefab, primaryPrefab.Definition, plan.VariantBlockIds[0]);

            if (!_itemsByKey.TryGetValue(plan.InternalName, out ShopItemRuntime? runtime))
            {
                runtime = new ShopItemRuntime(plan, primaryPrefab);
                _itemsByKey[plan.InternalName] = runtime;
            }

            runtime.Definition = primaryPrefab.Definition;
            runtime.ShopDefinition = FindShopDefinition(primaryPrefab.Definition);
            runtime.VariantPrefabs.Clear();
            runtime.VariantPrefabs.Add(primaryPrefab);
        }

        _shopItemsRegistered = _itemsByKey.Count == PlaceableNodesConstants.Plans.Length;
        if (_shopItemsRegistered)
        {
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Registered {_itemsByKey.Count} vanilla shop items through ModMogul.");
        }
    }

    private static Itemizer.ItemSpec CreateItemSpec(ShopItemPlan plan)
    {
        return new Itemizer.ItemSpec(
            blockID: plan.VariantBlockIds[0],
            internalName: plan.InternalName,
            displayName: plan.DisplayName,
            description: plan.Description,
            price: plan.Price,
            shopCategory: plan.ShopCategory,
            maxStackSize: PlaceableNodesConstants.MaxStackSize,
            isLockedByDefault: false,
            qFunction: "Cycle Variant");
    }

    private bool TryHydrateRuntimeDefinitions(ShopItemRuntime runtime)
    {
        if (runtime.Definition == null)
        {
            runtime.Definition = runtime.PrimaryPrefab.Definition;
        }

        BuildingInventoryDefinition? definition = runtime.Definition;
        if (definition == null)
        {
            return false;
        }

        definition.FallbackName = runtime.Plan.DisplayName;
        definition.FallbackDescription = runtime.Plan.Description;
        definition.MaxInventoryStackSize = PlaceableNodesConstants.MaxStackSize;
        definition.CanBePlacedInTerrain = true;
        definition.UseReverseRotationDirection = false;

        Sprite? icon = LoadIconSprite(runtime.Plan);
        if (icon != null)
        {
            definition.InventoryIcon = icon;
            definition.ProgrammerInventoryIcon = icon;
        }

        runtime.ShopDefinition ??= FindShopDefinition(definition);
        if (runtime.ShopDefinition != null)
        {
            runtime.ShopDefinition.BuildingInventoryDefinition = definition;
            runtime.ShopDefinition.Price = runtime.Plan.Price;
            runtime.ShopDefinition.IsLockedByDefault = false;
            runtime.ShopDefinition.IsDummyItem = false;
            runtime.ShopDefinition.PrefabToSpawn = null;
        }

        return true;
    }

    private static ShopItemDefinition? FindShopDefinition(BuildingInventoryDefinition? definition)
    {
        if (definition == null)
        {
            return null;
        }

        EconomyManager? economyManager = Singleton<EconomyManager>.Instance;
        if (economyManager == null)
        {
            return null;
        }

        return economyManager.AllShopItems
            .Select(item => item?.Definition)
            .FirstOrDefault(shopDefinition => shopDefinition?.BuildingInventoryDefinition == definition);
    }

    internal void InjectShopDefinitions(EconomyManager economyManager)
    {
        TryRegisterShopItems();
        TryConfigurePrefabs();
    }

    private static void ConfigureShellDefaults(BuildingObject shell, BuildingInventoryDefinition? definition, int blockId)
    {
        if (definition != null)
        {
            shell.Definition = definition;
            definition.CanBePlacedInTerrain = true;
        }

        shell.SavableObjectID = (SavableObjectID)blockId;
        shell.RequiresFlatGround = false;
        shell.PlacementNodeRequirement = PlacementNodeRequirement.None;
        shell.SupportType = SupportType.None;
        shell.BuildingSupportsEnabled = false;
        shell.RotatingShouldMirrorWhenSnapped = false;

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

    private Transform GetOrCreateRuntimePrefabRoot()
    {
        if (_runtimePrefabRoot != null)
        {
            return _runtimePrefabRoot;
        }

        GameObject runtimeRoot = new("__PlaceableNodesRuntime");
        runtimeRoot.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
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
}
