using RoR2;
using R2API;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using TheMysticSword.AspectAbilities.ContentManagement;

namespace TheMysticSword.AspectAbilities.Buffs
{
    public abstract class BaseBuff : BaseLoadableAsset
    {
        public BuffDef buffDef;

        public override void Load()
        {
            buffDef = ScriptableObject.CreateInstance<BuffDef>();
            OnLoad();

            asset = buffDef;
        }
    }
}
