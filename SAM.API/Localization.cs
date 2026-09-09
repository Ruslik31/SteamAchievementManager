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

namespace SAM.API
{
    public static class Localization
    {
        public static bool IsRussian => LanguageManager.CurrentLanguage == "russian";

        // GamePicker strings
        public static string PickerTitle => IsRussian
            ? "Steam Achievement Manager 7.0 | Выберите игру..."
            : "Steam Achievement Manager 7.0 | Pick a game... Any game...";

        public static string RefreshGames => IsRussian ? "Обновить список" : "Refresh Games";
        public static string AddGame => IsRussian ? "Добавить игру" : "Add Game";
        public static string Filter => IsRussian ? "Фильтр" : "Filter";
        public static string GameFiltering => IsRussian ? "Фильтр игр" : "Game filtering";
        public static string ShowGames => IsRussian ? "Показывать &игры" : "Show &games";
        public static string ShowDemos => IsRussian ? "Показывать &демо" : "Show &demos";
        public static string ShowMods => IsRussian ? "Показывать &моды" : "Show &mods";
        public static string ShowJunk => IsRussian ? "Показывать &мусор" : "Show &junk";
        public static string DownloadStatus => IsRussian ? "Статус загрузки" : "Download status";
        public static string DownloadingGameList => IsRussian ? "Загрузка списка игр..." : "Downloading game list...";
        public static string CheckingGameOwnership => IsRussian ? "Проверка наличия игр..." : "Checking game ownership...";
        public static string Language => IsRussian ? "Язык" : "Language";

        public static string DisplayingGames(int count, int total) => IsRussian
            ? $"Отображено игр: {count}. Всего игр: {total}."
            : $"Displaying {count} games. Total {total} games.";

        public static string DownloadingGameIcons(int count) => IsRussian
            ? $"Загрузка {count} иконок игр..."
            : $"Downloading {count} game icons...";

        public static string FailedToStartGameExe => IsRussian
            ? "Не удалось запустить SAM.Game.exe."
            : "Failed to start SAM.Game.exe.";

        public static string PleaseEnterValidGameId => IsRussian
            ? "Пожалуйста, введите корректный ID игры."
            : "Please enter a valid game ID.";

        public static string DontOwnGame => IsRussian
            ? "У вас нет этой игры."
            : "You don't own that game.";

        public static string Error => IsRussian ? "Ошибка" : "Error";
        public static string Warning => IsRussian ? "Предупреждение" : "Warning";
        public static string Question => IsRussian ? "Вопрос" : "Question";
        public static string Information => IsRussian ? "Информация" : "Information";

        // Manager (SAM.Game) strings
        public static string CommitChanges => IsRussian ? "Сохранить изменения" : "Commit Changes";
        public static string CommitChangesToolTip => IsRussian
            ? "Сохранить достижения и статистику для активной игры."
            : "Store achievements and statistics for active game.";

        public static string Refresh => IsRussian ? "Обновить" : "Refresh";
        public static string RefreshToolTip => IsRussian
            ? "Обновить достижения и статистику для активной игры."
            : "Refresh achievements and statistics for active game.";

        public static string Reset => IsRussian ? "Сбросить" : "Reset";
        public static string ResetToolTip => IsRussian
            ? "Сбросить достижения и/или статистику для активной игры."
            : "Reset achievements and/or statistics for active game.";

        public static string AchievementsTab => IsRussian ? "Достижения" : "Achievements";
        public static string StatisticsTab => IsRussian ? "Статистика" : "Statistics";

        public static string HeaderName => IsRussian ? "Название" : "Name";
        public static string HeaderDescription => IsRussian ? "Описание" : "Description";
        public static string HeaderUnlockTime => IsRussian ? "Время разблокировки" : "Unlock Time";
        public static string HeaderValue => IsRussian ? "Значение" : "Value";
        public static string HeaderExtra => IsRussian ? "Дополнительно" : "Extra";

        public static string LockAll => IsRussian ? "Заблокировать все" : "Lock All";
        public static string LockAllToolTip => IsRussian ? "Заблокировать все достижения." : "Lock all achievements.";
        public static string InvertAll => IsRussian ? "Инвертировать все" : "Invert All";
        public static string InvertAllToolTip => IsRussian ? "Инвертировать все достижения." : "Invert all achievements.";
        public static string UnlockAll => IsRussian ? "Разблокировать все" : "Unlock All";
        public static string UnlockAllToolTip => IsRussian ? "Разблокировать все достижения." : "Unlock all achievements.";

