using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
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

        public static void Prefix(ref RewardsGeneratorParams parms)
        {
            Goodwill_Settings settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();
            if (settings.boostRewards)
                parms.rewardValue = parms.rewardValue * (1f + (settings.boostRewardsProcentage / 100f));
            if(settings.enableMinLootValue)
                parms.minGeneratedRewardValue = parms.rewardValue * (settings.minLootValueProOfReward / 100f);
        }



        public static List<Reward> Postfix(List<Reward> __result, RewardsGeneratorParams parms, out float generatedRewardValue)
        {
            Goodwill_Settings settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();

            float unaccountedReward = parms.rewardValue;

            //// generate new items before goodwill rewards are added.
            sirrolin_quests_give_goodwill_ensureRewardWorth(__result, parms, settings, ref unaccountedReward);

            float wantedValue = parms.rewardValue * (1f + (settings.extraGoodwillPro / 100f)) + (settings.extraGoodwillFlat * settings.goodwillWorth);
            unaccountedReward = wantedValue;

            if (parms.giverFaction != null && parms.giverFaction.CanEverGiveGoodwillRewards)
            {
                //// Initiate reward in items value then remove for each item reward to get the missing amount.
                int goodwillIndex = calculateWorthofList(__result, parms, settings, ref unaccountedReward);

                Reward_Goodwill goodwillReward = new Reward_Goodwill();
                goodwillReward.faction = parms.giverFaction;

                //// Get the lowest of "missing goodwill for 100" & "max goodwill gain" & highest of -"Max Loss" & "Available Goodwill for the Amount"
                int goodwill = Math.Min(100 - goodwillReward.faction.PlayerGoodwill,
                    Math.Min(settings.maxGoodwillGain,
                    Math.Max(-settings.maxGoodwillLoss,
                    (int)Math.Ceiling(unaccountedReward / settings.goodwillWorth))));

                goodwillReward.amount = goodwill;

                //// Then account for the reward.
                unaccountedReward -= goodwillReward.amount * settings.goodwillWorth;

                //// finally we add the goodwill.
                if (goodwillReward.amount > 0 || (goodwillReward.amount < 0 && settings.canGoodwillBeNegative))
                {
                    //// If there's already a goodwill
                    if (goodwillIndex != -1)
                    {
                        ((Reward_Goodwill)__result[goodwillIndex]).amount = goodwillReward.amount;
                    }
                    //// If there's no goodwill already
                    else
                    {
                        __result.Add(goodwillReward);
                    }
                } //// if it's 0 (or negative but not allowed) and the quest normally provided Goodwill
                else if (goodwillReward.amount <= 0 && goodwillIndex != -1)
                {
                    __result.Remove(__result[goodwillIndex]);
                }
            }

            if(settings.tooMuchGoodwillGivesExtraLoot)
                sirrolin_quests_give_goodwill_ensureRewardWorth(__result, parms, settings, ref unaccountedReward);

            generatedRewardValue = wantedValue - unaccountedReward;

            return __result;
        }

        private static float sr_qgg_try_generating_new_rewards(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, float unaccountedReward)
        {
            if (unaccountedReward > settings.extraLootMinWorthForTry)
            {
                //// Try to generate items, upto the settings amount.
                for (int i = 1; i < settings.extraLootTries; i++)
                {
                    sirrolin_quests_give_goodwill_generateNewItems(__result, parms, settings, ref unaccountedReward);
                    if (unaccountedReward <= settings.extraLootMinWorthForTry)
                    {
                        break;
                    }
                }
            }

            return unaccountedReward;
        }

        private static int calculateWorthofList(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float unaccountedReward)
        {
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
                        //// In case someone gives goodwill to someone else than the giverFaction
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
                else if (reward is Reward_CampLoot)
                {
                    if (settings.campLootIgnoresGoodwill)
                    {
                        unaccountedReward = 0;
                        break;
                    }
                    else
                    {
                        unaccountedReward -= (settings.campLootProcentValue / 100) * parms.rewardValue;
                    }
                }
                else
                {
                    //// Decrease reward by item value
                    unaccountedReward -= reward.TotalMarketValue;
                }
                counter++;
            }

            return goodwillIndex;
        }

        public static float calculateWorth(Reward reward, Goodwill_Settings settings, RewardsGeneratorParams parms)
        {
            float rewardAmount = 0;
            //// Find out if it's goodwill
            if (reward is Reward_Goodwill goodwill)
            {
                if (goodwill.faction != parms.giverFaction)
                {
                    //// In case someone gives goodwill to someone else than the giverFaction
                    rewardAmount += goodwill.amount * settings.goodwillWorth;
                }
            }
            else if (reward is Reward_RoyalFavor favor)
            {
                //// if it's Favour ignore goodwill.
                if (settings.honourIgnoresGoodwill)
                {
                    rewardAmount = 0;
                }
                else
                {
                    //// Decrease reward by 200 times the favor
                    rewardAmount -= favor.amount * settings.honourWorth;
                }
            }
            else if (reward is Reward_Pawn pawn)
            {
                //// Decrease reward by the value of the pawn - For Some Reason it's special
                rewardAmount += pawn.pawn.MarketValue;
            }
            else if (reward is Reward_CampLoot)
            {
                if (settings.campLootIgnoresGoodwill)
                {
                    rewardAmount = 0;
                }
                else
                {
                    rewardAmount += (settings.campLootProcentValue / 100) * parms.rewardValue;
                }
            }
            else
            {
                //// Decrease reward by item value
                rewardAmount += reward.TotalMarketValue;
            }
            return rewardAmount;
        }

        private static void sirrolin_quests_give_goodwill_generateNewItems(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float unaccountedReward)
        {
            Reward_Items items = new Reward_Items();
            float unaccountedAfterGoodwill = unaccountedReward;
            float worthOfItemsGenerated;
            RewardsGeneratorParams newParm = new RewardsGeneratorParams();
            CopyParms(parms, newParm);
            newParm.rewardValue = unaccountedAfterGoodwill;
            newParm.minGeneratedRewardValue = unaccountedAfterGoodwill * 0.5f;
            items.InitFromValue(unaccountedAfterGoodwill, newParm, out worthOfItemsGenerated); //// future me, yes the worth is accuracte.
            unaccountedReward -= worthOfItemsGenerated;
            sirrolin_quests_give_goodwill_AddItems(__result, items);

            //// Debugging
            if (settings.debuggingOverflow)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Thing t in items.items)
                {
                    sb.Append("Quests Give Goodwill Overflow:" + t.Label + " worth: " + t.MarketValue * t.stackCount + " - ");
                }
                if (sb.Length > 0)
                    Log.Message(sb.ToString());
                else
                    Log.Message("Quests Give Goodwill Overflow: Tried to Getnerate items, but couldn't generate item worth at least:" + newParm.minGeneratedRewardValue + " but looking for " + unaccountedReward);
            }
        }

        private static void sirrolin_quests_give_goodwill_AddItems(List<Reward> __result, Reward_Items items)
        {
            //// Find out if there's already an item in the rewards list.
            Reward_Items resultItems = null;
            for (int i = 0; i < __result.Count; i++)
            {
                if (__result[i] is Reward_Items rItems)
                {
                    resultItems = rItems;
                }
            }

            //// it items are rewarded in the quest reward.
            if(resultItems != null)
            {
                //// for each new reward
                for (int i = 0; i < items.items.Count; i++)
                {
                    Thing thing = items.items[i];
                    //// check if item can be stacked with existing
                    int itemIndexInItems = resultItems.items.FindIndex((resultItem) => resultItem.CanStackWith(items.items[i]));
                    if (itemIndexInItems >= 0)
                    {
                        Thing resultThing = resultItems.items[itemIndexInItems];
                        resultThing.stackCount += thing.stackCount;
                    }
                    //// otherwise add it to the items in the rewards
                    else
                    {
                        resultItems.items.Add(thing);
                    }
                }
            } 
            //// if no items are rewarded for the quest yet.
            else
            {
                __result.Add(items);
            }
        }

        private static void sirrolin_quests_give_goodwill_ensureRewardWorth(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float unaccountedReward)
        {
            if (unaccountedReward > 0 && settings.boostRewards)
            {
                unaccountedReward = sr_qgg_try_generating_new_rewards(__result, parms, settings, unaccountedReward);
                unaccountedReward = sirrolin_quests_give_goodwill_grant_silver_reward(__result, settings, unaccountedReward);
            }
        }

        private static float sirrolin_quests_give_goodwill_grant_silver_reward(List<Reward> __result, Goodwill_Settings settings, float unaccountedReward)
        {
            if (unaccountedReward > 0 && settings.enableSilverRemainder)
            {
                Reward_Items items = new Reward_Items();
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = (int)unaccountedReward;
                items.items.Add(silver);
                sirrolin_quests_give_goodwill_AddItems(__result, items);
                unaccountedReward = 0;

                //// Debugging
                if (settings.debuggingOverflow)
                {
                    Log.Message("Still missing some value: " + silver.Label);
                }
            }

            return unaccountedReward;
        }

        private static void CopyParms(RewardsGeneratorParams parms, RewardsGeneratorParams newParm)
        {
            newParm.allowDevelopmentPoints = parms.allowDevelopmentPoints;
            newParm.allowGoodwill = parms.allowGoodwill;
            newParm.allowRoyalFavor = parms.allowRoyalFavor;
            newParm.allowXenogermReimplantation = parms.allowXenogermReimplantation;
            newParm.giverFaction = parms.giverFaction;
            newParm.giveToCaravan = parms.giveToCaravan;
            newParm.rewardValue = parms.rewardValue;
            newParm.allowRoyalFavor = parms.allowRoyalFavor;
            newParm.minGeneratedRewardValue = parms.minGeneratedRewardValue;
            newParm.thingRewardDisallowed = parms.thingRewardDisallowed;
            newParm.disallowedThingDefs = parms.disallowedThingDefs;
            newParm.chosenPawnSignal = parms.chosenPawnSignal;
        }
    }
}
