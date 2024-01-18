using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SirRolin.QuestsGiveGoodwill.HarmonyPatches
{
    [HarmonyPatch(typeof(RewardsGenerator), "DoGenerate")]
    public static class goodwillGiver
    {
        public static List<Reward> Postfix(List<Reward> __result, RewardsGeneratorParams parms)
        {
            if (parms.giverFaction != null && parms.giverFaction.CanEverGiveGoodwillRewards)
            {
                Goodwill_Settings settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();
                //// Initiate reward in items value then remove for each item reward to get the missing amount.
                float unaccountedReward = parms.rewardValue * (1f + (settings.extraGoodwillPro / 100f)) + (settings.extraGoodwillFlat * settings.goodwillWorth);
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
                            unaccountedReward -= goodwill.amount * settings.goodwillWorth;
                        }
                    }
                    else if (reward is Reward_RoyalFavor favor)
                    {

                        //// if it's Favour ignore goodwill.
                        if (settings.honourIgnoresGoodwill)
                        {
                            unaccountedReward = 0;
                            break;
                        }
                        else
                        {
                            //// Decrease reward by 200 times the favor
                            unaccountedReward -= favor.amount * settings.honourWorth;
                        }
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

                Reward_Goodwill goal = new Reward_Goodwill();
                goal.faction = parms.giverFaction;
                goal.amount = (int)(unaccountedReward / settings.goodwillWorth);

                ////Overflow
                if ((100 - goal.faction.PlayerGoodwill) < goal.amount)
                {
                    goal.amount = (100 - goal.faction.PlayerGoodwill);
                    //// Getting the items value of the reward.
                    Reward_Items items = new Reward_Items();
                    float unaccountedAfterGoodwill = unaccountedReward - (goal.amount * settings.goodwillWorth);
                    float worthOfItemsGenerated;
                    items.InitFromValue(unaccountedAfterGoodwill, parms, out worthOfItemsGenerated);
                    unaccountedReward -= worthOfItemsGenerated;

                    //// Debugging
                    if (settings.debuggingOverflow)
                    {
                        StringBuilder sb = new StringBuilder();
                        foreach (Thing t in items.items)
                        {
                            sb.Append(t.Label + " worth: " + t.MarketValue * t.stackCount + " - ");
                        }
                        Log.Message(sb.ToString());
                    }

                    if (unaccountedReward > goal.amount * settings.goodwillWorth)
                    {
                        Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                        silver.stackCount = (int)(unaccountedReward - (goal.amount * settings.goodwillWorth));
                        items.items.Add(silver);

                        //// Debugging
                        if (settings.debuggingOverflow)
                        {
                            Log.Message("Still missing some value: " + silver.Label);
                        }
                    }
                    else
                    {
                        StringBuilder sbTooMuch = new StringBuilder();
                        sbTooMuch.Append("Game gave us too much: ");
                        //// update the goodwill after introducing more rewards
                        //// ignored within 2 goodwill, cause it feels bad to not get it filled.
                        if (Mathf.Abs(goal.amount - (unaccountedReward / settings.goodwillWorth)) > 2) {
                            goal.amount = (int) (unaccountedReward / settings.goodwillWorth);
                            sbTooMuch.Append(goal.amount + " from " + unaccountedReward + " where each are worth " + settings.goodwillWorth);
                        } 
                        else
                        {
                            sbTooMuch.Append("But was within 2 of the goal: " + (unaccountedReward / settings.goodwillWorth));
                        }
                        if (settings.debuggingOverflow)
                        {
                            Log.Message(sbTooMuch.ToString());
                        }
                    }
                    __result.Add(items);
                }

                //// adding the Goodwill
                if (goal.amount > 0 || (goal.amount < 0 && settings.canGoodwillBeNegative))
                {
                    //// If there's already a goodwill
                    if (goodwillIndex != -1)
                    {
                        ((Reward_Goodwill) __result[goodwillIndex]).amount = goal.amount;
                    }
                    //// If there's no goodwill already
                    else
                    {
                        __result.Add(goal);
                    }
                } //// if it's 0 and the quest normally provided Goodwill
                else if (goal.amount == 0 & goodwillIndex != -1)
                {
                    __result.Remove(__result[goodwillIndex]);
                }
            }
            return __result;
        }
    }
}
