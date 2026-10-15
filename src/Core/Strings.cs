namespace HighCommand
{
    /// <summary>All user-visible texts of the mod, English and Russian. Tokens start with HIGHCOMMAND_.</summary>
    internal static class Strings
    {
        internal static void Register()
        {
            Loc.Add("HIGHCOMMAND_GAMEMODE_NAME", "High Command", "Верховное командование");
            Loc.Add("HIGHCOMMAND_GAMEMODE_DESC",
                "Command an army of survivors from above. Buy new heroes with gold dropped by enemies.",
                "Командуйте армией выживших сверху. Покупайте новых героев за золото, выпадающее из врагов.");

            // help sheet (F1)
            Loc.Add("HIGHCOMMAND_HELP_TITLE", "High Command - hotkeys", "Верховное командование - горячие клавиши");
            Loc.Add("HIGHCOMMAND_KEY_EDGE", "Screen edge", "Край экрана");
            Loc.Add("HIGHCOMMAND_KEY_WHEEL", "Mouse wheel", "Колесо мыши");
            Loc.Add("HIGHCOMMAND_KEY_MMB", "Middle mouse", "Средняя кнопка");
            Loc.Add("HIGHCOMMAND_KEY_MINIMAP", "Minimap click", "Клик по мини-карте");
            Loc.Add("HIGHCOMMAND_HELP_PAN", "Move the camera", "Двигать камеру");
            Loc.Add("HIGHCOMMAND_HELP_EDGE", "Move the camera", "Двигать камеру");
            Loc.Add("HIGHCOMMAND_HELP_ZOOM", "Zoom in / out (towards the point under the cursor, also up and down)", "Приблизить / отдалить (к точке под курсором, в том числе вверх и вниз)");
            Loc.Add("HIGHCOMMAND_HELP_ROTATE", "Rotate the camera", "Повернуть камеру");
            Loc.Add("HIGHCOMMAND_HELP_TILT", "Tilt the camera (flatter / from above)", "Наклонить камеру (ниже / сверху)");
            Loc.Add("HIGHCOMMAND_KEY_MMB_SIDE", "MMB sideways", "СКМ вбок");
            Loc.Add("HIGHCOMMAND_KEY_MMB_VERT", "MMB up / down", "СКМ вверх-вниз");
            Loc.Add("HIGHCOMMAND_KEY_RMB_OBJECT", "Right click a chest", "ПКМ по сундуку");
            Loc.Add("HIGHCOMMAND_HELP_LOOT", "The nearest hero goes to open it (Shift: a route)", "Ближайший герой идёт открыть (Shift — маршрут)");
            Loc.Add("HIGHCOMMAND_HELP_FOCUS", "Fly to the army (twice: follow it)", "К армии (дважды — следить за ней)");
            Loc.Add("HIGHCOMMAND_HELP_RESETCAM", "Reset the camera's turn, tilt and zoom, back onto the ground here", "Сбросить поворот, наклон и приближение, камера опускается на землю здесь");
            Loc.Add("HIGHCOMMAND_CAMERA_FOLLOW", "The camera follows the heroes (move it to let go)", "Камера следит за героями (сдвиньте её, чтобы отпустить)");
            Loc.Add("HIGHCOMMAND_HELP_MINIMAP", "Fly the camera there", "Перелёт камеры в эту точку");
            Loc.Add("HIGHCOMMAND_HELP_HELP", "This sheet", "Эта шпаргалка");
            Loc.Add("HIGHCOMMAND_HELP_PAUSE", "Pause menu", "Меню паузы");
            Loc.Add("HIGHCOMMAND_KEY_LMB", "Left click / drag", "ЛКМ / рамка");
            Loc.Add("HIGHCOMMAND_KEY_DOUBLE", "Double click", "Двойной клик");
            Loc.Add("HIGHCOMMAND_HELP_SELECT", "Select a hero / box select", "Выбрать героя / выделить рамкой");
            Loc.Add("HIGHCOMMAND_HELP_SELECT_ADD", "Add / remove from selection", "Добавить / убрать из выделения");
            Loc.Add("HIGHCOMMAND_HELP_SELECT_TYPE", "Select all of the same kind", "Выбрать всех такого же типа");
            Loc.Add("HIGHCOMMAND_HELP_SELECT_ALL", "Select all heroes", "Выбрать всех героев");
            Loc.Add("HIGHCOMMAND_HELP_GROUP_SET", "Set a control group", "Назначить группу");
            Loc.Add("HIGHCOMMAND_HELP_GROUP_RECALL", "Select group (twice: camera)", "Вызвать группу (дважды: камера)");
            Loc.Add("HIGHCOMMAND_HELP_CYCLE", "Next hero (Shift: previous)", "Следующий герой (Shift: предыдущий)");
            Loc.Add("HIGHCOMMAND_LEVEL_SHORT", "Lv {0}", "Ур. {0}");

            // orders
            Loc.Add("HIGHCOMMAND_KEY_RMB", "Right click", "ПКМ");
            Loc.Add("HIGHCOMMAND_HELP_RMB", "Move / attack / follow / use", "Идти / атаковать / следовать / использовать");
            Loc.Add("HIGHCOMMAND_HELP_QUEUE", "Queue the order", "Добавить приказ в очередь");
            Loc.Add("HIGHCOMMAND_HELP_ATTACKMOVE", "Attack-move to a point", "Идти в точку, сражаясь");
            Loc.Add("HIGHCOMMAND_HELP_FOCUSFIRE", "Focus fire on the enemy under the cursor", "Фокус на враге под курсором");
            Loc.Add("HIGHCOMMAND_HELP_STOPHOLD", "Stop / hold position", "Стоп / держать позицию");
            Loc.Add("HIGHCOMMAND_HELP_RETREAT", "Retreat / patrol / rally point", "Отступить / патруль / точка сбора");
            Loc.Add("HIGHCOMMAND_HELP_CAST", "Use secondary / utility / special / equipment", "Умения: второе / рывок / особое / снаряжение");
            Loc.Add("HIGHCOMMAND_HELP_AUTO", "Switch a skill's auto-use", "Вкл/выкл автоприменение умения");
            Loc.Add("HIGHCOMMAND_HELP_STANCE", "Stance: aggressive / defensive / passive", "Стойка: агрессивная / защитная / пассивная");
            Loc.Add("HIGHCOMMAND_HELP_FORMATION", "Formation: blob / line / column", "Построение: кучка / линия / колонна");
            Loc.Add("HIGHCOMMAND_HINT_ATTACKMOVE", "Attack-move: left click a point (right click cancels)", "Атака-движение: ЛКМ по точке (ПКМ — отмена)");
            Loc.Add("HIGHCOMMAND_HINT_FOCUS", "Focus fire: left click an enemy", "Фокус: ЛКМ по врагу");
            Loc.Add("HIGHCOMMAND_HINT_PATROL", "Patrol: left click the far point", "Патруль: ЛКМ по дальней точке");
            Loc.Add("HIGHCOMMAND_HINT_RALLY", "Rally point: left click", "Точка сбора: ЛКМ");
            Loc.Add("HIGHCOMMAND_HINT_CAST", "Skill: left click a target or ground", "Умение: ЛКМ по цели или земле");
            Loc.Add("HIGHCOMMAND_RALLY_SET", "Rally point set", "Точка сбора задана");
            Loc.Add("HIGHCOMMAND_FORMATION_BLOB", "blob", "кучка");
            Loc.Add("HIGHCOMMAND_FORMATION_LINE", "line", "линия");
            Loc.Add("HIGHCOMMAND_FORMATION_COLUMN", "column", "колонна");
            Loc.Add("HIGHCOMMAND_FORMATION_SET", "Formation: {0}", "Построение: {0}");
            Loc.Add("HIGHCOMMAND_STANCE_AGGRESSIVE", "aggressive", "агрессивная");
            Loc.Add("HIGHCOMMAND_STANCE_DEFENSIVE", "defensive", "защитная");
            Loc.Add("HIGHCOMMAND_STANCE_PASSIVE", "passive", "пассивная");
            Loc.Add("HIGHCOMMAND_STANCE_SET", "Stance: {0}", "Стойка: {0}");
            Loc.Add("HIGHCOMMAND_AUTO_ON", "Auto-use ON: {0}", "Автоприменение ВКЛ: {0}");
            Loc.Add("HIGHCOMMAND_AUTO_OFF", "Auto-use OFF: {0}", "Автоприменение ВЫКЛ: {0}");
            Loc.Add("HIGHCOMMAND_SLOT_PRIMARY", "primary", "основное");
            Loc.Add("HIGHCOMMAND_SLOT_SECONDARY", "secondary", "вторичное");
            Loc.Add("HIGHCOMMAND_SLOT_UTILITY", "utility", "рывок");
            Loc.Add("HIGHCOMMAND_SLOT_SPECIAL", "special", "особое");
            Loc.Add("HIGHCOMMAND_SLOT_EQUIPMENT", "equipment", "экипировка");

            // economy / barracks
            Loc.Add("HIGHCOMMAND_HELP_BARRACKS", "Barracks: recruit heroes, buy army upgrades", "Казарма: найм героев и улучшения армии");
            Loc.Add("HIGHCOMMAND_BARRACKS_NAME", "Barracks", "Казарма");
            Loc.Add("HIGHCOMMAND_BARRACKS_NOTE", "next hero <color={2}>${0}</color>    purse <color=#FFFCC4>${1}</color>",
                "следующий герой <color={2}>${0}</color>    казна <color=#FFFCC4>${1}</color>");
            Loc.Add("HIGHCOMMAND_BARRACKS_HINT", "Click a survivor to recruit them. Esc closes the window.", "Нажмите на выжившего, чтобы нанять. Esc закрывает окно.");
            Loc.Add("HIGHCOMMAND_BARRACKS_OWNED", "In the army", "В армии");
            Loc.Add("HIGHCOMMAND_BARRACKS_FULL", "army full", "армия полна");
            Loc.Add("HIGHCOMMAND_RECRUIT_LOCKED", "Locked", "Закрыт");
            Loc.Add("HIGHCOMMAND_RECRUIT_NOTALLOWED", "Unavailable", "Недоступен");
            Loc.Add("HIGHCOMMAND_RECRUIT_OWNED", "In the army", "Уже в армии");
            Loc.Add("HIGHCOMMAND_RECRUIT_FULL", "Army is full", "Армия заполнена");
            Loc.Add("HIGHCOMMAND_RECRUIT_NEEDGOLD", "Not enough gold: ${0}", "Не хватает золота: ${0}");
            Loc.Add("HIGHCOMMAND_RECRUITED", "Recruited: {0}", "Нанят: {0}");

            // army upgrades and veteran ranks
            Loc.Add("HIGHCOMMAND_UPGRADE_0_NAME", "Weapons", "Оружие");
            Loc.Add("HIGHCOMMAND_UPGRADE_0_DESC", "+10% damage for every hero per level.", "+10% урона каждому герою за уровень.");
            Loc.Add("HIGHCOMMAND_UPGRADE_1_NAME", "Armor", "Броня");
            Loc.Add("HIGHCOMMAND_UPGRADE_1_DESC", "+8 armor for every hero per level (about 7% less damage taken).", "+8 брони каждому герою за уровень (около 7% меньше урона).");
            Loc.Add("HIGHCOMMAND_UPGRADE_2_NAME", "Field medicine", "Полевая медицина");
            Loc.Add("HIGHCOMMAND_UPGRADE_2_DESC", "+0.6 health regeneration per second per level, growing with the hero's level.", "+0,6 здоровья в секунду за уровень, растёт с уровнем героя.");
            Loc.Add("HIGHCOMMAND_UPGRADE_3_NAME", "Logistics", "Логистика");
            Loc.Add("HIGHCOMMAND_UPGRADE_3_DESC", "+8% movement speed for every hero per level.", "+8% скорости бега каждому герою за уровень.");
            Loc.Add("HIGHCOMMAND_UPGRADE_HEADER", "Army upgrades: every hero, recruits too", "Улучшения армии: для всех героев, и новых тоже");
            Loc.Add("HIGHCOMMAND_UPGRADE_LEVEL", "level {0}/{1}", "ур. {0}/{1}");
            Loc.Add("HIGHCOMMAND_UPGRADE_MAXSHORT", "MAX", "МАКС.");
            Loc.Add("HIGHCOMMAND_DISTANCE", "{0} m", "{0} м");
            Loc.Add("HIGHCOMMAND_UPGRADE_MAXED","Fully upgraded", "Уже максимум");
            Loc.Add("HIGHCOMMAND_UPGRADE_DONE", "{0}: level {1}", "{0}: уровень {1}");
            Loc.Add("HIGHCOMMAND_RANK_UP", "{0} is now a veteran: rank {1} (+10% damage, +5 armor per rank)", "{0}: ранг ветерана {1} (+10% урона и +5 брони за ранг)");
            Loc.Add("HIGHCOMMAND_RANK_INFO", "Veteran rank {0} ({1} kills). Next rank at {2} kills.", "Ранг ветерана {0} ({1} убийств). Следующий ранг: {2} убийств.");
            Loc.Add("HIGHCOMMAND_RANK_INFO_MAX", "Veteran rank {0} ({1} kills), the highest.", "Ранг ветерана {0} ({1} убийств), высший.");
            Loc.Add("HIGHCOMMAND_ERR_NORUN", "No run in progress", "Забег не идёт");
            Loc.Add("HIGHCOMMAND_ERR_FAILED", "Could not recruit (gold refunded)", "Не удалось нанять (золото возвращено)");
            Loc.Add("HIGHCOMMAND_ERR_NOLINK", "No connection to the host", "Нет связи с хостом");
            Loc.Add("HIGHCOMMAND_ROLE_RANGED", "Ranged damage", "Дальний урон");
            Loc.Add("HIGHCOMMAND_ROLE_ASSASSIN", "Assassin", "Убийца");
            Loc.Add("HIGHCOMMAND_ROLE_TANK", "Tank", "Танк");
            Loc.Add("HIGHCOMMAND_ROLE_MELEE", "Melee", "Ближний бой");
            Loc.Add("HIGHCOMMAND_ROLE_CASTER", "Caster", "Заклинатель");
            Loc.Add("HIGHCOMMAND_ROLE_SNIPER", "Sniper", "Снайпер");
            Loc.Add("HIGHCOMMAND_ROLE_SUPPORT", "Support", "Поддержка");
            Loc.Add("HIGHCOMMAND_ROLE_SUMMONER", "Summoner", "Призыватель");
            Loc.Add("HIGHCOMMAND_ROLE_GENERIC", "Survivor", "Выживший");

            // lobby rules
            Loc.Add("HIGHCOMMAND_RULES_HEADER", "High Command", "Верховное командование");
            Loc.Add("HIGHCOMMAND_RULES_SUBTITLE", "Rules of the strategy mode", "Правила стратегического режима");
            Loc.Add("HIGHCOMMAND_RULES_EDIT", "Edit the High Command rules", "Изменить правила «Верховного командования»");
            Loc.Add("HIGHCOMMAND_RULE_CONFIG_NAME", "From settings", "Из настроек");
            Loc.Add("HIGHCOMMAND_RULE_GOLD", "Starting gold", "Стартовое золото");
            Loc.Add("HIGHCOMMAND_RULE_GOLD_DESC", "Gold the army starts the run with. \"From settings\" uses the value of the config file.",
                "Золото армии в начале забега. «Из настроек» берёт значение из файла настроек.");
            Loc.Add("HIGHCOMMAND_RULE_PRICE", "Hero prices", "Цены героев");
            Loc.Add("HIGHCOMMAND_RULE_PRICE_DESC", "Multiplier on the price of recruiting and reviving heroes.",
                "Множитель цены найма и воскрешения героев.");
            Loc.Add("HIGHCOMMAND_RULE_ARMY", "Army size limit", "Предел размера армии");
            Loc.Add("HIGHCOMMAND_RULE_ARMY_DESC", "The most heroes the army can have, including the starting one.",
                "Наибольшее число героев в армии, включая стартового.");
            Loc.Add("HIGHCOMMAND_RULE_THREAT", "Difficulty per extra hero", "Сложность за каждого героя");
            Loc.Add("HIGHCOMMAND_RULE_THREAT_DESC", "How much each hero beyond the first counts as an extra player for the game's difficulty scaling. 1 = like a real co-op player.",
                "Сколько «игроков» добавляет каждый герой сверх первого при расчёте сложности. 1 = как живой игрок в кооперативе.");
            Loc.Add("HIGHCOMMAND_RULE_LOOT", "Item drop rate", "Частота выпадения предметов");
            Loc.Add("HIGHCOMMAND_RULE_LOOT_DESC", "Multiplier on the chance that an enemy drops an item.",
                "Множитель шанса, что враг выронит предмет.");
            Loc.Add("HIGHCOMMAND_RULE_SHARING", "Item sharing", "Раздача предметов");
            Loc.Add("HIGHCOMMAND_RULE_SHARING_DESC", "How the army's items are shared between the heroes.", "Как предметы армии делятся между героями.");
            Loc.Add("HIGHCOMMAND_RULE_SHARING_SPLIT", "Split", "Делить");
            Loc.Add("HIGHCOMMAND_RULE_SHARING_SPLIT_DESC", "Every item unit belongs to one hero, like in a normal co-op run.",
                "Каждая единица предмета принадлежит одному герою, как в обычном кооперативе.");
            Loc.Add("HIGHCOMMAND_RULE_SHARING_MIRROR", "Mirror", "Зеркало");
            Loc.Add("HIGHCOMMAND_RULE_SHARING_MIRROR_DESC", "Every hero gets a copy of every item (much stronger).",
                "Каждый герой получает копию каждого предмета (намного сильнее).");

            // command card
            Loc.Add("HIGHCOMMAND_CARD_NOSELECTION", "Nobody selected", "Никто не выбран");
            Loc.Add("HIGHCOMMAND_CARD_AUTO_ON", "Automatic use: on (the hero uses it by itself)", "Автоприменение: вкл. (герой применяет сам)");
            Loc.Add("HIGHCOMMAND_CARD_AUTO_OFF", "Automatic use: off (only on your command)", "Автоприменение: выкл. (только по вашей команде)");
            Loc.Add("HIGHCOMMAND_CARD_SKILL_KEYS", "{0} use it now    {1} automatic use on / off", "{0} применить    {1} автоприменение вкл. / выкл.");
            Loc.Add("HIGHCOMMAND_CMD_ATTACKMOVE", "Attack-move", "Атака с ходу");
            Loc.Add("HIGHCOMMAND_CMD_ATTACKMOVE_DESC", "Go to the clicked point and attack everything met on the way. A second click on the icon puts it away.",
                "Идти в указанную точку и атаковать всех по пути. Повторный клик по значку — отмена.");
            Loc.Add("HIGHCOMMAND_CMD_STOP", "Stop", "Стоп");
            Loc.Add("HIGHCOMMAND_CMD_STOP_DESC", "Cancel every order, queued ones too, and stand where you are.", "Отменить все приказы (и очередь) и встать на месте.");
            Loc.Add("HIGHCOMMAND_CMD_REVIVE", "Revive", "Воскресить");
            Loc.Add("HIGHCOMMAND_CMD_REVIVE_DESC", "Revive the first fallen hero for ${0}. A click on a fallen hero's card revives that one.",
                "Воскресить первого павшего героя за ${0}. Клик по карточке павшего воскрешает именно его.");
            Loc.Add("HIGHCOMMAND_CMD_TELEPORTER", "Teleporter", "Телепорт");
            Loc.Add("HIGHCOMMAND_CMD_TELEPORTER_DESC",
                "One command for the whole teleporter: the nearest hero activates it, the army gathers in the zone while it charges, and once it is charged and the boss is dead the army leaves the stage.",
                "Одна команда на весь телепорт: ближайший герой запускает его, армия собирается в зоне, пока он заряжается, а когда он заряжен и босс убит, армия уходит с этапа.");
            Loc.Add("HIGHCOMMAND_CMD_HOLD", "Hold", "Держать");
            Loc.Add("HIGHCOMMAND_CMD_HOLD_DESC", "Hold position: stand still and fight only what comes into range.",
                "Держать позицию: стоять и бить только тех, кто подошёл.");
            Loc.Add("HIGHCOMMAND_CMD_FOCUS", "Focus", "Фокус");
            Loc.Add("HIGHCOMMAND_CMD_FOCUS_DESC", "Focus fire: everybody attacks the clicked enemy and ignores the rest.",
                "Фокус-огонь: все бьют выбранного врага и игнорируют остальных.");
            Loc.Add("HIGHCOMMAND_CMD_RETREAT", "Retreat", "Отход");
            Loc.Add("HIGHCOMMAND_CMD_RETREAT_DESC", "Run to the rally point without fighting.", "Бежать к точке сбора, не вступая в бой.");
            Loc.Add("HIGHCOMMAND_CMD_PATROL", "Patrol", "Патруль");
            Loc.Add("HIGHCOMMAND_CMD_PATROL_DESC", "Patrol between the current place and the clicked point, fighting on the way.",
                "Патрулировать между текущим местом и указанной точкой, сражаясь по пути.");
            Loc.Add("HIGHCOMMAND_CMD_RALLY", "Rally", "Сбор");
            Loc.Add("HIGHCOMMAND_CMD_RALLY_DESC", "Set the rally point: where reinforcements land and where heroes retreat to.",
                "Задать точку сбора: туда приземляются новобранцы и туда отходят герои.");
            Loc.Add("HIGHCOMMAND_CMD_FORMATION", "Formation", "Строй");
            Loc.Add("HIGHCOMMAND_CMD_FORMATION_DESC", "Cycle the formation used by move orders: blob, line, column.",
                "Переключить построение для приказов движения: куча, линия, колонна.");
            Loc.Add("HIGHCOMMAND_CMD_SEEKENEMIES", "Seek enemies", "Искать врагов");
            Loc.Add("HIGHCOMMAND_CMD_SEEKENEMIES_DESC", "Go after the nearest enemy anywhere on the map, fight everything on the way, then take the next one. The order stays until you give another.",
                "Идти к ближайшему врагу на всей карте, сражаясь по пути, затем к следующему. Приказ действует, пока не дадите другой.");
            Loc.Add("HIGHCOMMAND_CMD_SEEKCHESTS", "Seek chests", "Искать сундуки");
            Loc.Add("HIGHCOMMAND_CMD_SEEKCHESTS_DESC", "Open every chest of the map the gold can pay for, nearest first; each hero takes its own one. Shops, shrines and printers are not touched.",
                "Открыть все сундуки карты, на которые хватает золота, сначала ближние; каждый герой идёт к своему. Магазины, святыни и принтеры не трогаются.");
            Loc.Add("HIGHCOMMAND_SEEK_ENEMIES_GO", "Seeking enemies across the map", "Ищем врагов по всей карте");
            Loc.Add("HIGHCOMMAND_SEEK_CHESTS_GO", "Seeking chests: {0} affordable", "Ищем сундуки: по карману {0}");
            Loc.Add("HIGHCOMMAND_SEEK_CHESTS_START", "Seeking chests", "Ищем сундуки");
            Loc.Add("HIGHCOMMAND_SEEK_CHESTS_NONE", "No chest left that the gold can pay for", "Нет сундуков, на которые хватает золота");
            Loc.Add("HIGHCOMMAND_HELP_SEC_CAMERA", "Camera", "Камера");
            Loc.Add("HIGHCOMMAND_HELP_SEC_SELECT", "Selection", "Выделение");
            Loc.Add("HIGHCOMMAND_HELP_SEC_GENERAL", "General", "Общее");
            Loc.Add("HIGHCOMMAND_HELP_SEC_ORDERS", "Orders", "Приказы");
            Loc.Add("HIGHCOMMAND_HELP_SEC_ARMY", "Army and commander", "Армия и командир");
            Loc.Add("HIGHCOMMAND_HELP_CLOSE", "F1 or Esc to close", "F1 или Esc — закрыть");
            Loc.Add("HIGHCOMMAND_HELP_SEEK","Seek enemies / chests across the map", "Искать врагов / сундуки по всей карте");
            Loc.Add("HIGHCOMMAND_CMD_STANCE_AGGRESSIVE", "Aggressive stance", "Агрессивная стойка");
            Loc.Add("HIGHCOMMAND_CMD_STANCE_AGGRESSIVE_DESC", "Chase every enemy that is seen.", "Преследовать каждого замеченного врага.");
            Loc.Add("HIGHCOMMAND_CMD_STANCE_DEFENSIVE", "Defensive stance", "Защитная стойка");
            Loc.Add("HIGHCOMMAND_CMD_STANCE_DEFENSIVE_DESC", "Fight near the post and return to it afterwards.", "Драться у своего поста и возвращаться к нему.");
            Loc.Add("HIGHCOMMAND_CMD_STANCE_PASSIVE", "Passive stance", "Пассивная стойка");
            Loc.Add("HIGHCOMMAND_CMD_STANCE_PASSIVE_DESC", "Never start a fight, defend only when hit.", "Не начинать бой, защищаться только когда бьют.");

            // the commander's own bar
            Loc.Add("HIGHCOMMAND_MINI_BARRACKS", "Barracks", "Казарма");
            Loc.Add("HIGHCOMMAND_MINI_ITEMS", "Items", "Предметы");
            Loc.Add("HIGHCOMMAND_MINI_HELP", "Hotkeys", "Горячие клавиши");
            Loc.Add("HIGHCOMMAND_ABILITY_0_NAME", "Orbital strike", "Удар с орбиты");
            Loc.Add("HIGHCOMMAND_ABILITY_0_DESC", "Three blasts at the clicked point after a short warning. Hits enemies only; the damage grows with the stage.",
                "Три взрыва в указанной точке после короткого предупреждения. Бьёт только врагов; урон растёт со стадией.");
            Loc.Add("HIGHCOMMAND_ABILITY_1_NAME", "Field medic", "Медик");
            Loc.Add("HIGHCOMMAND_ABILITY_1_DESC", "Heals every hero for 40% of their health and adds a small barrier.",
                "Лечит каждого героя на 40% здоровья и даёт небольшой барьер.");
            Loc.Add("HIGHCOMMAND_ABILITY_2_NAME", "War cry", "Боевой клич");
            Loc.Add("HIGHCOMMAND_ABILITY_2_DESC", "Every hero gets bonus attack and movement speed for 12 seconds.",
                "Каждый герой получает бонус к скорости атаки и движения на 12 секунд.");
            Loc.Add("HIGHCOMMAND_ABILITY_3_NAME", "Mark", "Метка");
            Loc.Add("HIGHCOMMAND_ABILITY_3_DESC", "Mark an enemy: it takes extra damage for 10 seconds and every hero attacks it.",
                "Пометить врага: он получает больше урона 10 секунд, и все герои атакуют его.");
            Loc.Add("HIGHCOMMAND_ABILITY_COOLDOWN", "Ability is recharging: {0} s", "Способность перезаряжается: {0} с");
            Loc.Add("HIGHCOMMAND_ABILITY_COOLDOWN_INFO", "Cooldown: {0} s", "Перезарядка: {0} с");
            Loc.Add("HIGHCOMMAND_ABILITY_NOHEROES", "No living heroes", "Нет живых героев");
            Loc.Add("HIGHCOMMAND_ABILITY_NOTARGET", "Click on an enemy", "Нажмите на врага");
            Loc.Add("HIGHCOMMAND_HINT_ABILITY_POINT", "Click a point for the strike (right click cancels)", "Нажмите точку для удара (правая кнопка - отмена)");
            Loc.Add("HIGHCOMMAND_HINT_ABILITY_TARGET", "Click an enemy to mark it (right click cancels)", "Нажмите на врага, чтобы пометить (правая кнопка - отмена)");
            Loc.Add("HIGHCOMMAND_HELP_ABILITIES", "Commander abilities", "Способности командира");

            // what a right click would do (the small badge next to the pointer) and the orders menu
            Loc.Add("HIGHCOMMAND_CURSOR_MOVE", "Move", "Идти");
            Loc.Add("HIGHCOMMAND_CURSOR_ATTACK", "Attack", "Атаковать");
            Loc.Add("HIGHCOMMAND_CURSOR_FOLLOW", "Follow", "Следовать");
            Loc.Add("HIGHCOMMAND_CURSOR_INTERACT", "Use", "Использовать");
            Loc.Add("HIGHCOMMAND_CURSOR_SELECT", "Select", "Выбрать");
            Loc.Add("HIGHCOMMAND_CURSOR_QUEUE", " (queued)", " (в очередь)");

            // objects on the map: prices, the hero who goes, why it came back
            Loc.Add("HIGHCOMMAND_PRICE_LUNAR", "{0} lunar", "{0} лун.");
            Loc.Add("HIGHCOMMAND_PRICE_HEALTH", "{0}% HP", "{0}% здоровья");
            Loc.Add("HIGHCOMMAND_PRICE_ITEM", "{0} item", "предмет x{0}");
            Loc.Add("HIGHCOMMAND_LOOT_NEEDGOLD", "Not enough gold: ${0}", "Не хватает золота: ${0}");
            Loc.Add("HIGHCOMMAND_LOOT_CANNOTPAY", "The army cannot pay for this", "Армии нечем за это заплатить");
            Loc.Add("HIGHCOMMAND_LOOT_GOING", "{0} — {1}", "{0} — {1}");
            Loc.Add("HIGHCOMMAND_LOOT_FAILED_GOLD", "{0}: not enough gold (${1})", "{0}: не хватило золота (${1})");
            Loc.Add("HIGHCOMMAND_LOOT_FAILED_PRICE", "{0} has nothing to pay with", "{0}: нечем заплатить");
            Loc.Add("HIGHCOMMAND_LOOT_FAILED_REACH", "{0} cannot get there", "{0}: не может туда дойти");

            // the purse, the army's items, the objectives
            Loc.Add("HIGHCOMMAND_PURSE_TITLE", "The army's purse", "Казна армии");
            Loc.Add("HIGHCOMMAND_PURSE_TIP", "<style=cIsHealing>+{0}</style> gold a minute.\nNext hero: ${1} (the army has {2} of {3}).\nA revival: ${4}.",
                "<style=cIsHealing>+{0}</style> золота в минуту.\nСледующий герой: ${1} (в армии {2} из {3}).\nВоскрешение: ${4}.");
            Loc.Add("HIGHCOMMAND_PURSE_HIRE", "hire <color={0}>${1}</color>", "найм <color={0}>${1}</color>");
            Loc.Add("HIGHCOMMAND_PURSE_FULL", "army full", "армия полна");
            Loc.Add("HIGHCOMMAND_TOP_ITEMS", "Items", "Предметы");
            Loc.Add("HIGHCOMMAND_ITEMS_ARMY_COUNT", "The army has {0}.", "У армии: {0}.");
            Loc.Add("HIGHCOMMAND_ITEMS_OPEN", "who carries what", "у кого что");
            Loc.Add("HIGHCOMMAND_OBJ_TP_ACTIVATE", "activate", "запустить");
            Loc.Add("HIGHCOMMAND_OBJ_TP_GATHER", "gather", "собрать");
            Loc.Add("HIGHCOMMAND_OBJ_TP_LEAVE", "leave the stage", "уйти с этапа");
            Loc.Add("HIGHCOMMAND_OBJ_IN_ZONE", "in the zone {0}/{1}", "в зоне {0}/{1}");
            Loc.Add("HIGHCOMMAND_OBJ_CHESTS", "Open the chests ({0}/{1})", "Откройте сундуки ({0}/{1})");
            Loc.Add("HIGHCOMMAND_OBJ_CHESTS_KEY", "seek", "искать");

            // revival and teleporter
            Loc.Add("HIGHCOMMAND_REVIVE_NONE", "Nobody has fallen", "Никто не пал");
            Loc.Add("HIGHCOMMAND_REVIVE_ALIVE", "The hero is alive", "Герой жив");
            Loc.Add("HIGHCOMMAND_REVIVED", "Revived: {0}", "Воскрешён: {0}");
            Loc.Add("HIGHCOMMAND_ALERT_DIED", "{0} has fallen", "{0} погиб");
            Loc.Add("HIGHCOMMAND_ALERT_LOWHP", "{0} is almost dead", "{0} на грани гибели");
            Loc.Add("HIGHCOMMAND_HELP_ALERT", "Fly to the last alert", "Перелёт к последнему оповещению");
            Loc.Add("HIGHCOMMAND_HELP_REVIVE", "Revive the first fallen hero", "Воскресить первого павшего героя");
            Loc.Add("HIGHCOMMAND_HELP_TELEPORTER", "Teleporter: activate / gather / leave", "Телепорт: запустить / собраться / уйти");
            Loc.Add("HIGHCOMMAND_TP_NONE", "No teleporter on this stage", "На этой стадии нет телепорта");
            Loc.Add("HIGHCOMMAND_TP_NOHEROES", "No living heroes", "Нет живых героев");
            Loc.Add("HIGHCOMMAND_TP_ACTIVATING", "The nearest hero activates the teleporter", "Ближайший герой запускает телепорт");
            Loc.Add("HIGHCOMMAND_TP_LEAVING", "The nearest hero goes to leave the stage", "Ближайший герой идёт, чтобы покинуть стадию");
            Loc.Add("HIGHCOMMAND_TP_GATHER", "The army gathers at the teleporter", "Армия собирается у телепорта");
            Loc.Add("HIGHCOMMAND_TP_BUSY", "The teleporter is busy", "Телепорт занят");
            Loc.Add("HIGHCOMMAND_DEFEAT_CAN_REVIVE", "All heroes have fallen! Revive one within {0} s ({1}) or the run is lost",
                "Все герои пали! Воскресите одного за {0} с ({1}), иначе забег проигран");
            Loc.Add("HIGHCOMMAND_DEFEAT_AUTO_REVIVE", "All heroes have fallen! Revival in {0} s for ${1}, or now: {2}",
                "Все герои пали! Воскрешение через {0} с за ${1}, или сразу: {2}");
            Loc.Add("HIGHCOMMAND_DEFEAT_BROKE", "All heroes have fallen and there is no gold to revive: the run ends in {0} s",
                "Все герои пали, а золота на воскрешение нет: забег закончится через {0} с");
            Loc.Add("HIGHCOMMAND_DEFEAT_TIP_TITLE", "Revival", "Воскрешение");
            Loc.Add("HIGHCOMMAND_DEFEAT_TIP", "A click revives the first fallen hero for ${0}.", "Клик воскрешает первого павшего героя за ${0}.");

            // roster
            Loc.Add("HIGHCOMMAND_ROSTER_REVIVE", "revive <color={0}>${1}</color>", "воскресить <color={0}>${1}</color>");
            Loc.Add("HIGHCOMMAND_ROSTER_FALLEN", "Fallen. A click revives them for ${0}.", "Пал. Клик воскрешает за ${0}.");
            Loc.Add("HIGHCOMMAND_ROSTER_HINT", "Click: select. Shift+click: add. Double click: fly the camera there.",
                "Клик: выбрать. Shift+клик: добавить. Двойной клик: камера к герою.");
            Loc.Add("HIGHCOMMAND_ROSTER_STANCE", "Stance:", "Стойка:");
            Loc.Add("HIGHCOMMAND_ROSTER_ORDER", "Order:", "Приказ:");
            Loc.Add("HIGHCOMMAND_ORDER_MOVE", "moving", "идёт");
            Loc.Add("HIGHCOMMAND_ORDER_ATTACKMOVE", "attack-move", "атака-движение");
            Loc.Add("HIGHCOMMAND_ORDER_ATTACK", "attacking", "атакует");
            Loc.Add("HIGHCOMMAND_ORDER_HOLD", "holding", "держит");
            Loc.Add("HIGHCOMMAND_ORDER_FOLLOW", "following", "следует");
            Loc.Add("HIGHCOMMAND_ORDER_RETREAT", "retreating", "отходит");
            Loc.Add("HIGHCOMMAND_ORDER_PATROL", "patrolling", "патрулирует");
            Loc.Add("HIGHCOMMAND_ORDER_INTERACT", "using an object", "использует объект");
            Loc.Add("HIGHCOMMAND_ORDER_CAST", "casting", "применяет умение");
            Loc.Add("HIGHCOMMAND_ORDER_SEEKENEMIES", "seeking enemies", "ищет врагов");
            Loc.Add("HIGHCOMMAND_ORDER_SEEKCHESTS", "seeking chests", "ищет сундуки");

            // inventory
            Loc.Add("HIGHCOMMAND_HELP_INVENTORY", "Items: who carries what, drag to give", "Предметы: у кого что, перетащите, чтобы передать");
            Loc.Add("HIGHCOMMAND_INV_TITLE", "Army items ({0} total)", "Предметы армии (всего {0})");
            Loc.Add("HIGHCOMMAND_INV_HINT_SPLIT", "Drag an item onto another hero to give it; Shift: the whole stack",
                "Перетащите предмет на другого героя, чтобы отдать; Shift — вся стопка");
            Loc.Add("HIGHCOMMAND_INV_HINT_MIRROR", "Mirror: every hero has every item, nothing to hand over",
                "Зеркало: у каждого героя есть все предметы, передавать нечего");
            Loc.Add("HIGHCOMMAND_INV_AUTO", "Auto-distribute", "Раздать заново");
            Loc.Add("HIGHCOMMAND_INV_AUTO_DONE", "Items redistributed by role", "Предметы розданы заново по ролям");
            Loc.Add("HIGHCOMMAND_INV_EMPTY", "no items yet", "предметов пока нет");
            Loc.Add("HIGHCOMMAND_INV_MORE", "+{0} more", "ещё +{0}");
            Loc.Add("HIGHCOMMAND_INV_EQUIPMENT", "Equipment", "Снаряжение");
            Loc.Add("HIGHCOMMAND_INV_SPARE", "Spare equipment (drag onto a hero):", "Запасное снаряжение (перетащите на героя):");
            Loc.Add("HIGHCOMMAND_INV_NOEQUIP", "no equipment", "без снаряжения");
            Loc.Add("HIGHCOMMAND_INV_MOVED", "{0} -> {1}: {2}", "{0} -> {1}: {2}");
            Loc.Add("HIGHCOMMAND_INV_COUNT", "Owned: {0}", "В наличии: {0}");
            Loc.Add("HIGHCOMMAND_INV_DEAD", "fallen", "пал");
        }
    }
}
