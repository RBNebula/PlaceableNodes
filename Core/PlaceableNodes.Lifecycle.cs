using System.IO;
using BepInEx;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private void InitializePlugin()
    {
        _pluginRootPath = Path.Combine(Paths.PluginPath, ModInfo.DISPLAY_NAME_LONG);
        _resourceRootPath = Path.Combine(_pluginRootPath, PlaceableNodesConstants.ResourceFolderName);
        _nodeBundlePath = Path.Combine(_resourceRootPath, PlaceableNodesConstants.AssetBundlesFolderName, PlaceableNodesConstants.NodeAssetBundleFileName);
        _autoMinerDefinitionsDataPath = Path.Combine(_resourceRootPath, "Definitions", "AutoMinerResourceDefinitions.json");
        EnsureEmbeddedResourcesExtracted();

        _harmony.PatchAll();
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Initialized");
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Plugin root path: '{_pluginRootPath}'");
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Resource root path: '{_resourceRootPath}'");
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Auto-miner definitions data path: '{_autoMinerDefinitionsDataPath}'");
    }

    private void TickPlugin()
    {
        WriteAssetCatalogOnce();
        TryLoadNodeBundle();
        TryRegisterShopItems();
        TryConfigurePrefabs();
        EnsureSavablePrefabRegistration();
        TryRegisterRebindKeybinds();
        TryLoadAutoMinerDefinitionsFromDisk();
        HandleDefinitionHotkeys();
        UpdateDefinitionOverlay();
    }

    private void ShutdownPlugin()
    {
        _harmony.UnpatchSelf();
        if (_nodeAssetBundle != null)
        {
            try
            {
                _nodeAssetBundle.Unload(unloadAllLoadedObjects: false);
            }
            catch
            {
                // Unity can throw during teardown if the bundle is already being destroyed.
            }

            _nodeAssetBundle = null;
        }

        _bundlePrefabsByName.Clear();
        _iconsByRelativePath.Clear();
        _diskAutoMinerDefinitionsByName.Clear();
        _rebindCycleAutoMinerDefinitionHandle?.Dispose();
        _rebindCycleAutoMinerDefinitionHandle = null;
    }
}
