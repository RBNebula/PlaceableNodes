using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private readonly Harmony _harmony = new(ModInfo.HARMONY_ID);
    private readonly Dictionary<string, ShopItemRuntime> _itemsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GameObject> _bundlePrefabsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Sprite> _iconsByRelativePath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ResourceType, AutoMinerResourceDefinition> _runtimeAutoMinerDefinitions = new();
    private readonly Dictionary<string, AutoMinerResourceDefinition> _diskAutoMinerDefinitionsByName = new(StringComparer.OrdinalIgnoreCase);
    private GameObject? _cachedAutoMinerGhostTemplate;
    private readonly KeyboardShortcut _defaultCycleAutoMinerDefinitionBind = new(KeyCode.PageDown);
    private KeyboardShortcut _cycleAutoMinerDefinitionBind = KeyboardShortcut.Empty;
    private IDisposable? _rebindCycleAutoMinerDefinitionHandle;
    private bool _rebindKeybindsRegistered;
    private bool _rebindKeybindsAttempted;
    private bool _diskAutoMinerDefinitionsLoaded;
    private int _autoMinerDefinitionsLoadAttemptCount;
    private bool _autoMinerDefinitionsLoadStateLogged;
    private string _lastAutoMinerAvailabilityLogKey = string.Empty;
    private string _definitionOverlayText = string.Empty;
    private GUIStyle? _definitionOverlayStyle;
    private string _pluginRootPath = string.Empty;
    private string _resourceRootPath = string.Empty;
    private string _nodeBundlePath = string.Empty;
    private string _autoMinerDefinitionsDataPath = string.Empty;
    private bool _shopItemsRegistered;
    private bool _allPrefabsConfigured;
    private bool _savablePrefabsRegistered;
    private bool _assetCatalogWritten;
    private AssetBundle? _nodeAssetBundle;
    private Transform? _runtimePrefabRoot;
}
