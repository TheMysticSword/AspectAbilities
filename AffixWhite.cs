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
        private static List<CharacterBody> iceCrystalInstances = new List<CharacterBody>();
        
        public static float flyTime = 2f;
        public static int maxCrystals = 3;

        public static GameObject iceShockwave;
        public static AnimationCurve iceShockwaveCurve;
        public static float iceShockwaveDuration = 0.5f;

        public override void OnPluginAwake()
        {
            iceCrystal = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/TimeCrystalBody"), "AspectAbilitiesIceCrystalBody");
            iceCrystalProjectile = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/Projectiles/SporeGrenadeProjectile"), "AspectAbilitiesIceCrystalProjectile");
        }

        public override void OnLoad()
        {
            On.RoR2.EquipmentCatalog.Init += (orig) =>
            {
                orig();
                aspectAbility.equipmentDef = RoR2Content.Equipment.AffixWhite;
                aspectAbility.equipmentDef.cooldown = 45f;
                LanguageManager.appendTokens.Add(aspectAbility.equipmentDef.pickupToken);
                AspectAbilitiesPlugin.registeredAspectAbilities.Add(aspectAbility);
            };

            AspectAbilitiesContent.Resources.entityStateTypes.Add(typeof(GlacialWardDeath));

            // create glacial ward prefab
            MeshCollider meshCollider = iceCrystal.AddComponent<MeshCollider>();
            meshCollider.gameObject.layer = LayerIndex.defaultLayer.intVal;
            meshCollider.sharedMesh = iceCrystal.transform.Find("ModelBase").Find("Mesh").gameObject.GetComponent<MeshFilter>().sharedMesh;
            CharacterBody body = iceCrystal.GetComponent<CharacterBody>();
            Transform modelBaseTransform = iceCrystal.GetComponent<ModelLocator>().modelBaseTransform;
            CharacterModel model = modelBaseTransform.Find("Mesh").gameObject.GetComponent<CharacterModel>();
            model.body = body;
            body.baseNameToken = "ASPECTABILITIES_ICECRYSTAL_BODY_NAME";
            body.portraitIcon = Resources.Load<Texture>("Textures/MiscIcons/texMysteryIcon");
            body.bodyFlags = iceCrystal.GetComponent<CharacterBody>().bodyFlags | CharacterBody.BodyFlags.ImmuneToExecutes | CharacterBody.BodyFlags.HasBackstabImmunity;
            body.baseMaxHealth = 110f;
            body.levelMaxHealth = 33f;
            // replace the pink time crystal material with an ice material
            CharacterModel.RendererInfo[] rendererInfos = model.baseRendererInfos;
            for (int i = 0; i < rendererInfos.Length; i++)
            {
                rendererInfos[i].defaultMaterial = Object.Instantiate(Resources.Load<Material>("Materials/matIsFrozen"));
            }
            CharacterModel.LightInfo[] lightInfos = model.baseLightInfos;
            for (int i = 0; i < lightInfos.Length; i++)
            {
                lightInfos[i].defaultColor = iceCrystalColor;
            }

            // team area indicator
            GameObject teamIndicator = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/Projectiles/PoisonStakeProjectile").transform.Find("ActiveVisuals/TeamAreaIndicator, FullSphere").gameObject, "AspectAbilitiesIceCrystalTeamIndicator", false);
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

            iceCrystalExplosionEffect = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/Effects/TimeCrystalDeath"), "AspectAbilitiesIceCrystalDeath", false);
            iceCrystalExplosionEffect.GetComponent<EffectComponent>().soundName = "Play_mage_shift_wall_explode";
            Object.Destroy(iceCrystalExplosionEffect.GetComponent<ShakeEmitter>());
            iceCrystalExplosionEffect.transform.Find("Particles").Find("LongLifeNoiseTrails").gameObject.GetComponent<ParticleSystemRenderer>().material = Resources.Load<Material>("Materials/matIsFrozen");
            iceCrystalExplosionEffect.transform.Find("Particles").Find("Dash, Bright").gameObject.GetComponent<ParticleSystemRenderer>().material = Resources.Load<Material>("Materials/matIsFrozen");

            Object.Destroy(iceCrystalProjectile.GetComponent<ProjectileDamage>());
            Object.Destroy(iceCrystalProjectile.GetComponent<ProjectileImpactExplosion>());
            iceCrystalProjectile.AddComponent<ProjectileImpactEventCaller>();
            iceCrystalProjectile.AddComponent<GlacialProjectileTweaks>();
            ProjectileController projectileController = iceCrystalProjectile.GetComponent<ProjectileController>();
            projectileController.startSound = "Play_mage_m2_iceSpear_shoot";

            iceCrystalProjectileGhost = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/ProjectileGhosts/MageIceBombGhost"), "AspectAbilitiesIceCrystalProjectileGhost", false);
            //this is probably safe to remove, all it does is print errors in the console saying that the ghost is not instantiated by EffectManager.SpawnEffect
            Object.Destroy(iceCrystalProjectileGhost.GetComponent<EffectComponent>());
            Vector3 ghostLocalScale = iceCrystalProjectileGhost.transform.localScale;
            ghostLocalScale.z *= 0.3f;
            ghostLocalScale *= 1.5f;
            iceCrystalProjectileGhost.transform.localScale = ghostLocalScale;

            projectileController.ghostPrefab = iceCrystalProjectileGhost;

            // set up ice shockwave
            iceShockwave = PrefabAPI.InstantiateClone(new GameObject("iceshockwave"), AspectAbilitiesPlugin.TokenPrefix + "IceShockwave", false);

            iceShockwaveCurve = new AnimationCurve
            {
                keys = new Keyframe[]
                {
                    new Keyframe(0f, 0f),
                    new Keyframe(1f, 1f)
                },
                preWrapMode = WrapMode.Clamp,
                postWrapMode = WrapMode.Clamp
            };
            for (var i = 0; i < iceShockwaveCurve.keys.Length; i++) iceShockwaveCurve.SmoothTangents(i, 0f);

            iceShockwave.AddComponent<DestroyOnTimer>().duration = iceShockwaveDuration;

            EffectComponent effectComponent = iceShockwave.AddComponent<EffectComponent>();
            effectComponent.applyScale = true;
            effectComponent.soundName = "Play_item_proc_iceRingSpear";
            VFXAttributes vfxAttributes = iceShockwave.AddComponent<VFXAttributes>();
            vfxAttributes.vfxIntensity = VFXAttributes.VFXIntensity.High;
            vfxAttributes.vfxPriority = VFXAttributes.VFXPriority.Always;

            GameObject icicleAura = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/NetworkedObjects/IcicleAura"), "AspectAbilitiesIceCrystalIcicleAura", false);
            Object.Destroy(icicleAura.GetComponent<IcicleAuraController>());
            Object.Destroy(icicleAura.GetComponent<BuffWard>());
            Object.Destroy(icicleAura.GetComponent<TeamFilter>());
            Object.Destroy(icicleAura.GetComponent<NetworkIdentity>());
            icicleAura.name = "VisualAura";
            icicleAura.transform.SetParent(iceShockwave.transform);
            icicleAura.transform.localPosition = Vector3.zero;
            icicleAura.transform.localScale = Vector3.one;

            ObjectScaleCurve objectScaleCurve = icicleAura.AddComponent<ObjectScaleCurve>();
            objectScaleCurve.overallCurve = iceShockwaveCurve;
            objectScaleCurve.useOverallCurveOnly = true;
            objectScaleCurve.timeMax = iceShockwaveDuration;

            List<ParticleSystem> particleSystems = new List<ParticleSystem>();
            Transform particles = icicleAura.transform.Find("Particles");
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

            // put an icicle aura on the crystal too
            icicleAura = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/NetworkedObjects/IcicleAura"), "AspectAbilitiesIceCrystalIcicleAura2", false);
            Object.Destroy(icicleAura.GetComponent<IcicleAuraController>());
            Object.Destroy(icicleAura.GetComponent<BuffWard>());
            Object.Destroy(icicleAura.GetComponent<TeamFilter>());
            Object.Destroy(icicleAura.GetComponent<NetworkIdentity>());
            icicleAura.name = "VisualAura";
            icicleAura.transform.SetParent(iceCrystal.transform);
            icicleAura.transform.localPosition = Vector3.zero;
            icicleAura.transform.localScale = Vector3.one;

            particleSystems.Clear();
            particles = icicleAura.transform.Find("Particles");
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
            AspectAbilitiesContent.Resources.effectPrefabs.Add(iceShockwave);
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
                            finalPosition = target.bestHurtBox.transform.position;
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
            public ParticleSystem[] particleSystems;
            public bool effectOnDeath = true;
            public float stopwatch = 0f;
            public float shockwaveStopwatch = 0f;
            public float shockwaveStopwatchMax = 10f;
            public float shockwaveRadius = 60f;
            public List<CharacterBody> shockwavedBodies;
            public float shockwaveFireTime = 0f;
            public bool shockwaving = false;
            public SphereSearch sphereSearch;
            public TeamMask teamMask;

            public Vector3 rotation = Vector3.forward;
            public Vector3 rotationTarget = Vector3.forward;
            public Vector3 rotationVelocity = Vector3.zero;
            public float rotationTime = 0f;
            public float rotationTimeMax = 6f;

            public GameObject icicleAura;
            public float icicleAuraScale;
            public float icicleAuraScaleVelocity;

            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
                shockwavedBodies = new List<CharacterBody>();
                rotationTime = rotationTimeMax;
                Util.PlaySound("Play_item_proc_icicle", gameObject);
            }

            public void Start()
            {
                List<CharacterBody> instances = new List<CharacterBody>(iceCrystalInstances);
                if (instances.Count > 0)
                {
                    instances.RemoveAll(inst => inst.teamComponent.teamIndex != characterBody.teamComponent.teamIndex);
                    while (instances.Count > maxCrystals)
                    {
                        instances.OrderBy(inst => inst.gameObject.GetComponent<GlacialWardController>().stopwatch);
                        CharacterBody oldest = instances.First();
                        if (oldest)
                        {
                            oldest.gameObject.GetComponent<GlacialWardController>().effectOnDeath = false;
                            if (NetworkServer.active && oldest.healthComponent) oldest.healthComponent.Suicide();
                            instances.Remove(oldest);
                        }
                    }
                }
            }

            public void FixedUpdate()
            {
                stopwatch += Time.fixedDeltaTime;
                shockwaveStopwatch += Time.fixedDeltaTime;

                if (shockwaveStopwatch >= shockwaveStopwatchMax)
                {
                    shockwaveStopwatch = 0f;
                    if (NetworkServer.active)
                    {
                        EffectManager.SpawnEffect(iceShockwave, new EffectData
                        {
                            origin = transform.position,
                            scale = shockwaveRadius,
                            rotation = Quaternion.Euler(rotation)
                        }, true);
                        shockwaving = true;
                        shockwaveFireTime = 0f;
                        sphereSearch = new SphereSearch
                        {
                            mask = LayerIndex.entityPrecise.mask,
                            origin = transform.position,
                            queryTriggerInteraction = QueryTriggerInteraction.Collide,
                            radius = shockwaveRadius
                        };
                        teamMask = TeamMask.AllExcept(TeamComponent.GetObjectTeam(characterBody.gameObject));
                        shockwavedBodies.Clear();
                    }
                }

                if (shockwaving && NetworkServer.active)
                {
                    shockwaveFireTime += Time.fixedDeltaTime;
                    float t = shockwaveFireTime / iceShockwaveDuration;

                    sphereSearch.radius = shockwaveRadius * iceShockwaveCurve.Evaluate(t);
                    foreach (HurtBox hurtBox in sphereSearch.RefreshCandidates().FilterCandidatesByHurtBoxTeam(teamMask).FilterCandidatesByDistinctHurtBoxEntities().GetHurtBoxes())
                    {
                        if (hurtBox.healthComponent && hurtBox.healthComponent.body && !shockwavedBodies.Contains(hurtBox.healthComponent.body))
                        {
                            shockwavedBodies.Add(hurtBox.healthComponent.body);
                            hurtBox.healthComponent.body.AddTimedBuff(AspectAbilitiesContent.Buffs.IceCrystalDebuff, 12f);
                        }
                    }

                    if (t >= 1f)
                    {
                        shockwaving = false;
                        shockwaveFireTime = 0f;
                    }
                }

                icicleAuraScale = Mathf.SmoothDamp(icicleAuraScale, shockwaveRadius, ref icicleAuraScaleVelocity, 1f);
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
            }

            public void OnEnable()
            {
                iceCrystalInstances.Add(characterBody);
            }

            public void OnDisable()
            {
                iceCrystalInstances.Remove(characterBody);
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
                Explode();
            }

            public override void FixedUpdate()
            {
                base.FixedUpdate();
                stopwatch += Time.fixedDeltaTime;
            }

            private void Explode()
            {
                if (modelLocator)
                {
                    if (modelLocator.modelBaseTransform)
                    {
                        Destroy(modelLocator.modelBaseTransform.gameObject);
                    }
                    if (base.modelLocator.modelTransform)
                    {
                        Destroy(base.modelLocator.modelTransform.gameObject);
                    }
                }
                if (base.gameObject.GetComponent<GlacialWardController>().effectOnDeath && explosionEffectPrefab && UnityEngine.Networking.NetworkServer.active)
                {
                    EffectManager.SpawnEffect(explosionEffectPrefab, new EffectData
                    {
                        origin = base.transform.position,
                        scale = explosionRadius,
                        rotation = Quaternion.identity
                    }, true);
                }
                Destroy(base.gameObject);
            }

            public override EntityStates.InterruptPriority GetMinimumInterruptPriority()
            {
                return EntityStates.InterruptPriority.Death;
            }

            public static GameObject explosionEffectPrefab = iceCrystalExplosionEffect;
            public static float explosionRadius = EntityStates.Destructible.TimeCrystalDeath.explosionRadius;
            private float stopwatch;
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
                            EffectManager.SpawnEffect(Resources.Load<GameObject>("Prefabs/Effects/ImpactEffects/IceRingExplosion"), new EffectData { origin = crystal.transform.position, scale = 35f, rotation = Quaternion.Euler(impactInfo.estimatedImpactNormal) }, true);
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
