using UnityEngine;
using RoR2;

namespace TheMysticSword.AspectAbilities.Buffs
{
    public class AltLunarShell : BaseBuff
    {
        public static Material shellMaterial;

        public override void OnLoad()
        {
            // vanilla LunarShell calculates damage basing on health, ignoring shield, causing it to cap taken damage at 1 because lunar elites have only 1 health
            // that's why we are going to use a custom shell buff

            buffDef.name = "AltLunarShell";
            buffDef.iconSprite = Resources.Load<Sprite>("Textures/BuffIcons/texBuffLunarShellIcon");
            buffDef.buffColor = new Color32(97, 163, 239, 255);
            buffDef.canStack = false;
            buffDef.isDebuff = false;

            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += (orig, self) =>
            {
                orig(self);
                AffixLunar.AspectAbilitiesAffixLunar component = AffixLunar.GetAffixComponent(self.gameObject);
                self.UpdateSingleTemporaryVisualEffect(ref component.temporaryVisualEffect, "Prefabs/TemporaryVisualEffects/LunarDefense", self.bestFitRadius, self.HasBuff(AspectAbilitiesContent.Buffs.AltLunarShell), "");
            };

            shellMaterial = Resources.Load<Material>("Materials/matLunarGolemShield");

            On.RoR2.CharacterModel.UpdateOverlays += (orig, self) =>
            {
                orig(self);
                if (self.body)
                {
                    if (self.activeOverlayCount >= CharacterModel.maxOverlays) return;
                    if (self.body.HasBuff(AspectAbilitiesContent.Buffs.AltLunarShell))
                    {
                        Material[] array = self.currentOverlays;
                        int num = self.activeOverlayCount;
                        self.activeOverlayCount++;
                        array[num] = shellMaterial;
                    }
                }
            };

            GenericGameEvents.OnApplyDamageReductionModifiers += (damageInfo, attackerInfo, victimInfo, damage) =>
            {
                if (victimInfo.body && victimInfo.body.HasBuff(AspectAbilitiesContent.Buffs.AltLunarShell) && victimInfo.healthComponent)
                {
                    damage = Mathf.Min(damage, victimInfo.healthComponent.fullCombinedHealth * 0.1f);
                }
                return damage;
            };
        }
    }
}
