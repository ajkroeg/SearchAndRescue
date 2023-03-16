using System.Collections.Generic;
using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using BattleTech.Save;
using SVGImporter;
using static SearchAndRescue.Framework.Classes;

namespace SearchAndRescue.Framework
{
    public static class GlobalVars
    {
        public const string SAR_GeneralPrefix = "SAR_";
        public const string SAR_PilotCompanyTagPrefix = "SAR_PILOT_";
        public const string SAR_PilotSimUIDPrefix = "SAR_PilotSimUID_";
        public const string SAR_OpforFaction = "SAR_OpforFaction_";
        public const string SAR_BiomePrefix = "SAR_BIOME_";
        public const string SAR_SystemPrefix = "SAR_SYSTEM_";
        public const string SAR_PortraitSettingsPrefix = "SAR_PortraitSettings_";
        public const string SAR_RecoveryChanceStat = "SAR_RecoveryChanceMult";
        public const string SAR_InjuryChanceStat = "SAR_InjuryChanceMult";
        public const string SAR_ContractGUIDPrefix = "SAR_ContractGUID_";
    }
    public static class ModState
    {
        public static Dictionary<string, MissingPilotInfo> LostPilotsInfo = new Dictionary<string, MissingPilotInfo>();
        public static bool CompleteContractRunOnce = false;
        public static List<string> ContractNames = new List<string>();

        public static string NonProceduralContractGUID = "";

        public static void InitializeIcon()
        {
            DataManager dm = UnityGameInstance.BattleTechGame.DataManager;
            LoadRequest loadRequest = dm.CreateLoadRequest();
            loadRequest.AddLoadRequest<SVGAsset>(BattleTechResourceType.SVGAsset, ModInit.modSettings.ContractTimeoutIcon, null);
            loadRequest.ProcessRequests();
        }

        public static void InitializeMissionNames(this SimGameState sim)
        {
            if (ModState.ContractNames.Count > 0) return;
            ModInit.modLog?.Info?.Write($"[InitializeMissionNames] - Building list of SAR Mission names");

            LoadRequest loadRequest = sim.DataManager.CreateLoadRequest();
            foreach (var contractID in ModInit.modSettings.RecoveryContractIDs)
            {
                loadRequest.AddLoadRequest<ContractOverride>(BattleTechResourceType.ContractOverride, contractID, null);
            }
            loadRequest.ProcessRequests();
            foreach (var contractID in ModInit.modSettings.RecoveryContractIDs)
            {
                if (sim.DataManager.ContractOverrides.TryGet(contractID, out var contractOverride))
                {
                    contractOverride.FullRehydrate();
                    ModState.ContractNames.Add(contractOverride.contractName);
                    ModInit.modLog?.Info?.Write(
                        $"[InitializeMissionNames] - Added {contractOverride.contractName} for ID {contractOverride.ID}");
                }
            }
        }
    }
}
