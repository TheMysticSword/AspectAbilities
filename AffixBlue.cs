using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Navigation;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using System.Linq;

namespace TheMysticSword.AspectAbilities
{
    public static class AffixBlue
    {
        public static void Init()
        {
            NetworkingAPI.RegisterMessageType<OverloadingBlinkController.SyncFire>();

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<OverloadingBlinkController>();
            };

            AspectAbilities.RegisterAspectAbility(EquipmentIndex.AffixBlue, 15f,
                (self) =>
                {
                    // teleport to the cursor
                    Util.PlaySound("Play_jellyfish_spawn", self.characterBody.gameObject);
                    float minDistance = 35f;
                    float maxDistance = 2000f;
                    Ray aimRay = self.InvokeMethod<Ray>("GetAimRay");
                    RaycastHit raycastHit;
                    Vector3 startPosition = self.characterBody.transform.position;
                    Vector3 endPosition = aimRay.GetPoint(maxDistance);
                    if (Physics.Raycast(aimRay, out raycastHit, maxDistance, LayerIndex.world.mask, QueryTriggerInteraction.Ignore))
                    {
                        endPosition = raycastHit.point;
                    }

                    if (!self.characterBody.isPlayerControlled)
                    {
                        // ai tweaks:
                        // always teleport near the target, ignore the aim ray
                        BullseyeSearch bullseyeSearch = new BullseyeSearch();
                        bullseyeSearch.searchOrigin = aimRay.origin;
                        bullseyeSearch.searchDirection = aimRay.direction;
                        bullseyeSearch.maxDistanceFilter = maxDistance;
                        bullseyeSearch.teamMaskFilter = TeamMask.allButNeutral;
                        bullseyeSearch.filterByLoS = false;
                        bullseyeSearch.teamMaskFilter.RemoveTeam(TeamComponent.GetObjectTeam(self.gameObject));
                        bullseyeSearch.sortMode = BullseyeSearch.SortMode.Angle;
                        bullseyeSearch.RefreshCandidates();
                        // choose the closest target and prioritize targets of the same type (ground or flying)
                        HurtBox hurtBox = bullseyeSearch.GetResults().OrderBy(hb => Vector3.Distance(hb.transform.position, self.characterBody.transform.position) * (self.characterBody.isFlying == hb.healthComponent.body.isFlying ? 1f : 2f)).FirstOrDefault();
                        // teleport in front of the target with slight angle variation
                        // NEVER teleport behind a player - you won't notice an enemy that suddenly appears behind you
                        if (hurtBox)
                        {
                            float angleVariation = 35f;
                            float angle = (Util.QuaternionSafeLookRotation(hurtBox.healthComponent.body.inputBank.aimDirection).eulerAngles.y + Random.Range(-angleVariation, angleVariation)) * Mathf.Deg2Rad;
                            Vector3 offset = (25f + hurtBox.healthComponent.body.radius) * new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                            endPosition = hurtBox.transform.position + offset;
                        }
                    }

                    // teleport a bit farther if current teleport distance is too short
                    float currentDistance = Vector3.Distance(startPosition, endPosition);
                    if (currentDistance < minDistance)
                    {
                        Vector3 direction = (endPosition - startPosition).normalized;
                        float extraDistance = minDistance - currentDistance;
                        endPosition += extraDistance * direction;
                    }

                    // pick the nearest node to the endpoint. nodes are generally safer to use and won't get you stuck in terrain
                    NodeGraph nodes = self.characterBody.isFlying ? SceneInfo.instance.airNodes : SceneInfo.instance.groundNodes;
                    NodeGraph.NodeIndex nodeIndex = nodes.FindClosestNode(endPosition, self.characterBody.hullClassification);
                    nodes.GetNodePosition(nodeIndex, out endPosition);

                    // if the caster is a ground-type entity, move them up a little bit to prevent falling through the world
                    if (!self.characterBody.isFlying) endPosition += self.characterBody.transform.position - self.characterBody.footPosition;

                    self.characterBody.gameObject.GetComponent<OverloadingBlinkController>().Fire(startPosition, endPosition);
                    return true;
                });
        }

        public class OverloadingBlinkController : NetworkBehaviour
        {
            public CharacterBody characterBody;
            public Transform modelTransform;
            public CharacterModel characterModel;
            public HurtBoxGroup hurtBoxGroup;
            public CharacterMotor characterMotor;
            public float timer = 0f;
            public float timerMax = 0.3f;
            public float blinkEffectTimer = 0f;
            public float blinkEffectTimerMax = 0.3f;
            public Vector3 startPosition;
            public Vector3 endPosition;
            public Vector3 direction;
            public bool active = false;

            public void Awake()
            {
                characterBody = GetComponent<CharacterBody>();
                if (characterBody.modelLocator) modelTransform = characterBody.modelLocator.modelTransform;
                characterMotor = characterBody.characterMotor;
            }

