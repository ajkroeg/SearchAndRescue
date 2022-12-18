using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BattleTech.Framework;
using BattleTech.UI;
using BattleTech.UI.Tooltips;
using SVGImporter;
using UnityEngine;
using UnityEngine.UI;
using SearchAndRescue.Framework;
using System.Diagnostics.Contracts;
using BattleTech.Serialization.Handlers;
using Contract = BattleTech.Contract;

namespace SearchAndRescue.Patches
{
    public class ContractTimeout
    {
        [HarmonyPatch(typeof(SimGameState), "OnDayPassed", new Type[] {typeof(int)})]
        public static class SimGameState_OnDayPassed
        {
            static bool Prepare() => true;

            public static void Postfix(SimGameState __instance, int timeLapse)
            {
                for (int i = __instance.CurSystem.SystemContracts.Count - 1; i >= 0; i--)
                {
                    if (__instance.CurSystem.SystemContracts[i].OnDayPassed())
                    {
                        __instance.CurSystem.SystemContracts.RemoveAt(i);
                    }
                }
                for (int i = __instance.CurSystem.SystemBreadcrumbs.Count - 1; i >= 0; i--)
                {
                    if (__instance.CurSystem.SystemBreadcrumbs[i].OnDayPassed())
                    {
                        __instance.CurSystem.SystemBreadcrumbs.RemoveAt(i);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Contract), "OnDayPassed", new Type[] { })]
        public static class Contract_OnDayPassed
        {
            static bool Prepare() => true;

            public static void Postfix(Contract __instance, ref bool __result)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim == null) return;
                var contractWidget = sim.RoomManager.CmdCenterRoom.contractsWidget;//Traverse.Create(sim.RoomManager.CmdCenterRoom).Field("contractsWidget").GetValue<SGContractsWidget>();
                if (__result)
                {
                    var toRemove = "";
                    var removePilotName = "";
                    foreach (var lostPilotInfo in ModState.LostPilotsInfo)
                    {
                        if (lostPilotInfo.Value.MissingPilotSystem == __instance.TargetSystem)
                        {
                            toRemove = lostPilotInfo.Key;
                            removePilotName = lostPilotInfo.Value.MissingPilotDef.Description.Callsign;
                            var pilotDef = lostPilotInfo.Value.MissingPilotDef;
                            var biomeTag = $"{GlobalVars.SAR_BiomePrefix}{lostPilotInfo.Value.PilotBiomeSkin}";
                            var systemTag = $"{GlobalVars.SAR_SystemPrefix}{lostPilotInfo.Value.MissingPilotSystem}";
                            var pilotUIDTag = $"{GlobalVars.SAR_PilotSimUIDPrefix}{lostPilotInfo.Value.PilotSimUID}";
                            pilotDef.PilotTags.Add(biomeTag);
                            pilotDef.PilotTags.Add(systemTag);
                            pilotDef.PilotTags.Add(pilotUIDTag);
                            var pilotSon = pilotDef.ToJSON();
                            var pilotTag = GlobalVars.SAR_PilotCompanyTagPrefix + pilotSon;
                            pilotDef.SetRecentInjuryDamageType(DamageType.Unknown);
                            pilotDef.SetDiedInSystemID(lostPilotInfo.Value.MissingPilotSystem);
                            var pilot = new Pilot(pilotDef, lostPilotInfo.Value.PilotSimUID, true);
                            sim.Graveyard.Add(pilot);

                            ModInit.modLog?.Info?.Write($"[Contract_OnDayPassed] - created tag for removal from company.");
                            sim.CompanyTags.Remove(pilotTag);
                            break;
                        }
                    }
                    ModState.LostPilotsInfo.Remove(toRemove);
                    ModInit.modLog?.Info?.Write($"[Contract_OnDayPassed] - removed {toRemove} from missing pilot state.");

                    contractWidget.ListContracts(sim.GetAllCurrentlySelectableContracts(false), null);

                    sim.interruptQueue.QueuePauseNotification("Pilot Rescue EXPIRED", $"The window for recovery has passed for {removePilotName}. Another name for the wall.",
                            sim.GetCrewPortrait(SimGameCrew.Crew_Darius), "", null, "Continue", null, null);
 //                   return;
                }

