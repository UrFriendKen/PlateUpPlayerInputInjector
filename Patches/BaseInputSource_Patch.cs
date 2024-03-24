using Controllers;
using HarmonyLib;

namespace KitchenPlayerInputInjector.Patches
{
    [HarmonyPatch]
    static class BaseInputSource_Patch
    {
        [HarmonyPatch(typeof(InputSource), "GetData")]
        [HarmonyPrefix]
        static bool TriggerInputUpdate_Prefix(ref InputState __result)
        {
            return !PlaybackSystem.TryGetInputState(out __result);
        }
    }
}
