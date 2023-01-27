using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;
using BattleTech.Portraits;
using BattleTech.Save;
using BattleTech.StringInterpolation;
using Harmony;
using IRBTModUtils;
using SearchAndRescue.Framework;
using UnityEngine;
using static BattleTech.SimGameState;
using ModState = SearchAndRescue.Framework.ModState;

namespace SearchAndRescue
{
    public static class Utils
    {
        public static void SerializeMissingPilot(this SimGameState sim, Pilot simPilot)
        {
            if (ModState.LostPilotsInfo.ContainsKey(simPilot.pilotDef.Description.Id))
            {
                if (string.IsNullOrEmpty(ModState.LostPilotsInfo[simPilot.pilotDef.Description.Id].PilotSimUID))
                {
                    ModState.LostPilotsInfo[simPilot.pilotDef.Description.Id].PilotSimUID = simPilot.GUID;
                }
            }
            //save pilot to...soomething.
            var pilotDef = simPilot.ToPilotDef(true);
             
            var biomeTag = $"{GlobalVars.SAR_BiomePrefix}{ModState.LostPilotsInfo[simPilot.pilotDef.Description.Id].PilotBiomeSkin}";
            var systemTag = $"{GlobalVars.SAR_SystemPrefix}{ModState.LostPilotsInfo[simPilot.pilotDef.Description.Id].MissingPilotSystem}";
            var pilotUIDTag = $"{GlobalVars.SAR_PilotSimUIDPrefix}{simPilot.GUID}";
            pilotDef.PilotTags.Add(biomeTag);
            pilotDef.PilotTags.Add(systemTag);
            pilotDef.PilotTags.Add(pilotUIDTag);
            ModInit.modLog?.Info?.Write($"[SerializeMissingPilot] - Added biome {biomeTag}, system {systemTag}, and sUID {pilotUIDTag} to {simPilot.Callsign}'s pilot tags for recovery mission.");

            var pilotSon = pilotDef.ToJSON();
            var pilotTag = GlobalVars.SAR_PilotCompanyTagPrefix + pilotSon;
            sim.CompanyTags.Add(pilotTag);
        }

        public static void SerializeAllMissingPilots(this SimGameState sim)
        {
            //save pilot to...soomething.

            foreach (var missingPilotInfo in ModState.LostPilotsInfo)
            {
                var pilotDef = missingPilotInfo.Value.MissingPilotDef;
                var biomeTag = $"{GlobalVars.SAR_BiomePrefix}{missingPilotInfo.Value.PilotBiomeSkin}";
                var systemTag = $"{GlobalVars.SAR_SystemPrefix}{missingPilotInfo.Value.MissingPilotSystem}";
                var pilotUIDTag = $"{GlobalVars.SAR_PilotSimUIDPrefix}{missingPilotInfo.Value.PilotSimUID}";
                if (pilotDef.portraitSettings != null)
                {
                    var portraitTag = $"{GlobalVars.SAR_PortraitSettingsPrefix}{pilotDef.portraitSettings.ToJSON()}";
                    pilotDef.PilotTags.Add(portraitTag);
                }
                pilotDef.PilotTags.Add(biomeTag);
                pilotDef.PilotTags.Add(systemTag);
                pilotDef.PilotTags.Add(pilotUIDTag);
                ModInit.modLog?.Info?.Write($"[SerializeAllMissingPilots] - Added biome {biomeTag}, system {systemTag} and simUID {pilotUIDTag} to {pilotDef.Description.Callsign}'s pilot tags for recovery mission.");
                var pilotSon = pilotDef.ToJSON();
                var pilotTag = GlobalVars.SAR_PilotCompanyTagPrefix + pilotSon;
                sim.CompanyTags.Add(pilotTag);
                ModInit.modLog?.Trace?.Write($"[SerializeAllMissingPilots] - Added pilotTag {pilotTag} to company tags.");

            }
        }

