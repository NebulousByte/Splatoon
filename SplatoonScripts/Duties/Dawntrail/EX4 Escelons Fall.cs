﻿using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons;
using ECommons.Configuration;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using ECommons.Hooks;
using ECommons.Hooks.ActionEffectTypes;
using ECommons.ImGuiMethods;
using ECommons.Logging;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using ImGuiNET;
using Splatoon.SplatoonScripting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace SplatoonScriptsOfficial.Duties.Dawntrail;
public unsafe class EX4_Escelons_Fall : SplatoonScript
{
    public override HashSet<uint>? ValidTerritories { get; } = [1271];

    public override Metadata? Metadata => new(4, "NightmareXIV, Redmoonwow");

    uint StatusCloseFar = 2970;
    uint StatusParamClose = 758;
    uint StatusParamFar = 759;
    uint[] CastSwitcher = [43182, 43181];
    uint CastStandard = 43181;
    uint RoseBloom3rd = 43541;
    uint NpcNameId = 13861;//Name NPC ID: 13861
    int NumSwitches = 0;
    long ForceResetAt = long.MaxValue;
    List<bool> SequenceIsClose = [];
    bool AdjustPhase = false;
    bool THShockTargeted = false;

    IBattleNpc? Zelenia => Svc.Objects.OfType<IBattleNpc>().FirstOrDefault(x => x.NameId == this.NpcNameId && x.IsTargetable);

