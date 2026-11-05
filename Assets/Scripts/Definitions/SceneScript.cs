using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The words for a scene that plays out in the cafe, like the date (see <see cref="DateScene"/>). Each beat is
/// one thing the character says and the replies you can pick. A reply's answer is what they say back, before
/// the next beat. A beat with no replies has a single button to carry on.
/// </summary>
[CreateAssetMenu(fileName = "DateScene", menuName = "Bush/Scene Script")]
public class SceneScript : ScriptableObject
{
    [Serializable]
    public class Reply
    {
        [Tooltip("The button. <i>Italics</i> for something you do rather than say.")]
        public string label;
        [Tooltip("What they say back. Leave empty to go straight on.")]
        [TextArea(2, 5)] public string answer;
    }

    [Serializable]
    public class Beat
    {
        [TextArea(2, 5)] public string line;
        [Tooltip("The quest is handed in here, and what you get is shown under the line.")]
        public bool givesRewards;
        public List<Reply> replies = new();
    }

    [Tooltip("The story quest this scene plays for, instead of the usual conversation.")]
    public string questId = "quest.main_date";
    [Tooltip("Who you're talking to.")]
    public string npcId = "npc.kowhai";
    [Tooltip("The placeable the two of you sit at. Without one out, you sit where they're standing.")]
    public string tableId = "placeable.cafe_fancy_table";
    [Tooltip("Shown on the fade in.")]
    public string introCard = "Later that night";
    [Tooltip("Shown on the fade out.")]
    public string endCard = "The end, for now.";
    [Tooltip("The button for carrying on when there's nothing to choose.")]
    public string continueLabel = "...";
    [Tooltip("The quiet moment at the end, with no words, before the fade.")]
    [Min(0f)] public float quietSeconds = 3.5f;
    [Tooltip("How much closer the camera comes in. 1 is the normal view.")]
    [Min(1f)] public float zoom = 1.3f;
    public List<Beat> beats = new();
}
