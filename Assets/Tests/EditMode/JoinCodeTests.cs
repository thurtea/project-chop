using Chopper.Core;
using NUnit.Framework;

namespace Chopper.Tests.EditMode
{
    public class JoinCodeTests
    {
        [Test]
        public void GeneratedCodesAreValidAndHaveNoLookAlikes()
        {
            var random = new System.Random(7);
            for (int i = 0; i < 500; i++)
            {
                string code = JoinCode.Generate(random);
                Assert.IsTrue(JoinCode.IsValid(code), code);
                Assert.IsFalse(code.IndexOfAny(new[] { '0', 'O', '1', 'I' }) >= 0, code);
            }
        }

        [Test]
        public void TypedCodesAreCleanedUp()
        {
            Assert.AreEqual("K7P2QX", JoinCode.Normalize(" k7p-2qx "));
            Assert.IsTrue(JoinCode.IsValid(JoinCode.Normalize("k7p 2qx")));
        }

        [Test]
        public void WrongLengthsAndLettersAreRejected()
        {
            Assert.IsFalse(JoinCode.IsValid("K7P2Q"));
            Assert.IsFalse(JoinCode.IsValid("K7P2QXX"));
            Assert.IsFalse(JoinCode.IsValid("K7P2Q0"));
            Assert.IsFalse(JoinCode.IsValid(null));
        }
    }
}
