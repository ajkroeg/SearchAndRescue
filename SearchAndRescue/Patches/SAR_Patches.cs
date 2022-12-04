using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BattleTech.Save;
using BattleTech.Save.Test;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using IRBTModUtils;
using SearchAndRescue.Framework;
using UnityEngine;
using UnityEngine.UI;
using ModState = SearchAndRescue.Framework.ModState;
using Org.BouncyCastle.Utilities;
using static SearchAndRescue.Framework.Classes;
using FluffyUnderware.Curvy.Generator;

namespace SearchAndRescue
{
    public class SAR_Patches
    {
        [HarmonyPatch(typeof(AbstractActor), "InitEffectStats", new Type[] {})]
        public static class AbstractActor_InitEffectStats
        {
            public static void Postfix(AbstractActor __instance)
            {
                __instance.StatCollection.AddStatistic<float>("SAR_RecoveryChanceMult", 1f);
            }
        }

        [HarmonyPatch(typeof(AbstractActor), "EjectPilot", new Type[] {typeof(string), typeof(int), typeof(DeathMethod), typeof(bool)})]
        public static class AbstractActor_EjectPilot
        {
            public static void Postfix(AbstractActor __instance, string sourceID, int stackItemID, DeathMethod deathMethod, bool isSilent)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim == null) return;
                if (ModInit.modSettings.AlwaysRecoverContractIDs.Contains(__instance.Combat.ActiveContract.Override.ID) || ModInit.modSettings.AlwaysRecoverContractIDs.Contains(__instance.Combat.ActiveContract.Override.ContractTypeValue.Name)) return;
                var pilotDef = __instance.GetPilot().pilotDef;
                if (sim.PilotRoster.Any(x=>x.pilotDef.Description.Id == pilotDef.Description.Id))
                {
                    if (!__instance.IsPilotRecovered())
                    {
                        ModState.CompleteContractRunOnce = false;
                        var biome = __instance.Combat.ActiveContract.ContractBiome;
                        var missingPilotInfo = new Classes.MissingPilotInfo(pilotDef, __instance.GetPilot().GUID, sim.CurSystem.ID, biome);
                        ModState.LostPilotsInfo.Add(pilotDef.Description.Id, missingPilotInfo);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class AAR_UnitStatusWidget_FillInPilotData
        {
            public static void Postfix(AAR_UnitStatusWidget __instance, int xpEarned, UnitResult ___UnitData, ref GameObject ___SimGamePilotDataOverlay)
            {
                if (ModState.LostPilotsInfo.ContainsKey(___UnitData.pilot.pilotDef.Description.Id))
                {
                   
                    //no . doi sim pilot overlay,injuries horiz layout group,lopcalizable text

                    var injuryGroupComponent =
                        ___SimGamePilotDataOverlay.gameObject.GetComponentInChildren<HorizontalLayoutGroup>();
                    if (injuryGroupComponent != null)
                    {
                        var localizeText = injuryGroupComponent.gameObject.GetComponentInChildren<LocalizableText>();
                        localizeText.SetText("MISSING IN ACTION");
                    }
                    ___SimGamePilotDataOverlay.SetActive(true);

                }
            }
        }

        [HarmonyPatch(typeof(Contract), "CompleteContract", new Type[] { typeof(MissionResult), typeof(bool)})]
        public static class Contract_CompleteContract
        {
            static bool Prepare() => true; //enabled
            public static void Postfix(Contract __instance)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim == null) return;
                if (ModState.CompleteContractRunOnce == true) return;
                ModState.CompleteContractRunOnce = true;
                var biomes = new List<Biome.BIOMESKIN>();
                var selfFaction = Utils.GetFactionValueFromString("SelfEmployed");
                var targetFaction =
                    __instance.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5");
                ModInit.modLog?.Info?.Write(
                    $"[Contract_CompleteContract] - dump lost pilots: keys: {string.Join(", ", ModState.LostPilotsInfo.Keys)}\n{string.Join(", ", ModState.LostPilotsInfo.Values)}" );
                foreach (UnitResult unitResult in __instance.PlayerUnitResults)
                {
                    if (ModState.LostPilotsInfo.ContainsKey(unitResult.pilot.Description.Id))
                    {
                        biomes.Add(ModState.LostPilotsInfo[unitResult.pilot.pilotDef.Description.Id].PilotBiomeSkin);

                        var contractData = new SimGameState.AddContractData();
                        contractData.ContractName = ModInit.modSettings.RecoveryContractIDs.GetRandomElement();
                        contractData.Employer = "SelfEmployed";
                        contractData.Target = targetFaction.Name;
                        contractData.TargetSystem = sim.CurSystem.Def.CoreSystemID;
                        MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                        sim.AddContract(contractData);
                        MapRandomizer.ModState.IsSystemActionPatch = null;
                        ModInit.modLog?.Info?.Write(
                            $"[Contract_CompleteContract] - {unitResult.pilot.Callsign} MIA; Add contract with AddContractData: contractname: {contractData.ContractName} employer: {contractData.Employer} target:{contractData.Target}, targetsystem:{contractData.TargetSystem}.");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Contract), "OnDayPassed", new Type[] {})]
        public static class Contract_OnDayPassed
        {
            static bool Prepare() => true;

            public static void Postfix(Contract __instance, ref bool __result)
            {
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim == null) return;
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

                    Traverse.Create(__instance).Field("interruptQueue").GetValue<SimGameInterruptManager>()
                        .QueuePauseNotification("Pilot Rescue EXPIRED", $"The window for recovery has passed for {removePilotName}. Another name for the wall.",
                            sim.GetCrewPortrait(SimGameCrew.Crew_Darius), "", null, "Continue", null, null);

                }
            }
        }

