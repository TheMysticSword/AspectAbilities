using BepInEx;
using RoR2;
using RoR2.CharacterAI;
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
using System.Security;
using System.Security.Permissions;
using RoR2.ContentManagement;
using System.Collections;
using RoR2.Skills;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace TheMysticSword.AspectAbilities
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(JarlykMods.Durability.DurabilityPlugin.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Starstorm2.Starstorm.guid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [R2APISubmoduleDependency(nameof(LanguageAPI), nameof(NetworkingAPI), nameof(PrefabAPI))]
    public class AspectAbilitiesPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.TheMysticSword.AspectAbilities";
        public const string PluginName = "AspectAbilities";
        public const string PluginVersion = "1.4.8";

        public static System.Reflection.BindingFlags bindingFlagAll = (System.Reflection.BindingFlags)(-1);

        public static BepInEx.Logging.ManualLogSource logger;

        public const string TokenPrefix = PluginName + "_";

        public static bool starstorm2Loaded = false;

        public void Awake()
        {
            logger = Logger;

            GenericGameEvents.Init();
            LanguageManager.Init();
            StateSeralizerFix.Init();

            AspectAbilities.ContentManagement.ContentLoadHelper.PluginAwakeLoad<BaseAspectAbility>();

            // make elites auto-use equipment
            On.RoR2.CharacterBody.FixedUpdate += (orig, self) =>
            {
                if (self.equipmentSlot && self.equipmentSlot.stock > 0 && Run.instance.stageClearCount >= 15 - 5 * DifficultyCatalog.GetDifficultyDef(Run.instance.selectedDifficulty).scalingValue && self.inputBank && !self.isPlayerControlled)
                {
                    BaseAspectAbility aspectAbility = FindAspectAbility(self.equipmentSlot.equipmentIndex);
                    if (aspectAbility != default)
                    {
                        AspectAbilitiesBodyFields bodyFields = self.GetComponent<AspectAbilitiesBodyFields>();
                        if (bodyFields && bodyFields.aiCanUse)
                        {
                            bodyFields.aiCanUse = false;

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

                            bool enemyNearby = false;
                            if (aspectAbility.aiMaxDistance == Mathf.Infinity) enemyNearby = true;
                            else if (aspectAbility.aiMaxDistance <= 0f) enemyNearby = false;
                            else if (self.master)
                            {
                                BaseAI[] aiComponents = self.master.GetFieldValue<BaseAI[]>("aiComponents");
                                foreach (BaseAI ai in aiComponents)
                                {
                                    if (ai.currentEnemy.bestHurtBox && Vector3.Distance(self.corePosition, ai.currentEnemy.bestHurtBox.transform.position) <= aspectAbility.aiMaxDistance)
                                    {
                                        enemyNearby = true;
                                    }
                                }
                            }

                            float randomChance = (1f - self.healthComponent.combinedHealthFraction) * 200f;
                            if (!spawning && Util.CheckRoll(randomChance) && enemyNearby) self.inputBank.activateEquipment.PushState(true);
                        }
                    }
                }
                orig(self);
            };
            // make enigma artifact not reroll aspects
            On.RoR2.Artifacts.EnigmaArtifactManager.OnServerEquipmentActivated += (orig, equipmentSlot, equipmentIndex) =>
            {
                if (FindAspectAbility(equipmentIndex) != default) return;
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
                        AspectAbilitiesBodyFields bodyFields = body.GetComponent<AspectAbilitiesBodyFields>();
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
                self.gameObject.AddComponent<AspectAbilitiesBodyFields>();
            };

            // Load the content pack
            ContentManager.collectContentPackProviders += (addContentPackProvider) =>
            {
                addContentPackProvider(new AspectAbilitiesContent());
            };

            On.RoR2.EquipmentSlot.PerformEquipmentAction += (orig, self, equipmentDef2) =>
            {
                BaseAspectAbility aspectAbility = FindAspectAbility(equipmentDef2);
                if (aspectAbility != default)
                {
                    return aspectAbility.OnUse(self);
                }
                return orig(self, equipmentDef2);
            };

            // make Jarlyk's EquipmentDurability not affect enemies
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(JarlykMods.Durability.DurabilityPlugin.PluginGuid))
            {
                EquipmentDurabilityFix.Init();
            }

            starstorm2Loaded = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(Starstorm2.Starstorm.guid);
        }

        internal static List<BaseAspectAbility> registeredAspectAbilities = new List<BaseAspectAbility>();
        public static BaseAspectAbility FindAspectAbility(EquipmentDef equipmentDef)
        {
            return registeredAspectAbilities.FirstOrDefault(x => x.equipmentDef == equipmentDef);
        }
        public static BaseAspectAbility FindAspectAbility(EquipmentIndex equipmentIndex)
        {
            return registeredAspectAbilities.FirstOrDefault(x => x.equipmentDef ? x.equipmentDef.equipmentIndex == equipmentIndex : false);
        }

        internal static BaseAI.Target GetAITarget(CharacterMaster characterMaster)
        {
            BaseAI[] aiComponents = characterMaster.GetFieldValue<BaseAI[]>("aiComponents");
            List<BaseAI.Target> targets = new List<BaseAI.Target>();
            foreach (BaseAI ai in aiComponents)
            {
                if (ai.currentEnemy.gameObject)
                {
                    targets.Add(ai.currentEnemy);
                }
            }
            return targets.FirstOrDefault();
        }

        public class AspectAbilitiesBodyFields : NetworkBehaviour
        {
            public float aiUseDelay = 1f;
            public float aiUseDelayMax = 1f;
            public bool aiCanUse = false;

            public void FixedUpdate()
            {
                aiUseDelay -= Time.fixedDeltaTime;
                if (aiUseDelay <= 0f)
                {
                    aiCanUse = true;
                    aiUseDelay = aiUseDelayMax;
                }
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
                                return registeredAspectAbilities.Any(aspectAbility => aspectAbility.equipmentDef.equipmentIndex == equipmentSlot.equipmentIndex) && equipmentSlot.characterBody.teamComponent.teamIndex != TeamIndex.Player;
                            });
                            c.Emit(OpCodes.Brtrue, label);
                        }
                    }
                );
            }
        }
    }

    public class AspectAbilitiesContent : IContentPackProvider
    {
        public string identifier
        {
            get
            {
                return AspectAbilitiesPlugin.PluginName;
            }
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            contentPack.identifier = identifier;
            AspectAbilities.ContentManagement.ContentLoadHelper contentLoadHelper = new AspectAbilities.ContentManagement.ContentLoadHelper();
            System.Action[] loadDispatchers = new System.Action[]
            {
                () =>
                {
                    contentLoadHelper.DispatchLoad<BuffDef>(typeof(AspectAbilities.Buffs.BaseBuff), x => contentPack.buffDefs.Add(x));
                },
                () =>
                {
                    contentLoadHelper.DispatchLoad<BaseAspectAbility>(typeof(BaseAspectAbility), x => AspectAbilitiesPlugin.registeredAspectAbilities = AspectAbilitiesPlugin.registeredAspectAbilities.Concat(x).ToList());
                }
            };
            int num;
            for (int i = 0; i < loadDispatchers.Length; i = num)
            {
                loadDispatchers[i]();
                args.ReportProgress(Util.Remap((float)(i + 1), 0f, (float)loadDispatchers.Length, 0f, 0.05f));
                yield return null;
                num = i + 1;
            }
            while (contentLoadHelper.coroutine.MoveNext())
            {
                args.ReportProgress(Util.Remap(contentLoadHelper.progress.value, 0f, 1f, 0.05f, 0.95f));
                yield return contentLoadHelper.coroutine.Current;
            }
            loadDispatchers = new System.Action[]
            {
                () =>
                {
                    ContentLoadHelper.PopulateTypeFields<BuffDef>(typeof(Buffs), contentPack.buffDefs);
                    AspectAbilities.ContentManagement.ContentLoadHelper.AddModPrefixToAssets<BuffDef>(contentPack.buffDefs);
                },
                () =>
                {
                    contentPack.projectilePrefabs.Add(Resources.projectilePrefabs.ToArray());
                    contentPack.bodyPrefabs.Add(Resources.bodyPrefabs.ToArray());
                    contentPack.masterPrefabs.Add(Resources.masterPrefabs.ToArray());
                    contentPack.effectDefs.Add(Resources.effectPrefabs.ConvertAll(x => new EffectDef(x)).ToArray());
                    contentPack.entityStateTypes.Add(Resources.entityStateTypes.ToArray());
                    contentPack.networkSoundEventDefs.Add(Resources.networkSoundEventDefs.ToArray());
                    contentPack.skillDefs.Add(Resources.skillDefs.ToArray());
                }
            };
            for (int i = 0; i < loadDispatchers.Length; i = num)
            {
                loadDispatchers[i]();
                args.ReportProgress(Util.Remap((float)(i + 1), 0f, (float)loadDispatchers.Length, 0.95f, 0.99f));
                yield return null;
                num = i + 1;
            }
            loadDispatchers = null;
            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(contentPack, args.output);
            args.ReportProgress(1f);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            args.ReportProgress(1f);
            yield break;
        }

        private ContentPack contentPack = new ContentPack();

        public static class Buffs
        {
            public static BuffDef AltLunarShell;
            public static BuffDef IceCrystalDebuff;
            public static BuffDef StarstormVoidLocked;
        }

        public static class Resources
        {
            public static List<GameObject> projectilePrefabs = new List<GameObject>();
            public static List<GameObject> bodyPrefabs = new List<GameObject>();
            public static List<GameObject> masterPrefabs = new List<GameObject>();
            public static List<GameObject> effectPrefabs = new List<GameObject>();
            public static List<System.Type> entityStateTypes = new List<System.Type>();
            public static List<NetworkSoundEventDef> networkSoundEventDefs = new List<NetworkSoundEventDef>();
            public static List<SkillDef> skillDefs = new List<SkillDef>();
        }
    }
}