using System.Collections.Generic;
using System.Linq;
using BattleTech;
using BattleTech.Data;
using BattleTech.Framework;

namespace SearchAndRescue.Framework
{
    public class Classes
    {
        public const string RecoveryChanceStat = "SAR_BaseRecovery";
        public class AddRecoveryContractRequest
        {
            public SimGameState Sim;
            public StarSystem StarSystemSystem;
            public FactionValue Employer;
            public FactionValue Target;
            public List<Biome.BIOMESKIN> Biomes;
            public AddRecoveryContractRequest(SimGameState sim, StarSystem starSystem, FactionValue employer, FactionValue target, List<Biome.BIOMESKIN> biomes)
            {
                Sim = sim;
                StarSystemSystem = starSystem;
                Employer = employer;
                Target = target;
                Biomes = biomes;
            }

            // WE STRAIGHTUP STEALIN THIS FROM BLUE AGAIN
            //private static MethodInfo _getContractRangeDifficultyRange = AccessTools.Method(typeof(SimGameState), "GetContractRangeDifficultyRange");
            //private static MethodInfo _getContractOverrides = AccessTools.Method(typeof(SimGameState), "GetContractOverrides");
            //private static MethodInfo _getValidParticipants = AccessTools.Method(typeof(SimGameState), "GetValidParticipants");
            //private static MethodInfo _hasValidMaps = AccessTools.Method(typeof(SimGameState), "HasValidMaps");
            //private static MethodInfo _hasValidContracts = AccessTools.Method(typeof(SimGameState), "HasValidContracts");
            //private static MethodInfo _hasValidParticipants = AccessTools.Method(typeof(SimGameState), "HasValidParticipants");
            //private static MethodInfo _clearUsedBiomeFromDiscardPile = AccessTools.Method(typeof(SimGameState), "ClearUsedBiomeFromDiscardPile");
            //private static MethodInfo _filterActiveMaps = AccessTools.Method(typeof(SimGameState), "FilterActiveMaps");
            //private static MethodInfo _fillMapEncounterContractData = AccessTools.Method(typeof(SimGameState), "FillMapEncounterContractData");
            //private static MethodInfo _createProceduralContract = AccessTools.Method(typeof(SimGameState), "CreateProceduralContract");

            //private static FieldInfo _fieldSetContractEmployers = AccessTools.Field(typeof(StarSystemDef), "contractEmployerIDs");
            //private static FieldInfo _fieldSetContractTargets = AccessTools.Field(typeof(StarSystemDef), "contractTargetIDs");

