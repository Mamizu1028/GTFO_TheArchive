using Gear;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;

namespace TheArchive.Features.Fixes
{
    [EnableFeatureByDefault]
    public class WeaponRayUpdateFix : Feature
    {
        public override string Name => "Weapon Ray Update Fix";

        public override string Description => "Before each single shot of the firearm, update the shooting direction to ensure it aligns with the camera's orientation, avoiding discrepancies between the actual firing direction and the crosshair.";

        public override FeatureGroup Group => FeatureGroups.Fixes;

        private static FPSCamera _camera;

        [ArchivePatch(typeof(BulletWeapon), nameof(BulletWeapon.Fire))]
        private class BulletWeapon__Fire__Patch
        {
            private static void Prefix(BulletWeapon __instance)
            {
                if (__instance.Owner?.IsLocallyOwned ?? false || !__instance.FireButtonPressed)
                {
                    if (_camera == null)
                        _camera = __instance.Owner.FPSCamera;
                    _camera.UpdateCameraRay();
                }
            }
        }

        [ArchivePatch(typeof(Shotgun), nameof(Shotgun.Fire))]
        private class Shotgun__Fire__Patch
        {
            private static void Prefix(Shotgun __instance)
            {
                if (__instance.Owner?.IsLocallyOwned ?? false || !__instance.FireButtonPressed)
                {
                    if (_camera == null)
                        _camera = __instance.Owner.FPSCamera;
                    _camera.UpdateCameraRay();
                }
            }
        }
    }
}
