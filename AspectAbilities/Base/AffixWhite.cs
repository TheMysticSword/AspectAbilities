using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Navigation;
using RoR2.Projectile;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using System.Linq;
using System.Collections.Generic;
using MysticsRisky2Utils;
using UnityEngine.AddressableAssets;

namespace AspectAbilities
{
    public class AffixWhite : BaseAspectAbilityOverride
    {
        public static GameObject iceCrystal;
        public static GameObject iceCrystalProjectile;
        public static GameObject iceCrystalProjectileGhost;
        public static SpawnCard iceCrystalSpawnCard;
        public static GameObject iceCrystalExplosionEffect;
        public static Color iceCrystalColor = new Color(209f / 255f, 236f / 255f, 236f / 255f);

        public static ConfigOptions.ConfigurableValue<float> flyTime = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Crystal Fly Time",
            0.7f,
            0f,
            1000f,
            "How long should the crystal projectile fly before hitting the ground (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<float> crystalRadius = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Crystal Radius",
            30f,
            0f,
            1000f,
            "Radius of the ice crystal debuff range (in meters)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );
        public static ConfigOptions.ConfigurableValue<bool> radiusAffectedByHealth = ConfigOptions.ConfigurableValue.CreateBool(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Glacial",
            "Radius Affected By HP",
            true,
            "Should the ice crystal debuff range shrink the less health the crystal has?",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnLoad()
        {
            EquipmentCatalog.availability.CallWhenAvailable(() => Setup("Glacial", RoR2Content.Equipment.AffixWhite, 45f));

            AspectAbilitiesContent.Resources.entityStateTypes.Add(typeof(GlacialWardDeath));

            // create glacial ward prefab
            iceCrystal = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/TimeCrystalBody"), "AspectAbilitiesIceCrystalBody", false);
            Object.DestroyImmediate(iceCrystal.GetComponent<NetworkStateMachine>());
            iceCrystal.AddComponent<NetworkStateMachine>().stateMachines = new EntityStateMachine[]
            {
                iceCrystal.GetComponent<EntityStateMachine>()
            };
            MeshCollider meshCollider = iceCrystal.AddComponent<MeshCollider>();
            meshCollider.gameObject.layer = LayerIndex.defaultLayer.intVal;
            meshCollider.sharedMesh = iceCrystal.transform.Find("ModelBase").Find("Mesh").gameObject.GetComponent<MeshFilter>().sharedMesh;
            CharacterBody body = iceCrystal.GetComponent<CharacterBody>();
            Transform modelBaseTransform = iceCrystal.GetComponent<ModelLocator>().modelBaseTransform;
            CharacterModel model = modelBaseTransform.Find("Mesh").gameObject.GetComponent<CharacterModel>();
            model.body = body;
            body.baseNameToken = "ASPECTABILITIES_ICECRYSTAL_BODY_NAME";
            body.portraitIcon = LegacyResourcesAPI.Load<Texture>("Textures/MiscIcons/texMysteryIcon");
            body.bodyFlags = iceCrystal.GetComponent<CharacterBody>().bodyFlags | CharacterBody.BodyFlags.ImmuneToExecutes | CharacterBody.BodyFlags.HasBackstabImmunity;
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Glacial",
                "Crystal Base Max HP",
                160f,
                0f,
                100000f,
                "Base maximum health of the ice crystals",
                onChanged: (newValue) =>
                {
                    body.baseMaxHealth = newValue;
                    body.PerformAutoCalculateLevelStats();
                }
            );
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Glacial",
                "Crystal Base Regen",
                5f,
                0f,
                100000f,
                "Base health regeneration of the ice crystals (in HP/s)",
                onChanged: (newValue) =>
                {
                    body.baseRegen = newValue;
                    body.PerformAutoCalculateLevelStats();
                }
            );
            iceCrystal.AddComponent<DestroyOnTimer>();
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Glacial",
                "Crystal Duration",
                20f,
                0f,
                1000f,
                "How long should the ice crystals last (in seconds)",
                onChanged: (newValue) =>
                {
                    iceCrystal.GetComponent<DestroyOnTimer>().duration = newValue;
                }
            );
            // replace the pink time crystal material with an ice material
            CharacterModel.RendererInfo[] rendererInfos = model.baseRendererInfos;
            var mat = Material.Instantiate(Addressables.LoadAssetAsync<Material>("RoR2/Base/Common/VFX/matIcePillarBase.mat").WaitForCompletion());
            for (int i = 0; i < rendererInfos.Length; i++)
            {
                rendererInfos[i].defaultMaterial = mat;
            }
            CharacterModel.LightInfo[] lightInfos = model.baseLightInfos;
            for (int i = 0; i < lightInfos.Length; i++)
            {
                lightInfos[i].defaultColor = iceCrystalColor;
            }
            
