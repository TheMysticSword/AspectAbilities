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

namespace AspectAbilities
{
    public class AffixHaunted : BaseAspectAbilityOverride
    {
        public static GameObject healPulse;
        public static Color colorHaunted = new Color(0f / 255f, 180f / 255f, 140f / 255f);
        public static Color colorHauntedBright = new Color(150f / 255f, 220f / 255f, 220f / 255f);
        public static Texture2D remapHealingToHauntedTexture;

        public override void OnPluginAwake()
        {
            healPulse = new GameObject("AspectAbilitiesHealPulse");
            Object.DontDestroyOnLoad(healPulse);
            healPulse.SetActive(false);
            healPulse.AddComponent<NetworkIdentity>();
            PrefabAPI.RegisterNetworkPrefab(healPulse);
        }

        public override void OnLoad()
        {
            On.RoR2.EquipmentCatalog.Init += (orig) =>
            {
                orig();
                aspectAbility.equipmentDef = RoR2Content.Equipment.AffixHaunted;
                aspectAbility.equipmentDef.cooldown = 7f;
                LanguageManager.appendTokens.Add(aspectAbility.equipmentDef.pickupToken);
                AspectAbilitiesPlugin.registeredAspectAbilities.Add(aspectAbility);
            };

            remapHealingToHauntedTexture = new Texture2D(256, 16, TextureFormat.ARGB32, false);
            remapHealingToHauntedTexture.wrapMode = TextureWrapMode.Clamp;
            remapHealingToHauntedTexture.filterMode = FilterMode.Bilinear;
            for (int x = 0; x < 110; x++) for (int y = 0; y < 16; y++) remapHealingToHauntedTexture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            for (int x = 0; x < 93; x++) for (int y = 0; y < 16; y++) remapHealingToHauntedTexture.SetPixel(110 + x, y, new Color(colorHaunted.r, colorHaunted.g, colorHaunted.b, colorHaunted.a * (71f / 255f)));
            for (int x = 0; x < 53; x++) for (int y = 0; y < 16; y++) remapHealingToHauntedTexture.SetPixel(203 + x, y, new Color(colorHauntedBright.r, colorHauntedBright.g, colorHauntedBright.b, colorHauntedBright.a * (151f / 255f)));
            remapHealingToHauntedTexture.Apply();

            healPulse.AddComponent<HealPulseController>();

            NetworkingAPI.RegisterMessageType<HealPulseController.SyncFire>();
            NetworkingAPI.RegisterMessageType<HealPulseController.SyncFireFailed>();

            aspectAbility.onUseOverride = (self) =>
            {
                // cast an AoE heal for allies
                GameObject affixHauntedWard = self.characterBody.GetComponent<CharacterBody.AffixHauntedBehavior>().GetFieldValue<GameObject>("affixHauntedWard");
                if (affixHauntedWard)
                {
                    GameObject healPulseObject = Object.Instantiate(healPulse);
                    healPulseObject.SetActive(true);
                    NetworkServer.Spawn(healPulseObject);
                    HealPulseController healPulseController = healPulseObject.GetComponent<HealPulseController>();
                    healPulseController.Fire(self.characterBody.corePosition, affixHauntedWard.GetComponent<BuffWard>().radius, 0.33f, 1f, self.characterBody.teamComponent.teamIndex);

                    // remove bloom
                    Object.Destroy(healPulseController.displayedEffect.transform.Find("PP").gameObject);
                    // recolour the visual effect
                    ParticleSystem.MainModule particleSystem = healPulseController.displayedEffect.transform.Find("Particle System").gameObject.GetComponent<ParticleSystem>().main;
                    particleSystem.startColor = new ParticleSystem.MinMaxGradient(colorHaunted);
                    healPulseController.displayedEffect.transform.Find("Crosses").gameObject.GetComponent<ParticleSystemRenderer>().material.SetTexture("_RemapTex", remapHealingToHauntedTexture);
                    healPulseController.displayedEffect.transform.Find("Sphere").gameObject.GetComponent<MeshRenderer>().material.SetTexture("_RemapTex", remapHealingToHauntedTexture);
                    healPulseController.displayedEffect.transform.Find("Donut").gameObject.GetComponent<MeshRenderer>().material.SetTexture("_RemapTex", remapHealingToHauntedTexture);

                    // grab the "already healed" list, we will modify it
                    List<HealthComponent> healedTargets = healPulseController.healPulse.GetFieldValue<List<HealthComponent>>("healedTargets");
                    // add all celestines to the "already healed" list so that they don't heal each other and we don't heal ourselves
                    ReadOnlyCollection<TeamComponent> teamMembers = TeamComponent.GetTeamMembers(self.characterBody.teamComponent.teamIndex);
                    foreach (var teamMember in teamMembers)
                    {
                        if (teamMember.body.equipmentSlot && teamMember.body.equipmentSlot.equipmentIndex == RoR2Content.Equipment.AffixHaunted.equipmentIndex)
                        {
                            healedTargets.Add(teamMember.body.healthComponent);
                        }
                    }
                    if (self.characterBody.healthComponent)
                    {
                        // and add the caster to the "already healed" list so that they don't get healed
                        if (!healedTargets.Contains(self.characterBody.healthComponent))
                        {
                            healedTargets.Add(self.characterBody.healthComponent);
                        }
                        // heal ourselves for a lower value
                        self.characterBody.healthComponent.HealFraction(0.1f, default(ProcChainMask));
                    }

                    // replace the pulse's healed list with our new one
                    healPulseController.healPulse.SetFieldValue("healedTargets", healedTargets);
                }
                return true;
            };
        }

