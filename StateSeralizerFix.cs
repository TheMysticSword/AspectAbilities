using EntityStates;

namespace TheMysticSword.AspectAbilities
{
    public static class StateSeralizerFix
    {
        public static void Init()
        {
            MonoMod.RuntimeDetour.HookGen.HookEndpointManager.Add(typeof(SerializableEntityStateType).GetMethod("set_stateType", AspectAbilitiesPlugin.bindingFlagAll), (SetStateTypeDelegate)SetStateType);
        }

        public static void SetStateType(ref SerializableEntityStateType self, System.Type value)
        {
            self._typeName = value.AssemblyQualifiedName;
        }

        public delegate void SetStateTypeDelegate(ref SerializableEntityStateType self, System.Type value);
    }
}