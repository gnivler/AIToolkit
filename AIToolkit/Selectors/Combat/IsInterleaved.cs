﻿using BattleTech;

namespace AIToolkit.Selectors.Combat
{
    public class IsInterleaved : Selector<CombatGameState>
    {
        public override bool Select(string selectString, CombatGameState combat)
        {
            var isTrue = combat.TurnDirector.IsInterleaved;
            switch (selectString)
            {
                case "true":
                    return isTrue;
                case "false":
                    return !isTrue;
                default:
                    return false;
            }
        }
    }
}
