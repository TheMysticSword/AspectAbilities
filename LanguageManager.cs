using R2API;
using RoR2;
using System.Collections.Generic;

namespace TheMysticSword.AspectAbilities
{
    public class LanguageManager
    {
        public static void Init()
        {
            // append extra strings to aspect equipment (check .language)
            On.RoR2.Language.LoadStrings += (orig, self) =>
            {
                orig(self);
                foreach (KeyValuePair<EquipmentIndex, Dictionary<string, string>> keyValuePair in aspectAbilityStringTokens)
                {
                    OverlayEquipmentString(self, keyValuePair.Key, "pickup");
                }
            };
            // the .language strings get loaded after the hook above, so ASPECTABILITIES_AFFIXNAME_PICKUP gets appended instead
            // to fix the issue, we'll save the loaded .language strings and reload the language on the next update call
            Language.onCurrentLanguageChanged += () =>
            {
                if (Language.currentLanguage != null)
                {
                    foreach (KeyValuePair<EquipmentIndex, Dictionary<string, string>> keyValuePair in aspectAbilityStringTokens)
                    {
                        SaveEquipmentString(Language.currentLanguage, keyValuePair.Key, "pickup");
                    }
                }
            };
        }

        public static void Update()
        {
            if (reloadLanguage)
            {
                reloadLanguage = false;
                Language.CCLanguageReload(new ConCommandArgs());
            }
        }

        internal static Dictionary<EquipmentIndex, Dictionary<string, string>> aspectAbilityStringTokens = new Dictionary<EquipmentIndex, Dictionary<string, string>>();
        internal static bool reloadLanguage = false;
        internal static void SaveEquipmentString(Language language, EquipmentIndex equipmentIndex, string token)
        {
            if (aspectAbilityStringTokens.ContainsKey(equipmentIndex))
            {
                if (!aspectAbilityStringTokens[equipmentIndex].ContainsKey(token + "_" + language.name))
                {
                    aspectAbilityStringTokens[equipmentIndex].Add(token + "_" + language.name, language.GetLocalizedStringByToken("ASPECTABILITIES_" + equipmentIndex.ToString().ToUpper() + "_" + token.ToUpper()));
                    reloadLanguage = true;
                }
            }
        }
        internal static void OverlayEquipmentString(Language language, EquipmentIndex equipmentIndex, string token)
        {
            if (aspectAbilityStringTokens.ContainsKey(equipmentIndex) && aspectAbilityStringTokens[equipmentIndex].TryGetValue(token + "_" + language.name, out string newValue))
            {
                string oldValue = language.GetLocalizedStringByToken("EQUIPMENT_" + equipmentIndex.ToString().ToUpper() + "_" + token.ToUpper());
                LanguageAPI.Add(
                    "EQUIPMENT_" + equipmentIndex.ToString().ToUpper() + "_" + token.ToUpper(),
                    oldValue + " " + newValue,
                    language.name
                );
            }
        }
    }
}
