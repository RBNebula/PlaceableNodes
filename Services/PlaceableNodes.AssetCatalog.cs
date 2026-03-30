using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private void WriteAssetCatalogOnce()
    {
        if (_assetCatalogWritten || string.IsNullOrWhiteSpace(_pluginRootPath))
        {
            return;
        }

        List<ImportedAssetRecord> records = PlaceableNodesConstants.ImportedAssets.ToList();
        foreach (ImportedAssetRecord record in records)
        {
            string ripPath = Path.Combine(PlaceableNodesConstants.RipRootPath, record.SourceRelativePath);
            string projectPath = Path.Combine(_pluginRootPath, record.TargetRelativePath);

            record.ExistsInRip = File.Exists(ripPath);
            record.ExistsInProject = File.Exists(projectPath);
        }

        string catalogPath = Path.Combine(_pluginRootPath, PlaceableNodesConstants.ResourceFolderName, "RipAssetCatalog.json");
        Directory.CreateDirectory(Path.GetDirectoryName(catalogPath)!);
        File.WriteAllText(catalogPath, BuildCatalogJson(records), Encoding.UTF8);
        _assetCatalogWritten = true;
    }

    private static string BuildCatalogJson(IReadOnlyList<ImportedAssetRecord> records)
    {
        StringBuilder builder = new();
        builder.AppendLine("{");
        builder.AppendLine("  \"generatedBy\": \"PlaceableNodes\",");
        builder.AppendLine("  \"generatedOn\": \"" + System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",");
        builder.AppendLine("  \"records\": [");

        for (int i = 0; i < records.Count; i++)
        {
            ImportedAssetRecord record = records[i];
            builder.AppendLine("    {");
            builder.AppendLine("      \"kind\": \"" + EscapeJson(record.Kind) + "\",");
            builder.AppendLine("      \"resource\": \"" + EscapeJson(record.ResourceKey) + "\",");
            builder.AppendLine("      \"sourceRelativePath\": \"" + EscapeJson(record.SourceRelativePath) + "\",");
            builder.AppendLine("      \"targetRelativePath\": \"" + EscapeJson(record.TargetRelativePath) + "\",");
            builder.AppendLine("      \"existsInRip\": " + record.ExistsInRip.ToString().ToLowerInvariant() + ",");
            builder.AppendLine("      \"existsInProject\": " + record.ExistsInProject.ToString().ToLowerInvariant());
            builder.Append("    }");
            builder.AppendLine(i < records.Count - 1 ? "," : string.Empty);
        }

        builder.AppendLine("  ]");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