            // buff ward
            BuffWard buffWard = iceCrystal.transform.Find("ModelBase/Mesh").gameObject.AddComponent<BuffWard>();
            buffWard.animateRadius = false;
            RoR2Application.onLoad += () => buffWard.buffDef = AspectAbilitiesContent.Buffs.AspectAbilities_IceCrystalDebuff;
            ConfigOptions.ConfigurableValue.CreateFloat(
                AspectAbilitiesPlugin.PluginGUID,
                AspectAbilitiesPlugin.PluginName,
                AspectAbilitiesPlugin.config,
                "Glacial",
                "Debuff Duration",
                4f,
                0f,
                60f,
                "How long should the skill lock debuff last",
                onChanged: (newValue) =>
                {
                    buffWard.buffDuration = newValue;
                }
            );
            buffWard.interval = 1f;
            buffWard.invertTeamFilter = true;

            // team area indicator
            GameObject teamIndicator = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/Projectiles/PoisonStakeProjectile").transform.Find("ActiveVisuals/TeamAreaIndicator, FullSphere").gameObject, "AspectAbilitiesIceCrystalTeamIndicator", false);
            Object.Destroy(teamIndicator.transform.Find("ProximityDetonator").gameObject);
            teamIndicator.transform.SetParent(model.transform);
            teamIndicator.transform.localPosition = Vector3.zero;
            teamIndicator.transform.localScale = Vector3.one * 3f;

            TeamAreaIndicator teamAreaIndicator = teamIndicator.GetComponent<TeamAreaIndicator>();
            teamAreaIndicator.teamFilter = null;
            teamAreaIndicator.teamComponent = body.GetComponent<TeamComponent>();

            // remove all particle systems that we don't need
            Object.Destroy(modelBaseTransform.Find("Mesh").Find("Beam").gameObject);
            Object.Destroy(modelBaseTransform.Find("Swirls").gameObject);
            Object.Destroy(modelBaseTransform.Find("WarningRadius").gameObject);
            GlacialWardController glacialWardController = iceCrystal.AddComponent<GlacialWardController>();
            glacialWardController.buffWard = buffWard;
            iceCrystal.GetComponent<CharacterDeathBehavior>().deathState = new EntityStates.SerializableEntityStateType(typeof(GlacialWardDeath));

            // we need a spawncard in order to properly spawn this object (looks like some components don't get initialized unless you spawn the prefab through a spawncard)
            iceCrystalSpawnCard = ScriptableObject.CreateInstance<BodySpawnCard>();
            iceCrystalSpawnCard.name = "bscAspectAbilitiesIceCrystal";
            iceCrystalSpawnCard.directorCreditCost = 0;
            iceCrystalSpawnCard.forbiddenFlags = NodeFlags.None;
            iceCrystalSpawnCard.hullSize = HullClassification.Human;
            iceCrystalSpawnCard.nodeGraphType = MapNodeGroup.GraphType.Ground;
            iceCrystalSpawnCard.occupyPosition = true;
            iceCrystalSpawnCard.sendOverNetwork = true;
            iceCrystalSpawnCard.prefab = iceCrystal;

