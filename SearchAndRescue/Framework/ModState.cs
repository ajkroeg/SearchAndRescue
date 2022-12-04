using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BattleTech;
using BattleTech.Data;
using SVGImporter;
using static SearchAndRescue.Framework.Classes;

namespace SearchAndRescue.Framework
{
    public static class GlobalVars
    {
        public const string SAR_PilotCompanyTagPrefix = "SAR_PILOT_";
        public const string SAR_PilotSimUIDPrefix = "SAR_PilotSimUID_";
        public const string SAR_BiomePrefix = "SAR_BIOME_";
        public const string SAR_SystemPrefix = "SAR_SYSTEM_";
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
