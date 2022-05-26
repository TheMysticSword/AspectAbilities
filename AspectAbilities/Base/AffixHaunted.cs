using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Rendering.PostProcessing;
using RoR2;
using R2API;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MysticsRisky2Utils;

namespace AspectAbilities
{
    public class AffixHaunted : BaseAspectAbilityOverride
    {
        public static GameObject buffPulse;
        public static Color colorHaunted = new Color(0f / 255f, 180f / 255f, 140f / 255f);
        public static Color colorHauntedBright = new Color(150f / 255f, 220f / 255f, 220f / 255f);
        public static Texture2D remapHealingToHauntedTexture;

        public static ConfigOptions.ConfigurableValue<float> buffDuration = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Celestine",
            "Buff Duration",
            10f,
            0f,
            120f,
            "How long should the dodge buff last (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnPluginAwake()
        {
            buffPulse = new GameObject("AspectAbilitiesHealPulse");
            Object.DontDestroyOnLoad(buffPulse);
            buffPulse.SetActive(false);
            buffPulse.AddComponent<NetworkIdentity>();
            PrefabAPI.RegisterNetworkPrefab(buffPulse);
        }

        public override void OnLoad()
        {
            remapHealingToHauntedTexture = new Texture2D(256, 16, TextureFormat.ARGB32, false);
            remapHealingToHauntedTexture.wrapMode = TextureWrapMode.Clamp;
            remapHealingToHauntedTexture.filterMode = FilterMode.Bilinear;
            for (int x = 0; x < 110; x++) for (int y = 0; y < 16; y++) remapHealingToHauntedTexture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            for (int x = 0; x < 93; x++) for (int y = 0; y < 16; y++) remapHealingToHauntedTexture.SetPixel(110 + x, y, new Color(colorHaunted.r, colorHaunted.g, colorHaunted.b, colorHaunted.a * (71f / 255f)));
            for (int x = 0; x < 53; x++) for (int y = 0; y < 16; y++) remapHealingToHauntedTexture.SetPixel(203 + x, y, new Color(colorHauntedBright.r, colorHauntedBright.g, colorHauntedBright.b, colorHauntedBright.a * (151f / 255f)));
            remapHealingToHauntedTexture.Apply();

            buffPulse.AddComponent<BuffPulseController>();

            NetworkingAPI.RegisterMessageType<BuffPulseController.SyncFire>();
            NetworkingAPI.RegisterMessageType<BuffPulseController.SyncFireFailed>();

            {
                var vfx = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/NetworkedObjects/TeleporterHealNovaPulse").transform.Find("PulseEffect").gameObject, "HealPulseEffect", false);
                // remove certain parts of the effect
                Object.Destroy(vfx.transform.Find("PP").gameObject);
                Object.Destroy(vfx.transform.Find("Crosses").gameObject);
                // recolour the visual effect
                ParticleSystem.MainModule particleSystem = vfx.transform.Find("Particle System").gameObject.GetComponent<ParticleSystem>().main;
                particleSystem.startColor = new ParticleSystem.MinMaxGradient(colorHaunted);
                vfx.transform.Find("Sphere").gameObject.GetComponent<MeshRenderer>().material.SetTexture("_RemapTex", remapHealingToHauntedTexture);
                vfx.transform.Find("Donut").gameObject.GetComponent<MeshRenderer>().material.SetTexture("_RemapTex", remapHealingToHauntedTexture);

                BuffPulseController.displayedEffectPrefab = vfx;
            }

            EquipmentCatalog.availability.CallWhenAvailable(() => Setup("Celestine", RoR2Content.Equipment.AffixHaunted, 45f, onUseOverride: (self) =>
            {
                // give allies dodge chance
                GameObject affixHauntedWard = self.characterBody.GetComponent<CharacterBody.AffixHauntedBehavior>().affixHauntedWard;
                if (affixHauntedWard)
                {
                    GameObject buffPulseObject = Object.Instantiate(buffPulse);
                    buffPulseObject.SetActive(true);
                    NetworkServer.Spawn(buffPulseObject);
                    BuffPulseController healPulseController = buffPulseObject.GetComponent<BuffPulseController>();
                    healPulseController.Fire(self.characterBody.corePosition, affixHauntedWard.GetComponent<BuffWard>().radius, 0.66f, AspectAbilitiesContent.Buffs.AspectAbilities_HauntedDodge.buffIndex, buffDuration, self.characterBody.teamComponent.teamIndex);
                }
                return true;
            }));
        }

        public class BuffPulseController : NetworkBehaviour
        {
            public Vector3 origin;
            public float pulseRadius;
            public float pulseDuration = 1f;
            public TeamIndex teamIndex = TeamIndex.Neutral;
            public float progress = 0f;
            public float rate = 1f;
            public float buffDuration;
            public BuffIndex buffIndex;

            public SphereSearch sphereSearch;
            public TeamMask teamMask;
            private List<HurtBox> hurtBoxesList = new List<HurtBox>();
            public List<CharacterBody> buffedBodies = new List<CharacterBody>();

            public float timer = 0f;
            public GameObject displayedEffect;
            public static GameObject displayedEffectPrefab;
            private bool mustSyncFire = false;
            private float syncFireDelay = 0f;
            private float syncFireDelayMax = 2f / 60f;
            
