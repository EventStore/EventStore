using System;
using System.Text;
using EventStore.ClientAPI;

namespace EventStore.TestClient.Commands.RunTestScenarios
{
    internal class TestEvent : IEvent
    {
        public Guid EventId { get; private set; }
        public string Type { get; private set; }

        public byte[] Data { get; private set; }
        public byte[] Metadata { get; private set; }

        public TestEvent(int index)
        {
            var subIndex = (index % 50);

            EventId = Guid.NewGuid();
            Type = "TestEvent-" + subIndex.ToString();

            var body = new string('#', 1 + 17 * subIndex * subIndex);

            Data = Encoding.UTF8.GetBytes(string.Format("{0}-{1}-{2}", index, body.Length, body));
            Metadata = new byte[0];
        }

        public static void VerifyIfMatched(RecordedEvent evnt)
        {
            if (evnt.EventType.StartsWith("TestEvent"))
            {
                var data = Encoding.UTF8.GetString(evnt.Data);
                var atoms = data.Split('-');
                if (atoms.Length != 3)
                    throw new ApplicationException(string.Format("Invalid TestEvent object: currupted data format: {0}",
                                                                 RecordDetailsString(evnt)));

                var expectedLength = int.Parse(atoms[1]);
                if (expectedLength != atoms[2].Length)
                    throw new ApplicationException(string.Format("Invalid TestEvent object: not expected data length: {0}",
                                                                 RecordDetailsString(evnt)));

                if (new string('#', expectedLength) != atoms[2])
                    throw new ApplicationException(string.Format("Invalid TestEvent object: currupted data: {0}",
                                                                 RecordDetailsString(evnt)));
            }
        }

        private static string RecordDetailsString(RecordedEvent evnt)
        {
            var data = Encoding.UTF8.GetString(evnt.Data);
            return string.Format("[stream:{0}; eventNumber:{1}; type:{2}; data:{3}]",
                                                               evnt.EventStreamId,
                                                               evnt.EventNumber,
                                                               evnt.EventType,
                                                               data.Length > 12 ? (data.Substring(0, 12) + "...") : data);
        }
    }
}