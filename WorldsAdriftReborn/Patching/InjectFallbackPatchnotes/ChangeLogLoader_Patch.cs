using Bossa.Travellers.UI;
using HarmonyLib;

namespace WorldsAdriftReborn.Patching.Dynamic.InjectFallbackPatchnotes
{
    [HarmonyPatch(typeof(ChangeLogLoader))]
    internal class CustomChangeLogPatch
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ChangeLogLoader), "Start")]
        public static bool Start_Prefix(ChangeLogLoader __instance)
        {
            var patchNote1 = "<size=14>Worlds Adrift: Reborn | by sp00ktober</size><color=#bf9d82></color><size=11>30.08.2022</size>\nInitial project stages.\nEarly groundwork and experiments to bring Worlds Adrift back.";
            var patchNote2 = "<size=14>Worlds Adrift: Reborn | 01.01.2024</size><color=#bf9d82></color><size=11>Basic mod release that contains:</size>\nAbility to load into the game on an island\nSpawn as your chosen character and equip a glider\nGlider has infinite energy\n<size=11>GitHub: https://github.com/WAReborn/WorldsAdriftReborn</size>";
            var patchNote3 = "<size=14>Worlds Adrift: Offline v1.0.0a | 25.12.2025</size><color=#bf9d82></color><size=11>A fork of Worlds Adrift: Reborn that aims to provide a long-term solution for a working offline experience. This release contains the following improvements:</size>\nAll islands and weather walls added in their original positions from Update 31\nCustom installer added\nNoclip keybind added (P)\nPlayable completely offline.";
            var patchNote4 = "<size=14>Worlds Adrift: Offline v1.0.0 | TBA</size><color=#bf9d82></color><size=11>Planned:</size>\nAll databanks\nTrees\nWorld edges\nAncient Respawners";

            var parser = AccessTools.Method(typeof(ChangeLogLoader), "ParsePatchNotes");
            
            parser.Invoke(__instance, new object[]
            {
                // Notes that come first appear at the top
                patchNote4 + patchNote3 + patchNote2 + patchNote1
            });
            return false;
        }
    }
}