            public void Fire(Vector3 origin, float pulseRadius, float pulseDuration, BuffIndex buffIndex, float buffDuration, TeamIndex teamIndex)
            {
                transform.position = origin;
                Util.PlaySound("Play_item_proc_TPhealingNova", gameObject);

                sphereSearch = new SphereSearch
                {
                    mask = LayerIndex.entityPrecise.mask,
                    origin = origin,
                    queryTriggerInteraction = QueryTriggerInteraction.Collide,
                    radius = 0f
                };
                teamMask = default;
                teamMask.AddTeam(teamIndex);

                displayedEffect = Instantiate(displayedEffectPrefab, origin, Quaternion.identity);
                displayedEffect.SetActive(true);

                this.pulseRadius = pulseRadius;
                this.pulseDuration = pulseDuration;
                this.rate = 1f / this.pulseDuration;
                this.buffDuration = buffDuration;
                this.buffIndex = buffIndex;

                mustSyncFire = NetworkServer.active;
            }

            public class SyncFire : INetMessage
            {
                NetworkInstanceId objID;
                Vector3 origin;
                float pulseRadius;
                float pulseDuration;
                int buffIndex;
                float buffDuration;
                int teamIndex;

                public SyncFire()
                {
                }

                public SyncFire(NetworkInstanceId objID, Vector3 origin, float pulseRadius, float pulseDuration, BuffIndex buffIndex, float buffDuration, TeamIndex teamIndex)
                {
                    this.objID = objID;
                    this.origin = origin;
                    this.pulseRadius = pulseRadius;
                    this.pulseDuration = pulseDuration;
                    this.buffIndex = (int)buffIndex;
                    this.buffDuration = buffDuration;
                    this.teamIndex = (int)teamIndex;
                }

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                    origin = reader.ReadVector3();
                    pulseRadius = reader.ReadSingle();
                    pulseDuration = reader.ReadSingle();
                    buffIndex = reader.ReadInt32();
                    buffDuration = reader.ReadSingle();
                    teamIndex = reader.ReadInt32();
                }

                public void OnReceived()
                {
                    if (NetworkServer.active) return;
                    GameObject obj = Util.FindNetworkObject(objID);
                    if (!obj)
                    {
                        new SyncFireFailed(objID).Send(NetworkDestination.Server);
                        return;
                    }
                    BuffPulseController controller = obj.GetComponent<BuffPulseController>();
                    if (controller.mustSyncFire)
                        controller.Fire(origin, pulseRadius, pulseDuration, (BuffIndex)buffIndex, buffDuration, (TeamIndex)teamIndex);
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                    writer.Write(origin);
                    writer.Write(pulseRadius);
                    writer.Write(pulseDuration);
                    writer.Write(buffIndex);
                    writer.Write(buffDuration);
                    writer.Write(teamIndex);
                }
            }

            public class SyncFireFailed : INetMessage
            {
                NetworkInstanceId objID;

                public SyncFireFailed()
                {
                }

                public SyncFireFailed(NetworkInstanceId objID)
                {
                    this.objID = objID;
                }

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                }

                public void OnReceived()
                {
                    GameObject obj = Util.FindNetworkObject(objID);
                    BuffPulseController controller = obj.GetComponent<BuffPulseController>();
                    controller.mustSyncFire = true;
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                }
            }

            public void Update()
            {
                if (mustSyncFire && NetworkServer.active)
                {
                    syncFireDelay += Time.deltaTime;
                    if (syncFireDelay >= syncFireDelayMax)
                    {
                        new SyncFire(gameObject.GetComponent<NetworkIdentity>().netId, origin, pulseRadius, pulseDuration, buffIndex, buffDuration, teamIndex).Send(NetworkDestination.Clients);
                        mustSyncFire = false;
                    }
                }

                timer += Time.deltaTime;
                var r = pulseRadius * EntityStates.TeleporterHealNovaController.TeleporterHealNovaPulse.novaRadiusCurve.Evaluate(progress);
                if (displayedEffect)
                {
                    displayedEffect.transform.localScale = Vector3.one * r;
                }
                if (progress < 1f)
                {
                    sphereSearch.radius = r;
                    sphereSearch.RefreshCandidates().FilterCandidatesByHurtBoxTeam(teamMask).FilterCandidatesByDistinctHurtBoxEntities().GetHurtBoxes(hurtBoxesList);
                    foreach (var hurtBox in hurtBoxesList)
                    {
                        HealthComponent healthComponent = hurtBox.healthComponent;
                        if (healthComponent.body && !healthComponent.body.HasBuff(RoR2Content.Buffs.AffixHaunted) && !buffedBodies.Contains(healthComponent.body))
                        {
                            buffedBodies.Add(healthComponent.body);
                            if (NetworkServer.active)
                            {
                                healthComponent.body.AddTimedBuff(buffIndex, buffDuration);
                            }
                            Util.PlaySound("Play_item_proc_TPhealingNova_hitPlayer", healthComponent.gameObject);
                        }
                    }
                    hurtBoxesList.Clear();
                    progress += rate * Time.deltaTime;
                    if (progress > 1f) progress = 1f;
                }
                if (timer >= pulseDuration * 2)
                {
                    Destroy(gameObject);
                }
            }

            public void OnDestroy()
            {
                if (displayedEffect) Destroy(displayedEffect);
            }
        }
    }
}
