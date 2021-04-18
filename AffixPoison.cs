using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using R2API;
using System.Collections.Generic;
using System.Linq;

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
            malachiteUrchinOrbitalMaster.GetComponent<CharacterMaster>().bodyPrefab = malachiteUrchinOrbitalBody;

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
                public UrchinHolder(int index)
                {
                    this.index = index;
                    this.body = null;
                    this.spawnTime = Run.instance.time;
                    this.dampVelocity = Vector3.zero;
                    this.spawnedOnce = false;
                }
                public int index;
                public CharacterBody body;
                public bool spawnedOnce;
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
                urchins.RemoveAll(x => !x.alive && x.spawnedOnce);
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
                    RefreshSlots();
                    List<bool> indexStatus = Enumerable.Repeat(false, total).ToList();
                    foreach (UrchinHolder urchin in urchins)
                    {
                        if (urchin.alive || !urchin.spawnedOnce)
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
                        UrchinHolder oldest = urchins.Aggregate((current, next) => next.spawnTime < current.spawnTime ? next : current);
                        if (oldest.body && oldest.body.healthComponent)
                        {
                            oldest.body.healthComponent.Suicide();
                            RefreshSlots();
                        }
                        freeIndex = oldest.index;
                    }
                    // spawn the urchin facing outward of the circle
                    if (NetworkServer.active)
                    {
                        new MasterSummon
                        {
                            masterPrefab = malachiteUrchinOrbitalMaster,
                            position = FindSuitablePosition(freeIndex),
                            rotation = Quaternion.LookRotation(FindSuitableAngle(freeIndex)),
                            summonerBodyObject = characterBody.gameObject,
                            ignoreTeamMemberLimit = true,
                            preSpawnSetupCallback = (urchinMaster) =>
                            {
                                if (NetworkServer.active)
                                {
                                    urchins.Add(new UrchinHolder(freeIndex));
                                    int freeIndexCached = freeIndex;
                                    // can't do urchinMaster.GetBody() because the body doesn't exist yet
                                    urchinMaster.onBodyStart += (urchinBody) =>
                                    {
                                        UrchinHolderOnSpawn(freeIndexCached, urchinBody);
                                        AspectAbilitiesPlugin.AspectAbilitiesBodyFields bodyFields = urchinBody.GetComponent<AspectAbilitiesPlugin.AspectAbilitiesBodyFields>();
                                        if (bodyFields)
                                        {
                                            bodyFields.multiplierOnHitProcsOnSelf -= 1f;
                                            bodyFields.multiplierOnDeathProcsOnSelf -= 1f;
                                        }
                                    };
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

            public void UrchinHolderOnSpawn(int index, CharacterBody body)
            {
                int listIndex = urchins.FindIndex(x => x.index == index);
                if (listIndex != -1)
                {
                    UrchinHolder newUrchin = new UrchinHolder(index);
                    newUrchin.body = body;
                    newUrchin.spawnedOnce = true;
                    urchins[listIndex] = newUrchin;
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