        public class HealPulseController : NetworkBehaviour
        {
            public Vector3 origin
            {
                get
                {
                    if (healPulse != null) return healPulse.GetFieldValue<SphereSearch>("sphereSearch").origin;
                    return Vector3.zero;
                }
                set
                {
                    if (healPulse != null) healPulse.GetFieldValue<SphereSearch>("sphereSearch").origin = value;
                }
            }
            private float _radius;
            public float radius
            {
                get
                {
                    if (healPulse != null) return healPulse.GetFieldValue<float>("finalRadius");
                    return _radius;
                }
                set
                {
                    if (healPulse != null) healPulse.SetFieldValue("finalRadius", value);
                    _radius = value;
                }
            }
            public float healFraction
            {
                get
                {
                    if (healPulse != null) return healPulse.GetFieldValue<float>("healFractionValue");
                    return 0f;
                }
                set
                {
                    if (healPulse != null) healPulse.SetFieldValue("healFractionValue", value);
                }
            }
            private float _duration = 1f;
            public float duration
            {
                get
                {
                    return _duration;
                }
                set
                {
                    progress = (progress / rate) / value;
                    _duration = value;
                }
            }
            private TeamIndex _teamIndex = TeamIndex.Neutral;
            public TeamIndex teamIndex
            {
                get
                {
                    return _teamIndex;
                }
                set
                {
                    _teamIndex = value;
                    TeamMask teamMask = new TeamMask();
                    teamMask.AddTeam(value);
                    if (healPulse != null) healPulse.SetFieldValue("teamMask", teamMask);
                }
            }
            private float _progress = 0f;
            public float progress
            {
                get
                {
                    if (healPulse != null) return healPulse.GetFieldValue<float>("t");
                    return _progress;
                }
                set
                {
                    if (healPulse != null) healPulse.SetFieldValue("t", value);
                    timer = value / rate;
                    _progress = value;
                }
            }
            private float _rate = 1f;
            public float rate
            {
                get
                {
                    if (healPulse != null) return healPulse.GetFieldValue<float>("rate");
                    return _rate;
                }
                set
                {
                    if (healPulse != null) healPulse.SetFieldValue("rate", value);
                    _rate = value;
                }
            }
            public float timer = 0f;
            public object healPulse;
            public GameObject displayedEffect;
            private static GameObject displayedEffectPrefab = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/NetworkedObjects/TeleporterHealNovaPulse").transform.Find("PulseEffect").gameObject, "HealPulseEffect", false);
            private bool mustSyncFire = false;
            private float syncFireDelay = 0f;
            private float syncFireDelayMax = 2f / 60f;
            public static System.Reflection.ConstructorInfo healPulseConstructor = typeof(EntityStates.TeleporterHealNovaController.TeleporterHealNovaPulse).GetNestedTypeCached("HealPulse").GetConstructor(new System.Type[] { typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(TeamIndex) });

            public void Fire(Vector3 origin, float radius, float healFraction, float duration, TeamIndex teamIndex)
            {
                transform.position = origin;
                Util.PlaySound("Play_item_proc_TPhealingNova", gameObject);

                displayedEffect = Instantiate(displayedEffectPrefab, origin, Quaternion.identity);
                displayedEffect.SetActive(true);
                if (NetworkServer.active)
                {
                    healPulse = healPulseConstructor.Invoke(new object[] { origin, radius, healFraction, duration, teamIndex });
                    mustSyncFire = true;
                }
                else
                {
                    this.radius = radius;
                    this.duration = duration;
                    this.rate = 1f / this.duration;
                }
            }

            public class SyncFire : INetMessage
            {
                NetworkInstanceId objID;
                Vector3 origin;
                float radius;
                float healFraction;
                float duration;
                int teamIndex;

                public SyncFire()
                {
                }

                public SyncFire(NetworkInstanceId objID, Vector3 origin, float radius, float healFraction, float duration, TeamIndex teamIndex)
                {
                    this.objID = objID;
                    this.origin = origin;
                    this.radius = radius;
                    this.healFraction = healFraction;
                    this.duration = duration;
                    this.teamIndex = (int)teamIndex;
                }

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                    origin = reader.ReadVector3();
                    radius = reader.ReadSingle();
                    healFraction = reader.ReadSingle();
                    duration = reader.ReadSingle();
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
                    HealPulseController controller = obj.GetComponent<HealPulseController>();
                    controller.Fire(origin, radius, healFraction, duration, (TeamIndex)teamIndex);
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                    writer.Write(origin);
                    writer.Write(radius);
                    writer.Write(healFraction);
                    writer.Write(duration);
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
                    HealPulseController controller = obj.GetComponent<HealPulseController>();
                    controller.mustSyncFire = true;
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                }
            }

            public void FixedUpdate()
            {
                if (mustSyncFire && NetworkServer.active)
                {
                    syncFireDelay += Time.fixedDeltaTime;
                    if (syncFireDelay >= syncFireDelayMax)
                    {
                        new SyncFire(gameObject.GetComponent<NetworkIdentity>().netId, origin, radius, healFraction, duration, teamIndex).Send(NetworkDestination.Clients);
                        mustSyncFire = false;
                    }
                }

                timer += Time.fixedDeltaTime;
                if (displayedEffect)
                {
                    displayedEffect.transform.localScale = Vector3.one * radius * EntityStates.TeleporterHealNovaController.TeleporterHealNovaPulse.novaRadiusCurve.Evaluate(progress);
                }
                if (progress < 1)
                {
                    if (NetworkServer.active) healPulse.InvokeMethod("Update", Time.fixedDeltaTime);
                    else progress += rate * Time.fixedDeltaTime;
                    if (progress > 1f) progress = 1f;
                }
                if (timer >= duration * 2)
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
