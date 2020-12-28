using BepInEx;
using RoR2;
using R2API;
using R2API.Utils;
using R2API.Networking;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using UnityEngine;
using UnityEngine.Networking;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HG;

namespace TheMysticSword.AspectAbilities
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin("com.TheMysticSword.AspectAbilities", "Aspect Abilities", "1.2.0")]
    [R2APISubmoduleDependency(nameof(BuffAPI), nameof(EntityAPI), nameof(LanguageAPI), nameof(NetworkingAPI), nameof(PrefabAPI))]
    public class AspectAbilities : BaseUnityPlugin
    {
        public static System.Reflection.BindingFlags bindingFlagAll = (System.Reflection.BindingFlags)(-1);

        public void Awake()
        {
            AssetManager.Init();

            AffixRed.Init();
            AffixBlue.Init();
            AffixWhite.Init();
            AffixPoison.Init();
            AffixHaunted.Init();

            // make elites auto-use equipment
            On.RoR2.CharacterBody.FixedUpdate += (orig, self) =>
            {
                BodyFields bodyFields = self.GetComponent<BodyFields>();
                if (self.equipmentSlot && self.equipmentSlot.stock > 0 && registeredAspectAbilities.Contains(self.equipmentSlot.equipmentIndex) && self.inputBank && !self.isPlayerControlled && Run.instance.stageClearCount >= 15 - 5 * DifficultyCatalog.GetDifficultyDef(Run.instance.selectedDifficulty).scalingValue && bodyFields)
                {
                    bool spawning = false;
                    EntityStateMachine[] stateMachines = self.gameObject.GetComponents<EntityStateMachine>();
                    foreach (EntityStateMachine stateMachine in stateMachines)
                    {
                        if (stateMachine.initialStateType.stateType.IsInstanceOfType(stateMachine.state) && stateMachine.initialStateType.stateType != stateMachine.mainStateType.stateType)
                        {
                            spawning = true;
                            break;
                        }
                    }
                    if (!spawning && bodyFields.aiUseDelay <= 0) self.inputBank.activateEquipment.PushState(true);
                }
                orig(self);
            };
            // make enigma artifact not reroll aspects
            On.RoR2.Artifacts.EnigmaArtifactManager.OnServerEquipmentActivated += (orig, equipmentSlot, equipmentIndex) =>
            {
                if (registeredAspectAbilities.Contains(equipmentIndex)) return;
                orig(equipmentSlot, equipmentIndex);
            };

            // when spawning in a stage, delay enemy elite aspect usage
            Stage.onStageStartGlobal += (stage) =>
            {
                ReadOnlyCollection<CharacterMaster> readOnlyInstancesList = CharacterMaster.readOnlyInstancesList;
                for (int i = 0; i < readOnlyInstancesList.Count; i++)
                {
                    CharacterMaster characterMaster = readOnlyInstancesList[i];
                    if (characterMaster && characterMaster.hasBody)
                    {
                        CharacterBody body = characterMaster.GetBody();
                        BodyFields bodyFields = body.GetComponent<BodyFields>();
                        if (bodyFields)
                        {
                            bodyFields.aiUseDelay += Random.Range(6f, 12f);
                        }
                    }
                }
            };

            // add important components to characters on spawn
            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<BodyFields>();
            };

            On.RoR2.Language.LoadStrings += (orig, self) =>
            {
                orig(self);
                foreach (KeyValuePair<EquipmentIndex, Dictionary<string, string>> keyValuePair in aspectAbilityStringTokens)
                {
                    OverlayEquipmentString(self, keyValuePair.Key, "pickup");
                }
            };
            Language.onCurrentLanguageChanged += () =>
            {
                if (Language.currentLanguage != null)
                {
                    foreach (KeyValuePair<EquipmentIndex, Dictionary<string, string>> keyValuePair in aspectAbilityStringTokens)
                    {
                        SaveEquipmentString(Language.currentLanguage, keyValuePair.Key, "pickup");
                    }
                }
            };

            IL.RoR2.HealthComponent.TakeDamage += (il) =>
            {
                ILCursor c = new ILCursor(il);
                if (c.TryGotoNext(
                    MoveType.AfterLabel,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdfld<HealthComponent>("onIncomingDamageReceivers"),
                    x => x.MatchStloc(13)
                ))
                {
                    c.Emit(OpCodes.Ldarg_0);
                    c.Emit(OpCodes.Ldarg_1);
                    c.EmitDelegate<System.Action<HealthComponent, DamageInfo>>((self, damageInfo) =>
                    {
                        BodyFields bodyFields = self.body.gameObject.GetComponent<BodyFields>();
                        if (bodyFields)
                        {
                            damageInfo.procCoefficient *= bodyFields.multiplierOnHitProcsOnSelf;
                        }
                    });
                }
            };
            IL.RoR2.GlobalEventManager.OnCharacterDeath += (il) =>
            {
                ILCursor c = new ILCursor(il);
                ILLabel label = null;
                if (c.TryGotoNext(
                    MoveType.Before,
                    x => x.MatchLdloc(13),
                    x => x.MatchLdarg(1),
                    x => x.MatchCallvirt<CharacterBody>("HandleOnKillEffectsServer")
                ) && c.TryGotoPrev(
                    MoveType.After,
                    x => x.MatchLdloc(13),
                    x => x.MatchCallOrCallvirt<Object>("op_Implicit"),
                    x => x.MatchBrfalse(out label)
                ))
                {
                    c.Emit(OpCodes.Ldloc_2);
                    c.Emit(OpCodes.Ldloc, 13);
                    c.EmitDelegate<System.Func<CharacterBody, CharacterBody, bool>>((victimBody, attackerBody) =>
                    {
                        if (victimBody && attackerBody && attackerBody.master)
                        {
                            BodyFields bodyFields = victimBody.gameObject.GetComponent<BodyFields>();
                            if (bodyFields && !Util.CheckRoll(100f * bodyFields.multiplierOnDeathProcsOnSelf, attackerBody.master))
                            {
                                return false;
                            }
                        }
                        return true;
                    });
                    c.Emit(OpCodes.Brfalse, label);
                }
            };
        }

        public void Update()
        {
            if (reloadLanguage)
            {
                reloadLanguage = false;
                Language.CCLanguageReload(new ConCommandArgs());
            }
        }

        internal static Dictionary<EquipmentIndex, Dictionary<string, string>> aspectAbilityStringTokens = new Dictionary<EquipmentIndex, Dictionary<string, string>>();
        internal static bool reloadLanguage = false;
        internal static void SaveEquipmentString(Language language, EquipmentIndex equipmentIndex, string token)
        {
            if (aspectAbilityStringTokens.ContainsKey(equipmentIndex))
            {
                if (!aspectAbilityStringTokens[equipmentIndex].ContainsKey(token + "_" + language.name))
                {
                    aspectAbilityStringTokens[equipmentIndex].Add(token + "_" + language.name, language.GetLocalizedStringByToken("ASPECTABILITIES_" + equipmentIndex.ToString().ToUpper() + "_" + token.ToUpper()));
                    reloadLanguage = true;
                }
            }
        }
        internal static void OverlayEquipmentString(Language language, EquipmentIndex equipmentIndex, string token)
        {
            if (aspectAbilityStringTokens.ContainsKey(equipmentIndex) && aspectAbilityStringTokens[equipmentIndex].TryGetValue(token + "_" + language.name, out string newValue))
            {
                string oldValue = language.GetLocalizedStringByToken("EQUIPMENT_" + equipmentIndex.ToString().ToUpper() + "_" + token.ToUpper());
                LanguageAPI.Add(
                    "EQUIPMENT_" + equipmentIndex.ToString().ToUpper() + "_" + token.ToUpper(),
                    oldValue + " " + newValue,
                    language.name
                );
            }
        }

        internal static List<EquipmentIndex> registeredAspectAbilities = new List<EquipmentIndex>();
        internal static void RegisterAspectAbility(EquipmentIndex equipmentIndex, float cooldown, System.Func<EquipmentSlot, bool> onUse)
        {
            On.RoR2.EquipmentCatalog.Init += (orig) =>
            {
                orig();
                EquipmentCatalog.GetEquipmentDef(equipmentIndex).cooldown = cooldown;
            };

            aspectAbilityStringTokens.Add(equipmentIndex, new Dictionary<string, string>());

            On.RoR2.EquipmentSlot.PerformEquipmentAction += (orig, self, equipmentIndex2) =>
            {
                if (equipmentIndex2 == equipmentIndex)
                {
                    return onUse(self);
                }
                return orig(self, equipmentIndex2);
            };

            registeredAspectAbilities.Add(equipmentIndex);
        }

        internal static float GetEliteDamageMultiplier(EliteIndex eliteIndex)
        {
            CombatDirector.EliteTierDef[] eliteTiers = typeof(CombatDirector).GetFieldValue<CombatDirector.EliteTierDef[]>("eliteTiers");
            foreach (CombatDirector.EliteTierDef eliteTier in eliteTiers)
            {
                if (eliteTier.isAvailable() && System.Array.Exists(eliteTier.eliteTypes, eliteType => eliteType == eliteIndex))
                {
                    return eliteTier.damageBoostCoefficient;
                }
            }
            return 1f;
        }

        public class BodyFields : NetworkBehaviour
        {
            public float aiUseDelay = 1f;
            public float multiplierOnHitProcsOnSelf = 1f;
            public float multiplierOnDeathProcsOnSelf = 1f;

            public void FixedUpdate()
            {
                aiUseDelay -= Time.fixedDeltaTime;
            }
        }
    }
}