            iceCrystalExplosionEffect = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/TimeCrystalDeath"), "AspectAbilitiesIceCrystalDeath", false);
            iceCrystalExplosionEffect.GetComponent<EffectComponent>().soundName = "Play_mage_shift_wall_explode";
            Object.Destroy(iceCrystalExplosionEffect.GetComponent<ShakeEmitter>());
            iceCrystalExplosionEffect.transform.Find("Particles").Find("LongLifeNoiseTrails").gameObject.GetComponent<ParticleSystemRenderer>().material = LegacyResourcesAPI.Load<Material>("Materials/matIsFrozen");
            iceCrystalExplosionEffect.transform.Find("Particles").Find("Dash, Bright").gameObject.GetComponent<ParticleSystemRenderer>().material = LegacyResourcesAPI.Load<Material>("Materials/matIsFrozen");

            // set up projectile
            iceCrystalProjectile = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/Projectiles/SporeGrenadeProjectile"), "AspectAbilitiesIceCrystalProjectile", false);
            Object.Destroy(iceCrystalProjectile.GetComponent<ProjectileDamage>());
            Object.Destroy(iceCrystalProjectile.GetComponent<ProjectileImpactExplosion>());
            iceCrystalProjectile.AddComponent<ProjectileImpactEventCaller>();
            iceCrystalProjectile.AddComponent<GlacialProjectileTweaks>();
            ProjectileController projectileController = iceCrystalProjectile.GetComponent<ProjectileController>();
            projectileController.startSound = "Play_mage_m2_iceSpear_shoot";

            iceCrystalProjectileGhost = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/ProjectileGhosts/MageIceBombGhost"), "AspectAbilitiesIceCrystalProjectileGhost", false);
            //this is probably safe to remove, all it does is print errors in the console saying that the ghost is not instantiated by EffectManager.SpawnEffect
            Object.Destroy(iceCrystalProjectileGhost.GetComponent<EffectComponent>());
            Vector3 ghostLocalScale = iceCrystalProjectileGhost.transform.localScale;
            ghostLocalScale.z *= 0.3f;
            ghostLocalScale *= 1.5f;
            iceCrystalProjectileGhost.transform.localScale = ghostLocalScale;

            projectileController.ghostPrefab = iceCrystalProjectileGhost;

            // put an icicle aura on the crystal too
            var icicleAura = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/IcicleAura"), "AspectAbilitiesIceCrystalIcicleAura2", false);
            Object.Destroy(icicleAura.GetComponent<IcicleAuraController>());
            Object.Destroy(icicleAura.GetComponent<BuffWard>());
            Object.Destroy(icicleAura.GetComponent<TeamFilter>());
            Object.Destroy(icicleAura.GetComponent<NetworkIdentity>());
            icicleAura.name = "VisualAura";
            icicleAura.transform.SetParent(iceCrystal.transform.Find("ModelBase/Mesh"));
            icicleAura.transform.localPosition = Vector3.zero;
            icicleAura.transform.localScale = Vector3.one;

            var particleSystems = new List<ParticleSystem>();
            var particles = icicleAura.transform.Find("Particles");
            particleSystems.Add(particles.Find("Chunks").gameObject.GetComponent<ParticleSystem>());
            particleSystems.Add(particles.Find("Ring, Core").gameObject.GetComponent<ParticleSystem>());
            particleSystems.Add(particles.Find("Ring, Outer").gameObject.GetComponent<ParticleSystem>());
            particleSystems.Add(particles.Find("Ring, Procced").gameObject.GetComponent<ParticleSystem>());
            particleSystems.Add(particles.Find("SpinningSharpChunks").gameObject.GetComponent<ParticleSystem>());
            particleSystems.Add(particles.Find("Area").gameObject.GetComponent<ParticleSystem>());
            foreach (ParticleSystem particleSystem in particleSystems)
            {
                ParticleSystem.MainModule main = particleSystem.main;
                main.loop = true;
                main.playOnAwake = true;
            }

