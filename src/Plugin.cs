using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using StorageVision.Patches;
using UnityEngine;

namespace StorageVision
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "no.hakon.storagevision";
        public const string PluginName = "Storage Vision";
        public const string PluginVersion = "0.1.2";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private GameObject _runnerObject;

        private void Awake()
        {
            Log = Logger;
            ModConfig.Bind(Config);

            _harmony = new Harmony(PluginGuid);
            ApplyPatch(typeof(PinpointShelfPatch));

            // Own persistent object, so we do not depend on the BepInEx manager object surviving scene loads.
            _runnerObject = new GameObject("StorageVision_Runner");
            DontDestroyOnLoad(_runnerObject);
            _runnerObject.hideFlags = HideFlags.HideAndDontSave;
            _runnerObject.AddComponent<Runner>();
            _runnerObject.AddComponent<Core.ConfigMenu>();

            Log.LogInfo($"{PluginName} {PluginVersion} lastet");
        }

        private void ApplyPatch(Type patchClass)
        {
            try
            {
                _harmony.CreateClassProcessor(patchClass).Patch();
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to apply patch {patchClass.Name} (game update?): {e}");
            }
        }
    }
}
