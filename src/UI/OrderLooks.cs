using HighCommand.Orders;

namespace HighCommand.UI
{
    /// <summary>The name and picture of each order kind, as the roster and the command card show them.</summary>
    internal static class OrderLooks
    {
        internal static string Name(OrderKind kind)
        {
            switch (kind)
            {
                case OrderKind.Move: return Loc.Get("HIGHCOMMAND_ORDER_MOVE");
                case OrderKind.AttackMove: return Loc.Get("HIGHCOMMAND_ORDER_ATTACKMOVE");
                case OrderKind.Attack: return Loc.Get("HIGHCOMMAND_ORDER_ATTACK");
                case OrderKind.Hold: return Loc.Get("HIGHCOMMAND_ORDER_HOLD");
                case OrderKind.Follow: return Loc.Get("HIGHCOMMAND_ORDER_FOLLOW");
                case OrderKind.Retreat: return Loc.Get("HIGHCOMMAND_ORDER_RETREAT");
                case OrderKind.Patrol: return Loc.Get("HIGHCOMMAND_ORDER_PATROL");
                case OrderKind.Interact: return Loc.Get("HIGHCOMMAND_ORDER_INTERACT");
                case OrderKind.Cast: return Loc.Get("HIGHCOMMAND_ORDER_CAST");
                case OrderKind.SeekEnemies: return Loc.Get("HIGHCOMMAND_ORDER_SEEKENEMIES");
                case OrderKind.SeekChests: return Loc.Get("HIGHCOMMAND_ORDER_SEEKCHESTS");
                default: return "";
            }
        }

        /// <summary>The picture of an order; false for "no order" (nothing is drawn).</summary>
        internal static bool Glyph(OrderKind kind, out Glyph glyph)
        {
            switch (kind)
            {
                case OrderKind.Move: glyph = UI.Glyph.Move; return true;
                case OrderKind.AttackMove: glyph = UI.Glyph.AttackMove; return true;
                case OrderKind.Attack: glyph = UI.Glyph.Attack; return true;
                case OrderKind.Hold: glyph = UI.Glyph.Hold; return true;
                case OrderKind.Follow: glyph = UI.Glyph.Follow; return true;
                case OrderKind.Retreat: glyph = UI.Glyph.Retreat; return true;
                case OrderKind.Patrol: glyph = UI.Glyph.Patrol; return true;
                case OrderKind.Interact: glyph = UI.Glyph.Interact; return true;
                case OrderKind.Cast: glyph = UI.Glyph.Focus; return true;
                case OrderKind.SeekEnemies: glyph = UI.Glyph.SeekEnemies; return true;
                case OrderKind.SeekChests: glyph = UI.Glyph.SeekChests; return true;
                default: glyph = UI.Glyph.Stop; return false;
            }
        }

        internal static string StanceName(Stance stance) => Loc.Get("HIGHCOMMAND_STANCE_" + StanceKey(stance));

        private static string StanceKey(Stance stance)
        {
            switch (stance)
            {
                case Stance.Defensive: return "DEFENSIVE";
                case Stance.Passive: return "PASSIVE";
                default: return "AGGRESSIVE";
            }
        }
    }
}
