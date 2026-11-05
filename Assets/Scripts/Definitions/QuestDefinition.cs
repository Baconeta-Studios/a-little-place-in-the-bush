using System;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[Serializable]
public struct ObjectiveEntry
{
    public ObjectiveType type;
    [Tooltip("Item for Have, Deliver or Find. Recipe for Cook or the menu ones (empty = any). NPC for TalkTo. Placeable item for Place (empty = any) or Have Placed. Companion for Trip With. Nothing for Reach Cosiness or Have Seats, where Qty is the number.")]
    public GameDefinition target;
    [Min(1)] public int qty;
}

[Serializable]
public struct RewardEntry
{
    public RewardType type;
    [Tooltip("Item, recipe, companion or perk. Empty for coins.")]
    public GameDefinition target;
    [Tooltip("Item count, or the number of coins.")]
    public int qty;
}

[CreateAssetMenu(menuName = "Bush/Quest", fileName = "quest.new")]
public class QuestDefinition : GameDefinition
{
    public NpcDefinition giver;
    [Tooltip("Main story quests. Finishing all of them wins the game (per the GDD).")]
    public bool isMain;
    public List<QuestDefinition> prerequisites = new();
    [Tooltip("Only opens the day after its prerequisites were done (\"come back tomorrow\").")]
    public bool nextDay;
    [Tooltip("Doesn't open before this day. 0 for any day.")]
    [Min(0)] public int minDay;
    [Tooltip("Hidden until you own this item: a quest you start by finding something. Empty for none.")]
    public ItemDefinition requiresItem;
    [Tooltip("While this can be taken on, its giver is sure to turn up, from the next morning. For the next step of a chain.")]
    public bool giverComes;
    public List<ObjectiveEntry> objectives = new();
    public List<RewardEntry> rewards = new();

    [Header("Dialogue")]
    [TextArea(2, 4)] public string offerLine;
    [TextArea(2, 4)] public string activeLine;
    [TextArea(2, 4)] public string readyLine;
    [TextArea(2, 4)] public string doneLine;

    public QuestData ToData()
    {
        var data = new QuestData
        {
            id = id, title = displayName, summary = description, giver = Definitions.Id(giver), isMain = isMain, requiresItem = Definitions.Id(requiresItem), giverComes = giverComes, nextDay = nextDay, minDay = minDay,
            dialogue = new QuestDialogue { offer = offerLine, active = activeLine, ready = readyLine, done = doneLine }
        };
        foreach (QuestDefinition prerequisite in prerequisites) data.prerequisites.Add(Definitions.Id(prerequisite));
        foreach (ObjectiveEntry objective in objectives)
            data.objectives.Add(new QuestObjective { type = objective.type, target = Definitions.Id(objective.target), qty = objective.qty });
        foreach (RewardEntry reward in rewards)
            data.rewards.Add(new QuestReward { type = reward.type, target = Definitions.Id(reward.target), qty = reward.qty });
        return data;
    }
}