    public override void OnSetup()
    {
        Controller.RegisterElementFromCode("Out", "{\"Name\":\"Out\",\"type\":1,\"Enabled\":false,\"radius\":6.0,\"fillIntensity\":0.25,\"originFillColor\":1677721855,\"endFillColor\":1677721855,\"refActorNPCNameID\":13861,\"refActorComparisonType\":6,\"onlyTargetable\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0,\"refActorTetherConnectedWithPlayer\":[]}");
        Controller.RegisterElementFromCode("In", "{\"Name\":\"In\",\"type\":1,\"Enabled\":false,\"radius\":6.0,\"Donut\":20.0,\"fillIntensity\":0.25,\"originFillColor\":1677721855,\"endFillColor\":1677721855,\"refActorNPCNameID\":13861,\"refActorComparisonType\":6,\"onlyTargetable\":true,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0,\"refActorTetherConnectedWithPlayer\":[]}");

        Controller.RegisterElementFromCode("InIncorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":4278190335,\"thicc\":5.0,\"overlayText\":\">>> IN <<<\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("InCorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"color\":3355508480,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":3355508480,\"thicc\":5.0,\"overlayText\":\"> IN <\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("OutIncorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"color\":3372155135,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":3372155135,\"thicc\":5.0,\"overlayText\":\"<<< OUT >>>\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
        Controller.RegisterElementFromCode("OutCorrect", "{\"Name\":\"\",\"type\":1,\"radius\":1.0,\"color\":3355508480,\"Filled\":false,\"fillIntensity\":0.5,\"overlayTextColor\":3355508480,\"thicc\":5.0,\"overlayText\":\"< OUT >\",\"refActorType\":1,\"refActorTetherTimeMin\":0.0,\"refActorTetherTimeMax\":0.0}");
    }

    public override void OnSettingsDraw()
    {
        ImGuiEx.Text($"My bait if close first:");
        ImGuiEx.HelpMarker($"If close attack is first, which hit you take");
        ImGuiEx.RadioButtonBool("First (start in)", "Second (start out)", ref C.TakeFirstIfClose);
        ImGuiEx.Text($"My bait if far first:");
        ImGuiEx.HelpMarker($"If far attack is first, which hit you take");
        ImGuiEx.RadioButtonBool("First (start out)##2", "Second (start in)##2", ref C.TakeFirstIfFar);
        ImGui.Separator();
        ImGui.SetNextItemWidth(150f);
        ImGuiEx.SliderInt("Delay, ms", ref C.Delay, 0, 1000);
        ImGuiEx.HelpMarker("Delay helps to synchronize script with attack animation. If you want to see safe movement ASAP, set it to 0.");
        if(ImGui.CollapsingHeader("Debug"))
        {
            ImGuiEx.Text($"AdjustPhase: {AdjustPhase}");
            ImGuiEx.Text($"THShockTargeted: {THShockTargeted}");
            ImGuiEx.Text($"SequenceIsClose: {SequenceIsClose.Print()}");
            ImGuiEx.Text($"GetMyCloses: {GetMyCloses().Print()}");
            ImGuiEx.Text($"IsSelfClose: {IsSelfClose()}");
            ImGuiEx.Text($"NumSwitches: {NumSwitches}");
            ImGuiEx.Text($"ForceResetAt: {ForceResetAt}");
            ImGuiEx.Text($"{Svc.Objects.OfType<IPlayerCharacter>().OrderBy(x => Vector3.Distance(x.Position, Zelenia.Position)).Print("\n")}");
        }
    }

    public override void OnGainBuffEffect(uint sourceId, Status status)
    {
        if(sourceId == Zelenia?.EntityId && status.StatusId == this.StatusCloseFar)
        {
            //ForceResetAt = Environment.TickCount64 + 30000;
            SequenceIsClose.Add(this.StatusParamClose == status.Param);
            PluginLog.Debug($"Registered: {(SequenceIsClose.Last() ? "Close" : "Far")}");
        }
    }

    float GetRadius(bool isIn)
    {
        var z = Zelenia;
        if(z == null) return 5f;
        var breakpoint = Svc.Objects.OfType<IPlayerCharacter>().OrderBy(x => Vector2.Distance(x.Position.ToVector2(), z.Position.ToVector2())).ToList().SafeSelect(isIn?4:3);
        if(breakpoint == null) return 5f;
        var distance = Vector2.Distance(z.Position.ToVector2(), breakpoint.Position.ToVector2());
        //distance += isIn ? -0.5f : 0.5f;
        return Math.Max(0.5f, distance);
    }

    List<bool> GetMyCloses()
    {
        var myCloseFirst = this.SequenceIsClose[0] ? C.TakeFirstIfClose : C.TakeFirstIfFar;
        if(this.AdjustPhase)
        {
            if(THShockTargeted)
            {
                if(Player.Job is Job.DRK or Job.WAR or Job.GNB or Job.PLD or Job.WHM or Job.AST or Job.SCH or Job.SGE)
                {
                    myCloseFirst = false;
                }
                else
                {
                    myCloseFirst = true;
                }
            }
            else
            {
                if(Player.Job is Job.DRK or Job.WAR or Job.GNB or Job.PLD or Job.WHM or Job.AST or Job.SCH or Job.SGE)
                {
                    myCloseFirst = true;
                }
                else
                {
                    myCloseFirst = false;
                }
            }
        }
        List<bool> seq = [SequenceIsClose.SafeSelect(0) == myCloseFirst, SequenceIsClose.SafeSelect(1) != myCloseFirst, SequenceIsClose.SafeSelect(2) == myCloseFirst, SequenceIsClose.SafeSelect(3) != myCloseFirst];
        return seq;
    }

    bool IsSelfClose()
    {
        if(Zelenia == null) return false;
        return Svc.Objects.OfType<IPlayerCharacter>().OrderBy(x => Vector2.Distance(x.Position.ToVector2(), Zelenia.Position.ToVector2())).Take(4).Any(x => x.AddressEquals(Player.Object));
    }

    public override void OnUpdate()
    {
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
        if(Environment.TickCount64 > ForceResetAt || NumSwitches >= 4)
        {
            ForceResetAt = long.MaxValue;
            Reset();
            return;
        }

        if(this.SequenceIsClose.Count >= 1)
        {
            var isMyClose = GetMyCloses()[this.NumSwitches];
            var correct = IsSelfClose() == isMyClose;
            var e = Controller.GetElementByName(isMyClose ? $"In" : $"Out")!;
            e.Enabled = true;
            e.radius = GetRadius(isMyClose);
            Controller.GetElementByName(isMyClose ? $"In{(correct ? "Correct" : "Incorrect")}" : $"Out{(correct ? "Correct" : "Incorrect")}")!.Enabled = true;
        }
    }

    public override void OnActionEffectEvent(ActionEffectSet set)
    {
        if(set.Action == null) return;
        if(set.Action.Value.RowId.EqualsAny(this.CastSwitcher))
        {
            PluginLog.Information($"Switch");
            if(C.Delay > 0)
            {
                this.Controller.Schedule(() => NumSwitches++, C.Delay);
            }
            else
            {
                NumSwitches++;
            }
        }
    }

    public override void OnDirectorUpdate(DirectorUpdateCategory category)
    {
        if(category.EqualsAny(DirectorUpdateCategory.Complete, DirectorUpdateCategory.Recommence, DirectorUpdateCategory.Wipe)) Reset();
    }

    public override void OnStartingCast(uint source, uint castId)
    {
        if(castId == this.RoseBloom3rd)
        {
            PluginLog.Information($"Next Escelons Need Adjust");
            AdjustPhase = true;
        }
    }

    public override void OnVFXSpawn(uint target, string vfxPath)
    {
        if(AdjustPhase && vfxPath.Contains("vfx/lockon/eff/x6fd_shock_lock2v.avfx"))
        {
            if(target.TryGetObject(out var obj) && obj is IPlayerCharacter pc && !THShockTargeted)
            {
                if(pc.GetJob() is Job.DRK or Job.WAR or Job.GNB or Job.PLD or Job.WHM or Job.AST or Job.SCH or Job.SGE)
                {
                    PluginLog.Information($"TH Shock Targeted: {pc.Name.ToString()}");
                    THShockTargeted = true;
                }
            }
        }
    }

    void Reset()
    {
        this.SequenceIsClose.Clear();
        NumSwitches = 0;
        Controller.GetRegisteredElements().Each(x => x.Value.Enabled = false);
        AdjustPhase = false;
        THShockTargeted = false;
    }

    Config C => Controller.GetConfig<Config>();
    public class Config : IEzConfig
    {
        public bool TakeFirstIfClose = false;
        public bool TakeFirstIfFar = false;
        public int Delay = 800;
    }
}
