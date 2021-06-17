using UnityEngine;
using RoR2;
using MysticsRisky2Utils.ContentManagement;
using System.Collections.Generic;

namespace AspectAbilities
{
    public abstract class BaseAspectAbilityOverride : BaseLoadableAsset
    {
        public override string TokenPrefix => AspectAbilitiesPlugin.TokenPrefix;

        public AspectAbility aspectAbility;

        public override void Load()
        {
            aspectAbility = new AspectAbility
            {
                autoAppendedToken = true
            };
            OnLoad();
            asset = aspectAbility;
        }

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
