using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class CharacterSizesTests
    {
        [Test]
        public void EverydayEnemiesStayNearThePlayersHeight()
        {
            foreach (TreeSize size in new[] { TreeSize.Small, TreeSize.Medium, TreeSize.Large })
            {
                Assert.That(CharacterSizes.Draugr(size), Is.InRange(CharacterSizes.Player * 0.9f, CharacterSizes.Player * 1.15f));
                Assert.That(CharacterSizes.Elemental(size), Is.InRange(CharacterSizes.Player * 0.9f, CharacterSizes.Player * 1.8f));
            }
        }

        [Test]
        public void SizesGrowWithTheTreeAndBossesTowerWithoutDwarfingTheWorld()
        {
            Assert.Less(CharacterSizes.Elemental(TreeSize.Small), CharacterSizes.Elemental(TreeSize.Medium));
            Assert.Less(CharacterSizes.Elemental(TreeSize.Medium), CharacterSizes.Elemental(TreeSize.Large));
            Assert.Less(CharacterSizes.Draugr(TreeSize.Small), CharacterSizes.Draugr(TreeSize.Large));
            Assert.That(CharacterSizes.Warden, Is.InRange(CharacterSizes.Player * 2.5f, CharacterSizes.Player * 3.5f));
            Assert.GreaterOrEqual(CharacterSizes.PlayerLegX, 0.18f);
            Assert.That(CharacterSizes.CryptBoss, Is.InRange(CharacterSizes.Player * 1.5f, CharacterSizes.Player * 2.5f));
        }
    }
}
