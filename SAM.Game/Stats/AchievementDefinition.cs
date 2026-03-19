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
using System.Collections.Generic;
using SAM.API;

namespace SAM.Game.Stats
{
    internal class AchievementDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconNormal;
        public string IconLocked;
        public bool IsHidden;
        public int Permission;

        public static string GetLocalizedString(KeyValue kv, string language, string defaultValue)
        {
            if (kv == null) return defaultValue;
            var activeLanguage = API.LanguageManager.CurrentLanguage ?? "english";

            var candidates = new List<string> { activeLanguage, language };

            if (activeLanguage.Equals("russian", StringComparison.OrdinalIgnoreCase) || activeLanguage.Equals("ru", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add("russian");
                candidates.Add("ru");
                candidates.Add("rus");
            }
            else if (activeLanguage.Equals("english", StringComparison.OrdinalIgnoreCase) || activeLanguage.Equals("en", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add("english");
                candidates.Add("en");
                candidates.Add("eng");
            }

            candidates.Add("english");
            candidates.Add("en");

            foreach (var lang in candidates)
            {
                if (string.IsNullOrEmpty(lang)) continue;
                var child = kv[lang];
                if (child != null)
                {
                    var val = child.AsString("");
                    if (string.IsNullOrEmpty(val) == false)
                    {
                        return val;
                    }
                }
            }

            var direct = kv.AsString("");
            if (string.IsNullOrEmpty(direct) == false)
            {
                return direct;
            }

            if (kv.Children != null)
            {
                foreach (var child in kv.Children)
                {
                    var childVal = child.AsString("");
                    if (string.IsNullOrEmpty(childVal) == false)
                    {
                        return childVal;
                    }
                }
            }

            return defaultValue;
        }

        public static AchievementDefinition Load(KeyValue kv, string language)
        {
            if (kv == null) return null;

            var def = new AchievementDefinition
            {
                Id = kv.Name,
                Permission = kv["permission"].AsInteger(0),
                IsHidden = kv["permission"].AsInteger(0) != 0 || kv["hidden"].AsInteger(0) != 0
            };

            var display = kv["display"];
            if (display != null)
            {
                def.Name = GetLocalizedString(display["name"], language, def.Id);
                def.Description = GetLocalizedString(display["desc"], language, "");
                def.IconNormal = display["icon"].AsString("");
                def.IconLocked = display["icon_gray"].AsString("");
                def.IsHidden = display["hidden"].AsBoolean(false);
            }
            else
            {
                def.Name = def.Id;
            }

            return def;
        }

        public override string ToString()
        {
            return $"{this.Name ?? this.Id ?? base.ToString()}: {this.Permission}";
        }
    }
}
