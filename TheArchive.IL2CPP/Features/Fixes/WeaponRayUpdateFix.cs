using Gear;
using TheArchive.Core.Attributes;
using TheArchive.Core.FeaturesAPI;

namespace TheArchive.Features.Fixes
{
    [EnableFeatureByDefault]
    public class WeaponRayUpdateFix : Feature
    {
        public override string Name => "枪械射击方向修复";

        public override string Description => "在每次枪械单发开火前更新射击方向，确保方向为摄像机朝向，避免出现实际开火方向与准心不符的问题";

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
