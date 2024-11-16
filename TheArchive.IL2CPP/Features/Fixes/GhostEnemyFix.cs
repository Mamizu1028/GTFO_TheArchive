using Enemies;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;

namespace TheArchive.Features.Fixes
{
    [EnableFeatureByDefault]
    public class GhostEnemyFix : Feature
    {
        public override string Name => "Ghost Enemy Fix";

        public override string Description => "Avoid the appearance of ghost enemy bodies.";

        public override FeatureGroup Group => FeatureGroups.Fixes;

        [ArchivePatch(typeof(EnemyAgent), nameof(EnemyAgent.Alive), null, ArchivePatch.PatchMethodType.Setter)]
        private class EnemyAgent__set_Alive__Patch
        {
            private static void Postfix(EnemyAgent __instance, bool value)
            {
                if (value)
                    EnemyUpdateManager.Current.Register(__instance, __instance.CourseNode.m_enemyUpdateMode);
            }
        }
    }
}