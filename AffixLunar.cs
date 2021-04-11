using UnityEngine;
using RoR2;
using RoR2.Audio;
using UnityEngine.Networking;

namespace TheMysticSword.AspectAbilities
{
    public static class AffixLunar
    {
        public static NetworkSoundEventDef shellPrepSound;
        public static NetworkSoundEventDef shellUseSound;
        public static BuffDef altLunarShellBuff;
        public static Material shellMaterial;

        public static void Init()
        {
            shellPrepSound = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            shellPrepSound.eventName = "Play_lunar_golem_attack2_buildUp";
            AspectAbilitiesContent.Resources.networkSoundEventDefs.Add(shellPrepSound);

            shellUseSound = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            shellUseSound.eventName = "Play_lunar_golem_attack2_shieldActivate";
            AspectAbilitiesContent.Resources.networkSoundEventDefs.Add(shellUseSound);

            // vanilla LunarShell calculates damage basing on health, ignoring shield, causing it to cap taken damage at 1 because lunar elites have only 1 health
            // that's why we are going to use a custom shell buff
            altLunarShellBuff = ScriptableObject.CreateInstance<BuffDef>();
            altLunarShellBuff.name = "AspectAbilitiesLunarShell";
            altLunarShellBuff.iconSprite = Resources.Load<Sprite>("Textures/BuffIcons/texBuffLunarShellIcon");
            altLunarShellBuff.buffColor = new Color32(97, 163, 239, 255);
            altLunarShellBuff.canStack = false;
            altLunarShellBuff.isDebuff = false;

            AspectAbilitiesContent.Buffs.AltLunarShell = altLunarShellBuff;

            On.RoR2.CharacterBody.UpdateAllTemporaryVisualEffects += (orig, self) =>
            {
                orig(self);
                AspectAbilitiesAffixLunar component = GetAffixComponent(self.gameObject);
                self.UpdateSingleTemporaryVisualEffect(ref component.temporaryVisualEffect, "Prefabs/TemporaryVisualEffects/LunarDefense", self.bestFitRadius, self.HasBuff(altLunarShellBuff), "");
            };

            shellMaterial = Resources.Load<Material>("Materials/matLunarGolemShield");

            On.RoR2.CharacterModel.UpdateOverlays += (orig, self) =>
            {
                orig(self);
                if (self.body)
                {
                    if (self.activeOverlayCount >= CharacterModel.maxOverlays) return;
                    if (self.body.HasBuff(altLunarShellBuff))
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
                if (victimInfo.body && victimInfo.body.HasBuff(altLunarShellBuff) && victimInfo.healthComponent)
                {
                    damage = Mathf.Min(damage, victimInfo.healthComponent.fullCombinedHealth * 0.1f);
                }
                return damage;
            };

            AspectAbilitiesPlugin.RegisterAspectAbility(RoR2Content.Equipment.AffixLunar, 90f,
                (self) =>
                {
                    EffectData effectData = new EffectData
                    {
                        origin = self.characterBody.corePosition,
                        rotation = Quaternion.Euler(self.characterBody.inputBank.aimDirection)
                    };
                    effectData.SetHurtBoxReference(self.characterBody.mainHurtBox);
                    EffectManager.SpawnEffect(Resources.Load<GameObject>("Prefabs/Effects/LunarGolemShieldCharge"), effectData, true);
                    EntitySoundManager.EmitSoundServer(shellPrepSound.index, self.characterBody.networkIdentity);
                    AspectAbilitiesAffixLunar component = GetAffixComponent(self.characterBody.gameObject);
                    component.activate = true;
                    component.prepTime = 1f;
                    return true;
                });
        }

        public static AspectAbilitiesAffixLunar GetAffixComponent(GameObject obj)
        {
            AspectAbilitiesAffixLunar component = obj.GetComponent<AspectAbilitiesAffixLunar>();
            if (!component) component = obj.AddComponent<AspectAbilitiesAffixLunar>();
            return component;
        }

        public class AspectAbilitiesAffixLunar : MonoBehaviour
        {
            public bool activate = false;
            public float prepTime = 0f;
            public CharacterBody body;
            public TemporaryVisualEffect temporaryVisualEffect;

            public void Awake()
            {
                body = GetComponent<CharacterBody>();
            }

            public void FixedUpdate()
            {
                if (!NetworkServer.active) return;
                if (activate)
                {
                    prepTime -= Time.fixedDeltaTime;
                    if (prepTime <= 0f)
                    {
                        activate = false;
                        EntitySoundManager.EmitSoundServer(shellPrepSound.index, body.networkIdentity);
                        body.AddTimedBuff(altLunarShellBuff, 15f);
                    }
                }
            }
        }
    }
}
