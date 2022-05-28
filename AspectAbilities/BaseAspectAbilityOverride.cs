using UnityEngine;
using RoR2;
using MysticsRisky2Utils.ContentManagement;
using System.Collections.Generic;
using MysticsRisky2Utils;

namespace AspectAbilities
{
    public abstract class BaseAspectAbilityOverride : BaseLoadableAsset
    {
        public AspectAbility aspectAbility;

        public override void Load()
        {
            aspectAbility = ScriptableObject.CreateInstance<AspectAbility>();
            OnLoad();
            asset = aspectAbility;
        }

        public void Setup(string optionsSection, EquipmentDef equipmentDef, float cooldown, float aiMaxUseDistance = 60f, float aiMaxUseHealthFraction = 0.5f, System.Func<EquipmentSlot, bool> onUseOverride = null)
        {
            aspectAbility.equipmentDef = equipmentDef;
            AspectAbilitiesPlugin.RegisterAspectAbility(aspectAbility);
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                optionsSection,
                "Cooldown",
                cooldown,
                0f,
                1000f,
                "Cooldown of this aspect ability",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    equipmentDef.cooldown = newValue;
                }
            );
            ConfigOptions.ConfigurableValue.CreateBool(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                optionsSection,
                "Enigma Compatible",
                false,
                "Should this ability appear in the Artifact of Enigma pool? (Changes to this value take effect only at the start of a new run)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    equipmentDef.enigmaCompatible = newValue;
                }
            );
            ConfigOptions.ConfigurableValue.CreateBool(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                optionsSection,
                "Can Be Randomly Triggered",
                false,
                "Should this ability appear in Bottled Chaos pool (DLC1)? (Changes to this value take effect only at the start of a new run)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    equipmentDef.canBeRandomlyTriggered = newValue;
                }
            );
            LanguageManager.appendTokens.Add(equipmentDef.pickupToken);
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                optionsSection,
                "AI Max Use Distance",
                aiMaxUseDistance,
                0f,
                1000f,
                "AI-controlled bodies can use this aspect when the distance to their target is less than or equal to this (in meters)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    aspectAbility.aiMaxUseDistance = newValue;
                }
            );
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                optionsSection,
                "AI Max Use Health Fraction",
                aiMaxUseHealthFraction * 100f,
                0f,
                100f,
                "AI-controlled bodies can use this aspect when their health is less than or equal to this amount (in %)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    aspectAbility.aiMaxUseHealthFraction = newValue / 100f;
                }
            );
            On.RoR2.EquipmentSlot.PerformEquipmentAction += (orig, self, equipmentDef2) =>
            {
                if (equipmentDef2 == equipmentDef)
                {
                    return onUseOverride(self);
                }
                return orig(self, equipmentDef2);
            };
        }
    }
}
