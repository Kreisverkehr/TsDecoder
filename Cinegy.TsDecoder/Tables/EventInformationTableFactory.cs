/* Copyright 2017-2023 Cinegy GmbH.

  Licensed under the Apache License, Version 2.0 (the "License");
  you may not use this file except in compliance with the License.
  You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

  Unless required by applicable law or agreed to in writing, software
  distributed under the License is distributed on an "AS IS" BASIS,
  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
  See the License for the specific language governing permissions and
  limitations under the License.
*/

// Modified by Kreisverkehr in 2026: decode EIT schedules and fix section and descriptor parsing.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Cinegy.TsDecoder.Descriptors;
using Cinegy.TsDecoder.TransportStream;

namespace Cinegy.TsDecoder.Tables
{
    public class EventInformationTableFactory : TableFactory
    {
        /// <summary>
        /// The last decoded EventInformation table, with the EventInformationItems associated with that table section
        /// </summary>
        public EventInformationTable EventInformationTable { get; private set; }

        public EventInformationTable LastChangedTable { get; private set; }

        /// <summary>
        /// An agregated list of all current EventInformationItems, as pulled from all EventInformationTables with the same ID and version.
        /// </summary>
        public List<EventInformationItem> EventInformationItems { get; set; } = new List<EventInformationItem>();

        /// <summary>
        /// The decoded sections of the current-multiplex Event Information schedule tables.
        /// </summary>
        public List<EventInformationTable> ScheduledEventInformationTables { get; } = new List<EventInformationTable>();

        private new EventInformationTable InProgressTable
        {
            get { return base.InProgressTable as EventInformationTable; }
            set { base.InProgressTable = value; }
        }

        private HashSet<int> _sectionsCompleted = new HashSet<int>();

