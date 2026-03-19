/* Copyright (c) 2024 Rick (rick 'at' gibbed 'dot' us)
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 *
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 *
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Globalization;

namespace SAM.Game.Stats
{
    internal class FloatStatInfo : StatInfo
    {
        public float OriginalValue;
        public float FloatValue;

        public override object Value
        {
            get => this.FloatValue;
            set
            {
                if (value is float fVal)
                {
                    this.FloatValue = fVal;
                    return;
                }

                if (float.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float f) ||
                    float.TryParse(value?.ToString(), NumberStyles.Any, CultureInfo.CurrentCulture, out f))
                {
                    this.FloatValue = f;
                }
            }
        }

        public override bool IsModified => Math.Abs(this.FloatValue - this.OriginalValue) > 0.00001f;
    }
}
