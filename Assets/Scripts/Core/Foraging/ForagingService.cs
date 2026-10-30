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

            // 1. Modifiers: companion, then perks. The focus is applied once per item in step 3.
            List<ForageModifier> modifiers = CollectModifiers(design, setup);
            FocusData focus = design.GetFocus(setup.focusId);

            // 2-3. Eligible entries and their effective weights.
            List<Candidate> eligible = GetEligible(design, table, setup, context, modifiers, focus);

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
            List<Candidate> eligible = GetEligible(design, table, setup, context, modifiers, design.GetFocus(setup.focusId));
            int rolls = CountRolls(table, modifiers);
            preview.rolls = rolls;

            double total = 0;
            foreach (Candidate candidate in eligible)
                if (candidate.weight > 0) total += candidate.weight;

            foreach (Candidate candidate in eligible)
            {
                double p = total > 0 && candidate.weight > 0 ? candidate.weight / total : 0;
                double averageQty = candidate.item.unique
                    ? 1
                    : (candidate.entry.min + candidate.entry.max) / 2.0 + QuantityBonus(candidate.item, modifiers);

                // Bonus rolls add (chance x share of the tagged pool) finds of this item.
                double bonusFinds = 0;
                foreach (ForageModifier modifier in modifiers)
                {
                    if (modifier.type != ModifierType.BonusRoll || !candidate.item.HasTag(modifier.tag)) continue;
                    double tagTotal = 0;
                    foreach (Candidate other in eligible)
                        if (other.weight > 0 && other.item.HasTag(modifier.tag)) tagTotal += other.weight;
                    if (tagTotal > 0 && candidate.weight > 0) bonusFinds += Math.Min(1, modifier.value) * candidate.weight / tagTotal;
                }

                double expectedFinds = rolls * p + bonusFinds;
                if (candidate.item.unique) expectedFinds = Math.Min(1, expectedFinds); // found at most once per trip
                preview.odds.Add(new ForageOdds
                {
                    itemId = candidate.entry.itemId,
                    weight = candidate.weight,
                    chancePerRoll = p,
                    chanceAtLeastOne = 1 - Math.Pow(1 - p, rolls),
                    expectedQty = expectedFinds * averageQty
                });
            }

            return preview;
        }

        /// <summary>Companion modifiers, then each perk's. The focus isn't a modifier: it multiplies an item's weight once if the item has any of its tags.</summary>
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

            return modifiers;
        }

        // ----- Running an expedition in the game (changes state) -----

        /// <summary>Coins to take this setup's companion along.</summary>
        public static int TripCost(DesignData design, ExpeditionSetup setup) => design.GetCompanion(setup.companionId)?.tripCost ?? 0;

        /// <summary>Day companions forage by day, nocturnal ones at night, "both" either (but only once a day).</summary>
        public static bool IsAboutNow(GameState state, CompanionData companion) =>
            companion.activeAt == ActiveTime.Both || (companion.activeAt == ActiveTime.Night) == state.isNight;

        /// <summary>A companion who's been out today (by day or night) is tired until the next morning.</summary>
        public static bool IsTired(GameState state, string companionId) => state.companionsUsedToday.Contains(companionId);

        /// <summary>
        /// Whether this setup can be sent: the table and focus exist, the companion is unlocked, rested and
        /// affordable, and the perks are unlocked and fit the perk slots.
        /// </summary>
        public static bool CanBegin(DesignData design, GameState state, ExpeditionSetup setup, out string error)
        {
            error = null;
            CompanionData companion = design.GetCompanion(setup.companionId);
            int perkCount = setup.perkIds == null ? 0 : new HashSet<string>(setup.perkIds.FindAll(p => !string.IsNullOrEmpty(p))).Count;
            if (state.hasPendingExpedition) error = "An expedition is already under way.";
            else if (TripsLeftToday(design, state) == 0) error = state.isNight ? "That's enough for tonight. Time for bed." : "That's enough foraging for today.";
            else if (companion != null && !IsAboutNow(state, companion))
                error = state.isNight ? $"{companion.name} is fast asleep. They only forage by day." : $"{companion.name} only comes out at night.";
            else if (companion != null && IsTired(state, companion.id)) error = $"{companion.name} is tired. Try again tomorrow.";
            else if (companion != null && state.currency < companion.tripCost) error = $"Taking {companion.name} costs {companion.tripCost} coins.";
            else if (perkCount > design.rules.PerkSlots(state)) error = $"Only {design.rules.PerkSlots(state)} perk(s) can come along.";
            else if (design.GetForageTable(setup.tableId) == null) error = $"Unknown forage table '{setup.tableId}'.";
            else if (!string.IsNullOrEmpty(setup.companionId) && !Unlocks.IsCompanionUnlocked(design, state, setup.companionId))
                error = $"Companion '{setup.companionId}' isn't unlocked.";
            else if (!string.IsNullOrEmpty(setup.focusId) && design.GetFocus(setup.focusId) == null) error = $"Unknown focus '{setup.focusId}'.";
            else if (setup.perkIds != null)
            {
                foreach (string perkId in setup.perkIds)
                {
                    if (Unlocks.IsPerkUnlocked(design, state, perkId)) continue;
                    error = $"Perk '{perkId}' isn't unlocked.";
                    break;
                }
            }
            return error == null;
        }

        /// <summary>
        /// Call when the player presses Go. Copies the setup, works out the result from the seed and
        /// stores both in the state, so the results screen shows exactly what <see cref="Claim"/> pays,
        /// and a refresh during the dim or the results neither loses nor duplicates it. Save straight
        /// after. Returns null if the setup isn't allowed (<paramref name="force"/> skips the unlock
        /// checks, for cheats). Pressing Go twice returns the expedition already under way.
        /// </summary>
        public static PendingExpedition Begin(DesignData design, GameState state, ExpeditionSetup setup, uint seed, bool force = false,
            Economy economy = null)
        {
            if (state.hasPendingExpedition) return state.pendingExpedition;
            if (!force && !CanBegin(design, state, setup, out _)) return null;

            var copy = new ExpeditionSetup
            {
                tableId = setup.tableId,
                companionId = setup.companionId,
                focusId = setup.focusId,
                time = setup.time,
                perkIds = new List<string>()
            };
            if (setup.perkIds != null)
                foreach (string perkId in setup.perkIds)
                    if (!string.IsNullOrEmpty(perkId) && !copy.perkIds.Contains(perkId)) copy.perkIds.Add(perkId);

            state.expeditionsStarted++;
            if (state.isNight) state.nightTripsToday++;
            else state.forageTripsToday++;
            if (!string.IsNullOrEmpty(copy.companionId))
            {
                if (!state.companionsUsedToday.Contains(copy.companionId)) state.companionsUsedToday.Add(copy.companionId);
                int cost = TripCost(design, copy);
                if (cost > 0 && !(economy?.TrySpend(cost) ?? false)) state.currency = Math.Max(0, state.currency - cost);
            }
            state.pendingExpedition = new PendingExpedition
            {
                id = state.expeditionsStarted,
                seed = seed,
                setup = copy,
                result = Resolve(design, copy, new GameStateForageContext(state), seed)
            };
            state.hasPendingExpedition = true;
            return state.pendingExpedition;
        }

        /// <summary>
        /// Luck protection (<see cref="DayRules.luckProtectedQuest"/>): while that quest is under way, returns
        /// the first seed from <paramref name="seed"/> on whose trip the player finds everything its
        /// Have/Deliver objectives still need. Otherwise, or if no seed nearby does it, returns
        /// <paramref name="seed"/> unchanged. The trip is still an ordinary seeded trip, so the sandbox
        /// gives the same result for the seed it logs.
        /// </summary>
        public static uint LuckySeed(DesignData design, GameState state, ExpeditionSetup setup, uint seed, int tries = 2000)
        {
            QuestData quest = design.GetQuest(design.rules?.luckProtectedQuest);
            if (quest == null || !state.IsQuestActive(quest.id)) return seed;

            var needs = new Dictionary<string, int>();
            foreach (QuestObjective objective in quest.objectives)
            {
                if (objective.type != ObjectiveType.HaveItems && objective.type != ObjectiveType.DeliverItems) continue;
                int have = 0;
                foreach (ItemStack stack in state.inventory)
                    if (stack.itemId == objective.target) have += stack.qty;
                needs.TryGetValue(objective.target, out int already);
                int still = Math.Max(1, objective.qty) - have - already;
                if (still > 0) needs[objective.target] = already + still;
            }
            if (needs.Count == 0) return seed;

            var context = new GameStateForageContext(state);
            for (uint i = 0; i < tries; i++)
            {
                uint candidate = unchecked(seed + i);
                ForageResult result = Resolve(design, setup, context, candidate);
                bool enough = true;
                foreach (KeyValuePair<string, int> need in needs)
                {
                    ItemStack found = result.stacks.Find(s => s.itemId == need.Key);
                    if (found == null || found.qty < need.Value) { enough = false; break; }
                }
                if (enough) return candidate;
            }
            return seed;
        }

        /// <summary>Trips left today, or -1 for no limit.</summary>
        public static int TripsLeftToday(DesignData design, GameState state)
        {
            if (state.isNight) return Math.Max(0, (design.rules?.nightTrips ?? 1) - state.nightTripsToday);
            int limit = design.rules?.TripsPerDay(state) ?? 0;
            return limit <= 0 ? -1 : Math.Max(0, limit - state.forageTripsToday);
        }

        /// <summary>The pending expedition's result, for the results screen. Null if there isn't one.</summary>
        public static ForageResult GetPendingResult(GameState state) =>
            state.hasPendingExpedition ? state.pendingExpedition.result : null;

        /// <summary>
        /// Pays out the pending expedition exactly once: adds the stored items, counts the trip for the
        /// companion and teaches its perk when due. Calling it again does nothing.
        /// </summary>
        public static ExpeditionClaim Claim(DesignData design, GameState state, Economy economy, GameEvents events)
        {
            var claim = new ExpeditionClaim();
            if (!state.hasPendingExpedition) return claim;

            PendingExpedition pending = state.pendingExpedition;
            ForageResult result = pending.result ?? new ForageResult();
            state.hasPendingExpedition = false;
            state.pendingExpedition = new PendingExpedition();

            // Item by item rather than one transaction: if the design data changed under a saved
            // expedition, an item that no longer exists is skipped instead of losing the whole basket.
            // Items that teach a recipe unlock it as they're added (see Economy).
            foreach (ItemStack stack in result.stacks)
            {
                ItemData item = design.GetItem(stack.itemId);
                bool teaches = item != null && !string.IsNullOrEmpty(item.teachesRecipe) && !Unlocks.IsRecipeUnlocked(design, state, item.teachesRecipe);
                if (economy.Add(stack.itemId, stack.qty) && teaches && Unlocks.IsRecipeUnlocked(design, state, item.teachesRecipe))
                    claim.learnedRecipeIds.Add(item.teachesRecipe);
            }

            CompanionData companion = design.GetCompanion(pending.setup.companionId);
            if (companion != null)
            {
                CompanionProgress progress = state.GetOrAddCompanion(companion.id);
                progress.trips++;
                bool canTeach = !string.IsNullOrEmpty(companion.teachesPerk) && design.GetPerk(companion.teachesPerk) != null;
                if (canTeach && progress.trips >= companion.tripsToTeach && !Unlocks.IsPerkUnlocked(design, state, companion.teachesPerk))
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

        private static List<Candidate> GetEligible(DesignData design, ForageTableData table, ExpeditionSetup setup,
            IForageContext context, List<ForageModifier> modifiers, FocusData focus)
        {
            var eligible = new List<Candidate>();
            foreach (ForageEntry entry in table.entries)
            {
                ItemData item = design.GetItem(entry.itemId);
                if (item == null) continue;
                if (entry.time == EntryTime.Day && setup.time != TimeOfDay.Day) continue;
                if (entry.time == EntryTime.Night && setup.time != TimeOfDay.Night) continue;
                if (!string.IsNullOrEmpty(entry.requiresCompanion) && entry.requiresCompanion != setup.companionId) continue;
                if (!string.IsNullOrEmpty(entry.requiresQuest) && (context == null || !context.IsQuestActive(entry.requiresQuest))) continue;
                if (item.unique && context != null && context.IsUniqueCollected(item.id)) continue;

                double weight = entry.weight;
                foreach (ForageModifier modifier in modifiers)
                {
                    if (modifier.type == ModifierType.TagWeight && item.HasTag(modifier.tag)) weight *= modifier.value;
                    else if (modifier.type == ModifierType.RarityWeight && item.rarity == modifier.rarity) weight *= modifier.value;
                }
                if (focus != null && focus.tags.Exists(item.HasTag)) weight *= focus.multiplier;

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
