using BattleTech;
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
using BattleTech.Data;
using MapRandomizer.source;
using Classes = SearchAndRescue.Framework.Classes;
using UnityEngine;

namespace SearchAndRescue
{
    public class SAR_Patches
    {
        [HarmonyPatch(typeof(AbstractActor), "InitEffectStats", new Type[] { })]
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

                    if (ModInit.modSettings.AlwaysRecoverContractIDs.Contains(__instance.Combat.ActiveContract.Override
                        .FetchCachedOverrideID()) || ModInit.modSettings.AlwaysRecoverContractIDs.Contains(__instance.Combat.ActiveContract
                        .Override.ContractTypeValue.Name)) return;

                if (__instance.GetPilot().IsPlayerCharacter || sim.PilotRoster.Any(x =>
                        x.pilotDef.Description.Id == __instance.GetPilot().pilotDef.Description.Id))
                {
                    if (__instance.IsPilotInjured())
                    {
                        var bonusHealth = __instance.GetPilot().BonusHealth;
                        if (ModInit.modSettings.InjureIgnoreBonusHealth)
                        {
                            __instance.GetPilot().InjurePilot(sourceID, stackItemID, bonusHealth + 1,
                                DamageType.ComponentExplosion, null, __instance);
                        }
                        else
                        {
                            __instance.GetPilot().InjurePilot(sourceID, stackItemID, 1, DamageType.ComponentExplosion,
                                null, __instance);
                        }
                    }

                    var pilotDef = __instance.GetPilot().ToPilotDef(true);
                    if (!__instance.IsPilotRecovered(
                            __instance.Combat.ActiveContract.Override.employerTeam.FactionValue.Name !=
                            sim.CurSystem.OwnerValue.Name))
                    {
                        ModState.CompleteContractRunOnce = false;
                        //var biome = __instance.Combat.ActiveContract.ContractBiome;
                        var targetFaction =
                            __instance.Combat.ActiveContract.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5");
                        var missingPilotInfo = new Classes.MissingPilotInfo(pilotDef, __instance.GetPilot().GUID,
                            sim.CurSystem.ID, targetFaction.Name, true);
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
                    var cmdUseCost =
                        $"{missingPilot.Value.MissingPilotDef.Description.Callsign} Missing In Action! Complete rescue mission to recover.";

                    var cmdUseCostResult = new MissionObjectiveResult($"{cmdUseCost}", Guid.NewGuid().ToString(), false,
                        true, ObjectiveStatus.Ignored, false);
                    __instance.AddObjective(cmdUseCostResult);
                    missingPilot.Value.CurrentContract = false;
                }
            }
        }

        [HarmonyPatch(typeof(Contract), "GUID", MethodType.Getter)]
        public static class Contract_GUID_Getter
        {
            static bool Prepare() => ModInit.modSettings.enableTrace;

            public static void Postfix(Contract __instance, ref string __result)
            {
                if (__instance == null) return;
                if (__instance.Override == null) return;
                var overrideID = __instance?.Override.FetchCachedOverrideID();
                if (__instance.Name == null) return;
                if (ModState.ContractNames.Contains(__instance.Name) || ModInit.modSettings.RecoveryContractIDs.Contains(overrideID))
                    ModInit.modLog?.Info?.Write($"[Contract_GUID_Getter] getter for {__instance?.Name} {overrideID} got {__result}. Called by {Environment.StackTrace.ToString()}");
            }
        }

        [HarmonyPatch(typeof(Contract), "GUID", MethodType.Setter)]
        public static class Contract_GUID_Setter
        {
            static bool Prepare() => ModInit.modSettings.enableTrace;

            public static void Postfix(Contract __instance, string value)
            {
                if (__instance == null) return;
                if (__instance.Override == null) return;
                var overrideID = __instance?.Override.FetchCachedOverrideID();
                if (__instance.Name == null) return;
                if (ModState.ContractNames.Contains(__instance.Name) || ModInit.modSettings.RecoveryContractIDs.Contains(overrideID))
                    ModInit.modLog?.Info?.Write($"[Contract_GUID_Setter] Setter for {__instance?.Name} {overrideID} set {value}. Called by {Environment.StackTrace.ToString()}");
            }
        }

