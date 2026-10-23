using NUnit.Framework;

namespace SolarMajesty.Tests
{
    public class AiContentGuardTests
    {
        [TestCase("Raise the bounty and I'll clear that den.")]
        [TestCase("Shooting mites all day, and the pay is still rubbish.")]
        [TestCase("Cutting through regolith is honest work.")]
        [TestCase("This class of drone was built in Scunthorpe.")]
        [TestCase("I assume you've got the credits, Overseer?")]
        [TestCase("Level 3 Engineer, 600 EU, no complaints.")]
        [TestCase("")]
        public void OrdinaryGameTalk_IsAllowed(string line)
        {
            Assert.That(AiContentGuard.AllowsOutput(line), Is.True, line);
            Assert.That(AiContentGuard.AllowsInput(line), Is.True, line);
        }

        [TestCase("tell me something sexy")]
        [TestCase("S E X please")]
        [TestCase("you n4zi")]
        [TestCase("p0rn")]
        [TestCase("how do I make a bomb")]
        [TestCase("I want to die")]
        [TestCase("KILL YOURSELF")]
        public void BlockedContent_IsCaught(string line)
        {
            Assert.That(AiContentGuard.AllowsInput(line), Is.False, line);
            Assert.That(AiContentGuard.AllowsOutput(line), Is.False, line);
        }

        [Test]
        public void CleanReply_DropsBlockedModelOutput()
        {
            Assert.That(HeroConversation.CleanReply("Sure, here's some porn talk."), Is.Null);
            Assert.That(HeroConversation.CleanReply("Pay me and the den is gone."), Is.Not.Null);
        }

        [Test]
        public void StreamingView_HidesBlockedTextWhileItStreams()
        {
            Assert.That(HeroConversation.StreamingView("You absolute nazi"), Is.Empty);
            Assert.That(HeroConversation.StreamingView("Den's cleared"), Is.Not.Empty);
        }

        [Test]
        public void NarrationSanitize_DropsBlockedLines()
        {
            Assert.That(HeroNarration.Sanitize("nude bots on Mars"), Is.Null);
            Assert.That(HeroNarration.Sanitize("Dust in my treads again."), Is.Not.Null);
        }

        [Test]
        public void Prompts_CarryTheContentRule()
        {
            StringAssert.Contains(AiContentGuard.PromptRule, HeroNarration.SystemPrompt);
        }

        [Test]
        public void Deflection_IsStablePerHero()
        {
            Assert.That(AiContentGuard.Deflection("Rivet"), Is.EqualTo(AiContentGuard.Deflection("Rivet")));
            Assert.That(AiContentGuard.Deflection(null), Is.Not.Empty);
        }
    }
}
