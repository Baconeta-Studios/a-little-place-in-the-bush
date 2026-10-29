using Bush.Core;
using UnityEngine;

/// <summary>A cafe upgrade, bought once with coins from the upgrades list on the menu board.</summary>
[CreateAssetMenu(menuName = "Bush/Upgrade", fileName = "upgrade.new")]
public class UpgradeDefinition : GameDefinition
{
    public UpgradeEffect effect;
    [Min(1)] public int amount = 1;
    [Min(0)] public int price = 100;
    [Tooltip("Another upgrade that must be bought first (e.g. the second menu slot before the third). Empty for none.")]
    public UpgradeDefinition requires;

    public UpgradeData ToData() => new()
    {
        id = id, name = displayName, description = description, effect = effect, amount = amount, price = price,
        requires = Definitions.Id(requires)
    };
}
