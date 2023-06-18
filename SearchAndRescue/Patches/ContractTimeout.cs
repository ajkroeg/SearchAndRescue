using BattleTech;
using System;
using SearchAndRescue.Framework;
using Contract = BattleTech.Contract;

namespace SearchAndRescue.Patches
{
    public class ContractTimeout
    {
        [HarmonyPatch(typeof(Contract), "OnDayPassed", new Type[] { })]
        public static class Contract_OnDayPassed
        {
            static bool Prepare() => true;

            public static void Postfix(Contract __instance, ref bool __result)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim == null) return;
                //var contractWidget = sim.RoomManager.CmdCenterRoom.contractsWidget;//Traverse.Create(sim.RoomManager.CmdCenterRoom).Field("contractsWidget").GetValue<SGContractsWidget>();
                if (__result)
                {
                    var toRemove = "";
                    var removePilotName = "";
                    foreach (var lostPilotInfo in ModState.LostPilotsInfo)
                    {
                        if (lostPilotInfo.Value.RecoveryContractGUID == __instance.GUID) //&& __instance.ContractBiome == lostPilotInfo.Value.PilotBiomeSkin) remove biome stuff, too hard to ensure accuracy
                        {
                            toRemove = lostPilotInfo.Key;
                            removePilotName = lostPilotInfo.Value.MissingPilotDef.Description.Callsign;
                            var pilotDef = lostPilotInfo.Value.MissingPilotDef;
                            //var biomeTag = $"{GlobalVars.SAR_BiomePrefix}{lostPilotInfo.Value.PilotBiomeSkin}";
                            var systemTag = $"{GlobalVars.SAR_SystemPrefix}{lostPilotInfo.Value.MissingPilotSystem}";
                            var pilotUIDTag = $"{GlobalVars.SAR_PilotSimUIDPrefix}{lostPilotInfo.Value.PilotSimUID}";
                            //pilotDef.PilotTags.Add(biomeTag);
                            pilotDef.PilotTags.Add(systemTag);
                            pilotDef.PilotTags.Add(pilotUIDTag);
                            var pilotSon = pilotDef.ToJSON();
                            var pilotTag = GlobalVars.SAR_PilotCompanyTagPrefix + pilotSon;
                            pilotDef.DataManager = sim.DataManager;
                            var pilot = new Pilot(pilotDef, lostPilotInfo.Value.PilotSimUID, true);
                            sim.KillMissingPilot(pilot, lostPilotInfo.Value);
                            ModInit.modLog?.Info?.Write($"[Contract_OnDayPassed] - created tag for removal from company.");
                            sim.CompanyTags.Remove(pilotTag);
                            break;
                        }
                    }
                    ModState.LostPilotsInfo.Remove(toRemove);//what happens if more than one pilot contract expires? should be ok, since each contract is refreshing separately here
                    ModInit.modLog?.Info?.Write($"[Contract_OnDayPassed] - removed {toRemove} from missing pilot state.");

                    if (!string.IsNullOrEmpty(toRemove))
                    {
                        sim.interruptQueue.QueuePauseNotification("Pilot Rescue EXPIRED", $"The window for recovery has passed for {removePilotName}. Another name for the wall.",
                            sim.GetCrewPortrait(SimGameCrew.Crew_Darius), "", null, "Continue", null, null);
                        //                   return;
                    }
                }
            }
        }
    }
}
