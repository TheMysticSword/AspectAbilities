using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using R2API;
using System.Collections.Generic;
using System.Linq;
using R2API.Networking.Interfaces;
using R2API.Networking;

namespace TheMysticSword.AspectAbilities
{
    public class AffixPoison : BaseAspectAbility
    {
        public static GameObject malachiteUrchinOrbitalMaster;
        public static GameObject malachiteUrchinOrbitalBody;

        public override void OnPluginAwake()
        {
            // clone the body and the master in case we want to change the stats of the urchins
            malachiteUrchinOrbitalMaster = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterMasters/UrchinTurretMaster"), "AspectAbilitiesMalachiteUrchinOrbitalMaster");
            malachiteUrchinOrbitalBody = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/UrchinTurretBody"), "AspectAbilitiesMalachiteUrchinOrbitalBody");

            NetworkingAPI.RegisterMessageType<MalachiteOrbitalUrchin.SyncInit>();
            NetworkingAPI.RegisterMessageType<MalachiteOrbitalUrchin.RequestSyncInit>();
        }

        public override void OnLoad()
        {
            On.RoR2.EquipmentCatalog.Init += (orig) =>
            {
                orig();
                equipmentDef = RoR2Content.Equipment.AffixPoison;
                equipmentDef.cooldown = 90f;
                LanguageManager.appendTokens.Add(equipmentDef.pickupToken);
            };

            CharacterBody body = malachiteUrchinOrbitalBody.GetComponent<CharacterBody>();
            float statBoost = 3f;
            body.baseMaxHealth *= statBoost;
            body.levelMaxHealth *= statBoost;
            body.baseDamage *= statBoost;
            body.levelDamage *= statBoost;
            malachiteUrchinOrbitalMaster.GetComponent<CharacterMaster>().bodyPrefab = malachiteUrchinOrbitalBody;
            malachiteUrchinOrbitalMaster.AddComponent<MalachiteOrbitalUrchin>();

            AspectAbilitiesContent.Resources.bodyPrefabs.Add(malachiteUrchinOrbitalBody);
            AspectAbilitiesContent.Resources.masterPrefabs.Add(malachiteUrchinOrbitalMaster);

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<MalachiteOrbitalController>();
            };
        }

        public override bool OnUse(EquipmentSlot self)
        {
            MalachiteOrbitalController orbitalController = self.characterBody.GetComponent<MalachiteOrbitalController>();
            orbitalController.respawn += orbitalController.totalNormal;
            return true;
        }

        public class MalachiteOrbitalController : NetworkBehaviour
        {
            public CharacterBody characterBody;
            public int total;
            public int totalNormal
            {
                get
                {
                    int _totalNormal = 1;
                    if (characterBody) _totalNormal += (int)characterBody.radius;
                    return _totalNormal;
                }
            }
            public int totalMax
            {
                get
                {
                    return totalNormal;
                }
            }
            public int respawn = 0;
            public float respawnTimer = 0f;
            public float respawnTimerMax = 0.3f;
            public float radius {
                get
                {
                    if (characterBody) return 1f + characterBody.radius;
                    return 1f;
                }
            }
            public float rotation = 0f;
            public float rotationSpeed = 9f;
            public float height = 6f;
            public List<MalachiteOrbitalUrchin> urchins;
            
            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
                urchins = new List<MalachiteOrbitalUrchin>();
            }

            public void FixedUpdate()
            {
                urchins.RemoveAll(x => !x); // remove destroyed urchins from the list
                respawnTimer += Time.fixedDeltaTime;
                if (respawnTimer >= respawnTimerMax && respawn > 0)
                {
                    respawnTimer = 0f;
                    respawn--;
                    total = Mathf.Min(total + 1, totalMax);
                    int freeIndex = 0;
                    List<int> freeIndicies = new List<int>();
                    for (var i = 0; i < total; i++) freeIndicies.Add(i);
                    foreach (MalachiteOrbitalUrchin urchin in urchins) freeIndicies.Remove(urchin.index);
                    if (freeIndicies.Count > 0) freeIndex = RoR2Application.rng.NextElementUniform(freeIndicies);
                    else if (urchins.Count > 0)
                    {
                        List<MalachiteOrbitalUrchin> urchinsCopy = urchins.ToList();
                        urchinsCopy.Sort((x, y) => x.spawnTime > y.spawnTime ? 1 : -1);
                        MalachiteOrbitalUrchin urchinToKill = urchinsCopy.First();
                        freeIndex = urchinToKill.index;
                        if (urchinToKill.master) urchinToKill.master.TrueKill();
                    }
                    if (NetworkServer.active)
                    {
                        new MasterSummon
                        {
                            masterPrefab = malachiteUrchinOrbitalMaster,
                            position = MalachiteOrbitalUrchin.FindSuitablePosition(total, freeIndex, rotation, characterBody.corePosition, height, radius),
                            rotation = Quaternion.LookRotation(MalachiteOrbitalUrchin.FindSuitableAngle(total, freeIndex, rotation)),
                            summonerBodyObject = characterBody.gameObject,
                            ignoreTeamMemberLimit = true,
                            preSpawnSetupCallback = (urchinMaster) =>
                            {
                                Debug.Log(transform.position);
                                if (NetworkServer.active)
                                {
                                    MalachiteOrbitalUrchin component = urchinMaster.GetComponent<MalachiteOrbitalUrchin>();
                                    urchins.Add(component);
                                    component.ownerController = this;
                                    component.spawnTime = Run.FixedTimeStamp.now.t;
                                    Inventory inventory = urchinMaster.inventory;
                                    inventory.ResetItem(RoR2Content.Items.HealthDecay);
                                    inventory.CopyItemsFrom(characterBody.inventory);
                                    inventory.ResetItem(RoR2Content.Items.BoostHp); // don't boost stats from elite owners
                                    inventory.ResetItem(RoR2Content.Items.BoostDamage);
                                    inventory.GiveItem(RoR2Content.Items.HealthDecay, 45);
                                }
                            }
                        }.Perform();
                    }
                }

                rotation += rotationSpeed * Time.fixedDeltaTime;
            }

