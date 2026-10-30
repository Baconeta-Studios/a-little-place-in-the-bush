using System;
using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// Something nice about today, announced in the morning: a forage bonus, a busy cafe, a food critic, a
    /// familiar face, a gift on the doorstep. Each morning there's a good chance of one (higher in a cosier
    /// cafe), picked by weight, with cosiness making the rarer ones likelier. Never bad. Rolled once a day
    /// from the save's daily dice, so reloading doesn't change it. Effects are read where they apply:
    /// foraging (<see cref="ForageModifiersFor"/>), the cafe (<see cref="DayRules.FullDaySales"/>, the
    /// critic), visitors (the familiar face) and the doorstep gift (given when it's rolled).
    /// </summary>
    public static class DayEvents
    {
        /// <summary>Today's event, or null.</summary>
        public static DayEventData Today(DesignData design, GameState state) =>
            state.eventDay == state.day ? design.rules.GetEvent(state.todayEvent) : null;

        /// <summary>The chance of an event this morning: the base chance, raised by cosiness.</summary>
        public static double Chance(DesignData design, GameState state)
        {
            DayEventSettings settings = design.rules.events;
            double cosy = Math.Min(CafeStyle.Cosiness(design, state), settings.cosinessForFullBoost) / Math.Max(1.0, settings.cosinessForFullBoost);
            return Math.Min(1, settings.chance + settings.chanceFromCosiness * cosy);
        }

        /// <summary>An event's weight today: rarer events (by their rarity) get likelier as the cafe gets cosier.</summary>
        public static double WeightToday(DesignData design, GameState state, DayEventData dayEvent)
        {
            if (dayEvent.weight <= 0 || CafeStyle.Cosiness(design, state) < dayEvent.minCosiness) return 0;
            if (dayEvent.needsNight && !state.nightUnlocked) return 0;
            DayEventSettings settings = design.rules.events;
            double cosy = Math.Min(CafeStyle.Cosiness(design, state), settings.cosinessForFullBoost) / Math.Max(1.0, settings.cosinessForFullBoost);
            return dayEvent.weight * (1 + settings.rarerFromCosiness * cosy * (int)dayEvent.rarity);
        }

        /// <summary>
        /// Rolls this morning's event (called at the start of each day) and gives its doorstep gift, if any.
        /// No event before <see cref="DayEventSettings.firstDay"/>, and never the same one two days running.
        /// </summary>
        public static DayEventData RollToday(DesignData design, GameState state, Economy economy)
        {
            state.eventDay = state.day;
            state.todayEvent = "";
            state.criticDone = false;
            state.giftItem = "";
            state.giftExclusive = false;
            List<DayEventData> events = design.rules.dayEvents;
            if (events.Count == 0 || state.day < design.rules.events.firstDay) return null;

            var dice = new Mulberry32(Visitors.DailySeed(state, 3));
            if (dice.NextDouble() >= Chance(design, state)) return null;

            double total = 0;
            foreach (DayEventData e in events)
                if (e.id != state.lastEvent) total += WeightToday(design, state, e);
            if (total <= 0) return null;

            double r = dice.NextDouble() * total;
            DayEventData picked = null;
            foreach (DayEventData e in events)
            {
                if (e.id == state.lastEvent) continue;
                double w = WeightToday(design, state, e);
                if (w <= 0) continue;
                picked = e;
                r -= w;
                if (r < 0) break;
            }
            if (picked == null) return null;

            state.todayEvent = picked.id;
            state.lastEvent = picked.id;
            if (picked.gift.Count > 0) GiveGift(design, state, economy, picked, dice);
            return picked;
        }

        /// <summary>For testing (cheats): makes today's event this one, giving its gift if it has one.</summary>
        public static void Force(DesignData design, GameState state, Economy economy, string eventId)
        {
            DayEventData dayEvent = design.rules.GetEvent(eventId);
            state.eventDay = state.day;
            state.todayEvent = dayEvent?.id ?? "";
            state.criticDone = false;
            state.giftItem = "";
            state.giftExclusive = false;
            if (dayEvent != null && dayEvent.gift.Count > 0)
                GiveGift(design, state, economy, dayEvent, new Mulberry32(Visitors.DailySeed(state, (uint)state.expeditionsStarted + 9)));
            if (dayEvent?.familiarFace == true) Visitors.RollToday(design, state);
        }

        /// <summary>The chance a doorstep gift is something exclusive: the base chance, raised by cosiness.</summary>
        public static double ExclusiveChance(DesignData design, GameState state)
        {
            DayEventSettings settings = design.rules.events;
            double cosy = Math.Min(CafeStyle.Cosiness(design, state), settings.cosinessForFullBoost) / Math.Max(1.0, settings.cosinessForFullBoost);
            return Math.Min(1, settings.exclusiveChance + settings.exclusiveFromCosiness * cosy);
        }

        private static void GiveGift(DesignData design, GameState state, Economy economy, DayEventData dayEvent, IRandom dice)
        {
            bool exclusive = dice.NextDouble() < ExclusiveChance(design, state);
            GiftEntry gift = Pick(design, state, dayEvent.gift, exclusive, dice) ?? Pick(design, state, dayEvent.gift, !exclusive, dice);
            if (gift == null) return;
            if (economy != null && !economy.Add(gift.itemId, Math.Max(1, gift.qty))) return;
            state.giftItem = gift.itemId;
            state.giftQty = Math.Max(1, gift.qty);
            state.giftExclusive = gift.exclusive;
        }

        private static GiftEntry Pick(DesignData design, GameState state, List<GiftEntry> entries, bool exclusive, IRandom dice)
        {
            double total = 0;
            foreach (GiftEntry g in entries)
                if (g.exclusive == exclusive && Available(design, state, g)) total += g.weight;
            if (total <= 0) return null;
            double r = dice.NextDouble() * total;
            GiftEntry picked = null;
            foreach (GiftEntry g in entries)
            {
                if (g.exclusive != exclusive || !Available(design, state, g)) continue;
                picked = g;
                r -= g.weight;
                if (r < 0) break;
            }
            return picked;
        }

        /// <summary>A real item with weight, and not a unique one you already have.</summary>
        private static bool Available(DesignData design, GameState state, GiftEntry gift)
        {
            ItemData item = design.GetItem(gift.itemId);
            return item != null && gift.weight > 0 && !(item.unique && state.IsUniqueCollected(item.id));
        }

        /// <summary>Today's forage bonus for a trip at this time of day (none if the event isn't for then).</summary>
        public static List<ForageModifier> ForageModifiersFor(DesignData design, string eventId, TimeOfDay time)
        {
            DayEventData dayEvent = design.rules?.GetEvent(eventId);
            if (dayEvent == null || dayEvent.forage.Count == 0) return new List<ForageModifier>();
            if (dayEvent.forageAt == ActiveTime.Day && time != TimeOfDay.Day) return new List<ForageModifier>();
            if (dayEvent.forageAt == ActiveTime.Night && time != TimeOfDay.Night) return new List<ForageModifier>();
            return dayEvent.forage;
        }

        /// <summary>
        /// The familiar face: someone with a quest of yours under way who can visit at this time of day,
        /// guaranteed to drop by today. Falls back to any random visitor who could come. Null if nobody.
        /// </summary>
        public static string FamiliarFace(DesignData design, GameState state)
        {
            NpcData fallback = null;
            foreach (NpcData npc in design.npcs)
            {
                if (npc.visits != VisitType.Random || Visitors.ChanceToday(design, state, npc) <= 0) continue;
                bool questUnderWay = design.quests.Exists(q => q.giver == npc.id && state.IsQuestActive(q.id));
                if (questUnderWay) return npc.id;
                fallback ??= npc;
            }
            return fallback?.id;
        }
    }
}
