using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Turns an expedition setup into items. Follows docs/FORAGING.md step by step; the web
    /// sandbox does the same, so keep them in sync when the rules change.
    /// </summary>
    public static class ForagingService
    {
        // ----- Resolving (pure: no state changes) -----

        public static ForageResult Resolve(DesignData design, ExpeditionSetup setup, IForageContext context, uint seed)
        {
            ForageResult result = Resolve(design, setup, context, new Mulberry32(seed));
            result.seed = seed;
            return result;
        }

        public static ForageResult Resolve(DesignData design, ExpeditionSetup setup, IForageContext context, IRandom rng)
        {
            var result = new ForageResult();
            ForageTableData table = design.GetForageTable(setup.tableId);
            if (table == null) return result;

            // 1. Modifiers: companion, then perks, then focus.
            List<ForageModifier> modifiers = CollectModifiers(design, setup);

            // 2-3. Eligible entries and their effective weights.
            List<Candidate> eligible = GetEligible(design, table, setup.time, context, modifiers);

            // 4. Number of rolls.
            int rollCount = CountRolls(table, modifiers);
            result.rollCount = rollCount;

            var foundUniques = new HashSet<string>();

            // 5. Normal rolls.
            for (int i = 0; i < rollCount; i++)
            {
                ForageRoll roll = Roll(eligible, null, foundUniques, modifiers, rng, false);
                if (roll != null) result.rolls.Add(roll);
            }

            // 6. Bonus rolls.
            foreach (ForageModifier modifier in modifiers)
            {
                if (modifier.type != ModifierType.BonusRoll) continue;
                double x = rng.NextDouble();
                if (x >= modifier.value) continue;
                ForageRoll roll = Roll(eligible, modifier.tag, foundUniques, modifiers, rng, true);
                if (roll != null) result.rolls.Add(roll);
            }

            // 7. Merge into stacks and flag rare finds.
            foreach (ForageRoll roll in result.rolls)
            {
                ItemStack stack = result.stacks.Find(s => s.itemId == roll.itemId);
                if (stack == null) result.stacks.Add(new ItemStack(roll.itemId, roll.qty));
                else stack.qty += roll.qty;
            }

            foreach (ItemStack stack in result.stacks)
            {
                ItemData item = design.GetItem(stack.itemId);
                if (item != null && item.rarity >= Rarity.Rare) result.rareFinds.Add(stack.itemId);
            }

            return result;
        }

        /// <summary>Odds for each item with this setup, for the setup screen and tuning.</summary>
        public static ForagePreview Preview(DesignData design, ExpeditionSetup setup, IForageContext context)
        {
            var preview = new ForagePreview();
            ForageTableData table = design.GetForageTable(setup.tableId);
            if (table == null) return preview;

            List<ForageModifier> modifiers = CollectModifiers(design, setup);
            List<Candidate> eligible = GetEligible(design, table, setup.time, context, modifiers);
            int rolls = CountRolls(table, modifiers);
            preview.rolls = rolls;

            double total = 0;
            foreach (Candidate candidate in eligible) total += candidate.weight;

            foreach (Candidate candidate in eligible)
            {
                double p = total > 0 ? candidate.weight / total : 0;
                double averageQty = candidate.item.unique
                    ? 1
                    : (candidate.entry.min + candidate.entry.max) / 2.0 + QuantityBonus(candidate.item, modifiers);
                preview.odds.Add(new ForageOdds
                {
                    itemId = candidate.entry.itemId,
                    weight = candidate.weight,
                    chancePerRoll = p,
                    chanceAtLeastOne = 1 - Math.Pow(1 - p, rolls),
                    expectedQty = rolls * p * averageQty
                });
            }

            return preview;
        }

        /// <summary>Companion modifiers, then each perk's, then one TagWeight per focus tag.</summary>
        public static List<ForageModifier> CollectModifiers(DesignData design, ExpeditionSetup setup)
        {
            var modifiers = new List<ForageModifier>();
            CompanionData companion = design.GetCompanion(setup.companionId);
            if (companion != null) modifiers.AddRange(companion.modifiers);

            if (setup.perkIds != null)
            {
                foreach (string perkId in setup.perkIds)
                {
                    PerkData perk = design.GetPerk(perkId);
                    if (perk != null) modifiers.AddRange(perk.modifiers);
                }
            }

            FocusData focus = design.GetFocus(setup.focusId);
            if (focus != null)
            {
                foreach (string tag in focus.tags)
                    modifiers.Add(new ForageModifier(ModifierType.TagWeight, focus.multiplier, tag));
            }

            return modifiers;
        }

        // ----- Running an expedition in the game (changes state) -----

        /// <summary>
        /// Call when the player presses Go. Stores the expedition in the state so a refresh during
        /// the dim or the results screen doesn't lose or duplicate it. Save straight after.
        /// </summary>
        public static PendingExpedition Begin(GameState state, ExpeditionSetup setup, uint seed)
        {
            if (state.hasPendingExpedition) return state.pendingExpedition;

            state.expeditionsStarted++;
            state.pendingExpedition = new PendingExpedition
            {
                id = state.expeditionsStarted,
                seed = seed,
                setup = setup
            };
            state.hasPendingExpedition = true;
            return state.pendingExpedition;
        }

        /// <summary>The result of the pending expedition, worked out again from its seed. Null if there isn't one.</summary>
        public static ForageResult ResolvePending(DesignData design, GameState state)
        {
            if (!state.hasPendingExpedition) return null;
            PendingExpedition pending = state.pendingExpedition;
            return Resolve(design, pending.setup, new GameStateForageContext(state), pending.seed);
        }

        /// <summary>
        /// Pays out the pending expedition exactly once: adds the items, counts the trip for the
        /// companion and teaches its perk when due. Calling it again does nothing.
        /// </summary>
        public static ExpeditionClaim Claim(DesignData design, GameState state, Economy economy, GameEvents events)
        {
            var claim = new ExpeditionClaim();
            if (!state.hasPendingExpedition) return claim;

            ForageResult result = ResolvePending(design, state);
            ExpeditionSetup setup = state.pendingExpedition.setup;
            state.hasPendingExpedition = false;
            state.pendingExpedition = new PendingExpedition();

            var payout = new Transaction();
            foreach (ItemStack stack in result.stacks) payout.Gain(stack.itemId, stack.qty);
            if (payout.gains.Count > 0 && !economy.TryTransact(payout))
            {
                // Only possible if the design data changed under a pending expedition. Pay what we can.
                foreach (ItemStack stack in result.stacks) economy.Add(stack.itemId, stack.qty);
            }

            CompanionData companion = design.GetCompanion(setup.companionId);
            if (companion != null)
            {
                CompanionProgress progress = state.GetOrAddCompanion(companion.id);
                progress.trips++;
                bool canTeach = !string.IsNullOrEmpty(companion.teachesPerk) && design.GetPerk(companion.teachesPerk) != null;
                if (canTeach && progress.trips >= companion.tripsToTeach && !state.unlockedPerks.Contains(companion.teachesPerk))
                {
                    state.unlockedPerks.Add(companion.teachesPerk);
                    claim.learnedPerkId = companion.teachesPerk;
                    events?.RaisePerkLearned(companion.teachesPerk);
                }
            }

            state.expeditionsCompleted++;
            claim.paid = true;
            claim.result = result;
            events?.RaiseExpeditionCompleted(result);
            return claim;
        }

        // ----- Internals -----

        private class Candidate
        {
            public ForageEntry entry;
            public ItemData item;
            public double weight;
        }

        private static List<Candidate> GetEligible(DesignData design, ForageTableData table, TimeOfDay time,
            IForageContext context, List<ForageModifier> modifiers)
        {
            var eligible = new List<Candidate>();
            foreach (ForageEntry entry in table.entries)
            {
                ItemData item = design.GetItem(entry.itemId);
                if (item == null) continue;
                if (entry.time == EntryTime.Day && time != TimeOfDay.Day) continue;
                if (entry.time == EntryTime.Night && time != TimeOfDay.Night) continue;
                if (!string.IsNullOrEmpty(entry.requiresQuest) && (context == null || !context.IsQuestActive(entry.requiresQuest))) continue;
                if (item.unique && context != null && context.HasEverObtained(item.id)) continue;

                double weight = entry.weight;
                foreach (ForageModifier modifier in modifiers)
                {
                    if (modifier.type == ModifierType.TagWeight && item.HasTag(modifier.tag)) weight *= modifier.value;
                    else if (modifier.type == ModifierType.RarityWeight && item.rarity == modifier.rarity) weight *= modifier.value;
                }

                eligible.Add(new Candidate { entry = entry, item = item, weight = weight });
            }

            return eligible;
        }

        private static int CountRolls(ForageTableData table, List<ForageModifier> modifiers)
        {
            int rolls = table.rolls;
            foreach (ForageModifier modifier in modifiers)
                if (modifier.type == ModifierType.ExtraRolls) rolls += (int)Math.Floor(modifier.value);
            return Math.Max(1, rolls);
        }

        private static int QuantityBonus(ItemData item, List<ForageModifier> modifiers)
        {
            int bonus = 0;
            foreach (ForageModifier modifier in modifiers)
                if (modifier.type == ModifierType.TagQuantity && item.HasTag(modifier.tag)) bonus += (int)Math.Floor(modifier.value);
            return bonus;
        }

        /// <summary>One roll: two random numbers (pick, then quantity), or none if the pool is empty.</summary>
        private static ForageRoll Roll(List<Candidate> eligible, string onlyTag, HashSet<string> foundUniques,
            List<ForageModifier> modifiers, IRandom rng, bool bonus)
        {
            var pool = new List<Candidate>();
            double total = 0;
            foreach (Candidate candidate in eligible)
            {
                if (candidate.weight <= 0) continue;
                if (candidate.item.unique && foundUniques.Contains(candidate.item.id)) continue;
                if (onlyTag != null && !candidate.item.HasTag(onlyTag)) continue;
                pool.Add(candidate);
                total += candidate.weight;
            }

            if (pool.Count == 0) return null;

            double r = rng.NextDouble() * total;
            Candidate picked = pool[pool.Count - 1];
            foreach (Candidate candidate in pool)
            {
                r -= candidate.weight;
                if (r < 0)
                {
                    picked = candidate;
                    break;
                }
            }

            int span = picked.entry.max - picked.entry.min + 1;
            int qty = picked.entry.min + (int)Math.Floor(rng.NextDouble() * span);
            if (picked.item.unique)
            {
                qty = 1;
                foundUniques.Add(picked.item.id);
            }
            else
            {
                qty += QuantityBonus(picked.item, modifiers);
            }

            return new ForageRoll
            {
                itemId = picked.item.id,
                qty = qty,
                chance = picked.weight / total,
                bonus = bonus
            };
        }
    }
}
