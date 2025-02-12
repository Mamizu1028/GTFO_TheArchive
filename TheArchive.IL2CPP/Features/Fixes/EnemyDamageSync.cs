using SNetwork;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;

namespace TheArchive.Features.Fixes
{
    [EnableFeatureByDefault]
    public class EnemyDamageSync : Feature
    {
        public override string Name => "Enemy Health Sync";

        public override string Description => "When enabled as the host, the enemy's health will be synchronized to the client to correct the issue of incorrect kill indicator on the client.";

        public override FeatureGroup Group => FeatureGroups.Fixes;

        [ArchivePatch(typeof(Dam_EnemyDamageBase), nameof(Dam_EnemyDamageBase.ProcessReceivedDamage))]
        private class Dam_EnemyDamageBase__ProcessReceivedDamage__Patch
        {
            private static void Postfix(Dam_EnemyDamageBase __instance)
            {
                if (!SNet.IsMaster) return;

                pSetHealthData data = new();
                data.health.Set(__instance.Health, __instance.HealthMax);
                __instance.m_setHealthPacket.Send(data, SNet_ChannelType.GameNonCritical);
            }
        }
    }
}
