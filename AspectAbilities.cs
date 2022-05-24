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
using MysticsRisky2Utils;
using System.Reflection;
using BepInEx.Configuration;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace AspectAbilities
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInDependency(MysticsRisky2UtilsPlugin.PluginGUID)]
    [BepInDependency(JarlykMods.Durability.DurabilityPlugin.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [R2APISubmoduleDependency(nameof(LanguageAPI), nameof(NetworkingAPI), nameof(PrefabAPI))]
    public class AspectAbilitiesPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.themysticsword.aspectabilities";
        public const string PluginName = "AspectAbilities";
        public const string PluginVersion = "1.4.11";

        public static System.Reflection.BindingFlags bindingFlagAll = (System.Reflection.BindingFlags)(-1);

        public static ConfigFile config = new ConfigFile(Paths.ConfigPath + "\\TheMysticSword-AspectAbilities.cfg", true);
        public static BepInEx.Logging.ManualLogSource logger;
        public static Assembly executingAssembly;

        public static AssetBundle assetBundle;

        public static ConfigOptions.ConfigurableValue<bool> ignoreBalanceChanges = ConfigOptions.ConfigurableValue.CreateBool(
            PluginGUID,
            PluginName,
            config,
            "General",
            "Ignore Balance Changes",
            true,
            "If true, the mod will use default recommended values for mod balance. Otherwise, the mod will use your preferred values."
        );

        public static ConfigOptions.ConfigurableValue<bool> enemiesCanUseAspects = ConfigOptions.ConfigurableValue.CreateBool(
            PluginGUID,
            PluginName,
            config,
            "Enemy Aspect Usage",
            "Enable",
            false,
            "Let enemies use aspects?"
        );
        public static ConfigOptions.ConfigurableValue<int> enemyStageRequirementDrizzle = ConfigOptions.ConfigurableValue.CreateInt(
            PluginGUID,
            PluginName,
            config,
            "Enemy Aspect Usage",
            "Stage Requirement (Drizzle)",
            6,
            0,
            1000,
            "Enemies can use aspects beginning from this stage on Drizzle difficulty and below."
        );
        public static ConfigOptions.ConfigurableValue<int> enemyStageRequirementRainstorm = ConfigOptions.ConfigurableValue.CreateInt(
            PluginGUID,
            PluginName,
            config,
            "Enemy Aspect Usage",
            "Stage Requirement (Rainstorm)",
            3,
            0,
            1000,
            "Enemies can use aspects beginning from this stage on Rainstorm difficulty and other difficulties between Drizzle and Monsoon."
        );
        public static ConfigOptions.ConfigurableValue<int> enemyStageRequirementMonsoon = ConfigOptions.ConfigurableValue.CreateInt(
            PluginGUID,
            PluginName,
            config,
            "Enemy Aspect Usage",
            "Stage Requirement (Monsoon)",
            1,
            0,
            1000,
            "Enemies can use aspects beginning from this stage on Monsoon difficulty and above."
        );

        public void Awake()
        {
            logger = Logger;
            executingAssembly = Assembly.GetExecutingAssembly();

            assetBundle = AssetBundle.LoadFromFile(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "aspectabilitiesassetbundle"));
            MysticsRisky2Utils.SoftDependencies.SoftDependencyManager.RiskOfOptionsDependency.RegisterModInfo(PluginGUID, PluginName, "Adds on-use abilities to elite aspects", assetBundle.LoadAsset<Sprite>("Assets/Misc/Textures/texModIconAspectAbilities.png"));

            LanguageManager.Init();

            MysticsRisky2Utils.ContentManagement.ContentLoadHelper.PluginAwakeLoad<BaseAspectAbilityOverride>(executingAssembly);

            // make elites auto-use equipment
            On.RoR2.CharacterBody.FixedUpdate += (orig, self) =>
            {
                TryUseAspect(self);
                orig(self);
            };
            // make enigma artifact not reroll aspects
            On.RoR2.Artifacts.EnigmaArtifactManager.OnServerEquipmentActivated += (orig, equipmentSlot, equipmentIndex) =>
            {
                if (FindAspectAbility(equipmentIndex) != null) return;
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
                AspectAbility aspectAbility = FindAspectAbility(equipmentDef2);
                if (aspectAbility != null && aspectAbility.onUseOverride != null)
                {
                    return aspectAbility.onUseOverride(self);
                }
                return orig(self, equipmentDef2);
            };

            // make Jarlyk's EquipmentDurability not affect enemies
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(JarlykMods.Durability.DurabilityPlugin.PluginGuid))
            {
                EquipmentDurabilityFix.Init();
            }

            // create default AspectAbility instances for modded aspects
            RoR2Application.onLoad += () =>
            {
                if (beforeAspectAutoRegister != null) beforeAspectAutoRegister();
                foreach (var aspect in EliteCatalog.eliteDefs.Where(x => x.eliteEquipmentDef != null).Select(x => x.eliteEquipmentDef).Distinct())
                {
                    if (FindAspectAbility(aspect) == null)
                    {
                        RegisterAspectAbility(aspect, new AspectAbility());
                    }
                }
            };
        }

        public static System.Action beforeAspectAutoRegister;

        public static void TryUseAspect(CharacterBody body)
        {
            if (enemiesCanUseAspects)
            {
                if (body.equipmentSlot && body.equipmentSlot.stock > 0 && body.inputBank && !body.isPlayerControlled)
                {
                    var difficulty = DifficultyCatalog.GetDifficultyDef(Run.instance.selectedDifficulty).scalingValue;
                    var stage = Run.instance.stageClearCount + 1;
                    if (difficulty <= 1f && stage < enemyStageRequirementDrizzle) return;
                    else if (difficulty > 1f && difficulty < 3f && stage < enemyStageRequirementRainstorm) return;
                    else if (difficulty >= 3f && stage < enemyStageRequirementMonsoon) return;

                    AspectAbility aspectAbility = FindAspectAbility(body.equipmentSlot.equipmentIndex);
                    if (aspectAbility != null)
                    {
                        AspectAbilitiesBodyFields bodyFields = body.GetComponent<AspectAbilitiesBodyFields>();
                        if (bodyFields && bodyFields.aiCanUse)
                        {
                            bodyFields.aiCanUse = false;

                            EntityStateMachine[] stateMachines = body.gameObject.GetComponents<EntityStateMachine>();
                            foreach (EntityStateMachine stateMachine in stateMachines)
                            {
                                if (stateMachine.initialStateType.stateType.IsInstanceOfType(stateMachine.state) && stateMachine.initialStateType.stateType != stateMachine.mainStateType.stateType)
                                {
                                    return;
                                }
                            }

                            if (aspectAbility.aiMaxUseDistance <= 0f) return;
                            if (aspectAbility.aiMaxUseDistance != Mathf.Infinity && body.master)
                            {
                                BaseAI[] aiComponents = body.master.aiComponents;
                                foreach (BaseAI ai in aiComponents)
                                {
                                    if (ai.currentEnemy.bestHurtBox && Vector3.Distance(body.corePosition, ai.currentEnemy.bestHurtBox.transform.position) > aspectAbility.aiMaxUseDistance)
                                    {
                                        return;
                                    }
                                }
                            }

                            if (body.healthComponent && body.healthComponent.combinedHealthFraction > aspectAbility.aiMaxUseHealthFraction) return;
                            
                            body.inputBank.activateEquipment.PushState(true);
                        }
                    }
                }
            }
        }

        internal static Dictionary<EquipmentDef, AspectAbility> registeredAspectAbilities = new Dictionary<EquipmentDef, AspectAbility>();
        public static AspectAbility FindAspectAbility(EquipmentDef equipmentDef)
        {
            if (registeredAspectAbilities.ContainsKey(equipmentDef)) return registeredAspectAbilities[equipmentDef];
            return null;
        }
        public static AspectAbility FindAspectAbility(EquipmentIndex equipmentIndex)
        {
            return FindAspectAbility(EquipmentCatalog.GetEquipmentDef(equipmentIndex));
        }

        internal static BaseAI.Target GetAITarget(CharacterMaster characterMaster)
        {
            BaseAI[] aiComponents = characterMaster.aiComponents;
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
                                return FindAspectAbility(equipmentSlot.equipmentIndex) != null && equipmentSlot.characterBody.teamComponent.teamIndex != TeamIndex.Player;
                            });
                            c.Emit(OpCodes.Brtrue, label);
                        }
                    }
                );
            }
        }

        public static void RegisterAspectAbility(EquipmentDef equipmentDef, AspectAbility aspectAbility)
        {
            registeredAspectAbilities.Add(equipmentDef, aspectAbility);
        }
    }

    public class AspectAbility
    {
        public float aiMaxUseDistance = 60f;
        public float aiMaxUseHealthFraction = 0.5f;
        public System.Func<EquipmentSlot, bool> onUseOverride;
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
            MysticsRisky2Utils.ContentManagement.ContentLoadHelper contentLoadHelper = new MysticsRisky2Utils.ContentManagement.ContentLoadHelper();
            System.Action[] loadDispatchers = new System.Action[]
            {
                () =>
                {
                    contentLoadHelper.DispatchLoad<BuffDef>(AspectAbilitiesPlugin.executingAssembly, typeof(AspectAbilities.Buffs.BaseBuff), x => contentPack.buffDefs.Add(x));
                },
                () =>
                {
                    contentLoadHelper.DispatchLoad<AspectAbility>(AspectAbilitiesPlugin.executingAssembly, typeof(BaseAspectAbilityOverride), null);
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
                () => ContentLoadHelper.PopulateTypeFields<BuffDef>(typeof(Buffs), contentPack.buffDefs),
                () => contentPack.projectilePrefabs.Add(Resources.projectilePrefabs.ToArray()),
                () => contentPack.bodyPrefabs.Add(Resources.bodyPrefabs.ToArray()),
                () => contentPack.masterPrefabs.Add(Resources.masterPrefabs.ToArray()),
                () => contentPack.effectDefs.Add(Resources.effectPrefabs.ConvertAll(x => new EffectDef(x)).ToArray()),
                () => contentPack.entityStateTypes.Add(Resources.entityStateTypes.ToArray()),
                () => contentPack.networkSoundEventDefs.Add(Resources.networkSoundEventDefs.ToArray()),
                () => contentPack.skillDefs.Add(Resources.skillDefs.ToArray())
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