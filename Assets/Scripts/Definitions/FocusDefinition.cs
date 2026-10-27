using System.Collections.Generic;
using Bush.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Bush/Forage Focus", fileName = "focus.new")]
public class FocusDefinition : GameDefinition
{
    public Sprite icon;
    public List<string> tags = new();
    public double multiplier = 2;

    public FocusData ToData() => new() { id = id, name = displayName, tags = new List<string>(tags), multiplier = multiplier };
}
