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
    [BepInDependency(JarlykMods.Durability.DurabilityPlugin.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [R2APISubmoduleDependency(nameof(BuffAPI), nameof(EntityAPI), nameof(LanguageAPI), nameof(NetworkingAPI), nameof(PrefabAPI))]
    public class AspectAbilities : BaseUnityPlugin
    {
        const string PluginGUID = "com.TheMysticSword.AspectAbilities";
        const string PluginName = "AspectAbilities";
        const string PluginVersion = "1.2.2";

        public static System.Reflection.BindingFlags bindingFlagAll = (System.Reflection.BindingFlags)(-1);

        public void Awake()
        {
            AssetManager.Init();
            LanguageManager.Init();

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

            // make Jarlyk's EquipmentDurability not affect enemies
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(JarlykMods.Durability.DurabilityPlugin.PluginGuid))
            {
                EquipmentDurabilityFix.Init();
            }
        }

        public void Update()
        {
            LanguageManager.Update();
        }

        internal static List<EquipmentIndex> registeredAspectAbilities = new List<EquipmentIndex>();
        internal static void RegisterAspectAbility(EquipmentIndex equipmentIndex, float cooldown, System.Func<EquipmentSlot, bool> onUse)
        {
            On.RoR2.EquipmentCatalog.Init += (orig) =>
            {
                orig();
                EquipmentCatalog.GetEquipmentDef(equipmentIndex).cooldown = cooldown;
            };

            LanguageManager.aspectAbilityStringTokens.Add(equipmentIndex, new Dictionary<string, string>());

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

        public static class EquipmentDurabilityFix
        {
            public static void Init()
            {
                new MonoMod.RuntimeDetour.ILHook(
                    typeof(JarlykMods.Durability.DurabilityPlugin).GetMethod("EquipmentSlotOnExecuteIfReady", bindingFlagAll),
                    il =>
                    {
                        ILCursor c = new ILCursor(il);
                        ILLabel label = null;
                        if (c.TryGotoNext(MoveType.After,
                            x => x.MatchLdarg(2),
                            x => x.MatchCallvirt<EquipmentSlot>("get_stock"),
                            x => x.MatchStloc(0),
                            x => x.MatchLdarg(1),
                            x => x.MatchLdarg(2),
                            x => x.MatchCallvirt("On.RoR2.EquipmentSlot/orig_ExecuteIfReady", "Invoke"),
                            x => x.MatchDup(),
                            x => x.MatchBrfalse(out label),
                            x => x.MatchLdarg(2),
                            x => x.MatchCallvirt<EquipmentSlot>("get_stock"),
                            x => x.MatchLdloc(0),
                            x => x.MatchBge(out _)
                        ))
                        {
                            c.Emit(OpCodes.Ldarg_2);
                            c.EmitDelegate<System.Func<EquipmentSlot, bool>>((equipmentSlot) =>
                            {
                                return registeredAspectAbilities.Contains(equipmentSlot.equipmentIndex) && equipmentSlot.characterBody.teamComponent.teamIndex != TeamIndex.Player;
                            });
                            c.Emit(OpCodes.Brtrue, label);
                        }
                    }
                );
            }
        }
    }
}