using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Bush/NPC", fileName = "npc.new")]
public class NpcDefinition : GameDefinition
{
    public Sprite portrait;
    [Tooltip("How far (world units) their art sits lower when they're on a seat. For art whose feet point isn't where they'd " +
             "rest, like a spider's back legs: 0 for most, and it changes nothing when they're standing.")]
    public float seatedDrop;
    public string species;
    [Tooltip("Always: always at the cafe. Story: there while they give the current story quest. Random: drops by on some days.")]
    public VisitType visits;
    [Tooltip("Random visitors: chance (0-1) of dropping by on a given day, before the cosiness boost.")]
    [Range(0f, 1f)] public float visitChance = 0.3f;
    [Tooltip("Random visitors: the first day they can turn up.")]
    [Min(0)] public int firstDay;
    [Tooltip("Random visitors: only turn up once this quest is completed. Empty for any time.")]
    public QuestDefinition afterQuest;
    [Tooltip("About by day, by night (nocturnal), or both.")]
    public ActiveTime activeAt;
    [Tooltip("Recipes they sell (each recipe's price is on the recipe). Empty for no shop.")]
    public List<RecipeDefinition> shop = new();
    [Tooltip("The shop only opens once this quest is completed. Empty for straight away.")]
    public QuestDefinition shopAfterQuest;

    public NpcData ToData() => new()
    {
        id = id, name = displayName, species = species, description = description, visits = visits, visitChance = visitChance,
        firstDay = firstDay, afterQuest = Definitions.Id(afterQuest), activeAt = activeAt, shop = shop.FindAll(r => r).ConvertAll(Definitions.Id),
        shopAfterQuest = Definitions.Id(shopAfterQuest)
    };
}
