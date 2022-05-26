using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Projectile;
using R2API;
using R2API.Utils;
using System.Linq;
using MysticsRisky2Utils;

namespace AspectAbilities
{
    public class AffixRed : BaseAspectAbilityOverride
    {
        public static GameObject fireMissile;
        public static ConfigOptions.ConfigurableValue<int> missileCount = ConfigOptions.ConfigurableValue.CreateInt(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Blazing",
            "Missile Count",
            1,
            0,
            50,
            "How many missiles should be fired on each use",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<float> missileInterval = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Blazing",
            "Missile Firing Interval",
            0.2f,
            0f,
            100f,
            "How much time should pass between each missile launch (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<float> missileDamage = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Blazing",
            "Missile Damage",
            700f,
            0f,
            1000f,
            "How much damage should the missiles deal (in %)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<float> missileDamageEnemies = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Blazing",
            "Missile Damage (Enemies)",
            100f,
            0f,
            1000f,
            "How much damage should the missiles deal when used by enemies (in %)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<bool> missileWeakLockOn = ConfigOptions.ConfigurableValue.CreateBool(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Blazing",
            "Missile Weak Lock On",
            true,
            "Should the missiles target the position where the enemies were at the time of firing?",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnPluginAwake()
        {
            GameObject targetPrefab = new GameObject();
            Object.DontDestroyOnLoad(targetPrefab);
            targetPrefab.SetActive(false);
            targetPrefab.AddComponent<NetworkIdentity>();
            PrefabAPI.RegisterNetworkPrefab(targetPrefab);
            BlazingMissileControllerTweaks.targetPrefab = targetPrefab;
        }

        public override void OnLoad()
        {
            // create blazing missile prefab
            fireMissile = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/Projectiles/MageFireboltBasic"), "AspectAbilitiesFireMissile", false);
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Blazing",
                "Missile Proc Coefficient",
                1f,
                0f,
                1000f,
                "Proc coefficient of the missiles",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    fireMissile.GetComponent<ProjectileController>().procCoefficient = newValue;
                }
            );
            // we will use a homing missile controller instead of a simple projectile controller
            Object.Destroy(fireMissile.GetComponent<ProjectileSimple>());
            Object.Destroy(fireMissile.GetComponentInChildren<MineProximityDetonator>());
            fireMissile.AddComponent<ProjectileTargetComponent>();
            MissileController fireMissileController = fireMissile.AddComponent<MissileController>();
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Blazing",
                "Missile Max Speed",
                10f,
                0f,
                1000f,
                "How fast can the missiles be (in m/s)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    fireMissileController.maxVelocity = newValue;
                }
            );
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Blazing",
                "Missile Acceleration",
                25f,
                0f,
                1000f,
                "How quickly should the missiles gain speed (in m/s^2)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    fireMissileController.acceleration = newValue;
                }
            );
            fireMissileController.rollVelocity = 0f;
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Blazing",
                "Missile Lock On Delay",
                0.3f,
                0f,
                10f,
                "How much time should pass until the missiles lock on to the enemy (in seconds)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    fireMissileController.delayTimer = newValue;
                }
            );
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Blazing",
                "Missile Lock On Duration",
                1f,
                0f,
                16f,
                "How long should the missile homing last (in seconds)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    fireMissileController.giveupTimer = fireMissileController.delayTimer + newValue;
                }
            );
            fireMissileController.deathTimer = 16f;
            fireMissileController.turbulence = 2f;
            fireMissile.AddComponent<BlazingMissileControllerTweaks>();
            // we need a QuaternionPID for angular movement
            QuaternionPID fireMissileQuaternionPID = fireMissile.AddComponent<QuaternionPID>();
            fireMissileQuaternionPID.PID = new Vector3(10f, 0.3f, 0f);
            fireMissileQuaternionPID.inputQuat = new Quaternion(0f, 0f, 0f, 1f);
            fireMissileQuaternionPID.targetQuat = new Quaternion(0f, 0f, 0f, 1f);
            fireMissileQuaternionPID.outputVector = new Vector3(10f, 0.3f, 0f);
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Blazing",
                "Missile Turn Power",
                20f,
                0f,
                20f,
                "How quickly should the missiles turn while homing",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    fireMissileQuaternionPID.gain = newValue;
                }
            );

            var impactExplosion = fireMissile.GetComponent<ProjectileImpactExplosion>();
            impactExplosion.blastDamageCoefficient = 1f;
            impactExplosion.totalDamageMultiplier = 1f;
            impactExplosion.childrenDamageCoefficient = 1f;
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Blazing",
                "Missile Explosion Radius",
                10f,
                0f,
                100f,
                "Radius of the missile impact explosion (in meters)",
                useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry,
                onChanged: (newValue) =>
                {
                    impactExplosion.blastRadius = newValue;
                }
            );
            // link the self-destruction timers on both controllers so that the projectile doesn't destroy prematurely from one of the destruction timers being less than the other one
            impactExplosion.lifetime = fireMissileController.deathTimer;

            AspectAbilitiesContent.Resources.projectilePrefabs.Add(fireMissile);

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<BlazingMissileLauncher>();
            };
            EquipmentCatalog.availability.CallWhenAvailable(() => Setup("Blazing", RoR2Content.Equipment.AffixRed, 7f, onUseOverride: (self) =>
            {
                self.characterBody.GetComponent<BlazingMissileLauncher>().ammo += missileCount;
                return true;
            }));
        }

        public class BlazingMissileLauncher : NetworkBehaviour
        {
            public int ammo = 0;
            public float timer = 0f;
            public float timerMax
            {
                get { return missileInterval; }
            }
            public CharacterBody characterBody;
            public BullseyeSearch targetFinder;
            public HurtBox currentTarget;

            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
                targetFinder = new BullseyeSearch();
            }

            public void UpdateTarget()
            {
                targetFinder.teamMaskFilter = TeamMask.allButNeutral;
                targetFinder.teamMaskFilter.RemoveTeam(characterBody.teamComponent.teamIndex);
                targetFinder.sortMode = BullseyeSearch.SortMode.Angle;
                targetFinder.filterByLoS = true;
                float num;
                Ray ray = CameraRigController.ModifyAimRayIfApplicable(characterBody.inputBank.GetAimRay(), gameObject, out num);
                targetFinder.searchOrigin = ray.origin;
                targetFinder.searchDirection = ray.direction;
                targetFinder.maxAngleFilter = 360f;
                targetFinder.viewer = characterBody;

                targetFinder.RefreshCandidates();
                targetFinder.FilterOutGameObject(gameObject);

                currentTarget = targetFinder.GetResults().FirstOrDefault();
            }

            public void FixedUpdate()
            {
                if (characterBody && characterBody.healthComponent && characterBody.healthComponent.alive)
                {
                    timer += Time.fixedDeltaTime;
                    if (ammo > 0)
                    {
                        if (timer >= timerMax)
                        {
                            Util.PlaySound("Play_item_proc_missile_fire", characterBody.gameObject);
                            Util.PlaySound("Stop_item_proc_missile_fly_loop", characterBody.gameObject);
                            if (NetworkServer.active)
                            {
                                float damage = characterBody.damage * missileDamage / 100f;
                                if (characterBody.teamComponent.teamIndex != TeamIndex.Player) damage = characterBody.damage * missileDamageEnemies / 100f;
                                GameObject target = null;
                                if (!characterBody.isPlayerControlled)
                                {
                                    if (characterBody.master)
                                    {
                                        RoR2.CharacterAI.BaseAI.Target aiTarget = AspectAbilitiesPlugin.GetAITarget(characterBody.master);
                                        if (aiTarget != null)
                                        {
                                            target = aiTarget.gameObject;
                                        }
                                    }
                                }
                                else
                                {
                                    UpdateTarget();
                                    if (currentTarget)
                                    {
                                        target = currentTarget.healthComponent.body.gameObject;
                                    }
                                }
                                MissileUtils.FireMissile(
                                    characterBody.corePosition,
                                    characterBody,
                                    default,
                                    target,
                                    damage,
                                    Util.CheckRoll(characterBody.crit, characterBody.master),
                                    fireMissile,
                                    DamageColorIndex.Item,
                                    true
                                );
                            }
                            ammo--;
                            timer = 0f;
                        }
                    }
                    else
                    {
                        timer = timerMax;
                    }
                }
            }
        }

        private class BlazingMissileControllerTweaks : NetworkBehaviour
        {
            MissileController missileController;
            private bool angularVelocityReset = false;
            internal static GameObject targetPrefab;
            private GameObject target;
            private bool targetAcquired = false;

            private void Awake()
            {
                missileController = GetComponent<MissileController>();
                if (NetworkServer.active)
                {
                    target = Instantiate(targetPrefab);
                    target.SetActive(true);
                    NetworkServer.Spawn(target);
                }
            }

            private void FixedUpdate()
            {
                if (missileController)
                {
                    float timer = missileController.timer;
                    ProjectileTargetComponent targetComponent = missileController.targetComponent;
                    // make the missile follow a specific point instead of tracking the target
                    if (missileWeakLockOn && target && targetComponent)
                    {
                        if (targetComponent.target && !targetAcquired)
                        {
                            target.transform.position = targetComponent.target.position;
                            targetComponent.target = target.transform;
                            targetAcquired = true;
                        }
                    }
                    if (timer >= missileController.giveupTimer)
                    {
                        if (!angularVelocityReset) // angular velocity doesn't get reset when giveupTimer finishes, so the missile keeps spinning
                        {
                            Rigidbody rigidbody = missileController.rigidbody;
                            if (rigidbody)
                            {
                                rigidbody.angularVelocity = Vector3.zero;
                            }
                            angularVelocityReset = true;
                        }
                    }
                }
            }

            private void OnDestroy()
            {
                Object.Destroy(target);
            }
        }
    }
}
