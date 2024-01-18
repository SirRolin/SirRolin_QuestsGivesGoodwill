using System.Collections.Generic;

using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;


[HarmonyPatch(typeof(RewardsGenerator), "DoGenerate")]
class PatchGoodwill {
    const float GoodwillWorth = 100;
    const float extraGoodwillPro = 1.2f;
    const int extraGoodwillFlat = 2;
    public static List<Reward> Postfix(List<Reward> __result, RewardsGeneratorParams parms)
    {
        if (parms.giverFaction != null && parms.giverFaction.CanEverGiveGoodwillRewards)
        {
            //// Initiate reward in silver value then remove for each item reward to get the missing amount.
            float unaccountedReward = parms.rewardValue * extraGoodwillPro + extraGoodwillFlat * GoodwillWorth;
            int goodwillIndex = -1; //To check if there's already a goodwill for the faction.
            int counter = 0;
            foreach (Reward reward in __result)
            {

                //// Find out if it's goodwill
                if (reward is Reward_Goodwill goodwill)
                {
                    if (goodwill.faction == parms.giverFaction)
                    {
                        goodwillIndex = counter;
                    }
                    else
                    {
                        //// Decrease reward by 100 times the goodwill
                        unaccountedReward -= goodwill.amount * GoodwillWorth;
                    }
                }
                else if (reward is Reward_RoyalFavor favor)
                {
                    ////// Decrease reward by 200 times the favor
                    //unaccountedReward -= favor.amount * 200;

                    //// if it's Favour ignore goodwill.
                    unaccountedReward = 0;
                    break;
                }
                else if (reward is Reward_Pawn pawn)
                {
                    //// Decrease reward by the value of the pawn - For Some Reason it's special
                    unaccountedReward -= pawn.pawn.MarketValue;
                }
                else
                {
                    //// Decrease reward by item value
                    unaccountedReward -= reward.TotalMarketValue;
                }

                counter++;
            }

            //// If there's already a goodwill
            if (goodwillIndex != -1)
            {
                ((Reward_Goodwill)__result[goodwillIndex]).amount = Mathf.RoundToInt(unaccountedReward / GoodwillWorth);
            }
            //// If there's no goodwill already and the rewards are worth less than they should (which the 20% makes true most of the time.)
            else if (unaccountedReward >= GoodwillWorth)
            {
                Reward_Goodwill goodwill = new Reward_Goodwill
                {
                    faction = parms.giverFaction,
                    amount = Mathf.RoundToInt(unaccountedReward / GoodwillWorth)
                };
                __result.Add(goodwill);
            }
            //// If there's no goodwill already and the rewards are worth more than they should
            else if (unaccountedReward <= -GoodwillWorth)
            {
                Reward_Goodwill goodwill = new Reward_Goodwill
                {
                    faction = parms.giverFaction,
                    amount = -Mathf.RoundToInt(-unaccountedReward / GoodwillWorth)
                };
                __result.Add(goodwill);
            }
        }
        return __result;
    }
}

[StaticConstructorOnStartup]
public class PatchMain
{
    static PatchMain()
    {
        Harmony val = new Harmony("SR.QGG");
        val.PatchAll();
    }
}