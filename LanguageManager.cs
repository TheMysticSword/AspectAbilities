using RoR2;
using System.Collections.Generic;

namespace AspectAbilities
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
                result = self.GetLocalizedFormattedStringByToken("ASPECTABILITIES_" + token, result);
            }
            return result;
        }

        public static List<string> appendTokens = new List<string>();
    }
}