        public static void DeSerializeMissingPilots(this SimGameState sim)
        {
            for (var i = sim.CompanyTags.Count - 1; i >= 0; i--)
            {
                var tag = sim.CompanyTags[i];
                ModInit.modLog?.Debug?.Write($"[DeSerializeMissingPilots] process companytag {tag}");
                if (tag.StartsWith(GlobalVars.SAR_PilotCompanyTagPrefix))
                {
                    var tagPilot = tag.Substring(GlobalVars.SAR_PilotCompanyTagPrefix.Length);
                    var pilotDef = new PilotDef();
                    pilotDef.FromJSON(tagPilot);

                    if (sim.PilotRoster.Any(x => x.pilotDef.Description.Id == pilotDef.Description.Id))
                    {
                        ModInit.modLog?.Info?.Write($"[DeSerializeMissingPilots] Found supposedly missing pilot {pilotDef.Description.Id} in roster. Assuming pilot in roster is not MIA and removing from SAR.");
                        sim.CompanyTags.Remove(tag);
                        continue;
                    }

                    string simUID = "";
                    string systemTag = "";
                    Biome.BIOMESKIN biomeSkin = Biome.BIOMESKIN.UNDEFINED;
                    ModInit.modLog?.Debug?.Write($"[DeSerializeMissingPilots] process missing pilot tag {pilotDef.Description.Callsign}");
                    for (var index = pilotDef.PilotTags.Count - 1; index >= 0; index--)
                    {
                        var pilotTag = pilotDef.PilotTags[index];
                        if (pilotTag.StartsWith(GlobalVars.SAR_PilotSimUIDPrefix))
                        {
                            simUID = pilotTag.Substring(GlobalVars.SAR_PilotSimUIDPrefix.Length);
                            pilotDef.PilotTags.Remove(pilotTag);
                            ModInit.modLog?.Info?.Write($"[DeSerializeMissingPilots] - {pilotDef.Description.Callsign} - processed tag {pilotTag}, simUID set to {simUID}");
                            continue;
                        }
                        if (pilotTag.StartsWith(GlobalVars.SAR_SystemPrefix))
                        {
                            systemTag = pilotTag.Substring(GlobalVars.SAR_SystemPrefix.Length);
                            pilotDef.PilotTags.Remove(pilotTag);
                            ModInit.modLog?.Info?.Write($"[DeSerializeMissingPilots] - {pilotDef.Description.Callsign} - processed tag {pilotTag}, system tag set to {systemTag}");
                            continue;
                        }
                        if (pilotTag.StartsWith(GlobalVars.SAR_BiomePrefix))
                        {
                            var biomeTag = pilotTag.Substring(GlobalVars.SAR_BiomePrefix.Length);
                            Enum.TryParse<Biome.BIOMESKIN>(biomeTag, out biomeSkin);
                            pilotDef.PilotTags.Remove(pilotTag);
                            ModInit.modLog?.Info?.Write($"[DeSerializeMissingPilots] - {pilotDef.Description.Callsign} - processed tag {pilotTag}, biome tag set to {biomeSkin.ToString()}");
                            continue;
                        }
                        if (pilotTag.StartsWith(GlobalVars.SAR_PortraitSettingsPrefix))
                        {
                            var portraitSettingsString = pilotTag.Substring(GlobalVars.SAR_PortraitSettingsPrefix.Length);
                            var portraitSettings = new PortraitSettings();
                            portraitSettings.FromJSON(portraitSettingsString);
                            pilotDef.portraitSettings = portraitSettings;
                            pilotDef.PilotTags.Remove(pilotTag);
                        }
                    }

                    if (string.IsNullOrEmpty(simUID) ||string.IsNullOrEmpty(systemTag) || biomeSkin == Biome.BIOMESKIN.UNDEFINED)
                    {
                        ModInit.modLog?.Error?.Write($"[DeSerializeMissingPilots] - ERROR on deserialize. Null simUID {string.IsNullOrEmpty(simUID)}, system {string.IsNullOrEmpty(systemTag)}, or undefined biome {biomeSkin == Biome.BIOMESKIN.UNDEFINED}.");
                    }
                    var missingPilotInfo = new Classes.MissingPilotInfo(pilotDef, simUID, systemTag, biomeSkin, false);
                    if (ModState.LostPilotsInfo.ContainsKey(pilotDef.Description.Id))
                    {
                        if (ModState.LostPilotsInfo[pilotDef.Description.Id].PilotBiomeSkin ==
                            Biome.BIOMESKIN.UNDEFINED)
                            ModState.LostPilotsInfo[pilotDef.Description.Id].PilotBiomeSkin = biomeSkin;
                        if (string.IsNullOrEmpty(ModState.LostPilotsInfo[pilotDef.Description.Id].PilotSimUID))
                            ModState.LostPilotsInfo[pilotDef.Description.Id].PilotSimUID = simUID;
                        if (string.IsNullOrEmpty(ModState.LostPilotsInfo[pilotDef.Description.Id].MissingPilotSystem))
                            ModState.LostPilotsInfo[pilotDef.Description.Id].MissingPilotSystem = systemTag;
                        ModInit.modLog?.Error?.Write($"[DeSerializeMissingPilots] - ERROR - {pilotDef.Description.Id} already exist in missing pilot dictionary! Updated missing values.");
                    }
                    else
                    {
                        ModState.LostPilotsInfo.Add(pilotDef.Description.Id, missingPilotInfo);
                    }

                    sim.CompanyTags.Remove(tag);
                }
            }
        }