                var listedContracts = contractWidget.listedContracts;//Traverse.Create(contractWidget).Field("listedContracts").GetValue<List<SGContractsListItem>>();
                foreach (var contractElement in listedContracts)
                {
                    if (__instance.UsingExpiration)
                    {
                        var expirationElement = contractElement.expirationElement;//Traverse.Create(contractElement).Field("expirationElement").GetValue<GameObject>();
                        if (expirationElement != null)
                        {
                            var tooltipComponent = expirationElement.gameObject.GetComponent<HBSTooltip>();
                            var title = $"Time-Limited Contract!";
                            var details = $"This contract will expire in {contractElement.Contract.ExpirationTime} days!";
                            BaseDescriptionDef def =
                                new BaseDescriptionDef("ContractExpirationData", title, details, null);
                            tooltipComponent.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(def));
                            expirationElement.gameObject.SetActive(true);
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Contract), MethodType.Constructor,
            new Type[] {typeof(string), typeof(string), typeof(string), typeof(ContractTypeValue), typeof(GameInstance), typeof(ContractOverride), typeof(GameContext), typeof(bool), typeof(int), typeof(int), typeof(int?)})]
        public static class Contract_Constructor_Long
        {
            public static void Postfix(Contract __instance, string mapName, string mapPath, string encounterLayerGuid, ContractTypeValue contractTypeValue, GameInstance game, ContractOverride contractOverride, GameContext baseContext, bool fromSim = false, int difficulty = -1, int initialContractValue = 0, int? playerOneMoraleOverride = null)
            {
                if (contractOverride.usesExpiration && contractOverride.expirationTimeOverride > 0)
                {
                    __instance.SetExpiration(contractOverride.expirationTimeOverride);
                }
            }
        }

        [HarmonyPatch(typeof(SGContractsListItem), "Init", new Type[]{ typeof(Contract), typeof(SimGameState)})]
        public static class SGContractsListItem_Postfix
        {
            public static void Postfix(SGContractsListItem __instance, Contract contract, SimGameState sim)
            {
                if (contract.UsingExpiration && __instance.expirationElement == null)

                {
                    var timeLimitIcon =
                        UnityEngine.Object.Instantiate<GameObject>(__instance.travelIndicator,
                            __instance.travelIndicator.transform.parent);
                    var timeLimitRect = timeLimitIcon.gameObject.GetComponent<RectTransform>();
                    timeLimitRect.anchoredPosition = __instance.travelIndicator.activeSelf
                        ? new Vector2(-50f, -8.5f)
                        : new Vector2(-20f, -8.5f);

                    var iconComponent = timeLimitIcon.gameObject.GetComponent<SVGImage>();
                    var asset = UnityGameInstance.BattleTechGame.DataManager.GetObjectOfType<SVGAsset>(
                        ModInit.modSettings.ContractTimeoutIcon, BattleTechResourceType.SVGAsset);
                    iconComponent.vectorGraphics = asset;

                    var tooltipComponent = timeLimitIcon.gameObject.GetComponent<HBSTooltip>();
                    var title = $"Time-Limited Contract!";
                    var details = $"This contract will expire in {contract.ExpirationTime} days!";
                    BaseDescriptionDef def = new BaseDescriptionDef("ContractExpirationData", title, details, null);
                    tooltipComponent.SetDefaultStateData(TooltipUtilities.GetStateDataFromObject(def));
                    __instance.expirationElement = timeLimitIcon;
                    __instance.expirationElement.gameObject.SetActive(true);


                    //add work order entry?
 //                   var entry = new Classes.WorkOrderEntry_Notification_Timed($"{contract.Override.ID}_TimeLeft", $"Time-limited contract!", contract.ExpirationTime);
 //                   sim.RoomManager.AddWorkQueueEntry(entry);
                }
            }
        }

        [HarmonyPatch(typeof(TaskTimelineWidget), "AddEntry", new Type[] {typeof(WorkOrderEntry), typeof(bool)})]
        public static class TaskTimelineWidget_AddEntry
        {
            static bool Prepare() => true;
            public static bool Prefix(TaskTimelineWidget __instance, WorkOrderEntry entry, bool sortEntries = true)
            {
                if (__instance.ActiveItems.Keys.All(x => x is Classes.WorkOrderEntry_Notification_Timed && x.ID == entry.ID))
                    return false;
                return true;
            }
        }
    }
}
