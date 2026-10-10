/*
 * Copyright 2026 Kreisverkehr
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Cinegy.TsDecoder.Tables;
using Cinegy.TsDecoder.TransportStream;
using NUnit.Framework;

namespace Cinegy.TsDecoder.Tests.Tables
{
    [TestFixture]
    public class EventInformationTableFactoryTests
    {
        [TestCase(0x4f, false)]
        [TestCase(0x50, true)]
        [TestCase(0x5f, true)]
        [TestCase(0x60, false)]
        [TestCase(0x6f, false)]
        [TestCase(0x70, false)]
        public void AddPacket_AcceptsOnlyPresentFollowingAndCurrentMuxSchedules(byte tableId, bool expectedSchedule)
        {
            var factory = new EventInformationTableFactory();

            factory.AddPacket(CreatePacket(tableId, 0, 0, eventId: 1));

            Assert.That(factory.ScheduledEventInformationTables.Count, Is.EqualTo(expectedSchedule ? 1 : 0));
        }

        [Test]
        public void AddPacket_PreservesInterleavedScheduleSectionsAndTheirEvents()
        {
            var factory = new EventInformationTableFactory();

            factory.AddPacket(CreatePacket(0x50, 0, 0, eventId: 100));
            factory.AddPacket(CreatePacket(0x50, 0, 0, eventId: 200, serviceId: 2));

            Assert.That(factory.ScheduledEventInformationTables, Has.Count.EqualTo(2));
            Assert.That(factory.ScheduledEventInformationTables[0].TableId, Is.EqualTo(0x50));
            Assert.That(factory.ScheduledEventInformationTables[0].Items[0].EventId, Is.EqualTo(100));
            Assert.That(factory.ScheduledEventInformationTables[1].TableId, Is.EqualTo(0x50));
            Assert.That(factory.ScheduledEventInformationTables[1].ServiceId, Is.EqualTo(2));
            Assert.That(factory.ScheduledEventInformationTables[1].Items[0].EventId, Is.EqualTo(200));
        }

        [Test]
        public void AddPacket_PreservesPresentFollowingApiAndExposesScheduleOnDecoder()
        {
            var factory = new EventInformationTableFactory();
            factory.AddPacket(CreatePacket(0x4e, 0, 0, eventId: 100));

            Assert.That(factory.EventInformationTable.TableId, Is.EqualTo(0x4e));
            Assert.That(factory.ScheduledEventInformationTables, Is.Empty);

            var decoder = new global::Cinegy.TsDecoder.TransportStream.TsDecoder();
            var tableTypes = new System.Collections.Generic.List<TableType>();
            decoder.TableChangeDetected += (_, args) => tableTypes.Add(args.TableType);
            decoder.AddPacket(CreatePacket(0x4e, 0, 0, eventId: 100));
            decoder.AddPacket(CreatePacket(0x50, 0, 0, eventId: 200));

            Assert.That(decoder.ScheduledEventInformationTables, Has.Count.EqualTo(1));
            Assert.That(decoder.ScheduledEventInformationTables[0].Items[0].EventId, Is.EqualTo(200));
            Assert.That(tableTypes, Is.EqualTo(new[] { TableType.Eit, TableType.EitSchedule }));
        }

        [Test]
        public void AddPacket_IgnoresDuplicateSectionsAndReplacesOnlyChangedScheduleVersion()
        {
            var factory = new EventInformationTableFactory();
            var changeCount = 0;
            factory.TableChangeDetected += (_, _) => changeCount++;

            factory.AddPacket(CreatePacket(0x50, 0, 0, eventId: 100));
            factory.AddPacket(CreatePacket(0x50, 0, 0, eventId: 100));
            factory.AddPacket(CreatePacket(0x50, 1, 0, eventId: 101));
            factory.AddPacket(CreatePacket(0x50, 0, 1, eventId: 200));
            factory.AddPacket(CreatePacket(0x51, 0, 0, eventId: 300));

            Assert.That(factory.ScheduledEventInformationTables, Has.Count.EqualTo(2));
            Assert.That(factory.ScheduledEventInformationTables.FindAll(table => table.TableId == 0x50), Has.Count.EqualTo(1));
            Assert.That(factory.ScheduledEventInformationTables.Find(table => table.TableId == 0x50).VersionNumber, Is.EqualTo(2));
            Assert.That(factory.ScheduledEventInformationTables.Find(table => table.TableId == 0x51).VersionNumber, Is.EqualTo(0));
            Assert.That(changeCount, Is.EqualTo(4));
        }

        private static TsPacket CreatePacket(byte tableId, byte sectionNumber, byte version, ushort eventId, ushort serviceId = 1)
        {
            const ushort sectionLength = 27;
            var payload = new byte[1 + 3 + sectionLength];
            payload[0] = 0;
            payload[1] = tableId;
            payload[2] = (byte)(0xb0 | (sectionLength >> 8));
            payload[3] = (byte)sectionLength;
            payload[4] = (byte)(serviceId >> 8);
            payload[5] = (byte)serviceId;
            payload[6] = (byte)((version << 1) | 1);
            payload[7] = sectionNumber;
            payload[8] = 1;
            payload[9] = 0x00;
            payload[10] = 0x02;
            payload[11] = 0x00;
            payload[12] = 0x03;
            payload[13] = 1;
            payload[14] = tableId;
            payload[15] = (byte)(eventId >> 8);
            payload[16] = (byte)eventId;
            payload[25] = 0;
            payload[26] = 0;

            return new TsPacket
            {
                Pid = (ushort)PidType.EitPid,
                PayloadUnitStartIndicator = true,
                Payload = payload,
                PayloadLen = payload.Length
            };
        }
    }
}