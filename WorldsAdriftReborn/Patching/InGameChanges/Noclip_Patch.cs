using Bossa.Travellers.DevConsole;
using HarmonyLib;
using JetBrains.Annotations;
using Travellers.UI.HUDMessaging;
using UnityEngine;

namespace WorldsAdriftReborn.Patching.InGameChanges
{
    [HarmonyPatch(typeof(PlayerNoClipController), "FixedUpdate")]
    public static class Noclip_Patch
    {
        private static bool _pWasDown;

        [HarmonyPostfix, UsedImplicitly]
        public static void Postfix( PlayerNoClipController __instance )
        {
            bool pDown = Input.GetKey(KeyCode.P);
            if (!pDown || _pWasDown)
            {
                _pWasDown = pDown;
                return;
            }

            _pWasDown = true;
            var newValue = !(bool)AccessTools.Field(typeof(PlayerNoClipController), "_pureNoClipToggle")
                                           .GetValue(__instance);

            // Toggle noclip
            AccessTools.Field(typeof(PlayerNoClipController), "_pureNoClipToggle")
                       .SetValue(__instance,
                           newValue);

            // Apply immediately
            AccessTools.Method(typeof(PlayerNoClipController), "CheckForNoClip")
                       .Invoke(__instance, null);

            OSDMessage.SendMessage("No clip set to: " + (newValue ? "True" : "False"));
        }
    }
}
