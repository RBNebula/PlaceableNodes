using HarmonyLib;
using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlaceableNodes;

[HarmonyPatch(typeof(EconomyManager), "Start")]
internal static class PlaceableNodesEconomyManagerStartPatch
{
    private static void Prefix(EconomyManager __instance)
    {
        PlaceableNodes.Instance?.InjectShopDefinitions(__instance);
    }
}

[HarmonyPatch(typeof(OreNode), nameof(OreNode.MarkStaticPositionAsBroken))]
internal static class PlaceableNodesOreNodeMarkStaticPositionAsBrokenPatch
{
    private static bool Prefix(OreNode __instance)
    {
        return __instance.GetComponent<PlaceableNodesOreMarker>() == null;
    }
}

[HarmonyPatch(typeof(OreNode), nameof(OreNode.TakeDamage))]
internal static class PlaceableNodesOreNodeTakeDamagePatch
{
    private static bool Prefix(OreNode __instance, float damage, Vector3 position)
    {
        if (__instance.GetComponent<PlaceableNodesOreMarker>() == null)
        {
            return true;
        }

        SoundDefinition? takeDamageSound =
            PlaceableNodesUtility.GetPrivateField<SoundDefinition>(__instance, "_takeDamageSoundDefinition");
        if (takeDamageSound != null)
        {
            Singleton<SoundManager>.Instance.PlaySoundAtLocation(takeDamageSound, position);
        }

        __instance.Health -= damage;
        if (__instance.Health <= 0f)
        {
            __instance.BreakNode(position);
        }

        return false;
    }
}

[HarmonyPatch(typeof(OreNode), nameof(OreNode.DestroyFromLoading))]
internal static class PlaceableNodesOreNodeDestroyFromLoadingPatch
{
    private static bool Prefix(OreNode __instance)
    {
        return __instance.GetComponent<PlaceableNodesOreMarker>() == null;
    }
}

[HarmonyPatch(typeof(OreNode), "Start")]
internal static class PlaceableNodesOreNodeStartPatch
{
    private static void Postfix(OreNode __instance)
    {
        PlaceableNodes.Instance?.InitializePlacedOreNode(__instance);
    }
}

[HarmonyPatch(typeof(BuildingObject), nameof(BuildingObject.UpdateSupportsAbove))]
internal static class PlaceableNodesBuildingObjectUpdateSupportsAbovePatch
{
    private static void Postfix(BuildingObject __instance, bool isDestroyingThis)
    {
        if (isDestroyingThis)
        {
            return;
        }

        PlaceableNodesUtility.EnsurePlacementCollision(__instance);
    }
}

[HarmonyPatch(typeof(BuildingPlacementNode), "Start")]
internal static class PlaceableNodesBuildingPlacementNodeStartPatch
{
    private static void Postfix(BuildingPlacementNode __instance)
    {
        PlaceableNodesUtility.SanitizePlacementNodeGhostPrefab(__instance);
        try
        {
            PlaceableNodes.Instance?.RefreshPlacementNodeGhostIfNeeded(__instance);
        }
        catch (Exception ex)
        {
            if (PlaceableNodesUtility.IsCustomPlacementNode(__instance))
            {
                PlaceableNodesUtility.SanitizePlacementNodeGhostPrefab(__instance);
                Debug.LogWarning($"{ModInfo.LOG_PREFIX} Suppressed placement-node startup ghost error on '{__instance.name}': {ex.Message}");
            }
        }

        PlaceableNodesUtility.TryRepairPlacementNodeAttachment(__instance);
    }
}

[HarmonyPatch(typeof(BuildingPlacementNode), nameof(BuildingPlacementNode.ShowGhost))]
internal static class PlaceableNodesBuildingPlacementNodeShowGhostPatch
{
    private static readonly HashSet<string> LoggedGhostExceptionKeys = new(StringComparer.Ordinal);

