using DontLookBack.ObservationMaze;
using NUnit.Framework;

namespace DontLookBack.Tests.EditMode
{
    public sealed class DoorLinkCycleTests
    {
        [Test]
        public void ObservedDoorKeepsItsDestination()
        {
            var links = new DoorLinkCycle(1, 2, 3);

            Assert.That(links.TryShift(true), Is.False);
            Assert.That(links.Current, Is.EqualTo(1));
            Assert.That(links.ChangeCount, Is.Zero);
        }

        [Test]
        public void UnobservedDoorChangesDestinationPredictably()
        {
            var links = new DoorLinkCycle(1, 2, 3);

            Assert.That(links.TryShift(false), Is.True);
            Assert.That(links.Current, Is.EqualTo(2));
            Assert.That(links.TryShift(false), Is.True);
            Assert.That(links.Current, Is.EqualTo(3));
        }

        [Test]
        public void LinkCycleWrapsToFirstRoom()
        {
            var links = new DoorLinkCycle(4, 7);

            links.TryShift(false);
            links.TryShift(false);

            Assert.That(links.Current, Is.EqualTo(4));
            Assert.That(links.ChangeCount, Is.EqualTo(2));
        }
    }
}
