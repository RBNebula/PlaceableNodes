using System.IO;
using BepInEx;
using UnityEngine;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private const float MaintenanceTickIntervalSeconds = 0.5f;
    private const float DefinitionOverlayUpdateIntervalSeconds = 0.25f;

    private void InitializePlugin()
    {
        _pluginRootPath = Path.Combine(Paths.PluginPath, ModInfo.DISPLAY_NAME_LONG);
        _resourceRootPath = Path.Combine(_pluginRootPath, PlaceableNodesConstants.ResourceFolderName);
        _nodeBundlePath = Path.Combine(_resourceRootPath, PlaceableNodesConstants.AssetBundlesFolderName, PlaceableNodesConstants.NodeAssetBundleFileName);
        _autoMinerDefinitionsDataPath = Path.Combine(_resourceRootPath, "Definitions", "AutoMinerResourceDefinitions.json");
        InitializeDiagnostics();
        EnsureEmbeddedResourcesExtracted();

        _harmony.PatchAll();
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Initialized");
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Plugin root path: '{_pluginRootPath}'");
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Resource root path: '{_resourceRootPath}'");
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Auto-miner definitions data path: '{_autoMinerDefinitionsDataPath}'");
    }

    private void TickPlugin()
    {
        long tickStartedAt = BeginDiagnosticSample();
        float now = Time.unscaledTime;
        if (!_startupMaintenanceComplete && now >= _nextMaintenanceTickTime)
        {
            long maintenanceStartedAt = BeginDiagnosticSample();
            _nextMaintenanceTickTime = now + MaintenanceTickIntervalSeconds;
            WriteAssetCatalogOnce();
            TryLoadNodeBundle();
            TryRegisterShopItems();
            TryConfigurePrefabs();
            EnsureSavablePrefabRegistration();
            TryRegisterRebindKeybinds();
            TryLoadAutoMinerDefinitionsFromDisk();
            _startupMaintenanceComplete = IsStartupMaintenanceComplete();
            RecordMaintenanceDiagnostic(maintenanceStartedAt);
        }

        long hotkeyStartedAt = BeginDiagnosticSample();
        HandleDefinitionHotkeys();
        RecordHotkeyDiagnostic(hotkeyStartedAt);
        UpdatePlacementNodeParticipation();
        if (now >= _nextDefinitionOverlayUpdateTime)
        {
            long overlayStartedAt = BeginDiagnosticSample();
            _nextDefinitionOverlayUpdateTime = now + DefinitionOverlayUpdateIntervalSeconds;
            UpdateDefinitionOverlay();
            RecordOverlayDiagnostic(overlayStartedAt);
        }

        LogPerformanceDiagnosticsIfDue(now);
        RecordTickDiagnostic(tickStartedAt);
    }

    private bool IsStartupMaintenanceComplete()
    {
        return _assetCatalogWritten &&
            _nodeAssetBundle != null &&
            _shopItemsRegistered &&
            _allPrefabsConfigured &&
            _savablePrefabsRegistered &&
            (_rebindKeybindsRegistered || _rebindKeybindsAttempted) &&
            _diskAutoMinerDefinitionsLoaded;
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
        RestoreSuppressedPlacementNodes();
        _rebindCycleAutoMinerDefinitionHandle?.Dispose();
        _rebindCycleAutoMinerDefinitionHandle = null;
    }
}
