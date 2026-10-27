namespace Bush.Core
{
    public enum ItemCategory
    {
        Ingredient, // used in recipes
        Material,   // bark, silk: quests and repairs
        Treasure,   // shiny things: trades and selling
        KeyItem,    // quest items like recipe scraps: can't be sold or cooked
        Placeable   // decorations for build mode
    }

    public enum Rarity
    {
        Common,
        Uncommon,
        Rare,
        Legendary
    }

    public enum TimeOfDay
    {
        Day,
        Night
    }

    /// <summary>When a forage table entry can be found.</summary>
    public enum EntryTime
    {
        Any,
        Day,
        Night
    }

    /// <summary>See docs/FORAGING.md for what each modifier does.</summary>
    public enum ModifierType
    {
        TagWeight,
        TagQuantity,
        RarityWeight,
        ExtraRolls,
        BonusRoll
    }

    public enum ObjectiveType
    {
        HaveItems,           // target = item id; checked against the inventory, nothing is taken
        DeliverItems,        // target = item id; taken from the inventory when the quest is claimed
        CookRecipe,          // target = recipe id, or empty for any recipe
        CompleteExpeditions, // target unused
        TalkTo,              // target = npc id
        PlaceObject          // target = placeable item id, or empty for anything
    }

    public enum RewardType
    {
        Item,
        Coins,
        UnlockRecipe,
        UnlockCompanion,
        UnlockPerk
    }

    /// <summary>ReadyToClaim is never stored: it's worked out from the objectives.</summary>
    public enum QuestStatus
    {
        NotStarted,
        Active,
        ReadyToClaim,
        Completed
    }
}
