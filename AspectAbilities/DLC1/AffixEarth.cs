using UnityEngine;
using UnityEngine.Networking;
using RoR2;
using RoR2.Navigation;
using R2API.Networking;
using R2API.Networking.Interfaces;
using R2API.Utils;
using System.Linq;
using RoR2.CharacterAI;
using System.Collections.Generic;
using MysticsRisky2Utils;
using UnityEngine.AddressableAssets;

namespace AspectAbilities
{
    public class AffixEarth : BaseAspectAbilityOverride
    {
        public static ConfigOptions.ConfigurableValue<float> duration = ConfigOptions.ConfigurableValue.CreateFloat(
            AspectAbilitiesPlugin.PluginGUID,
            AspectAbilitiesPlugin.PluginName,
            AspectAbilitiesPlugin.config,
            "Mending (DLC1)",
            "Duration",
            4f,
            0f,
            60f,
            "How long should the buff last (in seconds)",
            useDefaultValueConfigEntry: AspectAbilitiesPlugin.ignoreBalanceChanges.bepinexConfigEntry
        );

        public override void OnLoad()
        {
            EquipmentCatalog.availability.CallWhenAvailable(() => Setup("Mending (DLC1)", Addressables.LoadAssetAsync<EquipmentDef>("RoR2/DLC1/EliteEarth/EliteEarthEquipment.asset").WaitForCompletion(), 30f, 1000f));

            aspectAbility.onUseOverride = (self) =>
            {
                if (self.characterBody)
                {
                    self.characterBody.AddTimedBuff(AspectAbilitiesContent.Buffs.AspectAbilities_EarthRegen, duration);
                }
                return true;
            };
        }

        public static AspectAbilitiesAffixEarth GetAffixComponent(GameObject obj)
        {
            AspectAbilitiesAffixEarth component = obj.GetComponent<AspectAbilitiesAffixEarth>();
            if (!component) component = obj.AddComponent<AspectAbilitiesAffixEarth>();
            return component;
        }

        public class AspectAbilitiesAffixEarth : MonoBehaviour
        {
            public TemporaryVisualEffect temporaryVisualEffect;
        }
    }
}
