using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Navigation;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using System.Linq;
using RoR2.CharacterAI;

namespace TheMysticSword.AspectAbilities
{
    public class AffixBlue : BaseAspectAbility
    {
        public override void OnLoad()
        {
            On.RoR2.EquipmentCatalog.Init += (orig) =>
            {
                orig();
                equipmentDef = RoR2Content.Equipment.AffixBlue;
                equipmentDef.cooldown = 15f;
                LanguageManager.appendTokens.Add(equipmentDef.pickupToken);
            };
            aiMaxDistance = Mathf.Infinity;

            NetworkingAPI.RegisterMessageType<OverloadingBlinkController.SyncFire>();

            On.RoR2.CharacterBody.Awake += (orig, self) =>
            {
                orig(self);
                self.gameObject.AddComponent<OverloadingBlinkController>();
            };
        }

        public override bool OnUse(EquipmentSlot self)
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
                // teleport in front of the target with slight angle variation
                // NEVER teleport behind a player - you won't notice an enemy that suddenly appears behind you
                if (self.characterBody.master)
                {
                    RoR2.CharacterAI.BaseAI.Target target = AspectAbilitiesPlugin.GetAITarget(self.characterBody.master);
                    if (target != null && target.bestHurtBox)
                    {
                        float angle = Random.value * 360f;
                        if (target.bestHurtBox.healthComponent.body.inputBank)
                        {
                            float angleVariation = 35f;
                            angle = (Util.QuaternionSafeLookRotation(target.bestHurtBox.healthComponent.body.inputBank.aimDirection).eulerAngles.y + Random.Range(-angleVariation, angleVariation)) * Mathf.Deg2Rad;
                        }
                        Vector3 offset = (25f + target.bestHurtBox.healthComponent.body.radius) * new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                        endPosition = target.bestHurtBox.transform.position + offset;
                    }
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
            
            bool groundNodesDesired = true; // can't use CharacterBody.isFlying because it considers Grandparents to be flying type
            bool useIsFlying = true; // but in case we can't get the desired node type, fall back to isFlying
            CharacterMaster master = self.characterBody.master;
            if (master)
            {
                BaseAI baseAI = master.GetComponent<BaseAI>();
                if (baseAI)
                {
                    groundNodesDesired = baseAI.desiredSpawnNodeGraphType == MapNodeGroup.GraphType.Ground;
                    useIsFlying = false;
                }
            }
            if (useIsFlying)
            {
                groundNodesDesired = !self.characterBody.isFlying;
            }

            // pick the nearest node to the endpoint. nodes are generally safer to use and won't get you stuck in terrain
            NodeGraph nodes = !groundNodesDesired ? SceneInfo.instance.airNodes : SceneInfo.instance.groundNodes;
            NodeGraph.NodeIndex nodeIndex = nodes.FindClosestNode(endPosition, self.characterBody.hullClassification);
            nodes.GetNodePosition(nodeIndex, out endPosition);

            // if the caster is a ground-type entity, move them up a little bit to prevent falling through the world
            if (!groundNodesDesired) endPosition += self.characterBody.transform.position - self.characterBody.footPosition;

            self.characterBody.gameObject.GetComponent<OverloadingBlinkController>().Fire(startPosition, endPosition);
            return true;
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
                    if (characterBody.rigidbody) characterBody.rigidbody.interpolation = RigidbodyInterpolation.None;
                    characterBody.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t), Quaternion.identity);
                    if (characterBody.rigidbody)
                    {
                        characterBody.rigidbody.position = Vector3.Lerp(startPosition, endPosition, t);
                        characterBody.rigidbody.rotation = Quaternion.identity;
                    }
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
