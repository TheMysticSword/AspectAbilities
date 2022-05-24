using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace AspectAbilities
{
    public class AffixVoid : BaseAspectAbilityOverride
    {
        public override void OnLoad()
        {
            EquipmentCatalog.availability.CallWhenAvailable(() => Setup("Voidtouched (DLC1)", DLC1Content.Equipment.EliteVoidEquipment, 7f));

            aspectAbility.onUseOverride = (self) =>
            {
                if (self.characterBody)
                {
                    Util.CleanseBody(self.characterBody, false, false, true, false, false, false);
                    if (self.characterBody.skillLocator) self.characterBody.skillLocator.ResetSkills();

                    EffectManager.SpawnEffect(Addressables.LoadAssetAsync<GameObject>("RoR2/Base/Nullifier/NullifierExplosion.prefab").WaitForCompletion(), new EffectData
                    {
                        origin = self.characterBody.corePosition,
                        scale = self.characterBody.bestFitRadius
                    }, true);
                }
                return true;
            };
        }
    }
}