            glacialWardController.icicleAura = icicleAura;
            Object.Destroy(particles.Find("Chunks").gameObject);
            Object.Destroy(particles.Find("Ring, Core").gameObject);
            Object.Destroy(particles.Find("Ring, Procced").gameObject);

            AspectAbilitiesContent.Resources.bodyPrefabs.Add(iceCrystal);
            AspectAbilitiesContent.Resources.effectPrefabs.Add(iceCrystalExplosionEffect);
            AspectAbilitiesContent.Resources.projectilePrefabs.Add(iceCrystalProjectile);

            aspectAbility.onUseOverride = (self) =>
            {
                // spawn a health-reducing crystal ward
                Ray aimRay = self.InvokeMethod<Ray>("GetAimRay");

                bool fire = false;
                Vector3 finalPosition = Vector3.zero;
                RaycastHit raycastHit;
                if (Physics.Raycast(aimRay, out raycastHit, 1000f, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                {
                    finalPosition = raycastHit.point;
                    fire = true;
                }
                if (!self.characterBody.isPlayerControlled)
                {
                    if (self.characterBody.master)
                    {
                        RoR2.CharacterAI.BaseAI.Target target = AspectAbilitiesPlugin.GetAITarget(self.characterBody.master);
                        if (target != null && target.bestHurtBox)
                        {
                            finalPosition = target.bestHurtBox.transform.position + crystalRadius * Random.onUnitSphere;
                            fire = true;
                        }
                    }
                }
                if (fire)
                {
                    Vector3 distance = finalPosition - aimRay.origin;
                    Vector2 distance2 = new Vector2(distance.x, distance.z);
                    float magnitude = distance2.magnitude;
                    Vector2 vector2 = distance2 / magnitude;
                    float y = Trajectory.CalculateInitialYSpeed(flyTime, distance.y);
                    float num = magnitude / flyTime;
                    Vector3 direction = new Vector3(vector2.x * num, y, vector2.y * num);
                    magnitude = direction.magnitude;

                    Quaternion rotation = Util.QuaternionSafeLookRotation(direction);
                    ProjectileManager.instance.FireProjectile(
                        iceCrystalProjectile,
                        aimRay.origin,
                        rotation,
                        self.characterBody.gameObject,
                        0f,
                        0f,
                        false,
                        DamageColorIndex.Default,
                        null,
                        magnitude
                    );
                    return true;
                }
                return false;
            };
        }

        public class GlacialWardController : MonoBehaviour
        {
            public CharacterBody characterBody;
            public HealthComponent healthComponent;
            public ParticleSystem[] particleSystems;
            public bool effectOnDeath = true;
            public SphereSearch sphereSearch;
            public TeamMask teamMask;
            public BuffWard buffWard;

            public Vector3 rotation = Vector3.forward;
            public Vector3 rotationTarget = Vector3.forward;
            public Vector3 rotationVelocity = Vector3.zero;
            public float rotationTime = 0f;
            public float rotationTimeMax = 6f;

            public GameObject icicleAura;
            public float icicleAuraScale = 0f;
            public float icicleAuraScaleVelocity;
            public float icicleAuraGrowthTime = 1f;

            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
                healthComponent = GetComponent<HealthComponent>();
                rotationTime = rotationTimeMax;
                Util.PlaySound("Play_item_proc_icicle", gameObject);
            }

            public void Start()
            {
                GetComponentInChildren<TeamFilter>().teamIndex = GetComponent<TeamComponent>().teamIndex;
            }

            public void FixedUpdate()
            {
                float targetRadius = crystalRadius;
                if (radiusAffectedByHealth && healthComponent) targetRadius = crystalRadius * healthComponent.combinedHealthFraction;
                icicleAuraScale = Mathf.SmoothDamp(icicleAuraScale, targetRadius, ref icicleAuraScaleVelocity, icicleAuraGrowthTime);
                icicleAura.transform.localScale = Vector3.one * icicleAuraScale;
                rotationTime += Time.fixedDeltaTime;
                if (rotationTime >= rotationTimeMax)
                {
                    rotationTarget = Random.rotation.eulerAngles;
                    rotationTime = 0f;
                }
                rotation = new Vector3(
                    Mathf.SmoothDamp(rotation.x, rotationTarget.x, ref rotationVelocity.x, rotationTimeMax),
                    Mathf.SmoothDamp(rotation.y, rotationTarget.y, ref rotationVelocity.y, rotationTimeMax),
                    Mathf.SmoothDamp(rotation.z, rotationTarget.z, ref rotationVelocity.z, rotationTimeMax)
                );
                icicleAura.transform.localRotation = Quaternion.Euler(rotation);

                if (buffWard && NetworkServer.active)
                {
                    buffWard.Networkradius = icicleAuraScale;
                }
            }

            public void OnDestroy()
            {
                Util.PlaySound("Stop_item_proc_icicle", gameObject);
            }
        }

