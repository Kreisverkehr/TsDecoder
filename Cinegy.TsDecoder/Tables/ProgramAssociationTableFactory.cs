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

// Modified by Kreisverkehr in 2026: assemble PAT sections spanning multiple transport packets.

using System;
using System.Diagnostics;
using Cinegy.TsDecoder.TransportStream;

namespace Cinegy.TsDecoder.Tables
{
    public class ProgramAssociationTableFactory : TableFactory
    {
        public ProgramAssociationTable ProgramAssociationTable { get; private set; }

        private new ProgramAssociationTable InProgressTable
        {
            get { return base.InProgressTable as ProgramAssociationTable; }
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

                InProgressTable = new ProgramAssociationTable { PointerField = packet.Payload[0] };

                if (InProgressTable.PointerField > packet.PayloadLen)
                {
                    Debug.Assert(true, "Program Association Table has packet pointer outside the packet.");
                }

                var pos = 1 + InProgressTable.PointerField;

                if (pos > 184)
                {
                    CorruptedPackets++;
                    InProgressTable = null;
                    return;
                }
                InProgressTable.VersionNumber = (byte)((packet.Payload[pos + 5] & 0x3E) >> 1);
            
                //if (ProgramAssociationTable != null && ProgramAssociationTable.VersionNumber == InProgressTable.VersionNumber)
                //{
                //    InProgressTable = null;
                //    return;
                //}

                InProgressTable.TransportStreamId = (ushort)((packet.Payload[pos + 3] << 8) + packet.Payload[pos + 4]);

                InProgressTable.TableId = packet.Payload[pos];
                InProgressTable.SectionLength = (ushort)(((packet.Payload[pos + 1] & 0x3) << 8) + packet.Payload[pos + 2]);

                InProgressTable.CurrentNextIndicator = (packet.Payload[pos + 5] & 0x1) != 0;
                InProgressTable.SectionNumber = packet.Payload[pos + 6];
                InProgressTable.LastSectionNumber = packet.Payload[pos + 7];

                if (InProgressTable.SectionLength - 9 < 1)
                {
                    CorruptedPackets++;
                    InProgressTable = null;
                    return;
                }
            }

            if (InProgressTable == null) return;

            AddData(packet);

            if (!HasAllBytes()) return;

            InProgressTable.ProgramNumbers = new ushort[(InProgressTable.SectionLength - 9) / 4];
            InProgressTable.Pids = new ushort[(InProgressTable.SectionLength - 9) / 4];

            var programStart = InProgressTable.PointerField + 9;
            var crcPosition = programStart;

            for (var i = 0; i < (InProgressTable.SectionLength - 9) / 4; i++)
            {
                InProgressTable.ProgramNumbers[i] =
                    (ushort)
                         ((Data[programStart + (i * 4)] << 8) + Data[programStart + 1 + (i * 4)]);
                InProgressTable.Pids[i] =
                    (ushort)
                    (((Data[programStart + 2 + (i * 4)] & 0x1F) << 8) +
                        Data[programStart + 3 + (i * 4)]);

                crcPosition += 4;
            }
            
            InProgressTable.Crc = (uint)(((Data[crcPosition]) << 24) + (Data[crcPosition + 1] << 16) +
                                  (Data[crcPosition + 2] << 8) + (Data[crcPosition + 3]));

            if (InProgressTable.Crc == ProgramAssociationTable?.Crc) return;

            ProgramAssociationTable = InProgressTable;

            OnTableChangeDetected();
        }
    }
}
