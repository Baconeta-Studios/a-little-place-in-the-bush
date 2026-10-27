using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Bush.Core.Tests
{
    public class DesignDataTests
    {
        [Test]
        public void TestDesign_IsValid()
        {
            List<string> errors = DesignValidator.Validate(TestDesign.Create());
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }

        [Test]
        public void Validator_FindsBrokenData()
        {
            DesignData design = TestDesign.Create();
            design.items.Add(new ItemData { id = TestDesign.Leaf });
            design.forageTables[0].entries.Add(new ForageEntry { itemId = "item.missing", weight = 0, min = 3, max = 1 });
            design.perks.Add(new PerkData { id = "perk.bad", modifiers = { new ForageModifier(ModifierType.TagWeight, 2, "nothing_has_this") } });
            design.items.Add(new ItemData { id = "item.bad_note", category = ItemCategory.KeyItem, tags = { "encounter" }, teachesRecipe = "recipe.missing" });
            design.forageTables[0].entries.Add(new ForageEntry { itemId = TestDesign.Leaf, weight = 1, min = 1, max = 1, requiresCompanion = "companion.missing" });
            design.placeables.Add(new PlaceableData { id = "placeable.bad", itemId = TestDesign.Leaf, footprintWidth = 0, footprintDepth = 1 });

            List<string> errors = DesignValidator.Validate(design);
            string all = string.Join("\n", errors);
            StringAssert.Contains("Duplicate item id 'item.leaf'", all);
            StringAssert.Contains("unknown item 'item.missing'", all);
            StringAssert.Contains("weight must be above 0", all);
            StringAssert.Contains("min (3) is more than max (1)", all);
            StringAssert.Contains("'nothing_has_this'", all);
            StringAssert.Contains("teaches unknown recipe recipe.missing", all);
            StringAssert.Contains("should be unique", all);
            StringAssert.Contains("requires unknown companion companion.missing", all);
            StringAssert.Contains("item.leaf should have the Placeable category", all);
            StringAssert.Contains("footprint must be bigger than 0", all);
        }

        [Test]
        public void DesignFile_RoundTrips()
        {
            DesignData design = TestDesign.Create();
            var errors = new List<string>();
            DesignData copy = DesignFile.FromDesign(design).ToDesign(errors);

            Assert.IsEmpty(errors);
            Assert.IsEmpty(DesignValidator.Validate(copy));
            Assert.AreEqual(design.items.Count, copy.items.Count);
            Assert.AreEqual(EntryTime.Night, copy.GetForageTable(TestDesign.Table).entries[3].time);
            Assert.AreEqual(TestDesign.Weka, copy.GetForageTable(TestDesign.Table).entries[^1].requiresCompanion);
            Assert.AreEqual(TestDesign.SecretPie, copy.GetItem(TestDesign.Note).teachesRecipe);
            Assert.AreEqual(1, copy.GetPlaceable(TestDesign.Chair).seats);
            Assert.AreEqual(0.8f, copy.GetPlaceable(TestDesign.Fern).footprintWidth, 1e-6);
            Assert.IsFalse(copy.GetPlaceable(TestDesign.Fern).canFlip);

            // The same seed must give the same expedition after a round trip.
            ExpeditionSetup setup = TestDesign.Setup(TestDesign.Weka, TestDesign.BigBasket, TestDesign.FocusBugs, TimeOfDay.Night);
            ForageResult a = ForagingService.Resolve(design, setup, new ForageContext(), 777u);
            ForageResult b = ForagingService.Resolve(copy, setup, new ForageContext(), 777u);
            Assert.AreEqual(a.rolls.Count, b.rolls.Count);
            for (int i = 0; i < a.rolls.Count; i++)
            {
                Assert.AreEqual(a.rolls[i].itemId, b.rolls[i].itemId);
                Assert.AreEqual(a.rolls[i].qty, b.rolls[i].qty);
            }
        }

        /// <summary>The real design data in Design/design-data.json must always load and validate.</summary>
        [Test]
        public void ProjectDesignData_IsValid()
        {
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Design", "design-data.json"));
            Assert.IsTrue(File.Exists(path), path);

            var file = UnityEngine.JsonUtility.FromJson<DesignFile>(File.ReadAllText(path));
            var errors = new List<string>();
            DesignData design = file.ToDesign(errors);
            errors.AddRange(DesignValidator.Validate(design));
            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
