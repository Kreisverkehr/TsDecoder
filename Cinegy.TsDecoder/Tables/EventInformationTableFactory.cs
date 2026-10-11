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
using System.Linq;
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

        private readonly Dictionary<(byte TableId, ushort ServiceId, ushort TransportStreamId, ushort OriginalNetworkId, bool CurrentNextIndicator), TableState> _tableStates = new();

        private new EventInformationTable InProgressTable
        {
            get { return base.InProgressTable as EventInformationTable; }
            set { base.InProgressTable = value; }
        }

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

            var tableIdentity = GetTableIdentity(InProgressTable);
            if (!_tableStates.TryGetValue(tableIdentity, out var tableState))
            {
                tableState = new TableState(InProgressTable.VersionNumber);
                _tableStates.Add(tableIdentity, tableState);
            }
            else if (tableState.VersionNumber != InProgressTable.VersionNumber)
            {
                tableState.Reset(InProgressTable.VersionNumber);
                ScheduledEventInformationTables.RemoveAll(table => HasSameScheduleIdentity(table, InProgressTable));
            }

            if (tableState.Sections.Contains(InProgressTable.SectionNumber))
            {
                InProgressTable = null;
                return;
            }

            var isScheduledTable = InProgressTable.TableId >= 0x50;

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
                tableState.Sections.Add(InProgressTable.SectionNumber);
                ScheduledEventInformationTables.Add(InProgressTable);
                LastChangedTable = InProgressTable;
                OnTableChangeDetected();
                return;
            }

            tableState.Sections.Add(InProgressTable.SectionNumber);
            tableState.Tables[InProgressTable.SectionNumber] = InProgressTable;
            EventInformationItems = _tableStates
                .Where(entry => entry.Key.TableId == 0x4e)
                .SelectMany(entry => entry.Value.Tables.Values)
                .SelectMany(table => table.Items)
                .ToList();

            EventInformationTable = InProgressTable;
            LastChangedTable = InProgressTable;

            OnTableChangeDetected();
        }

        private static (byte TableId, ushort ServiceId, ushort TransportStreamId, ushort OriginalNetworkId, bool CurrentNextIndicator) GetTableIdentity(EventInformationTable table)
        {
            return (table.TableId, table.ServiceId, table.TransportStreamId, table.OriginalNetworkId, table.CurrentNextIndicator);
        }

        private static bool HasSameScheduleIdentity(EventInformationTable left, EventInformationTable right)
        {
            return left.TableId == right.TableId &&
                   left.ServiceId == right.ServiceId &&
                   left.TransportStreamId == right.TransportStreamId &&
                   left.OriginalNetworkId == right.OriginalNetworkId &&
                   left.CurrentNextIndicator == right.CurrentNextIndicator;
        }

        private sealed class TableState
        {
            public TableState(byte versionNumber)
            {
                VersionNumber = versionNumber;
            }

            public byte VersionNumber { get; private set; }

            public HashSet<byte> Sections { get; } = new HashSet<byte>();

            public Dictionary<byte, EventInformationTable> Tables { get; } = new Dictionary<byte, EventInformationTable>();

            public void Reset(byte versionNumber)
            {
                VersionNumber = versionNumber;
                Sections.Clear();
                Tables.Clear();
            }
        }
    }
}
