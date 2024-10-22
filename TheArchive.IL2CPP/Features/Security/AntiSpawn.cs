using Enemies;
using SNetwork;
using System;
using TheArchive.Core.Attributes;
using TheArchive.Core.Attributes.Feature.Settings;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.Localization;
using TheArchive.Interfaces;
using TheArchive.Utilities;

namespace TheArchive.Features.Security
{
    [EnableFeatureByDefault]
    [RundownConstraint(Utils.RundownFlags.RundownSix, Utils.RundownFlags.Latest)]
    public class AntiSpawn : Feature
    {
        public override string Name => "Anti Spawn";

        public override FeatureGroup Group => FeatureGroups.Security;

        public override string Description => "Prevents clients from spawning in enemies.";

        public new static IArchiveLogger FeatureLogger { get; set; }

        [FeatureConfig]
        public static AntiSpawnSettings Settings { get; set; }

        public class AntiSpawnSettings
        {
            [FSDisplayName("Punish Friends")]
            [FSDescription("If (Steam) Friends should be affected as well.")]
            public bool PunishFriends { get; set; } = false;

            [FSDisplayName("Punishment")]
            [FSDescription("What to do with griefers that are trying to spawn in enemies.")]
            public PunishmentMode Punishment { get; set; } = PunishmentMode.Kick;

            [Localized]
            public enum PunishmentMode
            {
                NoneAndLog,
                Kick,
                KickAndBan
            }
        }

        public static bool PunishPlayer(SNet_Player player)
        {
            if (player == null)
                return true;

            if (player.IsFriend() && !Settings.PunishFriends)
            {
                FeatureLogger.Notice($"Friend \"{player.NickName}\" is spawning something in!");
                return false;
            }

            switch (Settings.Punishment)
            {
                case AntiSpawnSettings.PunishmentMode.KickAndBan:
                    PlayerLobbyManagement.BanPlayer(player);
                    goto default;
                case AntiSpawnSettings.PunishmentMode.Kick:
                    PlayerLobbyManagement.KickPlayer(player);
                    goto default;
                default:
                case AntiSpawnSettings.PunishmentMode.NoneAndLog:
                    FeatureLogger.Notice($"Player \"{player.NickName}\" tried to spawn something! ({Settings.Punishment})");
                    return true;
            }
        }

        [ArchivePatch(typeof(SNet_ReplicationManager<pEnemyGroupSpawnData, SNet_DynamicReplicator<pEnemyGroupSpawnData>>), nameof(SNet_ReplicationManager<pEnemyGroupSpawnData, SNet_DynamicReplicator<pEnemyGroupSpawnData>>.InternalSpawnRequestFromSlaveCallback), new Type[] { typeof(pEnemyGroupSpawnData) })]
        private class SNet_ReplicationManager_pEnemyGroupSpawnData_EnemyReplicator__InternalSpawnRequestFromSlaveCallback__Patch
        {
            private static bool Prefix()
            {
                if (SNet.IsMaster && !SNet.Capture.IsCheckpointRecall)
                {
                    bool cancelSpawn = true;

                    if (SNet.Replication.TryGetLastSender(out var sender))
                    {
                        cancelSpawn = PunishPlayer(sender);
                    }

                    if (cancelSpawn)
                    {
                        FeatureLogger.Fail("Cancelled enemy spawn!");
                        return false;
                    }
                }
                return true;
            }
        }


        [ArchivePatch(typeof(SNet_ReplicationManager<pEnemySpawnData, EnemyReplicator>), nameof(SNet_ReplicationManager<pEnemySpawnData, EnemyReplicator>.InternalSpawnRequestFromSlaveCallback), new Type[] { typeof(pEnemySpawnData) })]
        private class SNet_ReplicationManager_pEnemySpawnData_EnemyReplicator__InternalSpawnRequestFromSlaveCallback__Patch
        {
            private static bool Prefix()
            {
                if (SNet.IsMaster && !SNet.Capture.IsCheckpointRecall)
                {
                    bool cancelSpawn = true;

                    if (SNet.Replication.TryGetLastSender(out var sender))
                    {
                        cancelSpawn = PunishPlayer(sender);
                    }

                    if (cancelSpawn)
                    {
                        FeatureLogger.Fail("Cancelled enemy spawn!");
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
