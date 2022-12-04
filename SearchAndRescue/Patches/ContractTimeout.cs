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

namespace SearchAndRescue.Patches
{
    public class ContractTimeout
    {
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
            public static void Postfix(SGContractsListItem __instance, Contract contract, SimGameState sim, GameObject ___travelIndicator, ref GameObject ___expirationElement)
            {
                if (contract.UsingExpiration && ___expirationElement == null)

                {
                    var timeLimitIcon =
                        UnityEngine.Object.Instantiate<GameObject>(___travelIndicator,
                            ___travelIndicator.transform.parent);
                    var timeLimitRect = timeLimitIcon.gameObject.GetComponent<RectTransform>();
                    timeLimitRect.anchoredPosition = ___travelIndicator.activeSelf
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
                    ___expirationElement = timeLimitIcon;
                    ___expirationElement.gameObject.SetActive(true);
                }
            }
        }
    }
}