            public void OnDestroy()
            {
                foreach (MalachiteOrbitalUrchin urchin in urchins)
                {
                    if (urchin.master) urchin.master.TrueKill();
                }
            }
        }

        public class MalachiteOrbitalUrchin : MonoBehaviour
        {
            public int index = 0;
            public MalachiteOrbitalController ownerController;
            public CharacterBody body;
            public CharacterMaster master;
            public Vector3 dampVelocity;
            public float spawnTime;
            public bool networkInit = false;

            public void Awake()
            {
                master = GetComponent<CharacterMaster>();
                if (!NetworkServer.active && !networkInit) new RequestSyncInit
                {
                    objID = gameObject.GetComponent<NetworkIdentity>().netId,
                    index = index,
                    ownerObjID = ownerController ? ownerController.gameObject.GetComponent<NetworkIdentity>().netId : NetworkInstanceId.Invalid
                }.Send(NetworkDestination.Server);
            }

            public class SyncInit : INetMessage
            {
                public NetworkInstanceId objID;
                public int index;
                public NetworkInstanceId ownerObjID;

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                    index = reader.ReadInt32();
                    ownerObjID = reader.ReadNetworkId();
                }

                public void OnReceived()
                {
                    if (NetworkServer.active) return;
                    GameObject obj = Util.FindNetworkObject(objID);
                    if (obj)
                    {
                        MalachiteOrbitalUrchin component = obj.GetComponent<MalachiteOrbitalUrchin>();
                        component.index = index;
                        GameObject ownerObj = Util.FindNetworkObject(ownerObjID);
                        if (ownerObj) component.ownerController = ownerObj.GetComponent<MalachiteOrbitalController>();
                        component.networkInit = true;
                    }
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                    writer.Write(index);
                    writer.Write(ownerObjID);
                }
            }

            public class RequestSyncInit : INetMessage
            {
                public NetworkInstanceId objID;
                public int index;
                public NetworkInstanceId ownerObjID;

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                    index = reader.ReadInt32();
                    ownerObjID = reader.ReadNetworkId();
                }

                public void OnReceived()
                {
                    if (!NetworkServer.active) return;
                    GameObject obj = Util.FindNetworkObject(objID);
                    if (obj)
                    {
                        MalachiteOrbitalUrchin component = obj.GetComponent<MalachiteOrbitalUrchin>();
                        new SyncInit
                        {
                            objID = objID,
                            index = component.index,
                            ownerObjID = component.ownerController ? component.ownerController.gameObject.GetComponent<NetworkIdentity>().netId : NetworkInstanceId.Invalid
                        }.Send(NetworkDestination.Clients);
                    }
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                    writer.Write(index);
                    writer.Write(ownerObjID);
                }
            }

            public void FixedUpdate()
            {
                if (!body && master.hasBody)
                {
                    body = master.GetBody();
                }
                if (body)
                {
                    body.transform.position = Vector3.SmoothDamp(body.transform.position, FindSuitablePosition(ownerController.total, index, ownerController.rotation, ownerController.characterBody.corePosition, ownerController.height, ownerController.radius), ref dampVelocity, 0.05f);
                }
            }

            public static Vector3 FindSuitableAngle(int total, int index, float rotation)
            {
                if (total <= 1) return Vector3.zero;
                float angle = ((360f / (float)total * (float)index) + rotation) * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            }
            public static Vector3 FindSuitablePosition(int total, int index, float rotation, Vector3 corePosition, float height, float radius)
            {
                Vector3 vectorAngle = FindSuitableAngle(total, index, rotation);
                return corePosition + Vector3.up * height + vectorAngle * radius * 3f;
            }

            public void OnDestroy()
            {
                if (ownerController)
                {
                    ownerController.total = Mathf.Max(ownerController.total - 1, 0);
                }
            }
        }
    }
}
