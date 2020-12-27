using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using R2API;
using System.Collections.Generic;
using System.Linq;

namespace TheMysticSword.AspectAbilities
{
    public static class AffixPoison
    {
        public static GameObject malachiteUrchinOrbitalMaster;
        public static GameObject malachiteUrchinOrbitalBody;

        public static void Init()
        {
            malachiteUrchinOrbitalMaster = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterMasters/UrchinTurretMaster"), "AspectAbilitiesMalachiteUrchinOrbitalMaster");
            malachiteUrchinOrbitalBody = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("Prefabs/CharacterBodies/UrchinTurretBody"), "AspectAbilitiesMalachiteUrchinOrbitalBody");
            CharacterBody body = malachiteUrchinOrbitalBody.GetComponent<CharacterBody>();
            malachiteUrchinOrbitalMaster.GetComponent<CharacterMaster>().bodyPrefab = malachiteUrchinOrbitalBody;

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<MalachiteOrbitalController>();
            };

            AspectAbilities.RegisterAspectAbility(EquipmentIndex.AffixPoison, 90f,
                (self) =>
                {
                    MalachiteOrbitalController orbitalController = self.characterBody.GetComponent<MalachiteOrbitalController>();
                    orbitalController.respawn += orbitalController.totalNormal;
                    return true;
                });
        }

        public class MalachiteOrbitalController : NetworkBehaviour
        {
            public CharacterBody characterBody;
            public List<UrchinHolder> urchins = new List<UrchinHolder>();
            public int total;
            public int totalNormal
            {
                get
                {
                    int _totalNormal = 3;
                    if (characterBody) _totalNormal += (int)characterBody.radius;
                    return _totalNormal;
                }
            }
            public int totalMax
            {
                get
                {
                    return totalNormal * 3;
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
            
            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
            }

            public struct UrchinHolder
            {
                public UrchinHolder(int index, CharacterBody body)
                {
                    this.index = index;
                    this.body = body;
                    this.spawnTime = Run.instance.time;
                    this.dampVelocity = Vector3.zero;
                }
                public int index;
                public CharacterBody body;
                public float spawnTime;
                public bool alive
                {
                    get
                    {
                        return body && body.healthComponent && body.healthComponent.alive;
                    }
                }
                public Vector3 dampVelocity;
            }

            private void RefreshSlots()
            {
                foreach (UrchinHolder urchin in urchins)
                {
                    if (urchin.index >= total && urchin.alive) urchin.body.healthComponent.Suicide();
                }
                urchins.RemoveAll(x => !x.alive);
            }

            private Vector3 FindSuitableAngle(int index)
            {
                float angle = ((360f / (float)total * (float)index) + rotation) * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
            }
            private Vector3 FindSuitablePosition(int index)
            {
                Vector3 vectorAngle = FindSuitableAngle(index);
                return characterBody.corePosition + Vector3.up * height + vectorAngle * radius * 3f;
            }

            public void FixedUpdate()
            {
                respawnTimer += Time.fixedDeltaTime;
                if (respawnTimer >= respawnTimerMax && respawn > 0)
                {
                    respawnTimer = 0f;
                    respawn--;
                    total = Mathf.Clamp(total + 1, 0, totalMax);
                    // create a list of index statuses - we will use it to find a free slot for a new urchin
                    List<bool> indexStatus = Enumerable.Repeat(false, total).ToList();
                    foreach (UrchinHolder urchin in urchins)
                    {
                        if (urchin.alive)
                        {
                            indexStatus[urchin.index] = true;
                        }
                    }
                    // let's find a free slot!
                    int freeIndex = 0;
                    foreach (bool indexOccupied in indexStatus)
                    {
                        if (indexOccupied) freeIndex++;
                        else break;
                    }
                    // if the free slot index exceeds the total amount of slots, kill the oldest urchin and put the new one in its slot
                    if (freeIndex >= total)
                    {
                        UrchinHolder oldest = urchins.Aggregate(urchins.First(), (current, next) => next.spawnTime < current.spawnTime ? next : current);
                        if (oldest.body.healthComponent) oldest.body.healthComponent.Suicide();
                        freeIndex = oldest.index;
                    }
                    RefreshSlots();
                    // spawn the urchin facing outward of the circle
                    if (NetworkServer.active)
                    {
                        GameObject urchinMasterObject = Object.Instantiate(malachiteUrchinOrbitalMaster, FindSuitablePosition(freeIndex), Quaternion.LookRotation(FindSuitableAngle(freeIndex)));
                        CharacterMaster urchinMaster = urchinMasterObject.GetComponent<CharacterMaster>();
                        urchinMaster.teamIndex = characterBody.teamComponent.teamIndex;
                        NetworkServer.Spawn(urchinMasterObject);
                        if (urchinMaster && NetworkServer.active)
                        {
                            urchinMaster.SpawnBodyHere();
                            urchins.Add(new UrchinHolder(freeIndex, urchinMaster.GetBody()));
                            AspectAbilities.BodyFields bodyFields = urchinMaster.GetBody().gameObject.GetComponent<AspectAbilities.BodyFields>();
                            if (bodyFields)
                            {
                                bodyFields.multiplierOnHitProcsOnSelf -= 1f;
                                bodyFields.multiplierOnDeathProcsOnSelf -= 1f;
                            }
                            // copy the master's items
                            Inventory inventory = urchinMaster.GetBody().inventory;
                            inventory.ResetItem(ItemIndex.HealthDecay);
                            inventory.CopyItemsFrom(characterBody.inventory);
                            for (int itemIndex = 0; itemIndex < ItemCatalog.itemCount; itemIndex++)
                            {
                                if (ItemCatalog.GetItemDef((ItemIndex)itemIndex).ContainsTag(ItemTag.AIBlacklist))
                                {
                                    inventory.ResetItem((ItemIndex)itemIndex);
                                }
                            }
                            inventory.ResetItem(ItemIndex.BoostHp);
                            inventory.ResetItem(ItemIndex.BoostDamage);
                            inventory.GiveItem(ItemIndex.HealthDecay, 45);
                        }
                    }
                }

                rotation += rotationSpeed * Time.fixedDeltaTime;
                if (characterBody && characterBody.healthComponent && characterBody.healthComponent.alive)
                {
                    for (int i = 0; i < urchins.Count; i++)
                    {
                        UrchinHolder urchin = urchins[i];
                        if (urchin.alive)
                        {
                            urchin.body.transform.position = Vector3.SmoothDamp(urchin.body.transform.position, FindSuitablePosition(urchin.index), ref urchin.dampVelocity, 0.05f);
                        }
                    }
                    total = urchins.Count;
                }
                else
                {
                    if (NetworkServer.active)
                    {
                        Destroy(this);
                    }
                }
            }

            public void OnDestroy()
            {
                if (NetworkServer.active)
                {
                    foreach (UrchinHolder urchin in urchins)
                    {
                        if (urchin.alive)
                        {
                            urchin.body.healthComponent.Suicide();
                        }
                    }
                }
            }
        }
    }
}