            public void AddRecoveryContract()
            {

                // In order to force a given Employer and Target, we have to temoporarily munge the system we're in, such that
                // our Employer/Target are the only valid ones. We undo this at the end of getNewProceduralContract.
                var oldEmployers = new List<string>(StarSystemSystem.Def.contractEmployerIDs);//_fieldSetContractEmployers.GetValue(StarSystemSystem.Def);
                var oldTargets = new List<string>(StarSystemSystem.Def.contractTargetIDs);//(List<string>)_fieldSetContractTargets.GetValue(StarSystemSystem.Def);

                StarSystemSystem.Def.contractEmployerIDs = new List<string>(){ Employer.Name};
            StarSystemSystem.Def.contractTargetIDs = new List<string>(){ Target.Name};

                //_fieldSetContractEmployers.SetValue(StarSystemSystem.Def, new List<string>() { Employer.Name });
                //_fieldSetContractTargets.SetValue(StarSystemSystem.Def, new List<string>() { Target.Name });

                // In addition, we have to make sure that our Target is a valid enemy for the Employer - otherwise the base game's
                // `GenerateContractParticipants` will return an empty list and the contract will fail to generate.
                string[] oldEnemies = Employer.FactionDef.Enemies;
                List<string> enemies = oldEnemies.ToList();
                enemies.Add(Target.Name);
                Employer.FactionDef.Enemies = enemies.ToArray();//Traverse.Create(Employer.FactionDef).Property("Enemies").SetValue(enemies.ToArray());

                ModInit.modLog.Trace?.Write($"AddRecoveryContract: SimGameMode {Sim.SimGameMode}, GlobalDifficulty {Sim.GlobalDifficulty}");
                var difficultyRange = Sim.GetContractRangeDifficultyRange(StarSystemSystem, Sim.SimGameMode, Sim.GlobalDifficulty);//.Invoke(Sim, new object[] {  });

                //Type Diff = difficultyRange.GetType();
                //int min = (int)AccessTools.Field(Diff, "MinDifficulty").GetValue(difficultyRange);
                //AccessTools.Field(Diff, "MinDifficulty").SetValue(difficultyRange, 1);
                difficultyRange.MinDifficulty = 1;
                //int max = (int)AccessTools.Field(Diff, "MaxDifficulty").GetValue(difficultyRange);
                //AccessTools.Field(Diff, "MaxDifficulty").SetValue(difficultyRange, 100);
                difficultyRange.MaxDifficulty = 100;
                //int minClamped = (int)AccessTools.Field(Diff, "MinDifficultyClamped").GetValue(difficultyRange);
                //AccessTools.Field(Diff, "MinDifficultyClamped").SetValue(difficultyRange, ContractDifficulty.INVALID_UNSET);
                difficultyRange.MinDifficultyClamped = ContractDifficulty.INVALID_UNSET;
                //int maxClamped = (int)AccessTools.Field(Diff, "MaxDifficultyClamped").GetValue(difficultyRange);
                //AccessTools.Field(Diff, "MaxDifficultyClamped").SetValue(difficultyRange, ContractDifficulty.Hard);
                difficultyRange.MaxDifficultyClamped = ContractDifficulty.Hard;
                ModInit.modLog.Trace?.Write($"AddRecoveryContract difficultyRange: MinDifficulty 1, MaxDifficulty 100, MinClamped {ContractDifficulty.INVALID_UNSET}, MaxClamped {ContractDifficulty.Hard}");

                var validTypes = new int[] { (int)ContractType.Rescue }; // force only Rescue types

                var potentialContractsByType = Sim.GetContractOverrides(difficultyRange, validTypes);//Dictionary<int, List<ContractOverride>>)_getContractOverrides.Invoke(Sim, new object[] { difficultyRange, validTypes });

                var filteredContractsByType = new Dictionary<int, List<ContractOverride>>();
                foreach (var type in potentialContractsByType)
                {
                    var filtered = new List<ContractOverride>();
                    foreach (var contractOverride in type.Value)
                    {
                        if (ModInit.modSettings.RecoveryContractIDs.Contains(contractOverride.ID) || ModState.ContractNames.Contains(contractOverride.contractName))
                        {
                            filtered.Add(contractOverride);
                        }
                    }

                    if (filtered.Count > 0)
                    {
                        filteredContractsByType.Add(type.Key, filtered);
                    }
                }

                WeightedList<MapAndEncounters> playableMaps =
                    MetadataDatabase.Instance.GetReleasedMapsAndEncountersBySinglePlayerProceduralContractTypeAndTags(
                StarSystemSystem.Def.MapRequiredTags, StarSystemSystem.Def.MapExcludedTags, Biomes, true)
                .ToWeightedList(WeightedListType.SimpleRandom);

                var validParticipants = Sim.GetValidParticipants(StarSystemSystem);//_getValidParticipants.Invoke(Sim, new object[] { StarSystemSystem }));

                if (!(bool)Sim.HasValidMaps(StarSystemSystem, playableMaps))//.Invoke(Sim, new object[] { StarSystemSystem, playableMaps }))
                {
                    ModInit.modLog.Trace?.Write($"AddRecoveryContract - false _hasValidMaps");
                    return;
                }
                else if (!(bool)Sim.HasValidContracts(difficultyRange, filteredContractsByType))//Invoke(Sim, new object[] { difficultyRange, filteredContractsByType }))
                {
                    ModInit.modLog.Trace?.Write($"AddRecoveryContract - false _hasValidContracts");
                    return;
                }
                else if (!(bool)Sim.HasValidParticipants(StarSystemSystem, validParticipants))//.Invoke(Sim, new object[] { StarSystemSystem, validParticipants }))
                {
                    ModInit.modLog.Trace?.Write($"AddRecoveryContract - false _hasValidParticipants");
                    return;
                }
                Sim.ClearUsedBiomeFromDiscardPile(playableMaps);
                //_clearUsedBiomeFromDiscardPile.Invoke(Sim, new object[] { playableMaps });
                IEnumerable<int> mapWeights = from map in playableMaps
                                              select map.Map.Weight;

                var activeMaps = new WeightedList<MapAndEncounters>(WeightedListType.WeightedRandom, playableMaps.ToList(), mapWeights.ToList<int>(), 0);

                //_filterActiveMaps.Invoke(sim, new object[] { activeMaps, Sim.GlobalContracts });
                activeMaps.Reset(false);
                MapAndEncounters level = activeMaps.GetNext(false);

                var MapEncounterContractData = Sim.FillMapEncounterContractData(StarSystemSystem, difficultyRange, filteredContractsByType, validParticipants, level);//_fillMapEncounterContractData.Invoke(Sim, new object[] { StarSystemSystem, difficultyRange, filteredContractsByType, validParticipants, level }));
                bool HasContracts = MapEncounterContractData.HasContracts;//Traverse.Create(MapEncounterContractData).Property("HasContracts").GetValue<bool>();
                while (!HasContracts && activeMaps.ActiveListCount > 0)
                {
                    level = activeMaps.GetNext(false);
                    MapEncounterContractData = Sim.FillMapEncounterContractData(StarSystemSystem, difficultyRange, filteredContractsByType, validParticipants, level);//_fillMapEncounterContractData.Invoke(Sim, new object[] { StarSystemSystem, difficultyRange, filteredContractsByType, validParticipants, level }));
                }
                StarSystemSystem.SetCurrentContractFactions(FactionEnumeration.GetInvalidUnsetFactionValue(), FactionEnumeration.GetInvalidUnsetFactionValue());
                HashSet<int> Contracts = MapEncounterContractData.Contracts;//Traverse.Create(MapEncounterContractData).Field("Contracts").GetValue<HashSet<int>>();

                if (MapEncounterContractData == null || Contracts.Count == 0)
                {
                    List<string> mapDiscardPile = Sim.mapDiscardPile;//Traverse.Create(Sim).Field("mapDiscardPile").GetValue<List<string>>();
                    if (mapDiscardPile.Count > 0)
                    {
                        mapDiscardPile.Clear();
                    }
                    else
                    {
                        ModInit.modLog.Error?.Write($"Unable to find any valid contracts for available map pool.");
                    }
                }
                GameContext gameContext = new GameContext(Sim.Context);
                gameContext.SetObject(GameContextObjectTagEnum.TargetStarSystem, StarSystemSystem);

                Contract contract = Sim.CreateProceduralContract(StarSystemSystem, true, level, MapEncounterContractData, gameContext);// (Contract)_createProceduralContract.Invoke(Sim, new object[] { StarSystemSystem, true, level, MapEncounterContractData, gameContext });

                // Restore system and faction to previous values, now that we've forced the game to generate our desired contract.

                StarSystemSystem.Def.contractEmployerIDs = oldEmployers;//_fieldSetContractEmployers.SetValue(StarSystemSystem, oldEmployers);
                StarSystemSystem.Def.contractTargetIDs = oldTargets;//_fieldSetContractTargets.SetValue(StarSystemSystem, oldTargets);
                Employer.FactionDef.Enemies = oldEnemies;//Traverse.Create(Employer.FactionDef).Property("Enemies").SetValue(oldEnemies);

                Sim.RoomManager.CmdCenterRoom.ClearHoldForContract();

                return;

            }
        }
        public class MissingPilotInfo
        {
            public PilotDef MissingPilotDef;
            public string PilotSimUID;
            public string MissingPilotSystem;
            //public Biome.BIOMESKIN PilotBiomeSkin;
            public string SAR_Opfor = "";
            public bool CurrentContract = false;
            public string RecoveryContractGUID = "";

            public MissingPilotInfo()
            {
                MissingPilotDef = new PilotDef();
                PilotSimUID = "";
                MissingPilotSystem = null;
                //PilotBiomeSkin = Biome.BIOMESKIN.UNDEFINED;
                CurrentContract = false;
                SAR_Opfor = "";
                RecoveryContractGUID = "";
            }

            public MissingPilotInfo(PilotDef missingPilotDef, string pilotSimUID, string missingPilotSystem, string sarOpfor, bool currentContract)
            {
                MissingPilotDef = missingPilotDef;
                PilotSimUID = pilotSimUID;
                MissingPilotSystem = missingPilotSystem;
                //PilotBiomeSkin = pilotBiomeSkin;
                SAR_Opfor = sarOpfor;
                CurrentContract = currentContract;
            }
        }
    }
}