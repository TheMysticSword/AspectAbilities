using UnityEngine;

namespace AspectAbilities.Buffs
{
    public abstract class BaseBuff : MysticsRisky2Utils.BaseAssetTypes.BaseBuff
    {
        public override string TokenPrefix => AspectAbilitiesPlugin.TokenPrefix;
    }
}
