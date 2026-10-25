using System.Diagnostics;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private const float DefaultPerformanceDiagnosticsIntervalSeconds = 5f;

    private void InitializeDiagnostics()
    {
        _performanceDiagnosticsEnabled = Config.Bind(
            "Diagnostics",
            "EnablePerformanceDiagnostics",
            false,
            "Logs aggregated PlaceableNodes performance counters every few seconds.");

        _performanceDiagnosticsIntervalSeconds = Config.Bind(
            "Diagnostics",
            "PerformanceDiagnosticsIntervalSeconds",
            DefaultPerformanceDiagnosticsIntervalSeconds,
            "How often to log aggregated PlaceableNodes performance counters while diagnostics are enabled.");

        if (_performanceDiagnosticsEnabled.Value)
        {
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Performance diagnostics are enabled. Set Diagnostics.EnablePerformanceDiagnostics=false after profiling.");
        }
    }

    private bool PerformanceDiagnosticsEnabled => _performanceDiagnosticsEnabled?.Value == true;

    private long BeginDiagnosticSample()
    {
        return PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
    }

    private void RecordTickDiagnostic(long startedAt)
    {
        RecordDiagnostic(startedAt, ref _diagnosticTickTicks, ref _diagnosticTickCalls);
    }

    private void RecordMaintenanceDiagnostic(long startedAt)
    {
        RecordDiagnostic(startedAt, ref _diagnosticMaintenanceTicks, ref _diagnosticMaintenanceCalls);
    }

    private void RecordHotkeyDiagnostic(long startedAt)
    {
        RecordDiagnostic(startedAt, ref _diagnosticHotkeyTicks, ref _diagnosticHotkeyCalls);
    }

    private void RecordOverlayDiagnostic(long startedAt)
    {
        RecordDiagnostic(startedAt, ref _diagnosticOverlayTicks, ref _diagnosticOverlayCalls);
    }

    private void RecordGuiDiagnostic(long startedAt)
    {
        RecordDiagnostic(startedAt, ref _diagnosticGuiTicks, ref _diagnosticGuiCalls);
    }

    private void RecordSceneScanDiagnostic(long startedAt)
    {
        RecordDiagnostic(startedAt, ref _diagnosticSceneScanTicks, ref _diagnosticSceneScanCalls);
    }

    private void RecordDiagnostic(long startedAt, ref long totalTicks, ref int callCount)
    {
        if (startedAt == 0L)
        {
            return;
        }

        totalTicks += Stopwatch.GetTimestamp() - startedAt;
        callCount++;
    }

    private void LogPerformanceDiagnosticsIfDue(float now)
    {
        if (!PerformanceDiagnosticsEnabled || now < _nextPerformanceDiagnosticsLogTime)
        {
            return;
        }

        float interval = Mathf.Max(1f, _performanceDiagnosticsIntervalSeconds?.Value ?? DefaultPerformanceDiagnosticsIntervalSeconds);
        _nextPerformanceDiagnosticsLogTime = now + interval;

        int placementBootstraps = Object.FindObjectsByType<PlaceableNodesPlacementNodeBootstrap>(FindObjectsSortMode.None).Length;
        int colliderControllers = Object.FindObjectsByType<PlaceableNodesPlacementColliderController>(FindObjectsSortMode.None).Length;
        int oreInitializers = Object.FindObjectsByType<PlaceableNodesOreRuntimeInitializer>(FindObjectsSortMode.None).Length;
        int placementMarkers = Object.FindObjectsByType<PlaceableNodesPlacementItemMarker>(FindObjectsSortMode.None).Length;
        int oreMarkers = Object.FindObjectsByType<PlaceableNodesOreMarker>(FindObjectsSortMode.None).Length;
        int markedRenderers = 0;
        int markedColliders = 0;
        CountMarkedRendererAndColliderComponents(ref markedRenderers, ref markedColliders);

        Logger.LogInfo(
            $"{ModInfo.LOG_PREFIX} PerfDiag " +
            $"startupComplete={_startupMaintenanceComplete} " +
            $"items={_itemsByKey.Count} bundlePrefabs={_bundlePrefabsByName.Count} " +
            $"shop={_shopItemsRegistered} prefabs={_allPrefabsConfigured} savable={_savablePrefabsRegistered} diskDefs={_diskAutoMinerDefinitionsLoaded} " +
            $"cachedPlayer={_cachedPlayerController != null} cachedBuildMgr={_cachedBuildingManager != null} " +
            $"components bootstrap={placementBootstraps} colliderCtl={colliderControllers} oreInit={oreInitializers} placementMarkers={placementMarkers} oreMarkers={oreMarkers} markedRenderers={markedRenderers} markedColliders={markedColliders} " +
            $"tick={FormatDiagnostic(_diagnosticTickCalls, _diagnosticTickTicks)} " +
            $"maint={FormatDiagnostic(_diagnosticMaintenanceCalls, _diagnosticMaintenanceTicks)} " +
            $"hotkey={FormatDiagnostic(_diagnosticHotkeyCalls, _diagnosticHotkeyTicks)} " +
            $"overlay={FormatDiagnostic(_diagnosticOverlayCalls, _diagnosticOverlayTicks)} " +
            $"gui={FormatDiagnostic(_diagnosticGuiCalls, _diagnosticGuiTicks)} " +
            $"sceneScan={FormatDiagnostic(_diagnosticSceneScanCalls, _diagnosticSceneScanTicks)}");

        ResetPerformanceDiagnostics();
    }

    private static string FormatDiagnostic(int callCount, long elapsedTicks)
    {
        if (callCount == 0)
        {
            return "0";
        }

        double totalMilliseconds = elapsedTicks * 1000.0 / Stopwatch.Frequency;
        double averageMilliseconds = totalMilliseconds / callCount;
        return callCount.ToString(CultureInfo.InvariantCulture) +
            "/" + totalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) +
            "ms avg=" + averageMilliseconds.ToString("0.####", CultureInfo.InvariantCulture) +
            "ms";
    }

    private static void CountMarkedRendererAndColliderComponents(ref int rendererCount, ref int colliderCount)
    {
        foreach (PlaceableNodesPlacementItemMarker marker in Object.FindObjectsByType<PlaceableNodesPlacementItemMarker>(FindObjectsSortMode.None))
        {
            if (marker == null)
            {
                continue;
            }

            rendererCount += marker.GetComponentsInChildren<Renderer>(true).Length;
            colliderCount += marker.GetComponentsInChildren<Collider>(true).Length;
        }

        foreach (PlaceableNodesOreMarker marker in Object.FindObjectsByType<PlaceableNodesOreMarker>(FindObjectsSortMode.None))
        {
            if (marker == null)
            {
                continue;
            }

            rendererCount += marker.GetComponentsInChildren<Renderer>(true).Length;
            colliderCount += marker.GetComponentsInChildren<Collider>(true).Length;
        }
    }

    private void ResetPerformanceDiagnostics()
    {
        _diagnosticTickTicks = 0L;
        _diagnosticMaintenanceTicks = 0L;
        _diagnosticHotkeyTicks = 0L;
        _diagnosticOverlayTicks = 0L;
        _diagnosticGuiTicks = 0L;
        _diagnosticSceneScanTicks = 0L;
        _diagnosticTickCalls = 0;
        _diagnosticMaintenanceCalls = 0;
        _diagnosticHotkeyCalls = 0;
        _diagnosticOverlayCalls = 0;
        _diagnosticGuiCalls = 0;
        _diagnosticSceneScanCalls = 0;
    }
}
