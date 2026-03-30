using BepInEx;

namespace PlaceableNodes;

[BepInPlugin(ModInfo.PLUGIN_GUID, ModInfo.PLUGIN_NAME, ModInfo.PLUGIN_VERSION)]
[BepInDependency("com.rebind", BepInDependency.DependencyFlags.SoftDependency)]
public sealed partial class PlaceableNodes : BaseUnityPlugin
{
    internal static PlaceableNodes? Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        InitializePlugin();
    }

    private void Update()
    {
        TickPlugin();
    }

    private void OnDestroy()
    {
        ShutdownPlugin();
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }
    }
}