        public static float getSAR_RecoveryChanceMult(this AbstractActor actor)
        {
            return actor.StatCollection.GetValue<float>(GlobalVars.SAR_RecoveryChanceStat);
        }
        public static float getSAR_InjureChanceMult(this AbstractActor actor)
        {
            return actor.StatCollection.GetValue<float>(GlobalVars.SAR_InjuryChanceStat);
        }
        public static bool IsPilotRecovered(this AbstractActor actor)
        {
            if (actor.GetPilot().IsPlayerCharacter) return true;
            var chance = ModInit.modSettings.BasePilotRecoveryChance * actor.getSAR_RecoveryChanceMult();
            var roll = ModInit.Random.NextDouble();
            ModInit.modLog?.Info?.Write($"[IsPilotRecovered] - {actor.GetPilot().Callsign} Pilot recovery roll {roll} vs chance {chance}. Success? {roll <= chance}.");
            return roll <= chance;
        }

        public static bool IsPilotInjured(this AbstractActor actor)
        {
            //if (actor.GetPilot().IsPlayerCharacter) return false;
            var chance = ModInit.modSettings.InjureOnEjectChance * actor.getSAR_InjureChanceMult();
            var roll = ModInit.Random.NextDouble();
            ModInit.modLog?.Info?.Write($"[IsPilotInjured] - {actor.GetPilot().Callsign} Pilot injury roll {roll} vs chance {chance}. Success? {roll <= chance}.");
            return roll <= chance;
        }

        public static void StripSARTags(this PilotDef pilotDef)
        {
            for (var index = pilotDef.PilotTags.Count - 1; index >= 0; index--)
            {
                var tag = pilotDef.PilotTags[index];
                if (tag.StartsWith(GlobalVars.SAR_GeneralPrefix))
                {
                    pilotDef.PilotTags.Remove(tag);
                }
            }
        }
        public static void AddRecoveredPilotToRoster(this SimGameState sim, Pilot pilot)
        {
//            DataManager.InjectedDependencyLoadRequest injectedDependencyLoadRequest = new DataManager.InjectedDependencyLoadRequest(sim.DataManager);
//            pilot.pilotDef.GatherDependencies(sim.DataManager, injectedDependencyLoadRequest, 1000U);
            pilot.pilotDef.StripSARTags();
            pilot.FromPilotDef(pilot.pilotDef);
//            pilot.Hydrate(null, null);
//            pilot.SimGameInitFromSave();
            sim.PilotRoster.Add(pilot, 0);
            if (!string.IsNullOrEmpty(pilot.pilotDef.Description.Icon))
            {
                LoadRequest loadRequest = sim.DataManager.CreateLoadRequest(null, false);
                loadRequest.AddBlindLoadRequest(BattleTechResourceType.Sprite, pilot.pilotDef.Description.Icon, new bool?(false));
                loadRequest.ProcessRequests(10U);
            }
            else
            {
                pilot.pilotDef.PortraitSettings?.RenderPortrait(sim.DataManager, null, Array.Empty<PortraitManager.PortraitSizes>());
            }
        }

        public static void DismissMissingPilot(this SimGameState sim, Pilot pilot)
        {
            if (pilot == null || !sim.PilotRoster.Contains(pilot))
            {
                return;
            }
            sim.PilotRoster.Remove(pilot);
            sim.RefreshInjuries();
            sim.RoomManager.RefreshDisplay();
        }

        public static FactionValue GetFactionValueFromString(string factionID)
        {
            FactionValue factionValue = FactionEnumeration.GetInvalidUnsetFactionValue();
            bool flag = !string.IsNullOrEmpty(factionID);
            if (flag)
            {
                factionValue = FactionEnumeration.GetFactionByName(factionID);
            }
            return factionValue;
        }

        public static ContractDifficulty GetDifficultyEnumFromValue(int value)
        {
            if (value >= 7)
            {
                return ContractDifficulty.Hard;
            }
            if (value >= 4)
            {
                return ContractDifficulty.Medium;
            }
            return ContractDifficulty.Easy;
        }
        public static void GetDifficultyRangeForContractPublic(this SimGameState sim, StarSystem system, out int minDiff, out int maxDiff)
        {
            int sysDiff = system.Def.GetDifficulty(sim.SimGameMode); 
            int globalDiff = Mathf.FloorToInt(sim.GlobalDifficulty);
            var baseDiff = sysDiff + globalDiff;
            int contractDifficultyVariance = sim.Constants.Story.ContractDifficultyVariance;
            minDiff = Mathf.Max(1, baseDiff - contractDifficultyVariance);
            maxDiff = Mathf.Max(2, baseDiff + contractDifficultyVariance);
            ModInit.modLog?.Info?.Write($"[SAR - GetDifficultyRangeForContractPublic] - fetch difficulty range: Min: {minDiff} Max: {maxDiff} SystemDiff {sysDiff}, GlobalDiff {globalDiff}, basediff: {baseDiff}");
        }
    }
}