        public void AddPacket(TsPacket packet)
        {
            CheckPid(packet.Pid);

            if (packet.PayloadUnitStartIndicator)
            {
                if (InProgressTable != null)
                {
                    var continuationLength = packet.Payload[0];

                    if (continuationLength > 0)
                    {
                        if (1 + continuationLength > packet.PayloadLen)
                        {
                            InProgressTable = null;
                            return;
                        }

                        var continuationPacket = packet;
                        continuationPacket.Payload = new byte[continuationLength];
                        Buffer.BlockCopy(packet.Payload, 1, continuationPacket.Payload, 0, continuationLength);
                        continuationPacket.PayloadLen = continuationLength;
                        continuationPacket.PayloadUnitStartIndicator = false;

                        AddPacket(continuationPacket);
                    }
                }

                InProgressTable = new EventInformationTable { Pid = packet.Pid, PointerField = packet.Payload[0] };

                if (InProgressTable.PointerField > packet.PayloadLen)
                {
                    Debug.Assert(true, "Event Information Table has packet pointer outside the packet.");
                }

                var pos = 1 + InProgressTable.PointerField;

                InProgressTable.TableId = packet.Payload[pos];
                InProgressTable.SectionLength =
                    (ushort)(((packet.Payload[pos + 1] & 0x0F) << 8) + packet.Payload[pos + 2]);
            }

            if (InProgressTable == null) return;

            AddData(packet);

            if (!HasAllBytes()) return;

            var pointer = InProgressTable.PointerField;
            InProgressTable.ServiceId = (ushort)((Data[pointer + 4] << 8) + Data[pointer + 5]);
            InProgressTable.VersionNumber = (byte)(Data[pointer + 6] & 0x3E);
            InProgressTable.CurrentNextIndicator = (Data[pointer + 6] & 0x1) != 0;
            InProgressTable.SectionNumber = Data[pointer + 7];
            InProgressTable.LastSectionNumber = Data[pointer + 8];
            InProgressTable.TransportStreamId = (ushort)((Data[pointer + 9] << 8) + Data[pointer + 10]);
            InProgressTable.OriginalNetworkId = (ushort)((Data[pointer + 11] << 8) + Data[pointer + 12]);
            InProgressTable.SegmentLastSectionNumber = Data[pointer + 13];
            InProgressTable.LastTableId = Data[pointer + 14];

            //TODO: Refactor with enum for well-known table IDs, and add option below as filter
            if (InProgressTable.TableId != 0x4e &&
                (InProgressTable.TableId < 0x50 || InProgressTable.TableId > 0x5f))
            {
                InProgressTable = null;
                return;
            }

            if (InProgressTable.TableId == 0x4e &&
                EventInformationTable?.VersionNumber != InProgressTable.VersionNumber)
            {
                //if the version number of any section jumps, we need to refresh
                _sectionsCompleted = new HashSet<int>();
                EventInformationItems = new List<EventInformationItem>();
            }

            var isScheduledTable = InProgressTable.TableId >= 0x50;
            if (isScheduledTable)
            {
                var duplicateSection = ScheduledEventInformationTables.Find(table =>
                    HasSameScheduleIdentity(table, InProgressTable) &&
                    table.SectionNumber == InProgressTable.SectionNumber);
                if (duplicateSection != null && duplicateSection.VersionNumber == InProgressTable.VersionNumber)
                {
                    InProgressTable = null;
                    return;
                }
            }
            else if (_sectionsCompleted.Contains(InProgressTable.SectionNumber))
            {
                InProgressTable = null;
                return;
            }

            var startOfNextField = (ushort)(InProgressTable.PointerField + 15);

            var transportStreamLoopEnd = (ushort)(InProgressTable.PointerField + InProgressTable.SectionLength);

            var items = new List<EventInformationItem>();

            while (startOfNextField < transportStreamLoopEnd)
            {
                var item = new EventInformationItem
                {
                    EventId = (ushort)((Data[startOfNextField] << 8) + Data[startOfNextField + 1]),
                    StartTime = (((ulong)(Data[startOfNextField + 2]) << 32) + ((ulong)(Data[startOfNextField + 3]) << 24) + ((ulong)(Data[startOfNextField + 4]) << 16) + ((ulong)(Data[startOfNextField + 5]) << 8) + ((ulong)(Data[startOfNextField + 6]))),
                    Duration = (uint)((Data[startOfNextField + 7] << 16) + (Data[startOfNextField + 8] << 8) + Data[startOfNextField + 9]),
                    RunningStatus = (byte)((Data[startOfNextField + 10] >> 5) & 0x07),
                    FreeCAMode = ((Data[startOfNextField + 10] & 0x10) == 0x10),
                    DescriptorsLoopLength = (ushort)(((Data[startOfNextField + 10] & 0xF) << 8) + Data[startOfNextField + 11])
                };

                var descriptors = new List<Descriptor>();

                startOfNextField = (ushort)(startOfNextField + 12);
                var endOfDescriptors = (ushort)(startOfNextField + item.DescriptorsLoopLength);

                if (endOfDescriptors > transportStreamLoopEnd || endOfDescriptors > Data.Length)
                {
                    throw new InvalidDataException("Descriptor data in Event Information is marked beyond available data");
                }

                while (startOfNextField < endOfDescriptors)
                {
                    if (endOfDescriptors - startOfNextField < 2)
                    {
                        throw new InvalidDataException("Incomplete descriptor header in Event Information");
                    }

                    var descriptorEnd = startOfNextField + 2 + Data[startOfNextField + 1];
                    if (descriptorEnd > endOfDescriptors)
                    {
                        throw new InvalidDataException("Descriptor exceeds its Event Information loop");
                    }

                    var des = DescriptorFactory.DescriptorFromData(Data, startOfNextField);
                    descriptors.Add(des);
                    startOfNextField = (ushort)descriptorEnd;
                }
                item.Descriptors = descriptors;
                items.Add(item);
            }

            InProgressTable.Items = items;

            if (isScheduledTable)
            {
                var previousVersion = ScheduledEventInformationTables.Find(table => HasSameScheduleIdentity(table, InProgressTable));
                if (previousVersion != null && previousVersion.VersionNumber != InProgressTable.VersionNumber)
                {
                    ScheduledEventInformationTables.RemoveAll(table => HasSameScheduleIdentity(table, InProgressTable));
                }

                ScheduledEventInformationTables.Add(InProgressTable);
                LastChangedTable = InProgressTable;
                OnTableChangeDetected();
                return;
            }

            EventInformationItems.AddRange(items);

            if (InProgressTable.VersionNumber == EventInformationTable?.VersionNumber) return;

            EventInformationTable = InProgressTable;
            LastChangedTable = InProgressTable;
            _sectionsCompleted.Add(InProgressTable.SectionNumber);

            OnTableChangeDetected();
        }

        private static bool HasSameScheduleIdentity(EventInformationTable left, EventInformationTable right)
        {
            return left.TableId == right.TableId &&
                   left.ServiceId == right.ServiceId &&
                   left.TransportStreamId == right.TransportStreamId &&
                   left.OriginalNetworkId == right.OriginalNetworkId &&
                   left.CurrentNextIndicator == right.CurrentNextIndicator;
        }
    }
}
