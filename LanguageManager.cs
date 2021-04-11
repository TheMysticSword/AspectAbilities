using R2API;
using RoR2;
using System.Collections.Generic;

namespace TheMysticSword.AspectAbilities
{
    public class LanguageManager
    {
        public static void Init()
        {
            On.RoR2.Language.GetLocalizedStringByToken += Language_GetLocalizedStringByToken;
        }

        public static string Language_GetLocalizedStringByToken(On.RoR2.Language.orig_GetLocalizedStringByToken orig, Language self, string token)
        {
            string result = orig(self, token);
            if (appendTokens.Contains(token))
            {
                result = string.Format("{0} {1}", result, self.GetLocalizedStringByToken("ASPECTABILITIES_" + token));
            }
            return result;
        }

        public static List<string> appendTokens = new List<string>();
    }
}
