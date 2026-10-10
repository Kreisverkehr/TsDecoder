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

internal static class TimeSpanExtensions
{
    extension(TimeSpan)
    {
        /// <summary>
        /// Creates a <see cref="System.TimeSpan"/> from a transport stream duration.
        /// </summary>
        /// <remarks>A transport stream duration 16 bits coded as 6 digits BCD. 01:45:30 is coded as 0x014530</remarks>
        /// <param name="tsDuration">The duration in ts format</param>
        /// <returns>A Timespan representing the duratioon</returns>
        /// <exception cref="TsDurationFormatException">Thrown when any of the calculated digits do not correlate to a valid timespan</exception>
        internal static TimeSpan FromTsDuration(uint tsDuration)
        {
            int h, mi, s;
            h = Convert.ToInt32(Convert.BcdToUInt64((tsDuration >> 16) & 0xFF));
            mi = Convert.ToInt32(Convert.BcdToUInt64((tsDuration >> 8) & 0xFF));
            s = Convert.ToInt32(Convert.BcdToUInt64(tsDuration & 0xFF));

            try
            {
                return new TimeSpan(h, mi, s);
            }
            catch (Exception e)
            {
                throw new TsDurationFormatException("The given duration does not match the expected format.", e);
            }
        }
    }
}

[System.Serializable]
public class TsDurationFormatException : FormatException
{
    public TsDurationFormatException() { }
    public TsDurationFormatException(string message) : base(message) { }
    public TsDurationFormatException(string message, System.Exception inner) : base(message, inner) { }
}