        public static string ShowOnly => IsRussian ? "Показывать:" : "Show only";
        public static string Locked => IsRussian ? "закрытые" : "locked";
        public static string Unlocked => IsRussian ? "полученные" : "unlocked";
        public static string MatchingStringToolTip => IsRussian
            ? "Введите минимум 3 символа из названия или описания"
            : "Type at least 3 characters that must appear in the name or description";

        public static string EnableStatsEditing => IsRussian
            ? "Я понимаю, что изменение значений статистики может всё испортить, и винить в этом могу только себя."
            : "I understand by modifying the values of stats, I may screw things up and can't blame anyone but myself.";

        public static string DownloadingIcons(int count) => IsRussian
            ? $"Загрузка {count} иконок..."
            : $"Downloading {count} icons...";

        public static string GenericErrorNotOwned => IsRussian
            ? "общая ошибка — обычно это означает, что вы не владеете игрой"
            : "generic error -- this usually means you don't own the game";

        public static string ErrorRetrievingStats(string err) => IsRussian
            ? $"Ошибка при получении статистики: {err}"
            : $"Error while retrieving stats: {err}";

        public static string FailedToLoadSchema => IsRussian
            ? "Не удалось загрузить схему."
            : "Failed to load schema.";

        public static string ErrorHandlingAchievements => IsRussian
            ? "Ошибка при обработке полученных достижений."
            : "Error when handling achievements retrieval.";

        public static string ErrorHandlingStats => IsRussian
            ? "Ошибка при обработке полученной статистики."
            : "Error when handling stats retrieval.";

        public static string RetrievedAchievementsAndStats(int ach, int stats) => IsRussian
            ? $"Получено достижений: {ach}, статистик: {stats}."
            : $"Retrieved {ach} achievements and {stats} statistics.";

        public static string RetrievingStatInfo => IsRussian
            ? "Получение информации о статистике..."
            : "Retrieving stat information...";

        public static string ErrorSettingState(string id) => IsRussian
            ? $"Произошла ошибка при установке состояния для {id}, сохранение отменено."
            : $"An error occurred while setting the state for {id}, aborting store.";

        public static string ErrorSettingValue(string id) => IsRussian
            ? $"Произошла ошибка при установке значения для {id}, сохранение отменено."
            : $"An error occurred while setting the value for {id}, aborting store.";

        public static string ErrorStoringAborting => IsRussian
            ? "Произошла ошибка при сохранении, отмена."
            : "An error occurred while storing, aborting.";

        public static string StoredAchievementsAndStats(int ach, int stats) => IsRussian
            ? $"Сохранено достижений: {ach}, статистик: {stats}."
            : $"Stored {ach} achievements and {stats} statistics.";

        public static string StatIsProtected => IsRussian
            ? "Статистика защищена! Вы не можете её изменить"
            : "Stat is protected! -- you can't modify it";

        public static string InvalidValue => IsRussian ? "Некорректное значение" : "Invalid value";

        public static string ConfirmResetStats => IsRussian
            ? "Вы абсолютно уверены, что хотите сбросить статистику?"
            : "Are you absolutely sure you want to reset stats?";

        public static string ConfirmResetAchievementsToo => IsRussian
            ? "Вы хотите сбросить и достижения тоже?"
            : "Do you want to reset achievements too?";

        public static string ConfirmReallySure => IsRussian
            ? "Вы точно-точно уверены?"
            : "Really really sure?";

        public static string ProtectedAchievementNotice => IsRussian
            ? "К сожалению, это защищённое достижение, и им нельзя управлять с помощью Steam Achievement Manager."
            : "Sorry, but this is a protected achievement and cannot be managed with Steam Achievement Manager.";

        public static string ParseAppIdFailed => IsRussian
            ? "Не удалось прочитать ID приложения из аргумента командной строки."
            : "Could not parse application ID from command line argument.";

        public static string RunFromSteamDeclined => IsRussian
            ? "Инструмент не может запускаться из папки Steam."
            : "This tool declines to being run from the Steam directory.";

        public static string SteamNotRunning => IsRussian
            ? "Steam не запущен. Пожалуйста, запустите Steam и попробуйте снова."
            : "Steam is not running. Please start Steam then run this tool again.";

        public static string FamilyShareLocked => IsRussian
            ? "Если игра доступна по Family Share, возможно, доступ заблокирован,\nтак как владелец аккаунта в данный момент играет."
            : "If you have the game through Family Share, the game may be locked due to\nthe Family Share account actively playing a game.";

        public static string ExceptionalError => IsRussian
            ? "Произошла непредвиденная ошибка!"
            : "You've caused an exceptional error!";
    }
}
