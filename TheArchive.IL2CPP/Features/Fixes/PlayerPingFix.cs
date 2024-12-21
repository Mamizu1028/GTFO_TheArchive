using Enemies;
using GameData;
using Player;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;
using UnityEngine;

namespace TheArchive.Features.Fixes
{
    [EnableFeatureByDefault]
    public class PlayerPingFix : Feature
    {
        public override string Name => "玩家标记修复";

        public override string Description => "允许玩家进行随意标记和标记敌人";

        public override bool RequiresRestart => true;

        public override FeatureGroup Group => FeatureGroups.Fixes;

        [ArchivePatch(typeof(LayerManager), nameof(LayerManager.Setup))]
        private class LayerManager__PostSetup__Patch
        {
            private static void Postfix(LayerManager __instance)
            {
                LayerManager.MASK_PING_TARGET = __instance.GetMask("Default", "Default_NoGraph", "Default_BlockGraph", "Interaction", "Dynamic", "EnemyDamagable");
            }
        }

        [ArchivePatch(typeof(EnemyPrefabManager), nameof(EnemyPrefabManager.GenerateEnemy))]
        private class EnemyPrefabManager__GenerateEnemy__Patch
        {
            private static void Postfix(EnemyPrefabManager __instance, EnemyDataBlock data)
            {
                var prefab = EnemyPrefabManager.Current.m_enemyPrefabs[data.persistentID];
                foreach (var collider in prefab.GetComponentsInChildren<Collider>(true))
                {
                    var pingTarget = collider.GetComponent<PlayerPingTarget>();
                    if (pingTarget != null)
                        continue;
                    pingTarget = collider.gameObject.AddComponent<PlayerPingTarget>();
                    pingTarget.m_pingTargetStyle = eNavMarkerStyle.PlayerPingEnemy;
                }
            }
        }

        private static LocalPlayerAgent s_LocalPlayerAgent;
        private static PlayerPingTarget s_tempPlayerPingTarget;

        [ArchivePatch(typeof(LocalPlayerAgent), nameof(LocalPlayerAgent.Setup))]
        private class LocalPlayerAgent__Setup__Patch
        {
            private static void Postfix(LocalPlayerAgent __instance)
            {
                s_LocalPlayerAgent = __instance;
            }
        }

        [ArchivePatch(typeof(GuiManager), nameof(GuiManager.PlayerMarkerIsVisibleAndInFocus))]
        private class GuiManager__PlayerMarkerIsVisibleAndInFocus__Patch
        {
            private static void Postfix(bool __result)
            {
                if (__result)
                    return;

                if (Physics.Raycast(s_LocalPlayerAgent.CamPos, s_LocalPlayerAgent.FPSCamera.Forward, out var raycastHit, 40f, LayerManager.MASK_PING_TARGET, QueryTriggerInteraction.Ignore))
                {
                    s_tempPlayerPingTarget = raycastHit.collider.GetComponentInChildren<PlayerPingTarget>(true);
                    if (s_tempPlayerPingTarget == null)
                    {
                        s_tempPlayerPingTarget = raycastHit.collider.gameObject.AddComponent<PlayerPingTarget>();
                        s_tempPlayerPingTarget.m_pingTargetStyle = eNavMarkerStyle.PlayerPingLookat;
                    }
                    else if (!s_tempPlayerPingTarget.enabled)
                    {
                        s_tempPlayerPingTarget.enabled = true;
                    }
                }
            }
        }
    }
}
