using UnityEngine;
using RoR2;
using RoR2.Skills;

namespace TheMysticSword.AspectAbilities.Buffs
{
    public class StarstormVoidLocked : BaseBuff
    {
        public override void OnLoad()
        {
            buffDef.name = "StarstormVoidLocked";
            buffDef.buffColor = new Color32(203, 121, 213, 255);
            buffDef.canStack = false;
            buffDef.iconSprite = Resources.Load<Sprite>("Textures/BuffIcons/texBuffFullCritIcon");
            buffDef.isDebuff = true;

            SkillDef skillDef = ScriptableObject.CreateInstance<SkillDef>();
            skillDef.skillName = "StarstormVoidLocked";
            skillDef.skillNameToken = "ASPECTABILITIES_SKILL_VOIDLOCKED_NAME";
            skillDef.skillDescriptionToken = "ASPECTABILITIES_SKILL_VOIDLOCKED_DESCRIPTION";
            On.RoR2.Skills.SkillCatalog.Init += (orig) =>
            {
                orig();
                skillDef.icon = Object.Instantiate(SkillCatalog.GetSkillDef(SkillCatalog.FindSkillIndexByName("EngiCancelTargetingDummy")).icon);
            };
            skillDef.activationStateMachineName = "Weapon";
            skillDef.activationState = new EntityStates.SerializableEntityStateType(typeof(EntityStates.Idle));
            skillDef.interruptPriority = EntityStates.InterruptPriority.Any;
            skillDef.baseRechargeInterval = 0f;
            skillDef.baseMaxStock = 0;
            skillDef.rechargeStock = 0;
            skillDef.requiredStock = 0;
            skillDef.stockToConsume = 0;
            skillDef.resetCooldownTimerOnUse = false;
            skillDef.fullRestockOnAssign = false;
            skillDef.dontAllowPastMaxStocks = false;
            skillDef.beginSkillCooldownOnSkillEnd = false;
            skillDef.cancelSprintingOnActivation = false;
            skillDef.forceSprintDuringState = false;
            skillDef.canceledFromSprinting = false;
            skillDef.isCombatSkill = false;
            skillDef.mustKeyPress = true;

            AspectAbilitiesContent.Resources.skillDefs.Add(skillDef);

            On.RoR2.CharacterBody.OnClientBuffsChanged += (orig, self) =>
            {
                orig(self);
                if (self.skillLocator && self.skillLocator.primary)
                {
                    if (self.HasBuff(buffDef))
                    {
                        self.skillLocator.primary.SetSkillOverride(self, skillDef, GenericSkill.SkillOverridePriority.Replacement);
                    }
                    else
                    {
                        self.skillLocator.primary.UnsetSkillOverride(self, skillDef, GenericSkill.SkillOverridePriority.Replacement);
                    }
                }
            };

            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += (orig, self) =>
            {
                orig(self);
                self.UpdateSingleTemporaryVisualEffect(ref BaseAspectAbility.GetDefaultComponent(self.gameObject).tempEffect, "Prefabs/TemporaryVisualEffects/NullifyStack3Effect", self.radius, self.HasBuff(AspectAbilitiesContent.Buffs.StarstormVoidLocked), "");
            };
        }
    }
}