        [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract", new Type[]{})]
        public static class SimGameState_ResolveCompleteContract
        {
            public static void Prefix(SimGameState __instance)
            {
                //process recovery here
                if (ModInit.modSettings.RecoveryContractIDs.Contains(__instance.CompletedContract.Override.ID))
                {
                    var toRemove = new List<string>();
                    foreach (var lostPilotInfo in ModState.LostPilotsInfo)
                    {
                        if (lostPilotInfo.Value.MissingPilotSystem == __instance.CurSystem.ID)
                        {
                            var pilotInfo = lostPilotInfo.Value;
                            var pilot = new Pilot(pilotInfo.MissingPilotDef, pilotInfo.PilotSimUID, true);
                            if (__instance.CompletedContract.State == Contract.ContractState.Complete)
                            {
                                __instance.AddRecoveredPilotToRoster(pilot);
                                ModInit.modLog?.Info?.Write(
                                    $"[SimGameState_ResolveCompleteContract] - adding pilot {pilot.Callsign} to roster from recovery.");
                            }
                            else
                            {
                                var pilotSon = pilot.pilotDef.ToJSON();
                                var pilotTag = GlobalVars.SAR_PilotCompanyTagPrefix + pilotSon;
                                pilot.pilotDef.SetRecentInjuryDamageType(DamageType.Unknown);
                                pilot.pilotDef.SetDiedInSystemID(lostPilotInfo.Value.MissingPilotSystem);
                                __instance.Graveyard.Add(pilot);
                                ModInit.modLog?.Info?.Write($"[Contract_OnDayPassed] - created tag for removal from company.");
                                __instance.CompanyTags.Remove(pilotTag);
                            }


                            toRemove.Add(pilot.pilotDef.Description.Id);
                            break; // break; only one recover per contract
                        }
                    }
                    foreach (var remove in toRemove)
                    {
                        ModState.LostPilotsInfo.Remove(remove);
                    }
                }

                // probably create a popup tell you you reocverd them too. might need to use modstate?

                //process loss here
                foreach (UnitResult unitResult in __instance.CompletedContract.PlayerUnitResults)
                {
                    if (ModState.LostPilotsInfo.ContainsKey(unitResult.pilot.pilotDef.Description.Id))
                    {
                        for (var index = __instance.PilotRoster.Count - 1; index >= 0; index--)
                        {
                            var simPilot = __instance.PilotRoster[index];
                            if (unitResult.pilot.pilotDef.Description.Id == simPilot.pilotDef.Description.Id)
                            {
                                ModState.LostPilotsInfo[unitResult.pilot.pilotDef.Description.Id].PilotSimUID =
                                    simPilot.GUID;
                                ModInit.modLog?.Info?.Write($"[SimGameState_ResolveCompleteContract]: updated {unitResult.pilot.pilotDef.Description.Id} LostPilotInfo SimUID with {simPilot.GUID}");
                            //    __instance.SerializeMissingPilot(simPilot);

                                // what do with pilots? if set "away", theyll still get pulled for events and shit. just dismiss them, save info, and add back.
                                __instance.DismissMissingPilot(simPilot);
                                Traverse.Create(__instance).Field("interruptQueue").GetValue<SimGameInterruptManager>()
                                    .QueuePauseNotification("Pilot MIA!", $"Although we saw them eject, we were unable to locate {unitResult.pilot.Callsign}'s ejection seat. An SAR mission has been added to the command center.",
                                        __instance.GetCrewPortrait(SimGameCrew.Crew_Darius), "", null, "Continue", null, null);
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SGCharacterCreationCareerBackgroundSelectionPanel), "Done")]
        public static class SGCharacterCreationCareerBackgroundSelectionPanel_Done_Patch
        {
            public static void Postfix(SGCharacterCreationCareerBackgroundSelectionPanel __instance)
            {
                ModState.InitializeIcon();
            }
        }

        [HarmonyPatch(typeof(SimGameState), "Dehydrate",
            new Type[] {typeof(SimGameSave), typeof(SerializableReferenceContainer)})]
        public static class SGS_Dehydrate_Patch
        {
            public static void Prefix(SimGameState __instance)
            {
                __instance.SerializeAllMissingPilots();
            }
        }

        [HarmonyPatch(typeof(SimGameState), "Rehydrate", new Type[] {typeof(GameInstanceSave)})]
        public static class SGS_Rehydrate_Patch
        {
            public static void Postfix(SimGameState __instance)
            {
                ModState.InitializeIcon();
                __instance.DeSerializeMissingPilots();
            }
        }
    }
}
