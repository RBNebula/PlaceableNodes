using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private const string RebindApiTypeName = "Rebind.RebindApi";
    private const string ManagedKeybindPacketTypeName = "Rebind.ManagedKeybindPacket";

    private void TryRegisterRebindKeybinds()
    {
        if (_rebindKeybindsRegistered || _rebindKeybindsAttempted)
        {
            return;
        }

        _rebindKeybindsAttempted = true;

        _cycleAutoMinerDefinitionBind = _cycleAutoMinerDefinitionBind.MainKey == KeyCode.None
            ? _defaultCycleAutoMinerDefinitionBind
            : _cycleAutoMinerDefinitionBind;

        _rebindCycleAutoMinerDefinitionHandle = TryRegisterManagedKeybind(
            bindingId: "cycle_auto_miner_definition",
            keybindTitle: "Cycle Auto-Miner Definition",
            defaultKeybind: _defaultCycleAutoMinerDefinitionBind,
            apply: bind => _cycleAutoMinerDefinitionBind = bind);

        _rebindKeybindsRegistered = _rebindCycleAutoMinerDefinitionHandle != null;
    }

    private IDisposable? TryRegisterManagedKeybind(string bindingId, string keybindTitle, KeyboardShortcut defaultKeybind, Action<KeyboardShortcut> apply)
    {
        try
        {
            Assembly? rebindAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, "Rebind", StringComparison.Ordinal));
            if (rebindAssembly == null)
            {
                return null;
            }

            Type? apiType = rebindAssembly.GetType(RebindApiTypeName, throwOnError: false);
            Type? packetType = rebindAssembly.GetType(ManagedKeybindPacketTypeName, throwOnError: false);
            if (apiType == null || packetType == null)
            {
                return null;
            }

            object packet = Activator.CreateInstance(packetType)!;
            packetType.GetProperty("PluginGuid")?.SetValue(packet, ModInfo.PLUGIN_GUID);
            packetType.GetProperty("BindingId")?.SetValue(packet, bindingId);
            packetType.GetProperty("SectionTitle")?.SetValue(packet, ModInfo.DISPLAY_NAME_LONG);
            packetType.GetProperty("KeybindTitle")?.SetValue(packet, keybindTitle);
            packetType.GetProperty("DefaultKeybind")?.SetValue(packet, defaultKeybind);
            packetType.GetProperty("Apply")?.SetValue(packet, apply);

            MethodInfo? registerMethod = apiType.GetMethod("RegisterManagedKeybind", BindingFlags.Public | BindingFlags.Static);
            return registerMethod?.Invoke(null, new[] { packet }) as IDisposable;
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to register Rebind keybind '{bindingId}': {ex.Message}");
            return null;
        }
    }

    private void HandleDefinitionHotkeys()
    {
        if (_cycleAutoMinerDefinitionBind.MainKey == KeyCode.None || !_cycleAutoMinerDefinitionBind.IsDown())
        {
            return;
        }

        BuildingPlacementNode? heldNode = GetHeldCustomAutoMinerPlacementNode();
        if (heldNode == null)
        {
            return;
        }

        AutoMinerResourceDefinition? currentDefinition = heldNode.AutoMinerResourceDefinition;
        if (currentDefinition == null)
        {
            return;
        }

        ResourceType resourceType = currentDefinition.GetPrimaryResourceType();
        List<AutoMinerResourceDefinition> definitions = GetAvailableAutoMinerDefinitions(resourceType);
        LogAvailableDefinitionsForHeldNode("CyclePressed", heldNode, currentDefinition, definitions);
        if (definitions.Count == 0)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} No available auto-miner definitions found for held node '{heldNode.name}' with current definition '{currentDefinition.name}' and resource '{resourceType}'.");
            return;
        }

        int currentIndex = FindDefinitionIndex(definitions, currentDefinition);
        int nextIndex = currentIndex >= 0
            ? (currentIndex + 1) % definitions.Count
            : 0;

        ApplyAutoMinerDefinitionToHeldItem(heldNode, definitions[nextIndex]);
    }

    private void UpdateDefinitionOverlay()
    {
        BuildingPlacementNode? heldNode = GetHeldCustomAutoMinerPlacementNode();
        if (heldNode == null)
        {
            _definitionOverlayText = string.Empty;
            return;
        }

        AutoMinerResourceDefinition? definition = heldNode.AutoMinerResourceDefinition;
        if (definition == null)
        {
            _definitionOverlayText = "Auto-Miner Node\nDefinition: <none>";
            return;
        }

        ResourceType resourceType = definition.GetPrimaryResourceType();
        List<AutoMinerResourceDefinition> definitions = GetAvailableAutoMinerDefinitions(resourceType);
        int currentIndex = FindDefinitionIndex(definitions, definition);
        List<WeightedOreChance>? chances = PlaceableNodesUtility.GetPrivateField<List<WeightedOreChance>>(definition, "_possibleOrePrefabs");
        string outputs = chances == null || chances.Count == 0
            ? "none"
            : string.Join(", ", chances
                .Where(chance => chance?.OrePrefab != null)
                .Select(chance => $"{chance.OrePrefab.name} {chance.Weight:0.##}")
                .Take(4));

        _definitionOverlayText =
            $"Auto-Miner Node\n" +
            $"Definition: {definition.name}\n" +
            $"Resource: {resourceType}\n" +
            $"Index: {(currentIndex >= 0 ? currentIndex + 1 : 0)}/{definitions.Count}\n" +
            $"Spawn: {definition.SpawnProbability:0.##}% @ {definition.SpawnRate:0.##}\n" +
            $"Outputs: {outputs}\n" +
            $"Cycle: {FormatShortcut(_cycleAutoMinerDefinitionBind)}";
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_definitionOverlayText))
        {
            return;
        }

        _definitionOverlayStyle ??= BuildDefinitionOverlayStyle();
        Rect panelRect = new(16f, 16f, 430f, 130f);
        Color previousColor = GUI.color;
        GUI.color = Color.black;
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, alphaBlend: false);
        GUI.color = previousColor;
        GUI.Label(panelRect, _definitionOverlayText, _definitionOverlayStyle);
    }

    private void TryLoadAutoMinerDefinitionsFromDisk()
    {
        if (_diskAutoMinerDefinitionsLoaded)
        {
            if (!_autoMinerDefinitionsLoadStateLogged)
            {
                Logger.LogInfo($"{ModInfo.LOG_PREFIX} Skipping packaged auto-miner definition load because definitions are already marked as loaded.");
                _autoMinerDefinitionsLoadStateLogged = true;
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(_autoMinerDefinitionsDataPath))
        {
            if (!_autoMinerDefinitionsLoadStateLogged)
            {
                Logger.LogWarning($"{ModInfo.LOG_PREFIX} Skipping packaged auto-miner definition load because the data path is empty.");
                _autoMinerDefinitionsLoadStateLogged = true;
            }

            return;
        }

        if (!File.Exists(_autoMinerDefinitionsDataPath))
        {
            if (!_autoMinerDefinitionsLoadStateLogged)
            {
                Logger.LogWarning($"{ModInfo.LOG_PREFIX} Skipping packaged auto-miner definition load because the file does not exist at '{_autoMinerDefinitionsDataPath}'.");
                _autoMinerDefinitionsLoadStateLogged = true;
            }

            return;
        }

        try
        {
            _autoMinerDefinitionsLoadStateLogged = false;
            _autoMinerDefinitionsLoadAttemptCount++;
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Packaged auto-miner definition load attempt #{_autoMinerDefinitionsLoadAttemptCount} from '{_autoMinerDefinitionsDataPath}'.");
            string json = File.ReadAllText(_autoMinerDefinitionsDataPath);
            AutoMinerDefinitionExportFile? exportFile = ParseAutoMinerDefinitionExportFile(json);
            if (exportFile?.definitions == null || exportFile.definitions.Length == 0)
            {
                Logger.LogWarning($"{ModInfo.LOG_PREFIX} Packaged auto-miner definition file was empty or invalid.");
                _diskAutoMinerDefinitionsLoaded = true;
                return;
            }

            _diskAutoMinerDefinitionsByName.Clear();
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Found {exportFile.definitions.Length} packaged auto-miner definition records to process.");
            foreach (AutoMinerDefinitionExportRecord record in exportFile.definitions)
            {
                AutoMinerResourceDefinition? definition = CreateRuntimeAutoMinerDefinitionFromExport(record);
                if (definition != null && !string.IsNullOrWhiteSpace(definition.name))
                {
                    _diskAutoMinerDefinitionsByName[definition.name] = definition;
                    Logger.LogInfo($"{ModInfo.LOG_PREFIX} Created packaged runtime definition '{definition.name}' with primary resource '{definition.GetPrimaryResourceType()}'.");
                }
            }

            if (_diskAutoMinerDefinitionsByName.Count == 0)
            {
                Logger.LogWarning($"{ModInfo.LOG_PREFIX} Packaged auto-miner definitions were found but none could be created this attempt. Likely missing ore prefabs in memory.");
                return;
            }

            _diskAutoMinerDefinitionsLoaded = true;
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Loaded {_diskAutoMinerDefinitionsByName.Count} packaged auto-miner resource definitions from {_autoMinerDefinitionsDataPath}.");
        }
        catch (Exception ex)
        {
            _diskAutoMinerDefinitionsLoaded = true;
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to load packaged auto-miner resource definitions: {ex.Message}");
        }
    }

    private List<AutoMinerResourceDefinition> GetAvailableAutoMinerDefinitions(ResourceType? resourceType)
    {
        List<AutoMinerResourceDefinition> liveDefinitions = Resources.FindObjectsOfTypeAll<AutoMinerResourceDefinition>()
            .Where(definition => definition != null)
            .ToList();

        if (resourceType.HasValue)
        {
            liveDefinitions = liveDefinitions.Where(definition => definition.GetPrimaryResourceType() == resourceType.Value).ToList();
        }

        Dictionary<string, AutoMinerResourceDefinition> mergedDefinitions = new(StringComparer.OrdinalIgnoreCase);
        foreach (AutoMinerResourceDefinition definition in _diskAutoMinerDefinitionsByName.Values)
        {
            if (!resourceType.HasValue || definition.GetPrimaryResourceType() == resourceType.Value)
            {
                mergedDefinitions[definition.name] = definition;
            }
        }

        foreach (AutoMinerResourceDefinition definition in liveDefinitions.Distinct())
        {
            mergedDefinitions[definition.name] = definition;
        }

        List<AutoMinerResourceDefinition> materialized = mergedDefinitions.Values.ToList();

        if (resourceType.HasValue)
        {
            IReadOnlyList<string> curatedNames = PlaceableNodesConstants.GetCuratedAutoMinerDefinitionNames(resourceType.Value);
            if (curatedNames.Count > 0)
            {
                Dictionary<string, AutoMinerResourceDefinition> byName = materialized
                    .Where(definition => !string.IsNullOrWhiteSpace(definition.name))
                    .GroupBy(definition => definition.name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

                return curatedNames
                    .Where(name => byName.ContainsKey(name))
                    .Select(name => byName[name])
                    .ToList();
            }
        }

        return materialized
            .OrderBy(definition => definition.GetPrimaryResourceType().ToString(), StringComparer.OrdinalIgnoreCase)
            .ThenBy(definition => definition.name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void LogAvailableDefinitionsForHeldNode(string reason, BuildingPlacementNode heldNode, AutoMinerResourceDefinition currentDefinition, IReadOnlyList<AutoMinerResourceDefinition> definitions)
    {
        string key = string.Join("|",
            reason,
            heldNode.name,
            currentDefinition.name,
            currentDefinition.GetPrimaryResourceType(),
            definitions.Count,
            _diskAutoMinerDefinitionsByName.Count);
        if (string.Equals(_lastAutoMinerAvailabilityLogKey, key, StringComparison.Ordinal))
        {
            return;
        }

        _lastAutoMinerAvailabilityLogKey = key;
        string names = definitions.Count == 0
            ? "<none>"
            : string.Join(", ", definitions.Select(definition => definition.name));
        Logger.LogInfo(
            $"{ModInfo.LOG_PREFIX} Auto-miner definition availability [{reason}] heldNode='{heldNode.name}' current='{currentDefinition.name}' resource='{currentDefinition.GetPrimaryResourceType()}' " +
            $"diskCount={_diskAutoMinerDefinitionsByName.Count} liveCount={Resources.FindObjectsOfTypeAll<AutoMinerResourceDefinition>().Length} availableCount={definitions.Count} names=[{names}]");
    }

    private static int FindDefinitionIndex(IReadOnlyList<AutoMinerResourceDefinition> definitions, AutoMinerResourceDefinition currentDefinition)
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            if (ReferenceEquals(definitions[i], currentDefinition))
            {
                return i;
            }
        }

        if (string.IsNullOrWhiteSpace(currentDefinition.name))
        {
            return -1;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            if (string.Equals(definitions[i].name, currentDefinition.name, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private BuildingPlacementNode? GetHeldCustomAutoMinerPlacementNode()
    {
        BuildingObject? heldObject = GetHeldBuildingObject();
        if (heldObject == null || heldObject.GetComponent<PlaceableNodesPlacementItemMarker>() == null)
        {
            return null;
        }

        BuildingPlacementNode? node = heldObject.GetComponent<BuildingPlacementNode>();
        if (node == null || node.RequirementType != PlacementNodeRequirement.AutoMiner)
        {
            return null;
        }

        return node;
    }

    private static BuildingObject? GetHeldBuildingObject()
    {
        PlayerController? playerController = Object.FindFirstObjectByType<PlayerController>();
        if (playerController?.HeldObject != null)
        {
            return playerController.HeldObject.GetComponent<BuildingObject>();
        }

        BuildingManager? buildingManager = Object.FindFirstObjectByType<BuildingManager>();
        return buildingManager?.GetGhostObject();
    }

    private void ApplyAutoMinerDefinitionToHeldItem(BuildingPlacementNode heldNode, AutoMinerResourceDefinition definition)
    {
        heldNode.AutoMinerResourceDefinition = definition;

        BuildingObject? heldObject = heldNode.GetComponent<BuildingObject>();
        BuildingInventoryDefinition? definitionOwner = heldObject?.Definition;
        if (definitionOwner == null)
        {
            return;
        }

        foreach (ShopItemRuntime runtime in _itemsByKey.Values)
        {
            if (runtime.Plan.Kind != PlaceableNodeKind.AutoMinerPlacementNode || !ReferenceEquals(runtime.Definition, definitionOwner))
            {
                continue;
            }

            foreach (BuildingObject prefab in runtime.Definition.BuildingPrefabs ?? runtime.VariantPrefabs)
            {
                BuildingPlacementNode? prefabNode = prefab.GetComponent<BuildingPlacementNode>();
                if (prefabNode != null)
                {
                    prefabNode.AutoMinerResourceDefinition = definition;
                }
            }
        }
    }

    private AutoMinerResourceDefinition? CreateRuntimeAutoMinerDefinitionFromExport(AutoMinerDefinitionExportRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.name))
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Skipped packaged auto-miner definition record because it was null or had no name.");
            return null;
        }

        if (!Enum.TryParse(record.primaryResourceType, ignoreCase: true, out ResourceType primaryResourceType))
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Skipped packaged auto-miner definition '{record.name}' because primary resource type '{record.primaryResourceType}' could not be parsed.");
            return null;
        }

        List<WeightedOreChance> chances = new();
        if (record.possibleOrePrefabs != null)
        {
            foreach (AutoMinerDefinitionOreRecord oreRecord in record.possibleOrePrefabs)
            {
                OrePiece? orePrefab = FindExportedOrePrefab(oreRecord);
                if (orePrefab == null)
                {
                    Logger.LogWarning(
                        $"{ModInfo.LOG_PREFIX} Could not resolve packaged ore prefab for definition '{record.name}': " +
                        $"ore='{oreRecord.name}', resource='{oreRecord.resourceType}', pieceType='{oreRecord.pieceType}', weight={oreRecord.weight:0.##}.");
                    continue;
                }

                chances.Add(new WeightedOreChance
                {
                    OrePrefab = orePrefab,
                    Weight = oreRecord.weight
                });
            }
        }

        if (chances.Count == 0)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Skipped packaged auto-miner definition '{record.name}' because no ore prefabs could be resolved.");
            return null;
        }

        AutoMinerResourceDefinition definition = ScriptableObject.CreateInstance<AutoMinerResourceDefinition>();
        definition.hideFlags = HideFlags.HideAndDontSave;
        definition.name = record.name;
        definition.SpawnProbability = record.spawnProbability;
        definition.SpawnRate = record.spawnRate;
        PlaceableNodesUtility.SetPrivateField(definition, "_possibleOrePrefabs", chances);

        if (definition.GetPrimaryResourceType() == ResourceType.INVALID && primaryResourceType != ResourceType.INVALID)
        {
            Logger.LogWarning(
                $"{ModInfo.LOG_PREFIX} Skipped packaged auto-miner definition '{record.name}' because created runtime definition reported INVALID primary resource " +
                $"after loading {chances.Count} ore chances. Expected '{primaryResourceType}'.");
            return null;
        }

        return definition;
    }

    private OrePiece? FindExportedOrePrefab(AutoMinerDefinitionOreRecord record)
    {
        if (record == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Encountered null packaged ore record while resolving auto-miner definitions.");
            return null;
        }

        OrePiece? byName = Resources.FindObjectsOfTypeAll<OrePiece>()
            .FirstOrDefault(orePiece =>
                orePiece != null &&
                string.Equals(orePiece.name, record.name, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
        {
            Logger.LogInfo($"{ModInfo.LOG_PREFIX} Resolved packaged ore prefab '{record.name}' directly from loaded resources.");
            return byName;
        }

        if (!Enum.TryParse(record.resourceType, ignoreCase: true, out ResourceType resourceType) ||
            !Enum.TryParse(record.pieceType, ignoreCase: true, out PieceType pieceType))
        {
            Logger.LogWarning(
                $"{ModInfo.LOG_PREFIX} Could not parse packaged ore lookup for ore='{record.name}', resource='{record.resourceType}', pieceType='{record.pieceType}'.");
            return null;
        }

        OrePiece? fallbackPrefab = FindOrePiecePrefab(resourceType, pieceType);
        if (fallbackPrefab != null)
        {
            Logger.LogInfo(
                $"{ModInfo.LOG_PREFIX} Resolved packaged ore prefab '{record.name}' by fallback resource/piece lookup to '{fallbackPrefab.name}' ({resourceType}/{pieceType}).");
            return fallbackPrefab;
        }

        Logger.LogWarning(
            $"{ModInfo.LOG_PREFIX} Failed to resolve packaged ore prefab '{record.name}' by either direct name lookup or fallback resource/piece lookup ({resourceType}/{pieceType}).");
        return null;
    }

    private static GUIStyle BuildDefinitionOverlayStyle()
    {
        GUIStyle style = new(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 13,
            richText = false,
            wordWrap = true,
            padding = new RectOffset(10, 10, 10, 10)
        };

        style.normal.textColor = Color.white;
        return style;
    }

    private static string FormatShortcut(KeyboardShortcut shortcut)
    {
        return shortcut.MainKey == KeyCode.None ? "Unbound" : shortcut.ToString();
    }

    private AutoMinerDefinitionExportFile? ParseAutoMinerDefinitionExportFile(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        string normalizedJson = json.Trim();
        if (normalizedJson.Length > 0 && normalizedJson[0] == '\uFEFF')
        {
            normalizedJson = normalizedJson.Substring(1);
        }

        if (!SimpleJsonParser.TryParse(normalizedJson, out object? rootValue, out string? parseError))
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to parse packaged auto-miner definition JSON: {parseError}");
            return null;
        }

        if (rootValue is not Dictionary<string, object?> rootObject)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Packaged auto-miner definition JSON root was not an object.");
            return null;
        }

        if (!rootObject.TryGetValue("definitions", out object? definitionsValue) || definitionsValue is not List<object?> definitionList)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Packaged auto-miner definition JSON did not contain a valid 'definitions' array.");
            return null;
        }

        List<AutoMinerDefinitionExportRecord> definitions = new();
        foreach (object? definitionValue in definitionList)
        {
            if (definitionValue is not Dictionary<string, object?> definitionObject)
            {
                continue;
            }

            AutoMinerDefinitionExportRecord definition = new()
            {
                name = ReadString(definitionObject, "name"),
                primaryResourceType = ReadString(definitionObject, "primaryResourceType"),
                spawnProbability = ReadFloat(definitionObject, "spawnProbability"),
                spawnRate = ReadFloat(definitionObject, "spawnRate"),
                possibleOrePrefabs = ReadOreRecords(definitionObject).ToArray()
            };
            definitions.Add(definition);
        }

        return new AutoMinerDefinitionExportFile
        {
            definitions = definitions.ToArray()
        };
    }

    private static string ReadString(IReadOnlyDictionary<string, object?> source, string key)
    {
        return source.TryGetValue(key, out object? value) && value is string text
            ? text
            : string.Empty;
    }

    private static float ReadFloat(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out object? value) || value == null)
        {
            return 0f;
        }

        return value switch
        {
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            long longValue => longValue,
            int intValue => intValue,
            string stringValue when float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) => parsed,
            _ => 0f
        };
    }

    private static List<AutoMinerDefinitionOreRecord> ReadOreRecords(IReadOnlyDictionary<string, object?> source)
    {
        List<AutoMinerDefinitionOreRecord> result = new();
        if (!source.TryGetValue("possibleOrePrefabs", out object? oreListValue) || oreListValue is not List<object?> oreList)
        {
            return result;
        }

        foreach (object? oreValue in oreList)
        {
            if (oreValue is not Dictionary<string, object?> oreObject)
            {
                continue;
            }

            result.Add(new AutoMinerDefinitionOreRecord
            {
                name = ReadString(oreObject, "name"),
                resourceType = ReadString(oreObject, "resourceType"),
                pieceType = ReadString(oreObject, "pieceType"),
                weight = ReadFloat(oreObject, "weight")
            });
        }

        return result;
    }

    private sealed class SimpleJsonParser
    {
        private readonly string _text;
        private int _index;

        private SimpleJsonParser(string text)
        {
            _text = text;
        }

        public static bool TryParse(string text, out object? value, out string? error)
        {
            try
            {
                SimpleJsonParser parser = new(text);
                value = parser.ParseValue();
                parser.SkipWhitespace();
                if (!parser.IsAtEnd)
                {
                    throw new FormatException($"Unexpected trailing content at position {parser._index}.");
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                value = null;
                error = ex.Message;
                return false;
            }
        }

        private bool IsAtEnd => _index >= _text.Length;

        private object? ParseValue()
        {
            SkipWhitespace();
            if (IsAtEnd)
            {
                throw new FormatException("Unexpected end of JSON.");
            }

            return _text[_index] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => ParseString(),
                't' => ParseTrue(),
                'f' => ParseFalse(),
                'n' => ParseNull(),
                '-' => ParseNumber(),
                _ when char.IsDigit(_text[_index]) => ParseNumber(),
                _ => throw new FormatException($"Unexpected character '{_text[_index]}' at position {_index}.")
            };
        }

        private Dictionary<string, object?> ParseObject()
        {
            Dictionary<string, object?> result = new(StringComparer.OrdinalIgnoreCase);
            Expect('{');
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return result;
            }

            while (true)
            {
                SkipWhitespace();
                string key = ParseString();
                SkipWhitespace();
                Expect(':');
                object? value = ParseValue();
                result[key] = value;
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private List<object?> ParseArray()
        {
            List<object?> result = new();
            Expect('[');
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return result;
            }

            while (true)
            {
                result.Add(ParseValue());
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            StringBuilder builder = new();
            while (!IsAtEnd)
            {
                char c = _text[_index++];
                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (IsAtEnd)
                {
                    throw new FormatException("Unterminated escape sequence in string.");
                }

                char escaped = _text[_index++];
                switch (escaped)
                {
                    case '"': builder.Append('"'); break;
                    case '\\': builder.Append('\\'); break;
                    case '/': builder.Append('/'); break;
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        if (_index + 4 > _text.Length)
                        {
                            throw new FormatException("Incomplete unicode escape sequence.");
                        }

                        string hex = _text.Substring(_index, 4);
                        builder.Append((char)Convert.ToInt32(hex, 16));
                        _index += 4;
                        break;
                    default:
                        throw new FormatException($"Invalid escape character '{escaped}' in string.");
                }
            }

            throw new FormatException("Unterminated string literal.");
        }

        private object ParseNumber()
        {
            int start = _index;
            if (_text[_index] == '-')
            {
                _index++;
            }

            while (!IsAtEnd && char.IsDigit(_text[_index]))
            {
                _index++;
            }

            bool isFloatingPoint = false;
            if (!IsAtEnd && _text[_index] == '.')
            {
                isFloatingPoint = true;
                _index++;
                while (!IsAtEnd && char.IsDigit(_text[_index]))
                {
                    _index++;
                }
            }

            if (!IsAtEnd && (_text[_index] == 'e' || _text[_index] == 'E'))
            {
                isFloatingPoint = true;
                _index++;
                if (!IsAtEnd && (_text[_index] == '+' || _text[_index] == '-'))
                {
                    _index++;
                }

                while (!IsAtEnd && char.IsDigit(_text[_index]))
                {
                    _index++;
                }
            }

            string numberText = _text.Substring(start, _index - start);
            if (isFloatingPoint)
            {
                return double.Parse(numberText, CultureInfo.InvariantCulture);
            }

            return long.Parse(numberText, CultureInfo.InvariantCulture);
        }

        private bool ParseTrue()
        {
            ExpectKeyword("true");
            return true;
        }

        private bool ParseFalse()
        {
            ExpectKeyword("false");
            return false;
        }

        private object? ParseNull()
        {
            ExpectKeyword("null");
            return null;
        }

        private void ExpectKeyword(string keyword)
        {
            for (int i = 0; i < keyword.Length; i++)
            {
                if (IsAtEnd || _text[_index + i] != keyword[i])
                {
                    throw new FormatException($"Expected '{keyword}' at position {_index}.");
                }
            }

            _index += keyword.Length;
        }

        private void Expect(char expected)
        {
            SkipWhitespace();
            if (IsAtEnd || _text[_index] != expected)
            {
                throw new FormatException($"Expected '{expected}' at position {_index}.");
            }

            _index++;
        }

        private bool TryConsume(char expected)
        {
            SkipWhitespace();
            if (!IsAtEnd && _text[_index] == expected)
            {
                _index++;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }
    }

}
