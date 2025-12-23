using System.Reflection;
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
        private static readonly MethodInfo CheckMethod = AccessTools.Method(typeof(PlayerNoClipController), "CheckForNoClip");
        private static bool _pWasDown;

        [HarmonyPostfix, UsedImplicitly]
        public static void Postfix( PlayerNoClipController __instance, ref bool ____pureNoClipToggle )
        {
            bool pDown = Input.GetKey(KeyCode.P);
            if (!pDown || _pWasDown)
            {
                _pWasDown = pDown;
                return;
            }

            _pWasDown = true;
            ____pureNoClipToggle = !____pureNoClipToggle;
            CheckMethod.Invoke(__instance, null);

            OSDMessage.SendMessage("No clip set to: " + (____pureNoClipToggle ? "True" : "False"));
        }
    }
}
