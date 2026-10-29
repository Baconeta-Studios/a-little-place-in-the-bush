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
                case ObjectiveType.TalkTo: return $"Talk to {design.GetNpc(objective.target)?.name ?? objective.target}";
                case ObjectiveType.PlaceObject:
                    string thing = string.IsNullOrEmpty(objective.target) ? "something" : design.GetItem(objective.target)?.name ?? objective.target;
                    return qty == 1 ? $"Place {thing} in the cafe" : $"Place {qty} x {thing} in the cafe";
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
