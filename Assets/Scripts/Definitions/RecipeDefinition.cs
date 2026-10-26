using System;
using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[Serializable]
public struct IngredientEntry
{
    public ItemDefinition item;
    public int qty;
}

[CreateAssetMenu(menuName = "Bush/Recipe", fileName = "recipe.new")]
public class RecipeDefinition : GameDefinition
{
    public Sprite icon;
    public List<IngredientEntry> ingredients = new();
    public int coins;
    public bool unlockedByDefault;
    [Tooltip("Coins to learn it at the cafe. 0 = can't be bought (unlocked by default or by a quest).")]
    public int price;
    [Tooltip("Key items that piece this recipe together. Only used for the recipe book's progress; the quest does the unlocking.")]
    public List<ItemDefinition> scraps = new();

    public RecipeData ToData()
    {
        var data = new RecipeData
        {
            id = id, name = displayName, description = description, coins = coins, unlockedByDefault = unlockedByDefault, price = price
        };
        foreach (IngredientEntry ingredient in ingredients) data.ingredients.Add(new ItemAmount(Definitions.Id(ingredient.item), ingredient.qty));
        foreach (ItemDefinition scrap in scraps) data.scraps.Add(Definitions.Id(scrap));
        return data;
    }
}
