using System.Collections.Generic;
using BattleTech;
using BattleTech.Data;
using SVGImporter;
using static SearchAndRescue.Framework.Classes;

namespace SearchAndRescue.Framework
{
    public static class GlobalVars
    {
        public const string SAR_GeneralPrefix = "SAR_";
        public const string SAR_PilotCompanyTagPrefix = "SAR_PILOT_";
        public const string SAR_PilotSimUIDPrefix = "SAR_PilotSimUID_";
        public const string SAR_BiomePrefix = "SAR_BIOME_";
        public const string SAR_SystemPrefix = "SAR_SYSTEM_";
        public const string SAR_PortraitSettingsPrefix = "SAR_PortraitSettings_";
        public const string SAR_RecoveryChanceStat = "SAR_RecoveryChanceMult";
        public const string SAR_InjuryChanceStat = "SAR_InjuryChanceMult";
    }
    public static class ModState
    {
        public static Dictionary<string, MissingPilotInfo> LostPilotsInfo = new Dictionary<string, MissingPilotInfo>();
        public static bool CompleteContractRunOnce = false;
        public static void InitializeIcon()
        {
            DataManager dm = UnityGameInstance.BattleTechGame.DataManager;
            LoadRequest loadRequest = dm.CreateLoadRequest();
            loadRequest.AddLoadRequest<SVGAsset>(BattleTechResourceType.SVGAsset, ModInit.modSettings.ContractTimeoutIcon, null);
            loadRequest.ProcessRequests();
        }
    }
}
