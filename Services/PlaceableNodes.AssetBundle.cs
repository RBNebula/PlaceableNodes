using System.IO;
using UnityEngine;

namespace PlaceableNodes;

public sealed partial class PlaceableNodes
{
    private bool TryLoadNodeBundle()
    {
        if (_nodeAssetBundle != null)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(_nodeBundlePath) || !File.Exists(_nodeBundlePath))
        {
            return false;
        }

        AssetBundle? assetBundle = AssetBundle.LoadFromFile(_nodeBundlePath);
        if (assetBundle == null)
        {
            Logger.LogWarning($"{ModInfo.LOG_PREFIX} Failed to load assetbundle at {_nodeBundlePath}.");
            return false;
        }

        _nodeAssetBundle = assetBundle;
        _bundlePrefabsByName.Clear();

        foreach (GameObject prefab in assetBundle.LoadAllAssets<GameObject>())
        {
            if (prefab == null)
            {
                continue;
            }

            _bundlePrefabsByName[prefab.name] = prefab;
        }

        Logger.LogInfo($"{ModInfo.LOG_PREFIX} Loaded assetbundle '{Path.GetFileName(_nodeBundlePath)}' with {_bundlePrefabsByName.Count} prefabs.");
        return _bundlePrefabsByName.Count > 0;
    }

    private GameObject? FindBundledPrefab(string prefabName)
    {
        if (!TryLoadNodeBundle())
        {
            return null;
        }

        _bundlePrefabsByName.TryGetValue(prefabName, out GameObject? prefab);
        return prefab;
    }
}
