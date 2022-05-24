using UnityEngine;
using RoR2;
using MysticsRisky2Utils.BaseAssetTypes;
using MysticsRisky2Utils;
using UnityEngine.AddressableAssets;

namespace AspectAbilities.Buffs
{
    public class AltLunarShell : BaseBuff
    {
        public static Material shellMaterial;

        public static ConfigOptions.ConfigurableValue<float> maxDamageTaken = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Perfected",
            "Max HP Loss Per Hit",
            10f,
            0f,
            100f,
            "How much HP can you lose in one hit when the buff is active (in %)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnLoad()
        {
            // vanilla LunarShell calculates damage basing on health, ignoring shield, causing it to cap taken damage at 1 because lunar elites have only 1 health
            // that's why we are going to use a custom shell buff

            buffDef.name = "AspectAbilities_AltLunarShell";
            buffDef.buffColor = new Color32(97, 163, 239, 255);
            buffDef.canStack = false;
            buffDef.isDebuff = false;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/LunarGolem/bdLunarShell.asset").WaitForCompletion().iconSprite;

            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += (orig, self) =>
            {
                orig(self);
                AffixLunar.AspectAbilitiesAffixLunar component = AffixLunar.GetAffixComponent(self.gameObject);
                self.UpdateSingleTemporaryVisualEffect(ref component.temporaryVisualEffect, "Prefabs/TemporaryVisualEffects/LunarDefense", self.bestFitRadius, self.HasBuff(AspectAbilitiesContent.Buffs.AspectAbilities_AltLunarShell), "");
            };

            shellMaterial = LegacyResourcesAPI.Load<Material>("Materials/matLunarGolemShield");

            On.RoR2.CharacterModel.UpdateOverlays += (orig, self) =>
            {
                orig(self);
                if (self.body)
                {
                    if (self.activeOverlayCount >= CharacterModel.maxOverlays) return;
                    if (self.body.HasBuff(AspectAbilitiesContent.Buffs.AspectAbilities_AltLunarShell))
                    {
                        Material[] array = self.currentOverlays;
                        int num = self.activeOverlayCount;
                        self.activeOverlayCount++;
                        array[num] = shellMaterial;
                    }
                }
            };

            MysticsRisky2Utils.GenericGameEvents.OnApplyDamageReductionModifiers += GenericGameEvents_OnApplyDamageReductionModifiers;
        }

        public void GenericGameEvents_OnApplyDamageReductionModifiers(DamageInfo damageInfo, MysticsRisky2Utils.MysticsRisky2UtilsPlugin.GenericCharacterInfo attackerInfo, MysticsRisky2Utils.MysticsRisky2UtilsPlugin.GenericCharacterInfo victimInfo, ref float damage)
        {
            if (victimInfo.body && victimInfo.body.HasBuff(AspectAbilitiesContent.Buffs.AspectAbilities_AltLunarShell) && victimInfo.healthComponent)
            {
                damage = Mathf.Min(damage, victimInfo.healthComponent.fullCombinedHealth * maxDamageTaken / 100f);
            }
        }
    }
}
