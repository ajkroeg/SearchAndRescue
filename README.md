# SearchAndRescue

**Depends On IRBTModUtils and MapRandomizer!**
**Versions 1.2.0.0 and higher requires MapRandomizer vv1.2.0.0 or higher**
**Versions 1.1.0.0 and higher requires modtek v3 or higher**

This mod expands the consequences for the player's ejected pilots. On ejection, the unit makes a saving roll. On a success, the pilot is recovered normally. On failure, the pilot is listed as MIA and a Search And Rescue mission is added to the list of available contracts. ~~This mod also enables the unused `usesExpiration` and `expirationTimeOverride` for ContractOverrides~~ [this functionality has been moved to MapRandomizer], and by default the SAR Missions will expire (disappear) after 14 days, and the missing pilot will be added to the death wall.

mod.json settings follow:
```
"Settings": {
		"enableDebug": false,
		"enableTrace": true,
		"BasePilotRecoveryChance": 0.5,
		"FriendlyTerritoryRecoveryMult": 1.2,
		"InjureOnEjectChance": 0.5,
		"InjureIgnoreBonusHealth": true,
		"AlwaysRecoverContractIDs": [],
		"RecoveryContractIDs": [
			"Rescue_SAR_IntelligenceAgent_Hard",
			"Rescue_SAR_IntelligenceAgent_Med",
			"Rescue_SAR_AssetRetrieval_Easy"
		],
		"FactionOverrideMap": {
			"Locals": "ClanGhostBear"
		}
	},
```
`enableDebug` and `enableDebug` - bools, enable logging levels. recommend disable debug, leave trace enabled until sure no bugs remain.

`BasePilotRecoveryChance` - float. initialized as a company stat on career start or save load (if missing) which can then be modified by Argo Upgrades or events. further modified by AbstractActor statistic <float> `SAR_RecoveryChanceMult` and `FriendlyTerritoryRecoveryMult`

`FriendlyTerritoryRecoveryMult` - float, multiplier on recovery chance if your employer is also the planet owner. For example, given above settings and unit `SAR_RecoveryChanceMult` set to 1.0, final recovery chance when employer is system owner would be 0.6: `BasePilotRecoveryChance 0.5 x FriendlyTerritoryRecoveryMult 1.2 x SAR_RecoveryChanceMult 1.0 = 0.6`

`InjureOnEjectChance` - float, baseline chance of pilots being injured on ejection. further modified by AbstractActor statistic <float> `SAR_InjuryChanceMult`. Injure on eject is a separate roll from recovery.

`InjureIgnoreBonusHealth` - bool, if true, ejection injury "count" will be increased to compensate for BonusHealth (such as that added by reinforced cockpits)

~~`ContractTimeoutIcon`: string, name of svg resource used for icon when contract has timeout~~ moved to MapRandomizer mod

`AlwaysRecoverContractIDs`: list of contract IDs or ContractTypes (i.e `DefendBase_AllQuiet_NEW` or `DefendBase`)

`RecoveryContractIDs`: list of contract IDs which will be used for pilot rescue contracts. recommend having at least one for each "difficulty" tier. In addition, I highly recommend you include at least one contract of a type that has a valid map for every biome as a "fallback" of sorts. Recovery and SimpleBattle are good candidates.

`FactionOverrideMap`: Dictionary of string:string pairs. If a  pilot ejects and is not recovered versus the KEY faction, the resulting rescue mission will be against the matching VALUE faction. For example, in the above settings, a pilot ejected and not recovered while facing the Locals will have a rescue mission vs ClanGhostBear.

### So how does it work?

On ejection, pilot will make a roll against `BasePilotRecoveryChance` x unit statistic (float) `SAR_RecoveryChanceMult`. On success, pilot is recovered, on failure, pilot is listed as MIA (pilot is dismissed from the company; their current pilotDef (including xp earned, abilities leveled, etc) is serialized. A recovery mission is added to command center. By default these missions expire after 14 days. On accepting and completing the recovery mission, the pilot is recovered, and is restored to the player roster. If the player fails the recovery mission, or if time expires, the missing pilot is no longer recoverable and is added to the death wall. 
