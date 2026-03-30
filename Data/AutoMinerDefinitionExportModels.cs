using System;

namespace PlaceableNodes;

[Serializable]
public sealed class AutoMinerDefinitionExportFile
{
    public AutoMinerDefinitionExportRecord[] definitions = Array.Empty<AutoMinerDefinitionExportRecord>();
}

[Serializable]
public sealed class AutoMinerDefinitionExportRecord
{
    public string name = string.Empty;
    public string primaryResourceType = string.Empty;
    public float spawnProbability = 0f;
    public float spawnRate = 0f;
    public AutoMinerDefinitionOreRecord[] possibleOrePrefabs = Array.Empty<AutoMinerDefinitionOreRecord>();
}

[Serializable]
public sealed class AutoMinerDefinitionOreRecord
{
    public string name = string.Empty;
    public string resourceType = string.Empty;
    public string pieceType = string.Empty;
    public float weight = 0f;
}
