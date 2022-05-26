using UnityEngine;
using RoR2;
using MysticsRisky2Utils.BaseAssetTypes;
using UnityEngine.AddressableAssets;
using MysticsRisky2Utils;
using R2API;

namespace AspectAbilities.Buffs
{
    public class EarthRegen : BaseBuff
    {
        public static ConfigOptions.ConfigurableValue<float> regenBonus = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Mending (DLC1)",
            "Regen",
            12.5f,
            0f,
            1000000f,
            "Health regeneration from the buff (in HP/s)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnLoad()
        {
            buffDef.name = "AspectAbilities_EarthRegen";
            buffDef.buffColor = new Color32(161, 231, 79, 255);
            buffDef.canStack = false;
            buffDef.isDebuff = false;
            buffDef.iconSprite = Addressables.LoadAssetAsync<BuffDef>("RoR2/Base/Croco/bdCrocoRegen.asset").WaitForCompletion().iconSprite;

            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += (orig, self) =>
            {
                orig(self);
                AffixEarth.AspectAbilitiesAffixEarth component = AffixEarth.GetAffixComponent(self.gameObject);
                self.UpdateSingleTemporaryVisualEffect(ref component.temporaryVisualEffect, CharacterBody.AssetReferences.crocoRegenEffectPrefab, self.bestFitRadius, self.HasBuff(buffDef), "");
            };

            On.RoR2.CharacterBody.OnBuffFirstStackGained += CharacterBody_OnBuffFirstStackGained;

            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        private void CharacterBody_OnBuffFirstStackGained(On.RoR2.CharacterBody.orig_OnBuffFirstStackGained orig, CharacterBody self, BuffDef buffDef)
        {
            orig(self, buffDef);
            if (buffDef == this.buffDef) Util.PlaySound("Play_affix_mendingBomb_explode", self.gameObject);
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.HasBuff(buffDef)) args.baseRegenAdd += regenBonus;
        }
    }
}
