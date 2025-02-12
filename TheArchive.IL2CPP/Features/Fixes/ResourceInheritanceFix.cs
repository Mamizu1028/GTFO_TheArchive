using Player;
using SNetwork;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;

namespace TheArchive.Features.Fixes
{
    [EnableFeatureByDefault]
    public class ResourceInheritanceFix : Feature
    {
        public override string Name => "Resource Inheritance Fix";

        public override string Description => "Fix the issue of resource loss for a player who exits the game during play by transferring their resources to the next player who joins the game.";

        public override FeatureGroup Group => FeatureGroups.Fixes;


        [ArchivePatch(typeof(PlayerBackpackManager), nameof(PlayerBackpackManager.MasterAddAllItemsForDropin))]
        private class PlayerBackpackManager__MasterAddAllItemsForDropin__Prefix
        {
            private static void Prefix(SNet_Player sendToPlayer)
            {
                if (!SNet.IsMaster || sendToPlayer.IsLocal)
                    return;
                if (CurrentGameState != (int)eGameStateName.InLevel)
                    return;
                if (PlayerManager.Current.m_leaverBackpacks.Count == 0)
                    return;
                if (!PlayerBackpackManager.TryGetBackpack(sendToPlayer, out var targetBackpack))
                    return;
                bool hasResourcePack = false;
                bool hasConsumable = false;
                foreach (var leaverBackpack in PlayerManager.Current.m_leaverBackpacks)
                {
                    if (hasResourcePack && hasConsumable)
                        return;
                    if (!hasResourcePack && !targetBackpack.TryGetBackpackItem(InventorySlot.ResourcePack, out var resourcePack))
                    {
                        if (leaverBackpack.TryGetBackpackItem(InventorySlot.ResourcePack, out var leftResourcePack))
                        {
                            PlayerBackpackManager.MasterAddItem(leftResourcePack.Instance, targetBackpack);
                            leaverBackpack.BackpackItems.Remove(leftResourcePack);
                            hasResourcePack = true;
                        }
                    }
                    if (!hasConsumable && !targetBackpack.TryGetBackpackItem(InventorySlot.Consumable, out var consumable))
                    {
                        if (leaverBackpack.TryGetBackpackItem(InventorySlot.Consumable, out var leftConsumable))
                        {
                            PlayerBackpackManager.MasterAddItem(leftConsumable.Instance, targetBackpack);
                            leaverBackpack.BackpackItems.Remove(leftConsumable);
                            hasConsumable = true;
                        }
                    }
                }
            }
        }

        [ArchivePatch(typeof(PlayerManager), nameof(PlayerManager.OnPlayerSpawned))]
        private class PlayerManager__OnPlayerSpawned__Prefix
        {
            private static PlayerBackpack _tempBackpack;

            private static void Prefix(PlayerManager __instance, pPlayerSpawnData spawnData)
            {
                if (!SNet.IsMaster)
                    return;
                if (CurrentGameState != (int)eGameStateName.InLevel)
                    return;

                spawnData.snetPlayer.GetPlayer(out var player);
                if (player.IsBot && __instance.m_leaverBackpacks.Count != 0)
                {
                    _tempBackpack = __instance.m_leaverBackpacks[__instance.m_leaverBackpacks.Count - 1];
                }
            }

            private static void Postfix(PlayerManager __instance, pPlayerSpawnData spawnData)
            {
                if (!SNet.IsMaster)
                    return;
                if (CurrentGameState != (int)eGameStateName.InLevel)
                    return;

                spawnData.snetPlayer.GetPlayer(out var player);
                if (player.IsBot && _tempBackpack != null && !__instance.m_leaverBackpacks.Contains(_tempBackpack))
                {
                    __instance.m_leaverBackpacks.Add(_tempBackpack);
                }
            }
        }
    }
}
