using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Projectile;
using R2API;
using R2API.Utils;
using System.Linq;

namespace TheMysticSword.AspectAbilities
{
    public class AffixRed : BaseAspectAbility
    {
        public static GameObject fireMissile;
        /*
         * this shouldn't be weaker than disposable missile launcher
         * dml launches 12 missiles * 300% = 3600% damage
         * divide the total damage by dml's 45 second cooldown = 80%
         * multiply it by 15s (this aspect's cooldown) = 1200%
         */
        public static float totalMissileDamage = 24f;
        public static int totalMissilesPerUse = 6;

        public override void OnPluginAwake()
        {
            fireMissile = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/Projectiles/MageFireboltBasic"), "AspectAbilitiesFireMissile");

            GameObject targetPrefab = new GameObject();
            Object.DontDestroyOnLoad(targetPrefab);
            targetPrefab.SetActive(false);
            targetPrefab.AddComponent<NetworkIdentity>();
            PrefabAPI.RegisterNetworkPrefab(targetPrefab);
            BlazingMissileControllerTweaks.targetPrefab = targetPrefab;
        }

        public override void OnLoad()
        {
            On.RoR2.EquipmentCatalog.Init += (orig) =>
            {
                orig();
                equipmentDef = RoR2Content.Equipment.AffixRed;
                equipmentDef.cooldown = 15f;
                LanguageManager.appendTokens.Add(equipmentDef.pickupToken);
            };

            // create blazing missile prefab
            fireMissile.GetComponent<ProjectileController>().procCoefficient = 0.25f;
            // we will use a homing missile controller instead of a simple projectile controller
            Object.Destroy(fireMissile.GetComponent<ProjectileSimple>());
            fireMissile.AddComponent<ProjectileTargetComponent>();
            MissileController fireMissileController = fireMissile.AddComponent<MissileController>();
            fireMissileController.maxVelocity = 10f;
            fireMissileController.rollVelocity = 0f;
            fireMissileController.acceleration = 25f;
            fireMissileController.delayTimer = 0.4f;
            fireMissileController.giveupTimer = fireMissileController.delayTimer + 1f;
            fireMissileController.deathTimer = 16f;
            fireMissileController.turbulence = 0f;
            fireMissile.AddComponent<BlazingMissileControllerTweaks>();
            // we need a QuaternionPID for angular movement
            QuaternionPID fireMissileQuaternionPID = fireMissile.AddComponent<QuaternionPID>();
            fireMissileQuaternionPID.PID = new Vector3(10f, 0.3f, 0f);
            fireMissileQuaternionPID.inputQuat = new Quaternion(0f, 0f, 0f, 1f);
            fireMissileQuaternionPID.targetQuat = new Quaternion(0f, 0f, 0f, 1f);
            fireMissileQuaternionPID.outputVector = new Vector3(10f, 0.3f, 0f);
            // how quickly the missile should turn
            fireMissileQuaternionPID.gain = 20f;
            // link the self-destruction timers on both controllers so that the projectile doesn't destroy prematurely from one of the destruction timers being less than the other one
            fireMissile.GetComponent<ProjectileImpactExplosion>().lifetime = fireMissileController.deathTimer;

            AspectAbilitiesContent.Resources.projectilePrefabs.Add(fireMissile);

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<BlazingMissileLauncher>();
            };
        }

        public override bool OnUse(EquipmentSlot self)
        {
            self.characterBody.GetComponent<BlazingMissileLauncher>().ammo += totalMissilesPerUse;
            return true;
        }

        public class BlazingMissileLauncher : NetworkBehaviour
        {
            public int ammo = 0;
            public float timer = 0f;
            public float timerMax = 0.25f;
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
                if (characterBody && characterBody.healthComponent.alive)
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
                                float damage = characterBody.damage * (totalMissileDamage / (float)totalMissilesPerUse);
                                // make the damage equal for elite enemies and players
                                if (!characterBody.isPlayerControlled && characterBody.equipmentSlot.equipmentIndex == RoR2Content.Equipment.AffixRed.equipmentIndex)
                                {
                                    damage /= characterBody.inventory.GetItemCount(RoR2Content.Items.BoostDamage);
                                }
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
                                } else
                                {
                                    UpdateTarget();
                                    if (currentTarget)
                                    {
                                        target = currentTarget.healthComponent.body.gameObject;
                                    }
                                }
                                ProjectileManager.instance.FireProjectile(
                                    fireMissile,
                                    characterBody.corePosition,
                                    Util.QuaternionSafeLookRotation(Quaternion.FromToRotation(Vector3.forward, Vector3.up) * Util.ApplySpread(Vector3.forward, 0f, 60f, 1f, 1f)),
                                    characterBody.gameObject,
                                    damage,
                                    20f,
                                    Util.CheckRoll(characterBody.crit, characterBody.master),
                                    DamageColorIndex.Default,
                                    target,
                                    -1f
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
                    float timer = missileController.GetFieldValue<float>("timer");
                    ProjectileTargetComponent targetComponent = missileController.GetFieldValue<ProjectileTargetComponent>("targetComponent");
                    // make the missile follow a specific point instead of tracking the target
                    if (target && targetComponent)
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
                            Rigidbody rigidbody = missileController.GetFieldValue<Rigidbody>("rigidbody");
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
