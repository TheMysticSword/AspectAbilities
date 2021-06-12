using UnityEngine;
using RoR2;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using UnityEngine.Networking;
using R2API.Utils;

namespace AspectAbilities.Buffs
{
    public class IceCrystalDebuff : BaseBuff
    {
        public override Sprite LoadSprite(string assetName)
        {
            return Resources.Load<Sprite>("Textures/BuffIcons/texBuffPulverizeIcon");
        }

        public override void OnLoad()
        {
            buffDef.name = "IceCrystalDebuff";
            buffDef.buffColor = AffixWhite.iceCrystalColor;
            buffDef.canStack = true;
            buffDef.isDebuff = true;

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<AspectAbilitiesCurseCountLast>();
            };

            On.RoR2.CharacterBody.AddTimedBuff_BuffDef_float += (orig, self, buffDef, duration) =>
            {
                orig(self, buffDef, duration);
                foreach (CharacterBody.TimedBuff timedBuff in self.timedBuffs.FindAll(x => x.buffIndex == AspectAbilitiesContent.Buffs.IceCrystalDebuff.buffIndex))
                {
                    timedBuff.timer = duration;
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

            AddCursePenaltyModifier(0.5f);

            IL.RoR2.CharacterBody.RecalculateStats += (il) =>
            {
                ILCursor c = new ILCursor(il);
                int maxHealthPrevPos = 48;
                int maxShieldPrevPos = 49;
                int trueMaxHealthPos = 50;
                int trueMaxShieldPos = 52;
                int maxHealthDeltaPos = 80;
                int maxShieldDeltaPos = 81;
                // don't regen health when this curse penalty is removed
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_maxHealth"),
                    x => x.MatchLdloc(maxHealthPrevPos),
                    x => x.MatchSub(),
                    x => x.MatchStloc(maxHealthDeltaPos)
                );
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, trueMaxHealthPos);
                c.Emit(OpCodes.Ldloc, maxHealthDeltaPos);
                c.EmitDelegate<System.Func<CharacterBody, float, float, float>>((characterBody, trueMaxHealth, maxHealthDelta) =>
                {
                    if (characterBody.healthComponent)
                    {
                        AspectAbilitiesCurseCountLast curseCount = characterBody.GetComponent<AspectAbilitiesCurseCountLast>();
                        float healthGained = (trueMaxHealth / (1f + GetCurrent(characterBody))) - (trueMaxHealth / (1f + curseCount.value));
                        if (healthGained > 0)
                        {
                            maxHealthDelta -= healthGained;
                        }
                        else if (healthGained < 0)
                        {
                            float takeDamage = -healthGained * (characterBody.healthComponent.Networkhealth / (trueMaxHealth / (1f + curseCount.value)));
                            // don't reduce below 1
                            if (takeDamage > characterBody.healthComponent.Networkhealth)
                            {
                                takeDamage = characterBody.healthComponent.Networkhealth - 1f;
                            }
                            if (takeDamage > 0) characterBody.healthComponent.Networkhealth -= takeDamage;
                        }
                    }
                    return maxHealthDelta;
                });
                c.Emit(OpCodes.Stloc, maxHealthDeltaPos);
                // do the same with shields
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCallOrCallvirt<CharacterBody>("get_maxShield"),
                    x => x.MatchLdloc(maxShieldPrevPos),
                    x => x.MatchSub(),
                    x => x.MatchStloc(maxShieldDeltaPos)
                );
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, trueMaxShieldPos);
                c.Emit(OpCodes.Ldloc, maxShieldDeltaPos);
                c.EmitDelegate<System.Func<CharacterBody, float, float, float>>((characterBody, trueMaxHealth, maxHealthDelta) =>
                {
                    if (characterBody.healthComponent)
                    {
                        AspectAbilitiesCurseCountLast curseCount = characterBody.GetComponent<AspectAbilitiesCurseCountLast>();
                        float healthGained = (trueMaxHealth / (1f + GetCurrent(characterBody))) - (trueMaxHealth / (1f + curseCount.value));
                        if (healthGained > 0)
                        {
                            maxHealthDelta -= healthGained;
                        }
                        else if (healthGained < 0)
                        {
                            float takeDamage = -healthGained * (characterBody.healthComponent.Networkshield / (trueMaxHealth / (1f + curseCount.value)));
                            // don't reduce below 1
                            if (takeDamage > characterBody.healthComponent.Networkshield)
                            {
                                takeDamage = characterBody.healthComponent.Networkshield - 1f;
                            }
                            if (takeDamage > 0) characterBody.healthComponent.Networkshield -= takeDamage;
                        }
                        return maxHealthDelta;
                    }
                });
                c.Emit(OpCodes.Stloc, maxShieldDeltaPos);
            };
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                AspectAbilitiesCurseCountLast curseCount = self.GetComponent<AspectAbilitiesCurseCountLast>();
                curseCount.value = GetCurrent(self);
            };
        }

        public static float GetCurrent(CharacterBody characterBody)
        {
            BuffDef buffDef = AspectAbilitiesContent.Buffs.IceCrystalDebuff;
            return characterBody.HasBuff(buffDef) ? characterBody.GetBuffCount(buffDef) * 0.5f : 0f;
        }

        public class AspectAbilitiesCurseCountLast : NetworkBehaviour
        {
            public float value = 0f;
        }
    }
}