        [HarmonyPatch(typeof(Contract), "CompleteContract", new Type[] {typeof(MissionResult), typeof(bool)})]
        public static class Contract_CompleteContract
        {
            static bool Prepare() => true; //enabled

            public static void Postfix(Contract __instance)
            {
                ModInit.modLog?.Info?.Write(
                    $"[Contract_CompleteContract] contraact guid? {__instance.GUID}");
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                if (sim == null) return;
                if (ModState.CompleteContractRunOnce) return;
                if (!ModState.LostPilotsInfo.Any()) return;
                ModState.CompleteContractRunOnce = true;
                //var biomes = new List<Biome.BIOMESKIN>();
                var targetFaction =
                    __instance.GetTeamFaction("be77cadd-e245-4240-a93e-b99cc98902a5");
                ModInit.modLog?.Info?.Write(
                    $"[Contract_CompleteContract] - dump lost pilots: keys: {string.Join(", ", ModState.LostPilotsInfo.Keys)}");

                var correctDifficultyContracts = new List<string>();
                var wrongDifficultyContracts = new List<string>();
                sim.GetDifficultyRangeForContractPublic(sim.CurSystem, out int minDiff, out int maxDiff);

                foreach (var cid in ModInit.modSettings.RecoveryContractIDs)
                {
                    var tempOverride = sim.DataManager.ContractOverrides.Get(cid);
                    if (tempOverride != null)
                    {
                        if (tempOverride.difficulty <= maxDiff && tempOverride.difficulty >= minDiff)
                        {
                            correctDifficultyContracts.Add(cid);
                            ModInit.modLog?.Info?.Write(
                                $"[Contract_CompleteContract] - Added {cid} to pool due to difficulty {tempOverride.difficulty} in range {minDiff} - {maxDiff}");
                        }
                        else
                        {
                            wrongDifficultyContracts.Add(cid);
                            ModInit.modLog?.Info?.Write(
                                $"[Contract_CompleteContract] - {cid} has wrong difficulty, adding to fallback list");
                        }
                    }
                }

                correctDifficultyContracts.Shuffle();
                wrongDifficultyContracts.Shuffle();

                foreach (UnitResult unitResult in __instance.PlayerUnitResults)
                {
                    if (ModState.LostPilotsInfo.TryGetValue(unitResult.pilot.Description.Id, out var value))
                    {
                        //biomes.Add(ModState.LostPilotsInfo[unitResult.pilot.pilotDef.Description.Id].PilotBiomeSkin);
                        string contractName = "";
                        if (correctDifficultyContracts.Count > 0)
                        {
                            //contractName = potentialContracts.GetRandomElement();
                            foreach (var contract in correctDifficultyContracts)
                            {
                                var contractOverride = sim.DataManager.ContractOverrides.Get(contract).Copy();
                                MapRandomizer.ModState.AddContractBiomes = sim.CurSystem.Def.SupportedBiomes;
                                MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                                var releasedMapsAndEncountersByContractTypeAndOwnership =
                                    MetadataDatabase.Instance.GetReleasedMapsAndEncountersByContractTypeAndOwnership(
                                        contractOverride.ContractTypeValue.ID, false);
                                if (releasedMapsAndEncountersByContractTypeAndOwnership != null && releasedMapsAndEncountersByContractTypeAndOwnership.Count > 0)
                                {
                                    contractName = contract;
                                    break;
                                }
                                else
                                {
                                    ModInit.modLog?.Info?.Write(
                                        $"[Contract_CompleteContract]: no playable maps for type {contractOverride.contractTypeID}; {contractOverride.ContractTypeValue.ID}.");
                                }
                            }
                        }
                        else
                        {
                            ModInit.modLog?.Info?.Write(
                                $"[Contract_CompleteContract]: You did not configure a fallback with correct difficulty. Trying again without difficulty constraints.");
                            foreach (var contract in wrongDifficultyContracts)
                            {
                                var contractOverride = sim.DataManager.ContractOverrides.Get(contract).Copy();
                                MapRandomizer.ModState.AddContractBiomes = sim.CurSystem.Def.SupportedBiomes;
                                MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                                var releasedMapsAndEncountersByContractTypeAndOwnership =
                                    MetadataDatabase.Instance
                                        .GetReleasedMapsAndEncountersByContractTypeAndOwnership(
                                            contractOverride.ContractTypeValue.ID, false);

                                if (releasedMapsAndEncountersByContractTypeAndOwnership != null && releasedMapsAndEncountersByContractTypeAndOwnership.Count > 0)
                                {
                                    contractName = contract;
                                    break;
                                }
                                else
                                {
                                    ModInit.modLog?.Info?.Write(
                                        $"[Contract_CompleteContract]: no playable maps for type {contractOverride.contractTypeID}; {contractOverride.ContractTypeValue.ID}.");
                                }
                            }
                        }
                        if (string.IsNullOrEmpty(contractName))
                        {
                            contractName = ModInit.modSettings.RecoveryContractIDs.GetRandomElement();
                            ModInit.modLog?.Info?.Write(
                                $"[Contract_CompleteContract]: Couldn't find biome appropriate map for any recovery contracts, disabling biome enforcement and picking a random contract because you don't read the documentation. This hurts you more than it hurts me.");
                        }

                        var contractData = new SimGameState.AddContractData
                        {
                            ContractName = contractName,
                            Employer = "SelfEmployed",
                            Target = targetFaction.Name,
                            TargetSystem = sim.CurSystem.ID,
                            IsGlobal = true
                        };
                        MapRandomizer.ModState.AddContractBiomes = sim.CurSystem.Def.SupportedBiomes;
                        MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                        var contractAdded = sim.AddContract(contractData);
                        if (contractAdded != null && string.IsNullOrEmpty(contractAdded?.GUID))
                        {
                            //contractAdded.SetGuid(Guid.NewGuid().ToString());
                        }

                        value.RecoveryContractGUID =
                            contractAdded?.GUID; // i think this wont work. may also need to make sure it puts contract in save bits? maybe not, maybe just patch addtravel
                        ModInit.modLog?.Info?.Write(
                            $"[Contract_CompleteContract] - {unitResult.pilot.Callsign} MIA; Add contract with AddContractData: contractname: {contractData.ContractName} employer: {contractData.Employer} target:{contractData.Target}, targetsystem:{contractData.TargetSystem}. Recovery contract GUID {contractAdded?.GUID}");
                    }
                }
                MapRandomizer.ModState.AddContractBiomes = new List<Biome.BIOMESKIN>();
                MapRandomizer.ModState.IsSystemActionPatch = null;
            }
        }

