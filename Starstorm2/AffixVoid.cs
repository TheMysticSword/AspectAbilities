using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Navigation;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using System.Linq;
using RoR2.CharacterAI;
using Starstorm2;
using Starstorm2.Cores.Elites;
using RoR2.Audio;

namespace TheMysticSword.AspectAbilities
{
    public class AffixVoid : BaseAspectAbility
    {
        public static NetworkSoundEventDef useSound;
        public static NetworkSoundEventDef hitSound;

        public override void OnLoad()
        {
            if (AspectAbilitiesPlugin.starstorm2Loaded)
            {
                On.RoR2.EquipmentCatalog.Init += (orig) =>
                {
                    orig();
                    EquipmentDef affixVoidDef = Starstorm2.Modules.Items.equipmentDefs.FirstOrDefault(x => x.name == "AffixVoid");
                    if (affixVoidDef)
                    {
                        equipmentDef = affixVoidDef;
                        equipmentDef.cooldown = 45f;
                        LanguageManager.appendTokens.Add(equipmentDef.pickupToken);
                    }
                };
                aiMaxDistance = Mathf.Infinity;

                useSound = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
                useSound.eventName = "Play_ui_obj_nullWard_activate";
                AspectAbilitiesContent.Resources.networkSoundEventDefs.Add(useSound);

                hitSound = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
                hitSound.eventName = "Play_nullifier_attack1_root";
                AspectAbilitiesContent.Resources.networkSoundEventDefs.Add(hitSound);
            }
        }

        public override bool OnUse(EquipmentSlot self)
        {
            // temporarily disable the primary skill of each enemy in the bubble

            EntitySoundManager.EmitSoundServer(useSound.index, self.characterBody.gameObject);

            SphereSearch sphereSearch = new SphereSearch
            {
                mask = LayerIndex.entityPrecise.mask,
                origin = self.characterBody.corePosition,
                queryTriggerInteraction = QueryTriggerInteraction.Collide,
                radius = 0f
            };
            TeamMask teamMask = TeamMask.AllExcept(TeamComponent.GetObjectTeam(self.characterBody.gameObject));
            foreach (HurtBox hurtBox in sphereSearch.RefreshCandidates().FilterCandidatesByHurtBoxTeam(teamMask).FilterCandidatesByDistinctHurtBoxEntities().GetHurtBoxes())
            {
                if (hurtBox.healthComponent && hurtBox.healthComponent.body)
                {
                    hurtBox.healthComponent.body.AddTimedBuff(AspectAbilitiesContent.Buffs.StarstormVoidLocked, 8f);
                    EntitySoundManager.EmitSoundServer(hitSound.index, hurtBox.healthComponent.body.gameObject);
                }
            }

            return true;
        }
    }
}
