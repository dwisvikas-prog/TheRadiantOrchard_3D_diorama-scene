using NUnit.Framework;
using RadiantOrchard.Core;

namespace RadiantOrchard.Tests.EditMode
{
    public class EventBusTests
    {
        private readonly struct Ping
        {
            public readonly int Value;
            public Ping(int value) { Value = value; }
        }

        private readonly struct Pong { }

        [Test]
        public void PublishReachesEverySubscriberOfThatType()
        {
            var bus = new EventBus();
            int a = 0, b = 0;
            bus.Subscribe<Ping>(p => a += p.Value);
            bus.Subscribe<Ping>(p => b += p.Value * 10);

            bus.Publish(new Ping(2));

            Assert.AreEqual(2, a);
            Assert.AreEqual(20, b);
        }

        [Test]
        public void OtherEventTypesAreNotCalled()
        {
            var bus = new EventBus();
            bool pongCalled = false;
            bus.Subscribe<Pong>(_ => pongCalled = true);
            bus.Publish(new Ping(1));
            Assert.IsFalse(pongCalled);
        }

        [Test]
        public void UnsubscribeStopsDelivery()
        {
            var bus = new EventBus();
            int calls = 0;
            void Handler(Ping p) => calls++;

            bus.Subscribe<Ping>(Handler);
            bus.Publish(new Ping(1));
            bus.Unsubscribe<Ping>(Handler);
            bus.Publish(new Ping(1));

            Assert.AreEqual(1, calls);
            Assert.AreEqual(0, bus.SubscriberCount<Ping>());
        }

        [Test]
        public void PublishWithNoSubscribersIsSafe()
        {
            Assert.DoesNotThrow(() => new EventBus().Publish(new Pong()));
        }
    }
}