            public void Fire(Vector3 startPosition, Vector3 endPosition)
            {
                if (active)
                {
                    End(timer / timerMax);
                }

                timer = 0f;
                blinkEffectTimer = 0f;
                blinkEffectTimerMax = timerMax / (5f + characterBody.radius);
                active = true;

                if (modelTransform)
                {
                    characterModel = modelTransform.GetComponent<CharacterModel>();
                    hurtBoxGroup = modelTransform.GetComponent<HurtBoxGroup>();
                }
                if (characterModel) characterModel.invisibilityCount++;
                if (hurtBoxGroup) hurtBoxGroup.hurtBoxesDeactivatorCounter++;
                if (characterMotor) characterMotor.enabled = false;

                this.startPosition = startPosition;
                this.endPosition = endPosition;
                direction = (endPosition - startPosition).normalized;

                blinkEffectTimer = blinkEffectTimerMax;

                if (NetworkServer.active)
                {
                    new SyncFire(gameObject.GetComponent<NetworkIdentity>().netId, startPosition, endPosition).Send(NetworkDestination.Clients);
                }
            }

            public class SyncFire : INetMessage
            {
                NetworkInstanceId objID;
                Vector3 startPosition;
                Vector3 endPosition;

                public SyncFire()
                {
                }

                public SyncFire(NetworkInstanceId objID, Vector3 startPosition, Vector3 endPosition)
                {
                    this.objID = objID;
                    this.startPosition = startPosition;
                    this.endPosition = endPosition;
                }

                public void Deserialize(NetworkReader reader)
                {
                    objID = reader.ReadNetworkId();
                    startPosition = reader.ReadVector3();
                    endPosition = reader.ReadVector3();
                }

                public void OnReceived()
                {
                    if (NetworkServer.active) return;
                    GameObject obj = Util.FindNetworkObject(objID);
                    if (obj)
                    {
                        OverloadingBlinkController controller = obj.GetComponent<OverloadingBlinkController>();
                        controller.Fire(startPosition, endPosition);
                    }
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(objID);
                    writer.Write(startPosition);
                    writer.Write(endPosition);
                }
            }

            public void CreateBlinkEffect(Vector3 origin, float scale)
            {
                EffectManager.SpawnEffect(EntityStates.Huntress.BlinkState.blinkPrefab, new EffectData
                {
                    rotation = Util.QuaternionSafeLookRotation(endPosition - startPosition),
                    origin = origin,
                    scale = scale
                }, true);
            }

            public void UpdateBodyPosition(float t)
            {
                if (characterMotor)
                {
                    if (characterBody.characterDirection) characterMotor.velocity = Vector3.zero;
                    characterMotor.Motor.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t), Quaternion.identity, true);
                }
                else
                {
                    characterBody.rigidbody.interpolation = RigidbodyInterpolation.None;
                    characterBody.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t), Quaternion.identity);
                    characterBody.rigidbody.position = Vector3.Lerp(startPosition, endPosition, t);
                    characterBody.rigidbody.rotation = Quaternion.identity;
                }
            }

            public void End(float t = 1f)
            {
                active = false;
                Util.PlaySound("Play_mage_m1_impact", characterBody.gameObject);

                UpdateBodyPosition(t);

                if (characterBody.characterDirection) characterBody.characterDirection.forward = direction;
                if (modelTransform)
                {
                    TemporaryOverlay temporaryOverlay = modelTransform.gameObject.AddComponent<TemporaryOverlay>();
                    temporaryOverlay.duration = 1f;
                    temporaryOverlay.destroyComponentOnEnd = true;
                    temporaryOverlay.originalMaterial = Resources.Load<Material>("Materials/matHuntressFlashExpanded");
                    temporaryOverlay.inspectorCharacterModel = modelTransform.gameObject.GetComponent<CharacterModel>();
                    temporaryOverlay.alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
                    temporaryOverlay.animateShaderAlpha = true;
                }
                if (characterModel) characterModel.invisibilityCount--;
                if (hurtBoxGroup) hurtBoxGroup.hurtBoxesDeactivatorCounter--;
                if (characterMotor) characterMotor.enabled = true;
            }

            public void FixedUpdate()
            {
                if (active)
                {
                    timer += Time.fixedDeltaTime;
                    blinkEffectTimer += Time.fixedDeltaTime;
                    if (blinkEffectTimer >= blinkEffectTimerMax)
                    {
                        blinkEffectTimer = 0;
                        CreateBlinkEffect(characterBody.corePosition, characterBody.radius);
                    }
                    UpdateBodyPosition(timer / timerMax);
                    if (timer >= timerMax)
                    {
                        End();
                    }
                }
            }
        }
    }
}