        [HarmonyPatch(typeof(SimGameState), "ResolveCompleteContract", new Type[] { })]
        public static class SimGameState_ResolveCompleteContract
        {
            public static void Prefix(SimGameState __instance)
            {
                ModInit.modLog?.Trace?.Write($"[SimGameState_ResolveCompleteContract] enter SimGameState_ResolveCompleteContract");
                //process recovery here
                var overrideID = __instance.CompletedContract.Override.FetchCachedOverrideID();
                if (ModInit.modSettings.RecoveryContractIDs.Contains(overrideID) || ModState.ContractNames.Contains(__instance.CompletedContract.Override.contractName))
                {
                    ModInit.modLog?.Info?.Write($"[SimGameState_ResolveCompleteContract] found matching ID or Name for SAR contract");
                    var toRemove = new List<string>();
                    foreach (var lostPilotInfo in ModState.LostPilotsInfo)
                    {
                        if (string.IsNullOrEmpty(lostPilotInfo.Value.RecoveryContractGUID))
                        {
                            toRemove.Add(lostPilotInfo.Key);
                            ModInit.modLog?.Error?.Write(
                                $"[SimGameState_ResolveCompleteContract] - {lostPilotInfo.Key} RecoveryContractGUID was null. This should never happen at this point. tbone has been very bad and you should tell him so.");
                            continue;
                        }
                        ModInit.modLog?.Info?.Write($"[SimGameState_ResolveCompleteContract] - Checking lostPilotInfo GUID {lostPilotInfo.Value.RecoveryContractGUID} against contract GUID {__instance.CompletedContract.GUID}.");

                        if (lostPilotInfo.Value.RecoveryContractGUID == __instance.CompletedContract.GUID)
                        {
                            var pilotInfo = lostPilotInfo.Value;
                            pilotInfo.MissingPilotDef.DataManager = __instance.DataManager;
                            var pilot = new Pilot(pilotInfo.MissingPilotDef, pilotInfo.PilotSimUID, true);
                            var pilotSon = pilot.pilotDef.ToJSON();
                            var pilotTag = GlobalVars.SAR_PilotCompanyTagPrefix + pilotSon;

                            if (__instance.PilotRoster.Any(x =>
                                    x.pilotDef.Description.Id == pilotInfo.MissingPilotDef.Description.Id))
                            {
                                ModInit.modLog?.Info?.Write(
                                    $"[SimGameState_ResolveCompleteContract] - pilot {pilot.Callsign} already in roster, aborting. most likely not a new bug, but due to earlier tbone fuckups.");
                                __instance.CompanyTags.Remove(pilotTag);
                                toRemove.Add(lostPilotInfo.Key);
                                continue;
                            }

                            if (__instance.CompletedContract.State == Contract.ContractState.Complete)
                            {
                                ModInit.modLog?.Info?.Write($"[SimGameState_ResolveCompleteContract] CONTRACT STATE {__instance.CompletedContract.State}");

                                pilot.ForceRefreshDef();
                                __instance.AddRecoveredPilotToRoster(pilot);
                            }
                            else
                            {
                                ModInit.modLog?.Info?.Write($"[SimGameState_ResolveCompleteContract] CONTRACT STATE {__instance.CompletedContract.State}");
                                __instance.KillMissingPilot(pilot, lostPilotInfo.Value);
                            }

                            __instance.CompanyTags.Remove(pilotTag);
                            toRemove.Add(lostPilotInfo.Key);
                            break; // break; only one recover per contract. shouldnt matter anymore since we GUID match instead of system match?
                        }
                    }

                    foreach (var remove in toRemove)
                    {
                        ModState.LostPilotsInfo.Remove(remove);
                    }
                }
                ModInit.modLog?.Info?.Write($"[SimGameState_ResolveCompleteContract] post recovery SimGameState_ResolveCompleteContract");
                // probably create a popup tell you you reocverd them too. might need to use modstate?

                //process loss here
                foreach (UnitResult unitResult in __instance.CompletedContract.PlayerUnitResults)
                {
                    if (ModState.LostPilotsInfo.TryGetValue(unitResult.pilot.pilotDef.Description.Id, out var pilotInfo))
                    {
                        for (var index = __instance.PilotRoster.Count - 1; index >= 0; index--)
                        {
                            var simPilot = __instance.PilotRoster[index];
                            if (unitResult.pilot.pilotDef.Description.Id == simPilot.pilotDef.Description.Id)
                            {
                                pilotInfo.PilotSimUID =
                                    simPilot.GUID;
                                ModInit.modLog?.Info?.Write(
                                    $"[SimGameState_ResolveCompleteContract]: updated {unitResult.pilot.pilotDef.Description.Id} LostPilotInfo SimUID with {simPilot.GUID}");
                                //    __instance.SerializeMissingPilot(simPilot);

                                // what do with pilots? if set "away", theyll still get pulled for events and shit. just dismiss them, save info, and add back.
                                __instance.DismissMissingPilot(simPilot);
                                __instance.interruptQueue.QueuePauseNotification("Pilot MIA!",
                                    $"Although we saw them eject, we were unable to locate {unitResult.pilot.Callsign}'s ejection seat. An SAR mission has been added to the command center.",
                                    __instance.GetCrewPortrait(SimGameCrew.Crew_Darius), "", null, "Continue",
                                    null, null);
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SimGameState), "FinishCompleteBreadcrumbProcess", new Type[] {})]
        public static class SimGameState_FinishCompleteBreadcrumbProcess
        {
            public static void Prefix(SimGameState __instance)
            {
                if (__instance.activeBreadcrumb.Override.OnContractSuccessResults != null)
                {
                    foreach (SimGameEventResult simGameEventResult in __instance.activeBreadcrumb.Override.OnContractSuccessResults)
                    {
                        if (simGameEventResult.Actions != null)
                        {
                            SimGameResultAction[] actions = simGameEventResult.Actions;
                            for (int i = 0; i < actions.Length; i++)
                            {
                                if (actions[i].Type == SimGameResultAction.ActionType.System_StartNonProceduralContract)
                                {
                                    ModState.NonProceduralContractGUID = __instance.activeBreadcrumb.GUID;
                                    ModInit.modLog?.Info?.Write($"[SimGameState_FinishCompleteBreadcrumbProcess] stored NonProcedural breadcrumb GUID to modstate {ModState.NonProceduralContractGUID}");
                                }
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SimGameState), "AddPredefinedContract2", new Type[] {typeof(SimGameState.AddContractData)})]
        public static class SimGameState_AddPredefinedContract2 //need to make sure travel contracts get a GUID
        {
            public static void Postfix(SimGameState __instance, SimGameState.AddContractData contractData,
                ref Contract __result)
            {
                if (!contractData.IsGlobal) return;
                if (__result == null) return;

                if (string.IsNullOrEmpty(__result.GUID))
                {
                    if (!string.IsNullOrEmpty(contractData.SaveGuid))
                    {
                        __result.SetGuid(contractData.SaveGuid);
                        ModInit.modLog?.Info?.Write($"[SimGameState_AddPredefinedContract2] contract had saveGUID {__result.GUID}");
                    }
                    else if (!string.IsNullOrEmpty(ModState.NonProceduralContractGUID))
                    {
                        __result.SetGuid(ModState.NonProceduralContractGUID);
                        ModInit.modLog?.Info?.Write($"[SimGameState_AddPredefinedContract2] found Modstate GUID, set to {__result.GUID} and clearing state");
                        ModState.NonProceduralContractGUID = "";
                    }
                    else
                    {
                        __result.SetGuid(Guid.NewGuid().ToString());
                        ModInit.modLog?.Info?.Write($"[SimGameState_AddPredefinedContract2] contract had no GUID, generated new one {__result.GUID}");
                    }
                }
            }
        }

        [HarmonyPatch(typeof(SimGameState), "AddContract", new Type[]{typeof(SimGameState.AddContractData)})]
        public static class SimGameState_AddContract //need to make sure travel contracts get a GUID
        {
            public static void Postfix(SimGameState __instance, SimGameState.AddContractData contractData, ref Contract __result)
            {
                {
                    if (!contractData.IsGlobal) return;
                    if (__result == null) return;

                    if (string.IsNullOrEmpty(__result.GUID))
                    {
                        if (!string.IsNullOrEmpty(contractData.SaveGuid))
                        {
                            __result.SetGuid(contractData.SaveGuid);
                            ModInit.modLog?.Info?.Write($"[SimGameState_AddContract] contract had saveGUID {__result.GUID}");
                        }
                        else
                        {
                            __result.SetGuid(Guid.NewGuid().ToString());
                            ModInit.modLog?.Info?.Write($"[SimGameState_AddContract] contract had no GUID, generated new one {__result.GUID}");
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
                var sim = UnityGameInstance.BattleTechGame.Simulation;
                sim.InitializeMissionNames();
                if (!sim.CompanyStats.ContainsStatistic(Classes.RecoveryChanceStat))
                {
                    sim.CompanyStats.AddStatistic(Classes.RecoveryChanceStat,
                        ModInit.modSettings.BasePilotRecoveryChance);
                }
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
                if (!__instance.CompanyStats.ContainsStatistic(Classes.RecoveryChanceStat))
                {
                    __instance.CompanyStats.AddStatistic(Classes.RecoveryChanceStat,
                        ModInit.modSettings.BasePilotRecoveryChance);
                }
                __instance.InitializeMissionNames();
                __instance.DeSerializeMissingPilots();
                if (!ModState.LostPilotsInfo.Any()) return;
                //__instance.RehydrateIDAllContracts();
                if (ModState.LostPilotsInfo.Values.Any(x => string.IsNullOrEmpty(x.RecoveryContractGUID)))
                {
                    ModInit.modLog?.Info?.Write(
                        $"[SGS_Rehydrate_Patch] - Found LostPilotsInfos with null recovery contract GUID. Trying to nuke recovery contracts and regenerate");
                    if (__instance.GlobalContracts.Count == ModState.LostPilotsInfo.Count)
                    {
                        ModInit.modLog?.Info?.Write($"[SGS_Rehydrate_Patch] - LostPilotsInfos count equal to GlobalContracts count, can safely nuke all Global Contracts. I hope.");
                        for (int i = __instance.GlobalContracts.Count - 1; i >= 0; i--)
                        {
                            var overrideID = __instance.GlobalContracts[i].Override.FetchCachedOverrideID();
                            ModInit.modLog?.Info?.Write(
                                $"[SGS_Rehydrate_Patch] - NUKED FOR REGEN: Removed old recovery contract with id {overrideID} and GUID {__instance.GlobalContracts[i].GUID}");
                            __instance.GlobalContracts.RemoveAt(i);
                        }
                    }
                    else
                    {
                        for (int i = __instance.GlobalContracts.Count - 1; i >= 0; i--)
                        {
                            var overrideID = __instance.GlobalContracts[i].Override.FetchCachedOverrideID();
                            //__instance.GlobalContracts[i].Override.RehydrateOverrideID();
                            if (ModState.ContractNames.Contains(__instance.GlobalContracts[i].Override.contractName) || ModInit.modSettings.RecoveryContractIDs.Contains(overrideID))
                            {
                                ModInit.modLog?.Info?.Write(
                                    $"[SGS_Rehydrate_Patch] - Checking GlobalContract {overrideID} against SAR contract list");
                                ModInit.modLog?.Info?.Write(
                                    $"[SGS_Rehydrate_Patch] - NUKED FOR REGEN: Removed old recovery contract with id {overrideID} and GUID {__instance.GlobalContracts[i].GUID}");
                                __instance.GlobalContracts.RemoveAt(i);
                            }
                        }
                    }

                    for (int i = __instance.CurSystem.SystemContracts.Count - 1; i >= 0; i--)
                    {
                        var overrideID = __instance.CurSystem.SystemContracts[i].Override.FetchCachedOverrideID();
                        //__instance.CurSystem.SystemContracts[i].Override.RehydrateOverrideID();
                        if (ModState.ContractNames.Contains(__instance.CurSystem.SystemContracts[i].Override.contractName) || ModInit.modSettings.RecoveryContractIDs.Contains(overrideID))
                        {
                            ModInit.modLog?.Info?.Write(
                                $"[SGS_Rehydrate_Patch] - Checking system contract {overrideID} against SAR contract list");
                            ModInit.modLog?.Info?.Write(
                                $"[SGS_Rehydrate_Patch] - NUKED FOR REGEN: Removed old recovery contract with id {overrideID} and GUID {__instance.CurSystem.SystemContracts[i].GUID}");
                            __instance.CurSystem.SystemContracts.RemoveAt(i);
                        }
                    }

                    if (__instance.ActiveTravelContract != null)
                    {
                        var overrideID = __instance.ActiveTravelContract.Override.FetchCachedOverrideID();
                        if (__instance.CurSystem.ID == __instance.ActiveTravelContract.TargetSystem && (ModState.ContractNames.Contains(__instance.ActiveTravelContract.Override.contractName) ||
                                ModInit.modSettings.RecoveryContractIDs.Contains(overrideID)))
                        {
                            __instance.ClearBreadcrumb();
                            ModInit.modLog?.Info?.Write($"[SGS_Rehydrate_Patch] - Active travel contract was SAR in current system, clearing it");
                        }
                    }
                }
                var recoveryContractsGlobal = __instance.GlobalContracts.FindAll(x =>
                    ModInit.modSettings.RecoveryContractIDs.Contains(x.Override.FetchCachedOverrideID()) || ModState.ContractNames.Contains(x.Override.contractName));

                var recoveryContractsSystem = __instance.CurSystem.SystemContracts.FindAll(x => ModInit.modSettings.RecoveryContractIDs.Contains(x.Override.FetchCachedOverrideID()) || ModState.ContractNames.Contains(x.Override.contractName));

                recoveryContractsGlobal.AddRange(recoveryContractsSystem);

                if (__instance.ActiveTravelContract != null)
                {
                    var overrideID = __instance.ActiveTravelContract.Override.FetchCachedOverrideID();
                    if (__instance.CurSystem.ID == __instance.ActiveTravelContract.TargetSystem &&
                        (ModState.ContractNames.Contains(__instance.ActiveTravelContract.Override.contractName) ||
                         ModInit.modSettings.RecoveryContractIDs.Contains(overrideID)))
                    {
                        recoveryContractsGlobal.Add(__instance.ActiveTravelContract);
                    }
                }

                if (recoveryContractsGlobal.Count >= ModState.LostPilotsInfo.Count)
                {
                    MapRandomizer.ModState.IsSystemActionPatch = null;
                    MapRandomizer.ModState.AddContractBiomes = new List<Biome.BIOMESKIN>();
                    ModInit.modLog?.Info?.Write($"[SGS_Rehydrate_Patch] - recoveryContractsGlobal.Count {recoveryContractsGlobal.Count} >= ModState.LostPilotsInfo.Count {ModState.LostPilotsInfo.Count}");
                    return;
                }
                var addedContracts = 0;
                foreach (var missingPilot in ModState.LostPilotsInfo)
                {
                    var targetSystem = __instance.GetSystemById(missingPilot.Value.MissingPilotSystem);
                    if (recoveryContractsGlobal.Count + addedContracts >= ModState.LostPilotsInfo.Count)
                    {
                        MapRandomizer.ModState.IsSystemActionPatch = null;
                        MapRandomizer.ModState.AddContractBiomes = new List<Biome.BIOMESKIN>();
                        return;
                    }

                    var filteredRecoveries = recoveryContractsGlobal.FindAll(x =>
                        x.Override.targetTeam.faction == missingPilot.Value.SAR_Opfor &&
                        x.TargetSystemID == missingPilot.Value.MissingPilotSystem);

                    if (filteredRecoveries.Count == 0)
                    {
                        var correctDifficultyContracts = new List<string>();
                        var wrongDifficultyContracts = new List<string>();
                        __instance.GetDifficultyRangeForContractPublic(__instance.CurSystem, out int minDiff,
                            out int maxDiff);

                        foreach (var cid in ModInit.modSettings.RecoveryContractIDs)
                        {
                            var tempOverride = __instance.DataManager.ContractOverrides.Get(cid);
                            if (tempOverride != null)
                            {
                                if (tempOverride.difficulty <= maxDiff && tempOverride.difficulty >= minDiff)
                                {
                                    correctDifficultyContracts.Add(cid);
                                    ModInit.modLog?.Info?.Write(
                                        $"[SGS_Rehydrate_Patch] - Added {cid} due to difficulty {tempOverride.difficulty} in range {minDiff} - {maxDiff}");
                                }
                                else
                                {
                                    wrongDifficultyContracts.Add(cid);
                                    ModInit.modLog?.Info?.Write(
                                        $"[SGS_Rehydrate_Patch] - {cid} has wrong difficulty, adding to fallback list");
                                }
                            }
                        }

                        ModInit.modLog?.Trace?.Write($"[SGS_Rehydrate_Patch] - order of correct diff contracts before shuffle {string.Join("; ", correctDifficultyContracts)}");
                        ModInit.modLog?.Trace?.Write($"[SGS_Rehydrate_Patch] - order of wrong  diff contracts before shuffle {string.Join("; ", wrongDifficultyContracts)}");
                        correctDifficultyContracts.Shuffle();
                        wrongDifficultyContracts.Shuffle();
                        ModInit.modLog?.Trace?.Write($"[SGS_Rehydrate_Patch] - order of correct diff contracts after shuffle {string.Join("; ", correctDifficultyContracts)}");
                        ModInit.modLog?.Trace?.Write($"[SGS_Rehydrate_Patch] - order of wrong  diff contracts after shuffle {string.Join("; ", wrongDifficultyContracts)}");

                        string contractName = "";
                        if (correctDifficultyContracts.Count > 0)
                        {
                            ModInit.modLog?.Info?.Write($"[SGS_Rehydrate_Patch] - trying to generate contracts for system {targetSystem.Name} with available biomes {string.Join("; ", targetSystem.Def.SupportedBiomes)}");
                            //contractName = potentialContracts.GetRandomElement();
                            foreach (var contract in correctDifficultyContracts)
                            {
                                var contractOverride = __instance.DataManager.ContractOverrides.Get(contract).Copy();
                                MapRandomizer.ModState.AddContractBiomes = targetSystem.Def.SupportedBiomes;
                                MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                                var releasedMapsAndEncountersByContractTypeAndOwnership =
                                    MetadataDatabase.Instance.GetReleasedMapsAndEncountersByContractTypeAndOwnership(
                                        contractOverride.ContractTypeValue.ID, false);
                                if (releasedMapsAndEncountersByContractTypeAndOwnership != null && releasedMapsAndEncountersByContractTypeAndOwnership.Count > 0)
                                {
                                    ModInit.modLog?.Info?.Write(
                                        $"[SGS_Rehydrate_Patch]: Found usable map with GetReleasedMapsAndEncountersByContractTypeAndOwnership, setting contract to {contract}.");
                                    contractName = contract;
                                    break;
                                }
                                else
                                {
                                    ModInit.modLog?.Info?.Write(
                                        $"[SGS_Rehydrate_Patch]: no playable maps for type {contractOverride.contractTypeID}; {contractOverride.ContractTypeValue.ID}.");
                                }
                            }
                        }
                        else
                        {

                            ModInit.modLog?.Info?.Write(
                                $"[SGS_Rehydrate_Patch]: You did not configure a fallback with correct difficulty. Trying again without difficulty constraints.");
                            {
                                foreach (var contract in wrongDifficultyContracts)
                                {
                                    var contractOverride =
                                        __instance.DataManager.ContractOverrides.Get(contract).Copy();
                                    MapRandomizer.ModState.AddContractBiomes = targetSystem.Def.SupportedBiomes;
                                    MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                                    var releasedMapsAndEncountersByContractTypeAndOwnership =
                                        MetadataDatabase.Instance
                                            .GetReleasedMapsAndEncountersByContractTypeAndOwnership(
                                                contractOverride.ContractTypeValue.ID, false);
                                    if (releasedMapsAndEncountersByContractTypeAndOwnership != null && releasedMapsAndEncountersByContractTypeAndOwnership.Count > 0)
                                    {
                                        ModInit.modLog?.Info?.Write(
                                            $"[SGS_Rehydrate_Patch]: Found usable map with GetReleasedMapsAndEncountersByContractTypeAndOwnership, setting contract to {contract}.");
                                        contractName = contract;
                                        break;
                                    }
                                    else
                                    {
                                        ModInit.modLog?.Info?.Write(
                                            $"[SGS_Rehydrate_Patch]: no playable maps for type {contractOverride.contractTypeID}; {contractOverride.ContractTypeValue.ID}.");
                                    }
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(contractName))
                        {
                            contractName = ModInit.modSettings.RecoveryContractIDs.GetRandomElement();
                            ModInit.modLog?.Info?.Write(
                                $"[SGS_Rehydrate_Patch]: Couldn't find biome appropriate map for any recovery contracts, disabling biome enforcement and picking a random contract because you don't read the documentation. This hurts you more than it hurts me.");
                        }

                        var contractData = new SimGameState.AddContractData
                        {
                            ContractName = contractName,
                            Employer = "SelfEmployed",
                            Target = missingPilot.Value.SAR_Opfor,
                            TargetSystem = missingPilot.Value.MissingPilotSystem,
                            IsGlobal = true
                        };

                        MapRandomizer.ModState.IsSystemActionPatch = "ACTIVE";
                        MapRandomizer.ModState.AddContractBiomes = targetSystem.Def.SupportedBiomes;
                        var contractAdded = __instance.AddContract(contractData);
                        ModState.LostPilotsInfo[missingPilot.Value.MissingPilotDef.Description.Id]
                            .RecoveryContractGUID = contractAdded?.GUID;
                        for (var index = missingPilot.Value.MissingPilotDef.PilotTags.Count - 1; index >= 0; index--)
                        {
                            var tag = missingPilot.Value.MissingPilotDef.PilotTags[index];
                            if (!tag.StartsWith(GlobalVars.SAR_ContractGUIDPrefix)) continue;
                            missingPilot.Value.MissingPilotDef.PilotTags.Remove(tag);
                            missingPilot.Value.MissingPilotDef.PilotTags.Add(
                                $"{GlobalVars.SAR_ContractGUIDPrefix}{contractAdded.GUID}");
                        }

                        ModInit.modLog?.Info?.Write(
                            $"[SGS_Rehydrate_Patch] - {missingPilot.Value.MissingPilotDef.Description.Callsign} MIA; Add contract with AddContractData: contractname:" +
                            $"{contractData.ContractName} employer: {contractData.Employer} target:{contractData.Target}, targetsystem:{contractData.TargetSystem}. Recovery contract GUID {contractAdded?.GUID}.");

                        addedContracts++;
                    }
                }
                MapRandomizer.ModState.AddContractBiomes = new List<Biome.BIOMESKIN>();
                MapRandomizer.ModState.IsSystemActionPatch = null;

                //do a final re-cleanup here?
            }
        }
    }
}
