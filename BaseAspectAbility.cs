using UnityEngine;
using RoR2;
using TheMysticSword.AspectAbilities.ContentManagement;

namespace TheMysticSword.AspectAbilities
{
    public abstract class BaseAspectAbility : BaseLoadableAsset
    {
        public EquipmentDef equipmentDef;
        public float aiMaxDistance = 60f;
        public abstract bool OnUse(EquipmentSlot self);
    }
}
