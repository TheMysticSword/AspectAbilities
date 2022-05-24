using UnityEngine;
using RoR2;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;
using R2API.Utils;
using RoR2.Skills;
using MysticsRisky2Utils;
using MysticsRisky2Utils.BaseAssetTypes;

namespace AspectAbilities.Buffs
{
    public class IceCrystalDebuff : BaseBuff
    {
        public static SkillDef iceLockedSkillDef;

        public static ConfigOptions.ConfigurableValue<int> lockTimePrimary = ConfigOptions.ConfigurableValue.CreateInt(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Lock Time (Primary)",
            1,
            0,
            60,
            "How much time should an enemy spend in the crystal's range to get their primary skill locked (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<int> lockTimeSecondary = ConfigOptions.ConfigurableValue.CreateInt(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Lock Time (Secondary)",
            2,
            0,
            60,
            "How much time should an enemy spend in the crystal's range to get their secondary skill locked (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<int> lockTimeUtility = ConfigOptions.ConfigurableValue.CreateInt(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Lock Time (Utility)",
            3,
            0,
            60,
            "How much time should an enemy spend in the crystal's range to get their utility skill locked (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<int> lockTimeSpecial = ConfigOptions.ConfigurableValue.CreateInt(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Lock Time (Special)",
            4,
            0,
            60,
            "How much time should an enemy spend in the crystal's range to get their special skill locked (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<bool> gradualDecay = ConfigOptions.ConfigurableValue.CreateBool(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Gradual Debuff Decay",
            true,
            "If true, debuff stacks will gradually disappear over the duration of the debuff. Otherwise, all debuff stacks will disappear only once the debuff timer ends.",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnLoad()
        {
            buffDef.name = "AspectAbilities_IceCrystalDebuff";
            buffDef.buffColor = AffixWhite.iceCrystalColor;
            buffDef.canStack = true;
            buffDef.isDebuff = true;
            buffDef.iconSprite = AspectAbilitiesPlugin.assetBundle.LoadAsset<Sprite>("Assets/Misc/Textures/texAspectAbilitiesIceDebuff.png");

            On.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += (orig, self, buffDef, duration) =>
            {
                orig(self, buffDef, duration);
                var stacks = self.timedBuffs.FindAll(x => x.buffIndex == AspectAbilitiesContent.Buffs.AspectAbilities_IceCrystalDebuff.buffIndex);
                for (var i = 0; i < stacks.Count; i++)
                {
                    stacks[i].timer = duration * (gradualDecay ? ((float)(i + 1) / (float)stacks.Count) : 1f);
                }
            };

            On.RoR2.CharacterBody.FixedUpdate += (orig, self) =>
            {
                orig(self);
                if (self.HasBuff(buffDef))
                {
                    self.outOfDangerStopwatch = 0f;
                }
            };

            iceLockedSkillDef = ScriptableObject.CreateInstance<SkillDef>();
            iceLockedSkillDef.skillName = "AspectAbilities_IceLocked";
            iceLockedSkillDef.skillNameToken = "ASPECTABILITIES_SKILL_ICELOCKED_NAME";
            iceLockedSkillDef.skillDescriptionToken = "ASPECTABILITIES_SKILL_ICELOCKED_DESCRIPTION";
            iceLockedSkillDef.icon = AspectAbilitiesPlugin.assetBundle.LoadAsset<Sprite>("Assets/Misc/Textures/texAspectAbilitiesIceLockedSkillIcon.png");
            iceLockedSkillDef.activationStateMachineName = "Weapon";
            iceLockedSkillDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Idle));
            iceLockedSkillDef.interruptPriority = EntityStates.InterruptPriority.Any;
            iceLockedSkillDef.baseRechargeInterval = 0f;
            iceLockedSkillDef.baseMaxStock = 0;
            iceLockedSkillDef.rechargeStock = 0;
            iceLockedSkillDef.requiredStock = 0;
            iceLockedSkillDef.stockToConsume = 0;
            iceLockedSkillDef.resetCooldownTimerOnUse = false;
            iceLockedSkillDef.fullRestockOnAssign = false;
            iceLockedSkillDef.dontAllowPastMaxStocks = false;
            iceLockedSkillDef.beginSkillCooldownOnSkillEnd = false;
            iceLockedSkillDef.cancelSprintingOnActivation = false;
            iceLockedSkillDef.forceSprintDuringState = false;
            iceLockedSkillDef.canceledFromSprinting = false;
            iceLockedSkillDef.isCombatSkill = false;
            iceLockedSkillDef.mustKeyPress = true;

            AspectAbilitiesContent.Resources.skillDefs.Add(iceLockedSkillDef);

            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.skillLocator.primary)
            {
                if (sender.GetBuffCount(buffDef) >= lockTimePrimary)
                    sender.skillLocator.primary.SetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
                else
                    sender.skillLocator.primary.UnsetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
            }
            if (sender.skillLocator.secondary)
            {
                if (sender.GetBuffCount(buffDef) >= lockTimeSecondary)
                    sender.skillLocator.secondary.SetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
                else
                    sender.skillLocator.secondary.UnsetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
            }
            if (sender.skillLocator.utility)
            {
                if (sender.GetBuffCount(buffDef) >= lockTimeUtility)
                    sender.skillLocator.utility.SetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
                else
                    sender.skillLocator.utility.UnsetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
            }
            if (sender.skillLocator.special)
            {
                if (sender.GetBuffCount(buffDef) >= lockTimeSpecial)
                    sender.skillLocator.special.SetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
                else
                    sender.skillLocator.special.UnsetSkillOverride(sender, iceLockedSkillDef, GenericSkill.SkillOverridePriority.Replacement);
            }
        }
    }
}
