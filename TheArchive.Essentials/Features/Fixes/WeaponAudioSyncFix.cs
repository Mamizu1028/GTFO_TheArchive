using Gear;
using System.Collections.Generic;
using TheArchive.Core.Attributes.Feature;
using TheArchive.Core.Attributes.Feature.Patches;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.FeaturesAPI.Groups;
using static PlayerInventoryBase;

namespace TheArchive.Features.Fixes;

[EnableFeatureByDefault]
public class WeaponAudioSyncFix : Feature
{
    public override string Name => "Weapon Audio Sync Fix";

    public override string Description => "Synchronize the charging and cooling sounds of firearms with other players who have also enabled this feature.";

    public override GroupBase Group => GroupManager.Fixes;

    public override void OnEnable()
    {
        if (CurrentGameState < (int)eGameStateName.Lobby)
            return;

        foreach (var weapon in UnityEngine.Object.FindObjectsOfType<BulletWeapon>())
        {
            s_weaponSyncedUpdaters[weapon.GetInstanceID()] = new(weapon);
        }
    }

    public override void OnDisable()
    {
        s_weaponSyncedUpdaters.Clear();
    }

    private static Dictionary<int, BulletWeaponSyncedDataUpdater> s_weaponSyncedUpdaters = new();

    [ArchivePatch(typeof(BulletWeapon), nameof(BulletWeapon.Update))]
    private class BulletWeapon__Update__Patch
    {
        private static void Postfix(BulletWeapon __instance)
        {
            if (__instance.Owner.IsLocallyOwned)
                s_weaponSyncedUpdaters[__instance.GetInstanceID()].UpdateLocal();
        }
    }

    [ArchivePatch(typeof(BulletWeaponSynced), nameof(BulletWeaponSynced.OnGearSpawnComplete))]
    private class BulletWeaponSynced__OnGearSpawnComplete__Patch
    {
        private static void Postfix(BulletWeaponSynced __instance)
        {
            __instance.m_audioChargeup = BulletWeapon.GetRandomAudioEvents(__instance.AudioData.eventOnChargeup2D);
            __instance.m_audioCooldown = BulletWeapon.GetRandomAudioEvents(__instance.AudioData.eventOnCooldown2D);
            __instance.m_audioChargeupEnd = __instance.AudioData.eventOnChargeupEnd2D;
            __instance.m_audioCooldownEnd = __instance.AudioData.eventOnCooldownEnd2D;
        }
    }

    [ArchivePatch(typeof(PlayerInventoryBase), nameof(PlayerInventoryBase.ReceiveSimpleItemStatus))]
    private class PlayerInventoryBase__ReceiveSimpleItemStatus__Patch
    {
        private static void Postfix(PlayerInventoryBase __instance, pSimpleItemSyncData data)
        {
            if (__instance.Owner.IsLocallyOwned || __instance.m_wieldedItem == null)
                return;

            if (!s_weaponSyncedUpdaters.TryGetValue(__instance.m_wieldedItem.GetInstanceID(), out var updater))
                return;

            updater.ReceiveItemSync(data);
        }
    }

    [ArchivePatch(typeof(BulletWeapon), nameof(BulletWeapon.Setup))]
    private class BulletWeapon__Setup__Patch
    {
        private static void Postfix(BulletWeapon __instance)
        {
            s_weaponSyncedUpdaters[__instance.GetInstanceID()] = new(__instance);
        }
    }

    [ArchivePatch(typeof(BulletWeaponSynced), nameof(BulletWeaponSynced.Setup))]
    private class BulletWeaponSynced__Setup__Patch
    {
        private static void Postfix(BulletWeaponSynced __instance)
        {
            s_weaponSyncedUpdaters[__instance.GetInstanceID()] = new(__instance);
        }
    }

    [ArchivePatch(typeof(ItemEquippable), nameof(ItemEquippable.OnDestroy))]
    private class ItemEquippable__OnDestroy__Patch
    {
        private static void Prefix(ItemEquippable __instance)
        {
            s_weaponSyncedUpdaters.Remove(__instance.GetInstanceID());
        }
    }

    [ArchivePatch(typeof(BulletWeaponSynced), nameof(BulletWeaponSynced.UpdateRegular))]
    private class BulletWeaponSynced__UpdateRegular__Patch
    {
        private static void Prefix(BulletWeaponSynced __instance)
        {
            s_weaponSyncedUpdaters[__instance.GetInstanceID()].UpdateSync();
        }
    }

    private class BulletWeaponSyncedDataUpdater
    {
        public BulletWeaponSyncedDataUpdater(BulletWeapon weapon)
        {
            _weapon = weapon;
            _lastSyncData = default;
        }

        private readonly BulletWeapon _weapon;
        private pSimpleItemSyncData _lastSyncData;
        private float _lastSyncTime;
        private const float _syncTimeout = 0.5f;
        private const float _syncInterval = 0.125f;
        private bool _syncCharging;
        private bool _syncCooldown;

        public void ReceiveItemSync(pSimpleItemSyncData data)
        {
            _lastSyncData = data;
            _lastSyncTime = Clock.Time;
        }

        public void UpdateLocal()
        {
            if (!_weapon.IsEnabled)
                return;

            if (Clock.Time - _lastSyncTime > _syncInterval)
            {
                var data = new pSimpleItemSyncData()
                {
                    inCooldown = _weapon.m_archeType.HasCooldown && _weapon.m_archeType.m_nextBurstTimer > Clock.Time,
                    inFireMode = _weapon.m_archeType.m_inChargeup,
                    inAimMode = _weapon.AimButtonHeld
                };
                _weapon.Owner.Inventory.SendSimpleItemStatus(data, false);
                _lastSyncTime = Clock.Time;
            }
        }

        public void UpdateSync()
        {
            if (Clock.Time - _lastSyncTime > _syncTimeout)
            {
                _lastSyncData.inAimMode = false;
                _lastSyncData.inFireMode = false;
            }
            if (_lastSyncData.inFireMode)
            {
                if (!_syncCharging)
                {
                    _weapon.TriggerAudioChargeup();
                    _syncCharging = true;
                }
            }
            else if (_syncCharging)
            {
                _weapon.TriggerAudioChargeupEnd();
                _syncCharging = false;
            }
            if (_lastSyncData.inCooldown)
            {
                if (!_syncCooldown)
                {
                    _syncCooldown = true;
                    _weapon.TriggerAudioCooldown();
                    return;
                }
            }
            else if (_syncCooldown)
            {
                _weapon.TriggerAudioCooldownEnd();
                _syncCooldown = false;
            }
        }
    }
}
