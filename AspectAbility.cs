using RoR2;
using UnityEngine;

namespace AspectAbilities
{
    [CreateAssetMenu(fileName = "NewAspectAbility.asset", menuName = "Aspect Ability")]
    public class AspectAbility : ScriptableObject
    {
        [Tooltip("Aspect EquipmentDef associated with this ability")]
        public EquipmentDef equipmentDef;

        [Tooltip("AI-controlled bodies can use this aspect when the distance to their target is less than or equal to this (in meters)")]
        [Range(0f, 1000f)]
        public float aiMaxUseDistance = 60f;

        [Tooltip("AI-controlled bodies can use this aspect when their health fraction is less than or equal to this amount (between 0-1)")]
        [Range(0f, 1f)]
        public float aiMaxUseHealthFraction = 0.5f;
    }
}
