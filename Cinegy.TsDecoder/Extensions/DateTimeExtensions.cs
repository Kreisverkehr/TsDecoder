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

using System;

namespace Cinegy.TsDecoder.Extensions;

internal static class DateTimeExtensions
{
    extension(DateTime)
    {
        /// <summary>
        /// Calculates a clr <see cref="System.DateTime" from the given timestamp/>
        /// </summary>
        /// <param name="tsTimestamp">Timestamp in transport strem format</param>
        /// <returns>Null if the timestamp is set to undefined or the actual DateTime value</returns>
        /// <exception cref="TsTimestampFormatException">If the value is not undefined but it's format does not match the expected structure</exception>
        internal static DateTime? FromTsTimestamp(ulong tsTimestamp)
        {
            if ((tsTimestamp & 0xFFFFFFFFFF) == 0xFFFFFFFFFF)
                return null;

            var mjd = tsTimestamp >> 24;
            int y, m, d, k, h, mi, s;

            y = Convert.ToInt32((long)((mjd - 15078.2) / 365.25));
            m = Convert.ToInt32((long)((mjd - 14956.1 - (long)(y * 365.25)) / 30.6001));
            d = Convert.ToInt32((long)(mjd - 14956 - (ulong)(y * 365.25) - (ulong)(m * 30.6001)));
            k = m == 14 || m == 15 ? 1 : 0;
            y = y + k + 1900;
            m = m - 1 - k * 12;

            h = Convert.ToInt32(Convert.BcdToUInt64((tsTimestamp >> 16) & 0xFF));
            mi = Convert.ToInt32(Convert.BcdToUInt64((tsTimestamp >> 8) & 0xFF));
            s = Convert.ToInt32(Convert.BcdToUInt64(tsTimestamp & 0xFF));

            try
            {
                return new DateTime(y, m, d, h, mi, s, DateTimeKind.Utc);
            }
            catch (Exception e)
            {
                throw new TsTimestampFormatException("The given timestamp does not contain a valid date time.", e);
            }
        }
    }
}

[System.Serializable]
public class TsTimestampFormatException : FormatException
{
    public TsTimestampFormatException() { }
    public TsTimestampFormatException(string message) : base(message) { }
    public TsTimestampFormatException(string message, System.Exception inner) : base(message, inner) { }
}