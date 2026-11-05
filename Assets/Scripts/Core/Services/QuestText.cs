namespace Bush.Core
{
    /// <summary>Plain-English quest objectives and rewards, for the quest log, the tracker and dialogue.</summary>
    public static class QuestText
    {
        public static string Objective(DesignData design, QuestObjective objective)
        {
            int qty = objective.qty < 1 ? 1 : objective.qty;
            switch (objective.type)
            {
                case ObjectiveType.HaveItems: return $"Have {Amount(design, objective.target, qty)}";
                case ObjectiveType.DeliverItems: return $"Bring {Amount(design, objective.target, qty)}";
                case ObjectiveType.CookRecipe:
                    // Dishes are cooked when the cafe sells them at closing time, so say what the player does.
                    string dish = string.IsNullOrEmpty(objective.target) ? "any dish" : design.GetRecipe(objective.target)?.name ?? objective.target;
                    return qty == 1 ? $"Serve {dish} at the cafe" : $"Serve {dish} at the cafe {qty} times";
                case ObjectiveType.CompleteExpeditions: return qty == 1 ? "Go foraging" : $"Go foraging {qty} times";
                case ObjectiveType.CloseDay: return qty == 1 ? "Close the cafe for the day" : $"Close the cafe for the day {qty} times";
                case ObjectiveType.TalkTo: return $"Talk to {design.GetNpc(objective.target)?.name ?? objective.target}";
                case ObjectiveType.PlaceObject:
                    string thing = string.IsNullOrEmpty(objective.target) ? "something" : design.GetItem(objective.target)?.name ?? objective.target;
                    return qty == 1 ? $"Place {thing} in the cafe" : $"Place {qty} x {thing} in the cafe";
                case ObjectiveType.FindItems:
                    return qty == 1 ? $"Find {Amount(design, objective.target, 1)} on a foraging trip" : $"Find {Amount(design, objective.target, qty)} on foraging trips";
                case ObjectiveType.TripWith:
                    string who = design.GetCompanion(objective.target)?.name ?? objective.target;
                    return qty == 1 ? $"Go foraging with {who}" : $"Go foraging with {who} {qty} times";
                case ObjectiveType.CloseWithOnMenu:
                    string shown = string.IsNullOrEmpty(objective.target) ? "a dish" : design.GetRecipe(objective.target)?.name ?? objective.target;
                    return qty == 1 ? $"Close the cafe with {shown} on the menu" : $"Close the cafe with {shown} on the menu, {qty} times";
                case ObjectiveType.HaveOnMenu:
                    return string.IsNullOrEmpty(objective.target) ? $"Have {qty} dishes on the menu" : $"Have {design.GetRecipe(objective.target)?.name ?? objective.target} on the menu";
                case ObjectiveType.ReachCosiness: return $"Get the cafe's cosiness to {qty}";
                case ObjectiveType.HaveSeats: return qty == 1 ? "Have a seat out at the cafe" : $"Have {qty} seats out at the cafe";
                case ObjectiveType.HavePlaced: return $"Have {Amount(design, objective.target, qty)} out at the cafe";
                default: return objective.type.ToString();
            }
        }

        public static string Reward(DesignData design, QuestReward reward)
        {
            int qty = reward.qty < 1 ? 1 : reward.qty;
            switch (reward.type)
            {
                case RewardType.Item: return Amount(design, reward.target, qty);
                case RewardType.Coins: return $"{reward.qty} coins";
                case RewardType.UnlockRecipe: return $"New recipe: {design.GetRecipe(reward.target)?.name ?? reward.target}";
                case RewardType.UnlockCompanion: return $"New companion: {design.GetCompanion(reward.target)?.name ?? reward.target}";
                case RewardType.UnlockPerk: return $"New perk: {design.GetPerk(reward.target)?.name ?? reward.target}";
                case RewardType.UnlockNight: return "Night time: stay up after closing";
                default: return reward.type.ToString();
            }
        }

        private static string Amount(DesignData design, string itemId, int qty)
        {
            string name = design.GetItem(itemId)?.name ?? itemId;
            return qty == 1 ? name : $"{qty} x {name}";
        }
    }
}
