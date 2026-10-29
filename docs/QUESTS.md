# Quests, characters and dialogue

How quests get into the game, how the player tracks them, and how to put a character in a scene to talk to.

## In the game

- **Characters** (`Npc`) stand in the scene. Click one and the gecko walks over and a conversation opens in a box along the bottom of the screen (`DialogueBox`): portrait, name, what they say, and replies on the right. The cafe stays in view above it.
- A **!** floats over a character with a quest to offer, and a **?** over one waiting for you to hand a quest in.
- What they say depends on where you are with them, most important first:
  1. **A quest ready to hand in:** their *ready* line and your objectives, then *Here you go* / *Thanks!* hands it in and shows their *done* line and what you got. If they have something else (the next story step), *Anything else?* carries straight on.
  2. **A quest to offer** (story quests first): their *offer* line and the quest's title and summary, then *Sure!* or *Not now*.
  3. **A quest under way:** their *active* line and your progress.
  4. **Nothing:** the *done* line of the last quest you finished with them, or a friendly nod.
- **Quest log** (HUD **Quests** button): the story's current step at the top, then every side quest under way (no limit, it scrolls), then who might have something for you.
- **Tracker** (top right): the story quest and its next objective, or who to talk to. Tap it to open the quest log.
- Talking to someone counts for "talk to" objectives. Accepting a quest counts as talking to its giver, so a "come and say hello" quest is done in one conversation.

## The main story and side quests

There's one list of quests; a flag and prerequisites make the story:

- **Main quests** (`isMain: true`) form the story. Chain them with `prerequisites` (each one needs the one before), so there's only ever one story step at a time. The tracker always shows it. Finishing every main quest is the win condition.
- **Side quests** (`isMain: false`) can open up whenever their prerequisites are done, as many as you like, in any order.
- A character offers their main quests before their side quests.

## Adding a quest

Quests live in [`Design/design-data.json`](../Design/design-data.json) under `quests`. Edit the file, then in Unity run **Tools > Bush > Import Design Data**, which makes or updates the quest assets. **Tools > Bush > Validate Design Data** catches typos (unknown items, recipes, characters) and impossible quests.

```json
{
  "id": "quest.side_kiwi_snack",
  "title": "A midnight snack",
  "giver": "npc.ruru",
  "isMain": false,
  "prerequisites": ["quest.main_welcome"],
  "summary": "Ruru has a craving for worm pie.",
  "objectives": [
    { "type": "CookRecipe", "target": "recipe.worm_pie", "qty": 2 }
  ],
  "rewards": [
    { "type": "Coins", "target": "", "qty": 15 },
    { "type": "Item", "target": "item.shiny_button", "qty": 1 }
  ],
  "dialogue": {
    "offer": "I've been dreaming of worm pie. Could you make a couple?",
    "active": "Two worm pies. My tummy's rumbling.",
    "ready": "Is that... worm pie I smell?",
    "done": "Best pie in the bush."
  }
}
```

| Objective type | target | Notes |
| --- | --- | --- |
| `HaveItems` | item id | Checked against the bag; nothing is taken. |
| `DeliverItems` | item id | Taken from the bag when you hand the quest in. |
| `CookRecipe` | recipe id, or empty for any | Shown as "Serve ... at the cafe": dishes count when the cafe sells them at closing time. |
| `CompleteExpeditions` | (unused) | Forage trips. |
| `TalkTo` | npc id | Talking to them while the quest is under way. |
| `PlaceObject` | placeable item id, or empty for any | Placing furniture in build mode. |

| Reward type | target | |
| --- | --- | --- |
| `Item` | item id | `qty` of them. |
| `Coins` | (empty) | `qty` coins. |
| `UnlockRecipe` | recipe id | |
| `UnlockCompanion` | companion id | |
| `UnlockPerk` | perk id | |

Counting objectives (cook, forage, talk, place) only count while the quest is under way, so accept first.

## Adding a character

1. **The data:** add them to `npcs` in the design data (`id`, `name`, `species`, `description`) and import. That makes `Assets/Data/Npcs/npc.<name>.asset`; give it a **Portrait** sprite there if you have one (otherwise the dialogue shows the first letter of their name).
2. **In the scene:** make an object for them under World (Y-sorted like everything else, pivot at the feet), with their sprite as a child.
3. Add **Npc** and pick their NPC asset in **Npc**. Set **Marker Height** so the ! sits above their head.
4. Set the object's **Layer** to **Interactable** and add a **Box Collider 2D** (Is Trigger) covering them, or put it on a separate unscaled Click child like the cafe door.
5. Add a child with **StandPoint** on the walkable floor beside them, where the gecko should stand to talk.
6. Optional: **InteractionPrompt** (as on the cafe door) for the "Talk to ..." label on hover.
7. If they should block walking, also give them a collider on the **Obstacle** layer and rebuild the walkable grid.

## Code

- Rules (`Bush.Core`): `QuestService` (`GetTopic`, `Accept`, `Claim`, `CurrentMainQuest`, `ActiveSideQuests`, `AvailableSideQuests`), `QuestText` for readable objectives and rewards. Tests: `ConversationTests`, `QuestAndCafeTests`.
- Unity: `Npc` (interactable with the ! / ? marker), `DialogueBox`, `QuestLogPopup`, and the tracker in `GameHud`.
- Not yet: characters that only appear on some days, "come back tomorrow" timing, and choices beyond accept / not now.
