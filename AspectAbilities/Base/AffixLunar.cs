using UnityEngine;
using RoR2;
using RoR2.Audio;
using UnityEngine.Networking;
using MysticsRisky2Utils;

namespace AspectAbilities
{
    public class AffixLunar : BaseAspectAbilityOverride
    {
        public static NetworkSoundEventDef shellPrepSound;
        public static NetworkSoundEventDef shellUseSound;
        public static Material shellMaterial;

        public static ConfigOptions.ConfigurableValue<float> buffDuration = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Perfected",
            "Buff Duration",
            15f,
            0f,
            120f,
            "How long should the buff last (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnLoad()
        {
            shellPrepSound = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            shellPrepSound.eventName = "Play_lunar_golem_attack2_buildUp";
            AspectAbilitiesContent.Resources.networkSoundEventDefs.Add(shellPrepSound);

            shellUseSound = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            shellUseSound.eventName = "Play_lunar_golem_attack2_shieldActivate";
            AspectAbilitiesContent.Resources.networkSoundEventDefs.Add(shellUseSound);

            EquipmentCatalog.availability.CallWhenAvailable(() => Setup("Perfected", RoR2Content.Equipment.AffixLunar, 45f, onUseOverride: (self) =>
            {
                EffectData effectData = new EffectData
                {
                    origin = self.characterBody.corePosition,
                    rotation = Quaternion.Euler(self.characterBody.inputBank.aimDirection)
                };
                effectData.SetHurtBoxReference(self.characterBody.mainHurtBox);
                EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/LunarGolemShieldCharge"), effectData, true);
                EntitySoundManager.EmitSoundServer(shellPrepSound.index, self.characterBody.networkIdentity);
                AspectAbilitiesAffixLunar component = GetAffixComponent(self.characterBody.gameObject);
                component.activate = true;
                component.prepTime = 1f;
                return true;
            }));
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
                        EntitySoundManager.EmitSoundServer(shellUseSound.index, body.networkIdentity);
                        body.AddTimedBuff(AspectAbilitiesContent.Buffs.AspectAbilities_AltLunarShell, buffDuration);
                    }
                }
            }
        }
    }
}
