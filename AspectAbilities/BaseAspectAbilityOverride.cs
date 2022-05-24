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
            aspectAbility = new AspectAbility();
            OnLoad();
            asset = aspectAbility;
        }

        public void Setup(string optionsSection, EquipmentDef equipmentDef, float cooldown, float aiMaxUseDistance = 60f, float aiMaxUseHealthFraction = 0.5f)
        {
            AspectAbilitiesPlugin.RegisterAspectAbility(equipmentDef, aspectAbility);
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
        }
    }
}