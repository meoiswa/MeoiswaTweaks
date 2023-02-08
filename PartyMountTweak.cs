using SimpleTweaksPlugin.TweakSystem;

using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using System.Collections.Generic;

using SimpleTweaksPlugin;
using Dalamud.Game.ClientState.Conditions;
using Lumina.Excel.GeneratedSheets;
using System.Linq;
using System;
using FFXIVClientStructs.FFXIV.Client.Game.Group;

namespace MeoiswaTweaks;
public class PartyMountTweak : Tweak
{
  public override bool Experimental => true;
  public override string Name => "Automatic Multi-Seater Roulette";
  public override string Description => "Mount Roulette picks a random multi-seater when in a party";
  public unsafe delegate byte UseActionHandler(ActionManager* actionManager, ActionType actionType, uint actionID, long targetID = 3758096384U, uint a4 = 0U, uint a5 = 0U, uint a6 = 0U, void* a7 = default);

  private readonly Hook<UseActionHandler>? UseActionHook;

  private readonly IEnumerable<Mount> PartyMounts;
  private readonly Dalamud.Game.ClientState.Conditions.Condition Condition;
  private readonly Random Rng;

  public unsafe PartyMountTweak()
  {
    Rng = new Random();
    Condition = Service.Condition;

    var actionManager = ActionManager.Instance();

    PartyMounts = Service.Data.GetExcelSheet<Mount>()!
      .Where(mount =>
        mount.UIPriority > 0
        && mount.Icon != 0
        && mount.ExtraSeats > 0);

    var renderAddress = (nint)ActionManager.Addresses.UseAction.Value;

    if (renderAddress != 0)
    {
      UseActionHook = Hook<UseActionHandler>.FromAddress(renderAddress, OnUseAction);
    }
  }

  public override void Enable()
  {
    base.Enable();
    if (UseActionHook != null)
    {
      UseActionHook!.Enable();
    }
  }

  public override void Disable()
  {
    base.Disable();
    if (UseActionHook != null)
    {
      UseActionHook!.Disable();
    }
  }

  private ActionType previousActionType;
  private uint previousActionID;
  private unsafe byte OnUseAction(ActionManager* actionManager, ActionType actionType, uint actionID, long targetID, uint a4, uint a5, uint a6, void* a7)
  {
    if (Condition[ConditionFlag.Mounted] || Condition[ConditionFlag.Mounted2])
    {
      return UseActionHook!.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);
    }

    if (previousActionType == ActionType.General && previousActionID is 9 or 24)
    {
      if (GroupManager.Instance()->MemberCount > 0)
      {
        if (actionType is ActionType.Mount)
        {
          var available = PartyMounts
            .Where(mount => actionManager->GetActionStatus(ActionType.Mount, mount.RowId) == 0)
            .ToList();

          var mount = available.ElementAt(Rng.Next(1, available.Count));
          actionID = mount.RowId;
        }
      }
    }

    previousActionID = actionID;
    previousActionType = actionType;
    var result = UseActionHook!.Original(actionManager, actionType, actionID, targetID, a4, a5, a6, a7);

    return result;
  }
}
