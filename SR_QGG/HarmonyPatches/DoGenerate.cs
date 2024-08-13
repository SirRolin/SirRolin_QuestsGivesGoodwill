using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace SirRolin.QuestsGiveGoodwill.HarmonyPatches
{
    [HarmonyPatch(typeof(RewardsGenerator), "DoGenerate")]
    public static class GoodwillGiver
    {
        private static Goodwill_Settings settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();

        private class DiscardHelper
        {
            public List<(int count, float unitValue, Thing thing)> Things = new List<(int count, float unitValue, Thing thing)>();
            public float estimationValue;
            public DiscardHelper(float estimatedValue)
            {
                estimationValue = estimatedValue;
            }
            public void AddToList(int amount, Thing thing)
            {
                Things.Add((amount, thing.MarketValue, thing));
            }
            public DiscardHelper getCopy()
            {
                DiscardHelper output = new DiscardHelper(estimationValue);
                Things.ForEach((each) =>
                {
                    output.AddToList(each.count, each.thing);
                });
                return output;
            }
            public float getValue()
            {
                return Things.Sum(x => x.count * x.unitValue);
            }
            public float getWeightedValue()
            {
                return Math.Abs(Things.Sum(x => x.count * x.unitValue) - estimationValue) + settings.cleanupItemWeight * Things.Sum(x => x.count);
            }
            public void ReplaceIfBetter(ref DiscardHelper dh, StringBuilder debugStrB)
            {
                if (dh.getWeightedValue() > getWeightedValue())
                {
                    if (settings.debuggingVerbose) debugStrB.Append("Discarded for new:\n" + dh.ToString());
                    dh = this;
                }
                else if (settings.debuggingVerbose && (dh.Things.Count != Things.Count || dh.getValue() != getValue()))
                {
                    debugStrB.Append("Doesn't make it:\n" + ToString());
                }
            }
            public void Execute(List<Reward> ri, ref float unaccountedValue)
            {
                Things.ForEach(thing =>
                {
                    thing.thing.stackCount -= thing.Item1;
                });
                unaccountedValue -= getValue();

                for (int j = ri.Count - 1; j >= 0; j--)
                {
                    if (ri[j] is Reward_Items list)
                    {
                        for (int i = list.items.Count - 1; i >= 0; i--)
                        {
                            if (list.items[i].stackCount == 0)
                            {
                                list.items.RemoveAt(i);
                            }
                        }
                    }
                }
            }
            override public String ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Weight: " + getWeightedValue() + " - worth: " + getValue());
                foreach (var item in Things)
                {
                    sb.AppendLine(item.count.ToString() + "x " + item.thing.GetCustomLabelNoCount() + " (" + Math.Round(item.count * item.unitValue, 2) + ", " + Math.Round(item.unitValue, 2) + "/u)");
                }

                return sb.ToString();
            }
        }

        public static void Prefix(ref RewardsGeneratorParams parms)
        {
            settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();
            if (settings.boostRewards)
                parms.rewardValue = parms.rewardValue * (1f + (settings.boostRewardsProcentage / 100f));
            if (settings.enableMinLootValue)
                parms.minGeneratedRewardValue = parms.rewardValue * (settings.minLootValueProOfReward / 100f);
        }



        public static List<Reward> Postfix(List<Reward> __result, RewardsGeneratorParams parms, out float generatedRewardValue)
        {

            settings = LoadedModManager.GetMod<SirRolin.QuestsGiveGoodwill.QuestsGiveGoodwill>().GetSettings<Goodwill_Settings>();

            //// Debugging
            if (settings.debuggingVerbose)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("_result: " + PrintRewardList(__result) + "\nparms: " + parms.ToString());
                if (sb.Length > 0)
                    Log.Message(sb.ToString());
            }

            if (!parms.thingRewardDisallowed)
            {
                float unaccountedReward = parms.rewardValue;

                // Calculate the amount of value that's missing and getting the index of any goodwill while at it.
                int goodwillIndex = CalculateWorthofList(__result, parms, settings, ref unaccountedReward);

                //// Generate new items before goodwill rewards are added.
                TryGeneratingNewRewards(__result, parms, unaccountedReward);

                if (settings.tooMuchGoodwillGivesSilver)
                    EnsureRewardWorth(__result, parms, settings, ref unaccountedReward);

                float goodwillWorthToAdd = unaccountedReward + parms.rewardValue * (settings.extraGoodwillPro / 100f) + (settings.extraGoodwillFlat * settings.goodwillWorth);

                if (parms.giverFaction != null && parms.giverFaction.CanEverGiveGoodwillRewards && !parms.thingRewardItemsOnly)
                {
                    //// Initiate reward in items value then remove for each item reward to get the missing amount.
                    Reward_Goodwill goodwillReward = new Reward_Goodwill();
                    goodwillReward.faction = parms.giverFaction;

                    //// Get the lowest of "missing goodwill for 100" & "max goodwill gain" & highest of -"Max Loss" & "Available Goodwill for the Amount"
                    int goodwill = Math.Min(100 - goodwillReward.faction.PlayerGoodwill,
                        Math.Min(settings.maxGoodwillGain,
                        Math.Max(-settings.maxGoodwillLoss,
                        (int)Math.Ceiling(goodwillWorthToAdd / settings.goodwillWorth))));

                    goodwillReward.amount += goodwill;

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

                generatedRewardValue = goodwillWorthToAdd - unaccountedReward;
            }
            else
            {
                generatedRewardValue = 0;
            }


            return __result;
        }

        private static String PrintRewardList(List<Reward> rewards)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in rewards)
            {
                if (item is Reward_Items ri)
                {
                    foreach (var item1 in ri.items)
                    {
                        sb.AppendLine(item1.stackCount + "x " + item1.GetCustomLabelNoCount() + " (" + (item1.MarketValue * item1.stackCount) + ")");
                    }
                }
                else if (item is Reward_Pawn rp)
                {
                    sb.AppendLine("Pawn: " + rp.pawn.Name + " (" + rp.pawn.MarketValue + ")");
                }
                else if (item is Reward_Goodwill rg)
                {
                    sb.AppendLine("Goodwill: (" + rg.amount * settings.goodwillWorth + ")");
                }
                else if (item is Reward_RoyalFavor rh)
                {
                    sb.AppendLine("Honour: (" + rh.amount * settings.honourWorth + ")");
                }
                else
                {
                    sb.AppendLine("Other: " + item.GetType().Name + "(" + item.TotalMarketValue + ")");
                }
            }
            return sb.ToString();
        }

        private static float TryGeneratingNewRewards(List<Reward> __result, RewardsGeneratorParams parms, float unaccountedReward)
        {
            if (unaccountedReward > settings.extraLootMinWorthForTry)
            {
                //// Try to generate items, upto the settings amount.
                for (int i = 1; i < settings.extraLootTries; i++)
                {
                    GenerateNewItems(__result, parms, settings, ref unaccountedReward);
                    if (unaccountedReward <= settings.extraLootMinWorthForTry)
                    {
                        break;
                    }
                }
            }

            // Merge multiple stacks of the same item into 1
            foreach (var item in __result)
            {
                if (item is Reward_Items ri)
                {
                    for (int i = ri.items.Count - 1; i > 0; i--)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (ri.items[i].CanStackWith(ri.items[j]))
                            {
                                ri.items[j].stackCount += ri.items[i].stackCount;
                                ri.items.RemoveAt(i);
                                break;
                            }
                        }
                    }
                }
            }

            // Cleanup if enabled
            if (settings.enableCleanupLogic)
                ReduceItems(__result, ref unaccountedReward, settings.goodwillWorth * settings.extraGoodwillFlat);

            return unaccountedReward;
        }

        private static int CalculateWorthofList(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float unaccountedReward)
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

        public static float CalculateWorth(Reward reward, Goodwill_Settings settings, RewardsGeneratorParams parms)
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

        private static void GenerateNewItems(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float unaccountedReward)
        {
            Reward_Items items = new Reward_Items();
            float unaccountedAfterGoodwill = unaccountedReward;
            float worthOfItemsGenerated;
            RewardsGeneratorParams newParm = new RewardsGeneratorParams();
            CopyParms(parms, newParm);
            newParm.rewardValue = unaccountedAfterGoodwill;
            newParm.minGeneratedRewardValue = unaccountedAfterGoodwill * 0.5f;
            items.InitFromValue(unaccountedAfterGoodwill, newParm, out worthOfItemsGenerated); //// future me, yes the worth is accurate.
            unaccountedReward -= worthOfItemsGenerated;

            AddItems(__result, items);

            //// Debugging
            if (settings.debuggingOverflow)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Quests Give Goodwill Overflow:");
                foreach (Thing t in items.items)
                {
                    sb.AppendLine(t.Label + " worth: " + t.MarketValue * t.stackCount);
                }
                if (sb.Length > 0)
                    Log.Message(sb.ToString());
                else
                    Log.Message("Quests Give Goodwill Overflow: Tried to Generate items, but couldn't generate item worth at least:" + newParm.minGeneratedRewardValue + " but looking for " + unaccountedReward);
            }
        }
        private static void ReduceItems(List<Reward> items, ref float unaccountedReward, float offset)
        {
            List<(float mValue, Thing thing)> sortingList = new List<(float, Thing)>();
            foreach (var item in items)
            {
                if (item is Reward_Items reward_item)
                {
                    foreach (var item1 in reward_item.items)
                    {
                        sortingList.Add((item1.MarketValue, item1));
                    }
                }
            }
            if (sortingList.Count == 0 || offset - unaccountedReward <= 0)
            {
                return;
            }

            // Sort
            sortingList.SortByDescending(thing => thing.mValue);

            // Find highest value item that has a lower unit value than requested change
            int startIndex = 0;
            while (sortingList[startIndex].mValue > offset - unaccountedReward)
            {
                startIndex++;
                if (items.Count == startIndex)
                {
                    break;
                }
            }
            // if it's not the first, start at the item that costs slightly more than the requested change.
            if (startIndex > 0)
            {
                startIndex--;
            }

            // The best fit discard helper
            DiscardHelper discardHelper = new DiscardHelper(offset - unaccountedReward);

            // for building up a strin to the debug.
            StringBuilder debugStrb = new StringBuilder();
            if (settings.debuggingVerbose)
            {
                debugStrb.AppendLine("SR_QGG: Value to trim: " + (offset - unaccountedReward) + " - All items: ");
                sortingList.Skip(startIndex).OrderBy(x => x.mValue).Do((thing) => { debugStrb.AppendLine(thing.thing.stackCount + "x " + thing.thing.GetCustomLabelNoCount() + " (" + thing.mValue + "/u)"); });
            }

            // Initiate the construction of the many examples.
            // The way this works is by recursively calling the recursiveAdding, which only adds to the list at the end of the sorting list OR if it has exceeded it's cost.
            for (int i = startIndex; i < sortingList.Count; i++)
            {
                RecursiveAdding(ref discardHelper, sortingList, new DiscardHelper(offset - unaccountedReward), i, debugStrb);
            }


            // Debugging looking at all the posibilities.
            if (settings.debuggingVerbose)
            {
                debugStrb.AppendLine(discardHelper.ToString());
                Log.Message(debugStrb.ToString());
                debugStrb.Clear();
            }

            // Only executing the one found the most worth.
            discardHelper.Execute(items, ref unaccountedReward);
            discardHelper = null;
        }

        private static void RecursiveAdding(ref DiscardHelper currentBestDiscard, List<(float mValue, Thing thing)> choiceItems, DiscardHelper currentChecker, int currentIndex, StringBuilder debugStrB)
        {
            // max of this items we could remove
            int maxItems = (int)Math.Ceiling((currentChecker.estimationValue - currentChecker.getValue()) / choiceItems[currentIndex].mValue);

            // Limit it to the amount we have.
            maxItems = Math.Min(maxItems, choiceItems[currentIndex].thing.stackCount);

            // if no items can be added
            if (maxItems <= 0)
            {
                currentChecker.ReplaceIfBetter(ref currentBestDiscard, debugStrB);
                return;
            }
            // if at last item
            else if (currentIndex + 1 == choiceItems.Count)
            {
                // make a copy so we can have max items and 1 less.
                DiscardHelper otherChecker = currentChecker.getCopy();

                // add the ceiling
                currentChecker.AddToList(maxItems, choiceItems[currentIndex].thing);
                currentChecker.ReplaceIfBetter(ref currentBestDiscard, debugStrB);

                // add the floor
                if (maxItems > 1 && (currentChecker.getValue() > currentChecker.estimationValue))
                {
                    otherChecker.AddToList(maxItems - 1, choiceItems[currentIndex].thing);
                    otherChecker.ReplaceIfBetter(ref currentBestDiscard, debugStrB);
                }

                return;
            }

            // Steps to reduce by, min 1, otherwise at least 100 silver a go.
            int step = (int)Math.Max(100 / Math.Floor(choiceItems[currentIndex].mValue), 1);

            // The Recursive part of this function, add j count of item at Index, then call it with the next index again.
            for (int j = maxItems; j >= 0; j -= step)
            {
                DiscardHelper helperCopy = currentChecker.getCopy();
                if (j > 0)
                    helperCopy.AddToList(j, choiceItems[currentIndex].thing);
                RecursiveAdding(ref currentBestDiscard, choiceItems, helperCopy, currentIndex + 1, debugStrB);
            }
        }

        private static void AddItems(List<Reward> __result, Reward_Items items)
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
            if (resultItems != null)
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

        private static void EnsureRewardWorth(List<Reward> __result, RewardsGeneratorParams parms, Goodwill_Settings settings, ref float unaccountedReward)
        {
            if (unaccountedReward > 0 && settings.boostRewards)
            {
                unaccountedReward -= Grant_silver_reward(__result, settings, parms.rewardValue - unaccountedReward);
            }
        }

        private static float Grant_silver_reward(List<Reward> __result, Goodwill_Settings settings, float silverWanted)
        {
            if (silverWanted > 0 && settings.enableSilverRemainder)
            {
                Reward_Items items = new Reward_Items();
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = (int)silverWanted;
                items.items.Add(silver);
                AddItems(__result, items);

                //// Debugging
                if (settings.debuggingOverflow)
                {
                    Log.Message("Granted some Silver: " + silver.Label);
                }
                return silverWanted;
            }

            return 0;
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
