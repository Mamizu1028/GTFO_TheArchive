using SNetwork;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;

namespace TheArchive.Features.Fixes
{
    [EnableFeatureByDefault]
    public class EnemyDamageSync : Feature
    {
        public override string Name => "敌人生命值同步";

        public override string Description => "使客机可以获取敌人的实时生命值。\n在作为主机时启用后会将敌人生命值同步至客机。\n该功能应保持启用。";

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