    private static void Prefix(BuildingPlacementNode __instance)
    {
        try
        {
            PlaceableNodesUtility.SanitizePlacementNodeGhostPrefab(__instance);
            PlaceableNodes.Instance?.RefreshPlacementNodeGhostIfNeeded(__instance);
        }
        catch (Exception ex)
        {
            if (PlaceableNodesUtility.IsCustomPlacementNode(__instance))
            {
                PlaceableNodesUtility.SanitizePlacementNodeGhostPrefab(__instance);
                Debug.LogWarning($"{ModInfo.LOG_PREFIX} Suppressed placement-node pre-ShowGhost error on '{__instance.name}': {ex.Message}");
            }
            else
            {
                throw;
            }
        }

        PlaceableNodesUtility.TryRepairPlacementNodeAttachment(__instance);
    }

    private static Exception? Finalizer(BuildingPlacementNode __instance, Exception? __exception)
    {
        if (__exception == null)
        {
            return null;
        }

        if (!PlaceableNodesUtility.IsCustomPlacementNode(__instance))
        {
            return __exception;
        }

        PlaceableNodesUtility.SanitizePlacementNodeGhostPrefab(__instance);
        string logKey = $"{__instance.name}|{__exception.GetType().FullName}|{__exception.Message}";
        if (LoggedGhostExceptionKeys.Add(logKey))
        {
            Debug.LogWarning($"{ModInfo.LOG_PREFIX} Suppressed placement-node ShowGhost exception on '{__instance.name}': {__exception.GetType().Name}: {__exception.Message}");
        }

        return null;
    }
}

[HarmonyPatch(typeof(RapidAutoMinerDrillBit), "PerformAttack")]
internal static class PlaceableNodesRapidAutoMinerDrillBitPerformAttackPatch
{
    private static readonly System.Reflection.MethodInfo AttachToAutominerMethod =
        AccessTools.Method(typeof(RapidAutoMinerDrillBit), "AttachToAutominer");

    private static readonly System.Reflection.FieldInfo DrillBitHitWorldSoundField =
        AccessTools.Field(typeof(RapidAutoMinerDrillBit), "_sound_hit_world");

    private const float DrillBitDamage = 10f;

    private static bool Prefix(RapidAutoMinerDrillBit __instance, float delaySeconds, ref IEnumerator __result)
    {
        __result = PerformAttackReplacement(__instance, delaySeconds);
        return false;
    }

    private static IEnumerator PerformAttackReplacement(RapidAutoMinerDrillBit drillBit, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (drillBit == null || !drillBit.gameObject.activeInHierarchy || drillBit.Owner == null)
        {
            yield break;
        }

        Camera camera = drillBit.Owner.GetComponentInChildren<Camera>();
        if (camera == null || !Physics.Raycast(camera.transform.position, camera.transform.forward, out RaycastHit hitInfo, drillBit.UseRange, drillBit.HitLayers))
        {
            yield break;
        }

        RapidAutoMiner? autoMiner = hitInfo.collider.GetComponentInParent<RapidAutoMiner>();
        if (autoMiner != null && !drillBit.IsBroken())
        {
            AttachToAutominerMethod?.Invoke(drillBit, new object[] { autoMiner, true });
        }

        IDamageable? damageable = hitInfo.collider.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(DrillBitDamage, hitInfo.point);
            Singleton<ParticleManager>.Instance.CreateParticle(
                Singleton<ParticleManager>.Instance.OreNodeHitParticlePrefab,
                hitInfo.point,
                Quaternion.LookRotation(hitInfo.normal));
        }
        else
        {
            SoundDefinition? hitWorldSound = DrillBitHitWorldSoundField.GetValue(drillBit) as SoundDefinition;
            if (hitWorldSound != null)
            {
                Singleton<SoundManager>.Instance.PlaySoundAtLocation(hitWorldSound, hitInfo.point);
            }

            Singleton<ParticleManager>.Instance.CreateParticle(
                Singleton<ParticleManager>.Instance.GenericHitImpactParticle,
                hitInfo.point,
                Quaternion.LookRotation(hitInfo.normal));
        }

        Rigidbody body = hitInfo.collider.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.AddForceAtPosition(camera.transform.forward * 5f, hitInfo.point, ForceMode.Impulse);
        }
    }
}
