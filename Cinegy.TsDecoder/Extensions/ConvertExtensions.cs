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

internal static class ConvertExtensions
{
    extension(Convert)
    {
        /// <summary>
        /// Converts a binary coded decimal to an actual 64-bit integer.
        /// </summary>
        /// <remarks>It shifts through each nibble, calculates the corresponding digit and adds them up to the final value.</remarks>
        /// <param name="bcd">value in BCD format</param>
        /// <returns>A valid 64-bit integer</returns>
        /// <exception cref="FormatException">Thrown if it encounters a digit larger than 9</exception>
        internal static ulong BcdToUInt64(ulong bcd)
        {
            ulong result = 0;

            for (var shift = 60; shift >= 0; shift -= 4)
            {
                var digit = (bcd >> shift) & 0xF;
                if (digit > 9)
                    throw new FormatException("Invalid BCD-Digit.");

                result = checked(result * 10 + digit);
            }

            return result;
        }
    }
}