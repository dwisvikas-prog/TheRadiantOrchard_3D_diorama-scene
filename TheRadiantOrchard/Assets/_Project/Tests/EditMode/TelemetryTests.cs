using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;

namespace RadiantOrchard.Tests.EditMode
{
    public class TelemetryTests
    {
        private sealed class RecordingSink : ITelemetrySink
        {
            public readonly List<string> Events = new List<string>();
            public void Log(string eventName, string details) => Events.Add(eventName + "|" + details);
        }

        private ITelemetrySink _previous;

        [SetUp]
        public void SetUp() => _previous = Telemetry.Sink;

        [TearDown]
        public void TearDown() => Telemetry.Sink = _previous;

        [Test]
        public void WithoutASinkNothingHappens()
        {
            Telemetry.Sink = null;
            Assert.DoesNotThrow(() => Telemetry.Log("session_start", "New"));
        }

        [Test]
        public void EventsGoToTheSink()
        {
            var sink = new RecordingSink();
            Telemetry.Sink = sink;
            Telemetry.Log("session_start", "Main");
            Telemetry.Log("session_end");
            CollectionAssert.AreEqual(new[] { "session_start|Main", "session_end|" }, sink.Events);
        }
    }
}
