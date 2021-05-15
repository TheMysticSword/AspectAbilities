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
using HG;

namespace TheMysticSword.AspectAbilities
{
    public class AffixWhite : BaseAspectAbility
    {
        public static GameObject iceCrystal;
        public static GameObject iceCrystalProjectile;
        public static GameObject iceCrystalProjectileGhost;
        public static SpawnCard iceCrystalSpawnCard;
        public static GameObject iceCrystalExplosionEffect;
        public static Color iceCrystalColor = new Color(209f / 255f, 236f / 255f, 236f / 255f);
        public static float cursePenaltyStackFrequency = 0.2f;
        public static float cursePenaltyPerStack = (15f / 100f) * cursePenaltyStackFrequency; // 15% health reduction per second
        private static List<CharacterBody> iceCrystalInstances = new List<CharacterBody>();

        public static float defaultRadius = 60f;
        public static float defaultGrowTime = 6f;
        public static float flyTime = 2f;
        public static int maxCrystals = 3;

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
                equipmentDef = RoR2Content.Equipment.AffixWhite;
                equipmentDef.cooldown = 45f;
                LanguageManager.appendTokens.Add(equipmentDef.pickupToken);
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
            // remove all particle systems that we don't need
            Object.Destroy(modelBaseTransform.Find("Mesh").Find("Beam").gameObject);
            Object.Destroy(modelBaseTransform.Find("Swirls").gameObject);
            Object.Destroy(modelBaseTransform.Find("WarningRadius").gameObject);
            // we will attach the ice relic snowstorm aura effect to this
            GameObject icicleAura = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/NetworkedObjects/IcicleAura"), "AspectAbilitiesIceCrystalIcicleAura", false);
            Object.Destroy(icicleAura.GetComponent<IcicleAuraController>());
            Object.Destroy(icicleAura.GetComponent<BuffWard>());
            Object.Destroy(icicleAura.GetComponent<TeamFilter>());
            Object.Destroy(icicleAura.GetComponent<NetworkIdentity>());
            icicleAura.name = "VisualAura";
            icicleAura.transform.SetParent(iceCrystal.transform);
            icicleAura.transform.localPosition = Vector3.zero;
            iceCrystal.AddComponent<GlacialWardController>();
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
            iceCrystalExplosionEffect.GetComponent<EffectComponent>().soundName = "Play_item_proc_iceRingSpear";
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

            AspectAbilitiesContent.Resources.bodyPrefabs.Add(iceCrystal);
            AspectAbilitiesContent.Resources.effectPrefabs.Add(iceCrystalExplosionEffect);
            AspectAbilitiesContent.Resources.projectilePrefabs.Add(iceCrystalProjectile);
        }

        public override bool OnUse(EquipmentSlot self)
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
        }

        public class GlacialWardController : MonoBehaviour
        {
            public CharacterBody characterBody;
            public GameObject visualAura;
            public ParticleSystem[] particleSystems;
            public float radius = 0f;
            public float maxRadius = defaultRadius;
            public float growTime = defaultGrowTime;
            public float growVelocity = 0f;
            public Vector3 rotation = Vector3.forward;
            public Vector3 rotationTarget = Vector3.forward;
            public Vector3 rotationVelocity = Vector3.zero;
            public float rotationTime = 0f;
            public float rotationTimeMax = 6f;
            public bool effectOnDeath = true;
            public float stopwatch = 0f;

            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
                visualAura = transform.Find("VisualAura").gameObject;
                Transform particles = visualAura.transform.Find("Particles");
                // ArrayUtils.ArrayAppend(ref particleSystems, particles.Find("Chunks").gameObject.GetComponent<ParticleSystem>());
                // ArrayUtils.ArrayAppend(ref particleSystems, particles.Find("Ring, Core").gameObject.GetComponent<ParticleSystem>());
                ArrayUtils.ArrayAppend(ref particleSystems, particles.Find("Ring, Outer").gameObject.GetComponent<ParticleSystem>());
                // ArrayUtils.ArrayAppend(ref particleSystems, particles.Find("Ring, Procced").gameObject.GetComponent<ParticleSystem>());
                ArrayUtils.ArrayAppend(ref particleSystems, particles.Find("SpinningSharpChunks").gameObject.GetComponent<ParticleSystem>());
                ArrayUtils.ArrayAppend(ref particleSystems, particles.Find("Area").gameObject.GetComponent<ParticleSystem>());
                Util.PlaySound("Play_item_proc_icicle", gameObject);
                foreach (ParticleSystem particleSystem in particleSystems)
                {
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.loop = true;
                    particleSystem.Play();
                }
                rotationTime = rotationTimeMax;
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
                            if (NetworkServer.active) oldest.healthComponent.Suicide();
                            instances.Remove(oldest);
                        }
                    }
                }
            }

            public void FixedUpdate()
            {
                stopwatch += Time.fixedDeltaTime;

                radius = Mathf.SmoothDamp(radius, maxRadius, ref growVelocity, growTime);

                if (NetworkServer.active)
                {
                    float radiusSqr = Mathf.Pow(radius, 2);
                    for (TeamIndex teamIndex = 0; teamIndex < TeamIndex.Count; teamIndex++)
                    {
                        if (teamIndex != characterBody.teamComponent.teamIndex)
                        {
                            foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(teamIndex))
                            {
                                if ((teamComponent.transform.position - transform.position).sqrMagnitude <= radiusSqr)
                                {
                                    CharacterBody body2 = teamComponent.GetComponent<CharacterBody>();
                                    if (body2)
                                    {
                                        body2.GetComponent<Buffs.IceCrystalDebuff.CurseCount>().Stack(cursePenaltyPerStack, cursePenaltyStackFrequency);
                                    }
                                }
                            }
                        }
                    }
                }

                visualAura.transform.localScale = Vector3.one * radius;
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
                visualAura.transform.localRotation = Quaternion.Euler(rotation);
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

            public GlacialWardDeath()
            {
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
