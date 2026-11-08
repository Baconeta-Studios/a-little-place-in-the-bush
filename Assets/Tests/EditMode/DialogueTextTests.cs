using NUnit.Framework;

namespace Bush.Core.Tests
{
    public class DialogueTextTests
    {
        [TestCase("How's it going?")]
        [TestCase("Snff. Snff snff.")]
        [TestCase("<i>(a bite of the scroll)</i> I was seven, the last time.")]
        [TestCase("...All right, that's good.")]
        [TestCase("<b>Hello</b>")]
        public void Speech_HasAVoice(string line) => Assert.IsTrue(DialogueText.IsSpoken(line));

        [TestCase("<i>Gus gives you a friendly nod.</i>")]
        [TestCase("(nods)")]
        [TestCase("*shuffles shyly*")]
        [TestCase("<i>(a bite)</i> ...")]
        [TestCase("...")]
        [TestCase("")]
        [TestCase(null)]
        public void Narration_IsSilent(string line) => Assert.IsFalse(DialogueText.IsSpoken(line));

        [Test]
        public void Narration_IsItalic()
        {
            string line = DialogueText.Narration("Weka gives you a friendly nod.");
            Assert.AreEqual("<i>Weka gives you a friendly nod.</i>", line);
            Assert.IsFalse(DialogueText.IsSpoken(line));
        }
    }
}
