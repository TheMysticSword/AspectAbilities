using UnityEngine;
using RoR2;
using TheMysticSword.AspectAbilities.ContentManagement;
using System.Collections.Generic;

namespace TheMysticSword.AspectAbilities
{
    public abstract class BaseAspectAbility : BaseLoadableAsset
    {
        public EquipmentDef equipmentDef;
        public float aiMaxDistance = 60f;
        public abstract bool OnUse(EquipmentSlot self);

        public class AspectAbilitiesDefaultAbilityComponent : MonoBehaviour
        {
            public TemporaryVisualEffect tempEffect;
        }

        public static AspectAbilitiesDefaultAbilityComponent GetDefaultComponent(GameObject gameObject)
        {
            AspectAbilitiesDefaultAbilityComponent component = gameObject.GetComponent<AspectAbilitiesDefaultAbilityComponent>();
            if (!component) component = gameObject.AddComponent<AspectAbilitiesDefaultAbilityComponent>();
            return component;
        }
    }
}
