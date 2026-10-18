using System;
using HarmonyLib;

namespace StorageVision.Patches
{
    /// <summary>
    /// Optionally suppresses the game's arrows above shelves while picking online orders.
    /// The arrows are purely visual, so skipping their generation has no gameplay effect.
    /// </summary>
    [HarmonyPatch(typeof(PinpointShelfFromTray), "GeneratePinpointers")]
    internal static class PinpointShelfPatch
    {
        private static bool Prefix(PinpointShelfFromTray __instance)
        {
            try
            {
                if (!ModConfig.HideOrderArrows.Value)
                {
                    return true;
                }
                // Hide arrows that may already be visible, but keep currentProductID so our order target still works.
                int productId = __instance.currentProductID;
                __instance.DeactivatePinpointers();
                __instance.currentProductID = productId;
                return false;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"PinpointShelfPatch failed, falling back to vanilla: {e}");
                return true;
            }
        }
    }
}
