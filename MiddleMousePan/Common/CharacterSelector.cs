using Kingmaker;
using Kingmaker.Controllers.TurnBased;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.Common;
using Kingmaker.UI.Selection;
using Owlcat.Runtime.Core;
using System.Collections.Generic;
using System.Linq;

namespace MiddleMousePan.Common;

public static class CharacterSelector
{

    public static void SelectNextCharacter()
    {
        SelectShiftedCharacter(1);
    }

    public static void SelectPrevCharacter()
    {
        SelectShiftedCharacter(-1);
    }

    private static void SelectShiftedCharacter(int shift)
    {
        if (SelectionManagerBase.Instance == null) return;
        if (TurnController.IsInTurnBasedCombat() && !Game.Instance.TurnController.IsPreparationTurn) return;
        var curUnit = Game.Instance.SelectionCharacter.SelectedUnit;
        List<BaseUnitEntity> actualGroup = GetSelectableUnits(Game.Instance.SelectionCharacter.ActualGroup).ToList();
        if (actualGroup.Empty()) return;
        int num = (actualGroup.IndexOf(curUnit.Value) + shift) % actualGroup.Count;
        if (num < 0)
        {
            num += actualGroup.Count;
        }
        SelectionManagerBase.Instance.SelectUnit(actualGroup[num].View, true, true, true);
    }


    private static IEnumerable<BaseUnitEntity> GetSelectableUnits(IEnumerable<BaseUnitEntity> units)
    {
        if (TurnController.IsInTurnBasedCombat() && !Game.Instance.TurnController.IsPreparationTurn)
        {
            return Enumerable.Empty<BaseUnitEntity>();
        }

        if (Game.Instance.CurrentlyLoadedArea.IsShipArea)
        {
            units = units.Where((BaseUnitEntity u) => u.IsMainCharacter);
        }

        return units.Where((BaseUnitEntity u) => u.IsInGame && u.IsDirectlyControllable());
    }
}
