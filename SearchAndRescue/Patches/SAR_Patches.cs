using BattleTech;
using Harmony;
using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech.Framework;
using BattleTech.Save;
using BattleTech.Save.Test;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using SearchAndRescue.Framework;
using UnityEngine.UI;
using ModState = SearchAndRescue.Framework.ModState;
using SimGameState = BattleTech.SimGameState;

namespace SearchAndRescue
{
    public class SAR_Patches
    {
        [HarmonyPatch(typeof(AbstractActor), "InitEffectStats", new Type[] {})]
        public static class AbstractActor_InitEffectStats
        {
            public static void Postfix(AbstractActor __instance)
            {
                __instance.StatCollection.AddStatistic<float>(GlobalVars.SAR_RecoveryChanceStat, 1f);
                __instance.StatCollection.AddStatistic<float>(GlobalVars.SAR_InjuryChanceStat, 1f);
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
                
                if (__instance.GetPilot().IsPlayerCharacter || sim.PilotRoster.Any(x=>x.pilotDef.Description.Id == __instance.GetPilot().pilotDef.Description.Id))
                {
                    if (__instance.IsPilotInjured())
                    {
                        var bonusHealth = __instance.GetPilot().BonusHealth;
                        if (ModInit.modSettings.InjureIgnoreBonusHealth)
                        {
                            __instance.GetPilot().InjurePilot(sourceID, stackItemID, bonusHealth + 1, DamageType.ComponentExplosion, null, __instance);
                        }
                        else
                        {
                            __instance.GetPilot().InjurePilot(sourceID, stackItemID, 1, DamageType.ComponentExplosion, null, __instance);
                        }
                    }
                    var pilotDef = __instance.GetPilot().ToPilotDef(true);
                    if (!__instance.IsPilotRecovered())
                    {
                        ModState.CompleteContractRunOnce = false;
                        var biome = __instance.Combat.ActiveContract.ContractBiome;
                        var missingPilotInfo = new Classes.MissingPilotInfo(pilotDef, __instance.GetPilot().GUID, sim.CurSystem.ID, biome, true);
                        ModState.LostPilotsInfo.Add(pilotDef.Description.Id, missingPilotInfo);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(AAR_UnitStatusWidget), "FillInPilotData")]
        public static class AAR_UnitStatusWidget_FillInPilotData
        {
            public static void Postfix(AAR_UnitStatusWidget __instance, int xpEarned)
            {
                if (ModState.LostPilotsInfo.ContainsKey(__instance.UnitData.pilot.pilotDef.Description.Id))
                {
                   
                    //no . doi sim pilot overlay,injuries horiz layout group,lopcalizable text

                    var injuryGroupComponent =
                        __instance.SimGamePilotDataOverlay.gameObject.GetComponentInChildren<HorizontalLayoutGroup>();
                    if (injuryGroupComponent != null)
                    {
                        var localizeText = injuryGroupComponent.gameObject.GetComponentInChildren<LocalizableText>();
                        localizeText.SetText("MISSING IN ACTION");
                    }
                    __instance.SimGamePilotDataOverlay.SetActive(true);

                }
            }
        }

        [HarmonyPatch(typeof(AAR_ContractObjectivesWidget), "FillInObjectives")]
        public static class AAR_ContractObjectivesWidget_FillInObjectives_Patch
        {
            public static void Postfix(AAR_ContractObjectivesWidget __instance)
            {
                if (__instance.simState == null) return;

                foreach (var missingPilot in ModState.LostPilotsInfo)
                {
                    if (!missingPilot.Value.CurrentContract) continue;
                    var cmdUseCost = $"{missingPilot.Value.MissingPilotDef.Description.Callsign} Missing In Action! Complete rescue mission to recover.";

                    var cmdUseCostResult = new MissionObjectiveResult($"{cmdUseCost}", Guid.NewGuid().ToString(), false, true, ObjectiveStatus.Ignored, false);
                    __instance.AddObjective(cmdUseCostResult);
                    missingPilot.Value.CurrentContract = false;
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
                
                var potentialContracts = new List<string>();

                sim.GetDifficultyRangeForContractPublic(sim.CurSystem, out int minDiff, out int maxDiff);

                foreach (var cid in ModInit.modSettings.RecoveryContractIDs)
                {
                    var tempOverride = sim.DataManager.ContractOverrides.Get(cid);
                    if (tempOverride != null)
                    {
                        if (tempOverride.difficulty <= maxDiff && tempOverride.difficulty >= minDiff)
                        {
                            potentialContracts.Add(cid);
                            ModInit.modLog?.Info?.Write($"[Contract_CompleteContract] - Added {cid} due to difficulty {tempOverride.difficulty} in range {minDiff} - {maxDiff}");
                        }
                    }
                }

                foreach (UnitResult unitResult in __instance.PlayerUnitResults)
                {
                    if (ModState.LostPilotsInfo.ContainsKey(unitResult.pilot.Description.Id))
                    {
                        biomes.Add(ModState.LostPilotsInfo[unitResult.pilot.pilotDef.Description.Id].PilotBiomeSkin);
                        string contractName;
                        if (potentialContracts.Count > 0)
                        {
                            contractName = potentialContracts.GetRandomElement();
                            ModInit.modLog?.Info?.Write($"[Contract_CompleteContract]: selected {contractName} from difficulty-appropriate contracts. hopefully.");
                        }
                        else
                        {
                            contractName = ModInit.modSettings.RecoveryContractIDs.GetRandomElement();
                            ModInit.modLog?.Info?.Write($"[Contract_CompleteContract]: selected {contractName} from all potential bc we couldnt find anything with right difficulty.");
                        }
                        var contractData = new SimGameState.AddContractData
                        {
                            ContractName = contractName,
                            Employer = "SelfEmployed",
                            Target = targetFaction.Name,
                            TargetSystem = sim.CurSystem.ID,
                            IsGlobal = true
                    };
                        MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                        sim.AddContract(contractData);
                        MapRandomizer.ModState.IsSystemActionPatch = null;
                        ModInit.modLog?.Info?.Write(
                            $"[Contract_CompleteContract] - {unitResult.pilot.Callsign} MIA; Add contract with AddContractData: contractname: {contractData.ContractName} employer: {contractData.Employer} target:{contractData.Target}, targetsystem:{contractData.TargetSystem}.");
                    }
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
                            pilotInfo.MissingPilotDef.DataManager = __instance.DataManager;
                            var pilot = new Pilot(pilotInfo.MissingPilotDef, pilotInfo.PilotSimUID, true);
                            var pilotSon = pilot.pilotDef.ToJSON();
                            var pilotTag = GlobalVars.SAR_PilotCompanyTagPrefix + pilotSon;
                            if (__instance.CompletedContract.State == Contract.ContractState.Complete)
                            {
                                pilot.ForceRefreshDef();
                                __instance.AddRecoveredPilotToRoster(pilot);
                                
                                ModInit.modLog?.Info?.Write(
                                    $"[SimGameState_ResolveCompleteContract] - adding pilot {pilot.Callsign} to roster from recovery.");
                            }
                            else
                            {
                                pilot.pilotDef.SetRecentInjuryDamageType(DamageType.Unknown);
                                pilot.pilotDef.SetDiedInSystemID(lostPilotInfo.Value.MissingPilotSystem);
                                __instance.Graveyard.Add(pilot);
                                ModInit.modLog?.Info?.Write($"[Contract_OnDayPassed] - created tag for removal from company.");
                            }

                            __instance.CompanyTags.Remove(pilotTag);
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
                                __instance.interruptQueue.QueuePauseNotification("Pilot MIA!", $"Although we saw them eject, we were unable to locate {unitResult.pilot.Callsign}'s ejection seat. An SAR mission has been added to the command center.",
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
