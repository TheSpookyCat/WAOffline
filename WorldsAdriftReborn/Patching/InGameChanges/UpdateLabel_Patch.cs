using System;
using HarmonyLib;
using JetBrains.Annotations;
using Travellers.UI.DebugDisplay;

namespace WorldsAdriftReborn.Patching.InGameChanges
{
    [HarmonyPatch(typeof(ProductionBuildState), "CreateBuildNumber")]
    public static class UpdateLabel_Patch
    {
        private const string VERSION = "v1.0.0a";
        
        [HarmonyPrefix, UsedImplicitly]
        public static bool Override( ref string __result )
        {
            __result = $"WA: Offline {VERSION}{Environment.NewLine}";
            return false;
        }
    }
}
