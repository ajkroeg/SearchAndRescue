using System;
using System.Collections.Generic;
using System.Reflection;
using Harmony;
using IRBTModUtils.Logging;
using Newtonsoft.Json;
using Random = System.Random;

namespace SearchAndRescue
{
    public static class ModInit
    {
        internal static DeferringLogger modLog;
        private static string modDir;
        public static readonly Random Random = new Random();

        internal static Settings modSettings;
        public const string HarmonyPackage = "us.tbone.SearchAndRescue";
        public static void Init(string directory, string settings)
        {

            modDir = directory;
            Exception settingsException = null;
            try
            {
                modSettings = JsonConvert.DeserializeObject<Settings>(settings);
            }
            catch (Exception ex)
            {
                settingsException = ex;
                modSettings = new Settings();
            }
            //HarmonyInstance.DEBUG = true;
            modLog = new DeferringLogger(modDir, "SAR", modSettings.enableDebug, modSettings.enableTrace);
            if (settingsException != null)
            {
                ModInit.modLog?.Error?.Write($"EXCEPTION while reading settings file! Error was: {settingsException}");
            }

            ModInit.modLog?.Info?.Write($"Initializing Search And Rescue - Version {typeof(Settings).Assembly.GetName().Version}");
            var harmony = HarmonyInstance.Create(HarmonyPackage);
            //FileLog.Log(HarmonyPackage);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            //dump settings
            ModInit.modLog?.Info?.Write($"Settings dump: {settings}");
        }
    }
    class Settings
    {
        public bool enableDebug = false;
        public bool enableTrace = false;

        public float BasePilotRecoveryChance = 0.5f;
        public float FriendlyTerritoryRecoveryMult = 1.2f;
        public float InjureOnEjectChance = 0f;
        public bool InjureIgnoreBonusHealth = false;
        
        public string ContractTimeoutIcon = "";
        public List<string> AlwaysRecoverContractIDs = new List<string>();
        public List<string> RecoveryContractIDs = new List<string>(); //should be rescue contracts.
    }
}