        public class GlacialWardDeath : EntityStates.BaseState
        {
            public override void OnEnter()
            {
                base.OnEnter();
                if (modelLocator)
                {
                    if (modelLocator.modelBaseTransform)
                    {
                        Destroy(modelLocator.modelBaseTransform.gameObject);
                    }
                    if (modelLocator.modelTransform)
                    {
                        Destroy(modelLocator.modelTransform.gameObject);
                    }
                }
                if (iceCrystalExplosionEffect && NetworkServer.active)
                {
                    EffectManager.SpawnEffect(iceCrystalExplosionEffect, new EffectData
                    {
                        origin = base.transform.position,
                        scale = 6f,
                        rotation = Quaternion.identity
                    }, true);
                }
                Destroy(gameObject);
            }

            public override EntityStates.InterruptPriority GetMinimumInterruptPriority()
            {
                return EntityStates.InterruptPriority.Death;
            }
        }

        private class GlacialProjectileTweaks : MonoBehaviour
        {
            Rigidbody rigidbody;
            TeamIndex teamIndex;

            private void Start()
            {
                ProjectileController controller = GetComponent<ProjectileController>();
                ProjectileImpactEventCaller impactEventCaller = GetComponent<ProjectileImpactEventCaller>();
                CharacterBody ownerBody = controller.owner.GetComponent<CharacterBody>();
                rigidbody = GetComponent<Rigidbody>();
                teamIndex = ownerBody.teamComponent.teamIndex;

                impactEventCaller.impactEvent.AddListener((ProjectileImpactInfo impactInfo) =>
                {
                    // don't collide with entities, only with the world
                    if (!impactInfo.collider.GetComponent<HurtBox>())
                    {
                        GameObject crystal = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(iceCrystalSpawnCard, new DirectorPlacementRule { placementMode = DirectorPlacementRule.PlacementMode.Direct, position = impactInfo.estimatedPointOfImpact }, RoR2Application.rng));
                        if (crystal)
                        {
                            EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/ImpactEffects/IceRingExplosion"), new EffectData { origin = crystal.transform.position, scale = 35f, rotation = Quaternion.Euler(impactInfo.estimatedImpactNormal) }, true);
                            Util.PlaySound("Play_mage_m2_iceSpear_impact", crystal);
                            crystal.transform.up = impactInfo.estimatedImpactNormal;
                            crystal.GetComponent<TeamComponent>().teamIndex = teamIndex;
                        }
                        Object.Destroy(gameObject);
                    }
                });
            }

            private void FixedUpdate()
            {
                if (rigidbody.velocity != Vector3.zero) rigidbody.rotation = Util.QuaternionSafeLookRotation(rigidbody.velocity);
            }
        }
    }
}
