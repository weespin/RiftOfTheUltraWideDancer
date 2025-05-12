using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace UltrawideDancer
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class UltraWideDancerPlugin : BaseUnityPlugin
    {
        private static BepInEx.Logging.ManualLogSource logger;
        public static float currentAspectRatio;

        private void Awake()
        {
            logger = Logger;
            logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            UpdateInitialAspectRatio();

            Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
            harmony.Patch(
                typeof(Screen).GetMethod("SetResolution", new[] { typeof(int), typeof(int), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(UltraWideDancerPlugin).GetMethod(nameof(OnResolutionChanged),
                    BindingFlags.Static | BindingFlags.NonPublic))
            );
        }

        private static void UpdateInitialAspectRatio()
        {
            currentAspectRatio = (float)Screen.width / (float)Screen.height;
            logger.LogInfo($"Aspect ratio updated to: {currentAspectRatio}");
        }

        private static void OnResolutionChanged(int width, int height, bool fullscreen)
        {
            currentAspectRatio = (float)width / (float)height;
            logger.LogInfo($"Resolution changed to {width}x{height}, new aspect ratio: {currentAspectRatio}");
        }

        public static float CurrentAspectRatio => currentAspectRatio;
    }
    [HarmonyPatch]
    public class AspectRatioPatches
    {
        private static BepInEx.Logging.ManualLogSource Log;

        static AspectRatioPatches()
        {
            Log = BepInEx.Logging.Logger.CreateLogSource("UltrawideDancer");
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            var methods = new[]
            {
                AccessTools.Method("ContentAspectPreserver:Update"),
                AccessTools.Method("Shared.Camera.RiftCameraController:UpdateCameraAspectRatio"),
                AccessTools.Method("ScreenContainer:Update"),
                AccessTools.Method("AspectRatioEnforcer:Start")
            };

            foreach (var method in methods)
            {
                if (method != null)
                {
                    Log.LogInfo($"Found method to patch: {method.DeclaringType.Name}::{method.Name}");
                }
                else
                {
                    Log.LogError("Failed to find a target method!");
                }
            }

            return methods;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var instructionList = new List<CodeInstruction>(instructions);
            int replacementCount = 0;

            foreach (var instruction in instructionList)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 &&
                    instruction.operand?.GetType() == typeof(float) &&
                    Mathf.Approximately((float)instruction.operand, 1.7777778f))
                {
                    replacementCount++;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(UltraWideDancerPlugin), nameof(UltraWideDancerPlugin.CurrentAspectRatio)));
                }
                else
                {
                    yield return instruction;
                }
            }

            Log.LogInfo($"{__originalMethod.DeclaringType.Name}::{__originalMethod.Name} -> Patched {replacementCount} instructions");
        }
    }
}