using System;
using System.IO;
using System.Reflection;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private const string EmbeddedNodeBundleResourceName = "PlaceableNodes.Embedded.AssetBundle.minemogul_nodes";
    private const string EmbeddedAutoMinerDefinitionsResourceName = "PlaceableNodes.Embedded.Definitions.AutoMinerResourceDefinitions.json";

    private void EnsureEmbeddedResourcesExtracted()
    {
        try
        {
            Directory.CreateDirectory(_pluginRootPath);
            Directory.CreateDirectory(Path.Combine(_resourceRootPath, PlaceableNodesConstants.AssetBundlesFolderName));
            Directory.CreateDirectory(Path.Combine(_resourceRootPath, "Definitions"));

            ExtractEmbeddedResourceIfNeeded(EmbeddedNodeBundleResourceName, _nodeBundlePath);
            ExtractEmbeddedResourceIfNeeded(EmbeddedAutoMinerDefinitionsResourceName, _autoMinerDefinitionsDataPath);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to extract embedded runtime resources: {ex.Message}");
        }
    }

    private void ExtractEmbeddedResourceIfNeeded(string resourceName, string destinationPath)
    {
        Assembly assembly = typeof(PlaceableNodes).Assembly;
        using Stream? resourceStream = assembly.GetManifestResourceStream(resourceName);
        if (resourceStream == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Embedded resource '{resourceName}' was not found.");
            return;
        }

        byte[] resourceBytes = ReadAllBytes(resourceStream);
        if (File.Exists(destinationPath))
        {
            byte[] existingBytes = File.ReadAllBytes(destinationPath);
            if (BytesEqual(existingBytes, resourceBytes))
            {
                return;
            }
        }

        string? directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(destinationPath, resourceBytes);
        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Extracted embedded resource to '{destinationPath}'.");
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using MemoryStream memoryStream = new();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static bool BytesEqual(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }
}
