using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Navigation;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System.Collections;
using System.Collections.Generic;
using HG;

namespace TheMysticSword.AspectAbilities
{
    public static class AffixWhite
    {
        public static GameObject iceCrystal;
        public static SpawnCard iceCrystalSpawnCard;
        public static GameObject iceCrystalExplosionEffect;
        public static Color iceCrystalColor = new Color(209f / 255f, 236f / 255f, 236f / 255f);
        public static BuffIndex iceCrystalDebuff;
        public static float cursePenaltyStackFrequency = 0.2f;
        public static float cursePenaltyPerStack = (4f / 100f) * cursePenaltyStackFrequency;
        private static List<CharacterBody> iceCrystalInstances = new List<CharacterBody>();

        public static void Init()
        {
            iceCrystalDebuff = BuffAPI.Add(new CustomBuff(new BuffDef
            {
                buffColor = iceCrystalColor,
                canStack = true,
                iconPath = "Textures/BuffIcons/texBuffPulverizeIcon",
                isDebuff = true,
                name = "AspectAbilitiesIceCrystalDebuff"
            }));

            NetworkingAPI.RegisterMessageType<CurseCount.SyncStack>();

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<CurseCount>();
            };

            // actual debuff logic. we need to inject into RecalculateStats - the function that defines all character stats and considers all items and buffs
            IL.RoR2.CharacterBody.RecalculateStats += (il) =>
            {
                ILCursor c = new ILCursor(il);
                // increase curse penalty
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchLdcR4(1),
                    x => x.MatchCallvirt<CharacterBody>("set_cursePenalty")
                );
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<System.Action<CharacterBody>>((characterBody) =>
                {
                    CurseCount curseCount = characterBody.GetComponent<CurseCount>();
                    if (curseCount.current > 0)
                    {
                        characterBody.InvokeMethod("set_cursePenalty", characterBody.cursePenalty + curseCount.current);
                    }
                });
                // don't regen health when this curse penalty is removed
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCallvirt<CharacterBody>("get_maxHealth"),
                    x => x.MatchLdloc(39),
                    x => x.MatchSub(),
                    x => x.MatchStloc(70)
                );
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, 41);
                c.Emit(OpCodes.Ldloc, 70);
                c.EmitDelegate<System.Func<CharacterBody, float, float, float>>((characterBody, trueMaxHealth, maxHealthDelta) =>
                {
                    CurseCount curseCount = characterBody.GetComponent<CurseCount>();
                    float healthGained = (trueMaxHealth / (1f + curseCount.current)) - (trueMaxHealth / (1f + curseCount.last));
                    if (healthGained > 0)
                    {
                        maxHealthDelta -= healthGained;
                    }
                    else if (healthGained < 0)
                    {
                        float takeDamage = -healthGained;
                        if (takeDamage > characterBody.healthComponent.Networkhealth)
                        {
                            takeDamage = characterBody.healthComponent.Networkhealth - 1f;
                        }
                        if (takeDamage > 0) characterBody.healthComponent.Networkhealth -= takeDamage;
                    }
                    return maxHealthDelta;
                });
                c.Emit(OpCodes.Stloc, 70);
                // do the same with shields
                c.GotoNext(
                    MoveType.After,
                    x => x.MatchLdarg(0),
                    x => x.MatchCallvirt<CharacterBody>("get_maxShield"),
                    x => x.MatchLdloc(40),
                    x => x.MatchSub(),
                    x => x.MatchStloc(71)
                );
                c.Emit(OpCodes.Ldarg_0);
                c.Emit(OpCodes.Ldloc, 43);
                c.Emit(OpCodes.Ldloc, 71);
                c.EmitDelegate<System.Func<CharacterBody, float, float, float>>((characterBody, trueMaxHealth, maxHealthDelta) =>
                {
                    CurseCount curseCount = characterBody.GetComponent<CurseCount>();
                    float healthGained = (trueMaxHealth / (1f + curseCount.current)) - (trueMaxHealth / (1f + curseCount.last));
                    if (healthGained > 0)
                    {
                        maxHealthDelta -= healthGained;
                    }
                    else if (healthGained < 0)
                    {
                        float takeDamage = -healthGained;
                        if (takeDamage > characterBody.healthComponent.Networkshield)
                        {
                            takeDamage = characterBody.healthComponent.Networkshield - 1f;
                        }
                        if (takeDamage > 0) characterBody.healthComponent.Networkshield -= takeDamage;
                    }
                    return maxHealthDelta;
                });
                c.Emit(OpCodes.Stloc, 71);
            };
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                orig(self);
                CurseCount curseCount = self.GetComponent<CurseCount>();
                curseCount.last = curseCount.current;
            };

            // create glacial ward prefab
            iceCrystal = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/TimeCrystalBody"), "AspectAbilitiesIceCrystalBody");
            MeshCollider meshCollider = iceCrystal.AddComponent<MeshCollider>();
            meshCollider.gameObject.layer = LayerIndex.defaultLayer.intVal;
            meshCollider.sharedMesh = iceCrystal.transform.Find("ModelBase").Find("Mesh").gameObject.GetComponent<MeshFilter>().sharedMesh;
            CharacterBody body = iceCrystal.GetComponent<CharacterBody>();
            Transform modelBaseTransform = iceCrystal.GetComponent<ModelLocator>().modelBaseTransform;
            CharacterModel model = modelBaseTransform.Find("Mesh").gameObject.GetComponent<CharacterModel>();
            model.body = body;
            body.baseNameToken = "UNIDENTIFIED_KILLER_NAME";
            body.portraitIcon = Resources.Load<Texture>("Textures/BodyIcons/texUnidentifiedKillerIcon");
            body.bodyFlags = iceCrystal.GetComponent<CharacterBody>().bodyFlags | CharacterBody.BodyFlags.ImmuneToExecutes;
            body.baseMaxHealth = 200f;
            body.levelMaxHealth = 60f;
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
            GameObject icicleAura = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/NetworkedObjects/IcicleAura"), "AspectAbilitiesIceCrystalIcicleAura");
            Object.Destroy(icicleAura.GetComponent<IcicleAuraController>());
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

            On.RoR2.BodyCatalog.Init += (orig) =>
            {
                orig();
                AspectAbilities.Assets.RegisterBody(iceCrystal);
            };
            On.RoR2.EffectCatalog.Init += (orig) =>
            {
                orig();
                AspectAbilities.Assets.RegisterEffect(iceCrystalExplosionEffect);
            };

            AspectAbilities.RegisterAspectAbility(EquipmentIndex.AffixWhite, 45f,
                (self) =>
                {
                    // spawn a health-reducing crystal ward
                    float maxDistance = 1000f;
                    Ray aimRay = self.InvokeMethod<Ray>("GetAimRay");

                    if (!self.characterBody.isPlayerControlled)
                    {
                        // ai tweaks:
                        // don't cast this from across the map
                        maxDistance = 60f;
                        // always spawn ward near the target
                        BullseyeSearch bullseyeSearch = new BullseyeSearch();
                        bullseyeSearch.searchOrigin = aimRay.origin;
                        bullseyeSearch.searchDirection = aimRay.direction;
                        bullseyeSearch.maxDistanceFilter = maxDistance;
                        bullseyeSearch.teamMaskFilter = TeamMask.allButNeutral;
                        bullseyeSearch.filterByLoS = false;
                        bullseyeSearch.teamMaskFilter.RemoveTeam(TeamComponent.GetObjectTeam(self.gameObject));
                        bullseyeSearch.sortMode = BullseyeSearch.SortMode.Angle;
                        bullseyeSearch.RefreshCandidates();
                        List<HurtBox> hurtBoxes = bullseyeSearch.GetResults().ToList();
                        hurtBoxes.RemoveAll(x => x.healthComponent.body.isFlying);
                        // choose the healthiest target and prioritize ground targets
                        hurtBoxes.OrderBy(hb => hb.healthComponent.fullCombinedHealth * (hb.healthComponent.body.isFlying ? 0.25f : 1f));
                        HurtBox hurtBox = hurtBoxes.LastOrDefault();
                        if (hurtBox)
                        {
                            // don't cast this if the target is too far away
                            if (Vector3.Distance(hurtBox.transform.position, self.characterBody.corePosition) > maxDistance) return false;

                            // spawn somewhere in front of the target so that they notice it
                            float angleVariation = 35f;
                            float angle = (Util.QuaternionSafeLookRotation(hurtBox.healthComponent.body.inputBank.aimDirection).eulerAngles.y + Random.Range(-angleVariation, angleVariation)) * Mathf.Deg2Rad;
                            Vector3 offset = (GlacialWardController.defaultRadius / 2f + hurtBox.healthComponent.body.radius) * new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                            Vector3 finalPosition = hurtBox.transform.position + offset;

                            // choose the closest ground node to the chosen position to prevent spawning in walls and in the air
                            NodeGraph nodes = SceneInfo.instance.groundNodes;
                            NodeGraph.NodeIndex nodeIndex = nodes.FindClosestNode(finalPosition, hurtBox.healthComponent.body.hullClassification);
                            nodes.GetNodePosition(nodeIndex, out finalPosition);

                            // reposition the aim ray and change distance in such a way that it's aimed at the ground
                            aimRay.origin = finalPosition + Vector3.up * 0.5f;
                            aimRay.direction = Vector3.down;
                        }
                        else
                        {
                            // if no targets are found, don't cast
                            return false;
                        }
                    }

                    RaycastHit raycastHit;
                    if (Physics.Raycast(aimRay, out raycastHit, maxDistance, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    {
                        GameObject crystal = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(iceCrystalSpawnCard, new DirectorPlacementRule { placementMode = DirectorPlacementRule.PlacementMode.Direct, position = raycastHit.point }, RoR2Application.rng));
                        if (crystal)
                        {
                            EffectManager.SpawnEffect(Resources.Load<GameObject>("Prefabs/Effects/ImpactEffects/IceRingExplosion"), new EffectData { origin = crystal.transform.position, scale = 35f, rotation = Quaternion.Euler(raycastHit.normal) }, true);
                            Util.PlaySound("Play_mage_m2_iceSpear_impact", crystal);
                            crystal.transform.up = raycastHit.normal;
                            crystal.GetComponent<TeamComponent>().teamIndex = self.characterBody.teamComponent.teamIndex;
                            crystal.GetComponent<GlacialWardController>().master = self.characterBody.master;
                            AspectAbilities.BodyFields bodyFields = crystal.GetComponent<AspectAbilities.BodyFields>();
                            if (bodyFields)
                            {
                                bodyFields.multiplierOnHitProcsOnSelf -= 1f;
                                bodyFields.multiplierOnDeathProcsOnSelf -= 1f;
                            }
                        }
                        return true;
                    }
                    return false;
                });
        }

        public class GlacialWardController : MonoBehaviour
        {
            public CharacterBody characterBody;
            public CharacterMaster master;
            public GameObject visualAura;
            public ParticleSystem[] particleSystems;
            public static float defaultRadius = 16f;
            public float radius = defaultRadius;
            public float scaleVelocity = 0f;
            public Vector3 rotation = Vector3.forward;
            public Vector3 rotationTarget = Vector3.forward;
            public Vector3 rotationVelocity = Vector3.zero;
            public float rotationTime = 0f;
            public float rotationTimeMax = 6f;
            public bool effectOnDeath = true;
            public float stopwatch = 0f;
            public float curseTimer = 0f;
            public float curseTimerMax = cursePenaltyStackFrequency;

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
                    if (instances.Count > 5)
                    {
                        instances.OrderBy(inst => inst.gameObject.GetComponent<GlacialWardController>().stopwatch);
                        CharacterBody oldest = instances.First();
                        if (oldest)
                        {
                            oldest.gameObject.GetComponent<GlacialWardController>().effectOnDeath = false;
                            oldest.healthComponent.Suicide();
                        }
                    }
                }
            }

            public void FixedUpdate()
            {
                stopwatch += Time.fixedDeltaTime;

                if (NetworkServer.active)
                {
                    curseTimer += Time.fixedDeltaTime;
                    if (curseTimer >= curseTimerMax)
                    {
                        curseTimer = 0f;
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
                                            body2.GetComponent<CurseCount>().Stack(cursePenaltyPerStack, cursePenaltyStackFrequency);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                visualAura.transform.localScale = Vector3.one * Mathf.SmoothDamp(visualAura.transform.localScale.x, radius, ref scaleVelocity, 0.5f);
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

        public class CurseCount : NetworkBehaviour
        {
            public CharacterBody characterBody;
            public float current = 0f;
            public float last = 0f;
            public float decayTotal = 0f;
            public float decayTime = 3f;
            public float noDecayTime = 0f;
            public float decayStatsUpdateTime = 0f;
            public float decayStatsUpdateTimeMax = cursePenaltyStackFrequency;

            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
            }

            public void Stack(float count, float noDecayTime)
            {
                current += count;
                this.noDecayTime = noDecayTime + 0.1f;
                decayTotal = current;
                characterBody.SetFieldValue("outOfDangerStopwatch", 0f);
                characterBody.SetFieldValue("statsDirty", true);

                if (NetworkServer.active)
                {
                    while (characterBody.GetBuffCount(iceCrystalDebuff) < decayTime)
                    {
                        characterBody.AddBuff(iceCrystalDebuff);
                    }
                    new SyncStack(gameObject.GetComponent<NetworkIdentity>().netId, count, noDecayTime).Send(NetworkDestination.Clients);
                }
            }

            public class SyncStack : INetMessage
            {
                NetworkInstanceId objID;
                float count;
                float noDecayTime;

                public SyncStack()
                {
                }

                public SyncStack(NetworkInstanceId objID, float count, float noDecayTime)
                {
                    this.objID = objID;
                    this.count = count;
                    this.noDecayTime = noDecayTime;
                }

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                    count = reader.ReadSingle();
                    noDecayTime = reader.ReadSingle();
                }

                public void OnReceived()
                {
                    if (NetworkServer.active) return;
                    GameObject obj = Util.FindNetworkObject(objID);
                    if (!obj) return;
                    CurseCount curseCount = obj.GetComponent<CurseCount>();
                    curseCount.Stack(count, noDecayTime);
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                    writer.Write(count);
                    writer.Write(noDecayTime);
                }
            }

            public void FixedUpdate()
            {
                noDecayTime -= Time.fixedDeltaTime;
                if (noDecayTime <= 0 && current > 0f)
                {
                    current -= (decayTotal / decayTime) * Time.fixedDeltaTime;
                    decayStatsUpdateTime += Time.fixedDeltaTime;
                    if (decayStatsUpdateTime >= decayStatsUpdateTimeMax)
                    {
                        characterBody.SetFieldValue("statsDirty", true);
                        decayStatsUpdateTime = 0f;
                    }
                    if (current < 0f)
                    {
                        characterBody.SetFieldValue("statsDirty", true);
                        current = 0f;
                        if (NetworkServer.active) characterBody.RemoveBuff(iceCrystalDebuff);
                    } else
                    {
                        if (NetworkServer.active)
                        {
                            while (((current / decayTotal) * decayTime) < (characterBody.GetBuffCount(iceCrystalDebuff) - 1))
                            {
                                characterBody.RemoveBuff(iceCrystalDebuff);
                            }
                        }
                    }
                }
            }
        }
    }
}
