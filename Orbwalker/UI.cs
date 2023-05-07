﻿using Dalamud.Interface.Components;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game;
using Orbwalker;
using PInvoke;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Unmoveable
{
    internal unsafe static class UI
    {
        internal static void Draw()
        {
            ImGuiEx.EzTabBar("Default",
                ("Settings", Settings, null, true),
                ("Debug", Debug, ImGuiColors.DalamudGrey3, true),
                InternalLog.ImGuiTab()

                );
        }

        static void Spacing(bool cont = false)
        {
            ImGuiEx.TextV($" {(cont? "├": "└")} ");
            ImGui.SameLine();
        }

        static void Settings()
        {
            var cur = ImGui.GetCursorPos();
            if(ThreadLoadImageHandler.TryGetTextureWrap(Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName, "res", "q.png"), out var t))
            {
                ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - 20);
                ImGui.Image(t.ImGuiHandle, new(20, 20));
                if (ImGui.IsItemHovered())
                {
                    ImGui.BeginTooltip();
                    if (ThreadLoadImageHandler.TryGetTextureWrap(Path.Combine(Svc.PluginInterface.AssemblyLocation.DirectoryName, "res", "t.png"), out var t2))
                    {
                        ImGui.Image(t2.ImGuiHandle, new Vector2(t2.Width, t2.Height));
                    }
                    ImGui.EndTooltip();
                }
            }
            ImGui.SetCursorPos(cur);
            
            if(ImGui.Checkbox($"Enable Orbwalker", ref P.Config.Enabled))
            {
                P.Memory.EnableDisableBuffer();
            }
            ImGuiEx.Text($"Movement");
            ImGuiGroup.BeginGroupBox();
            ImGuiEx.Text($"Slidecast Window Calibration:");
            ImGuiComponents.HelpMarker("Switches between automatic slidecast window calibration or allows you to set a manual value. Automatic mode is fully reliable but will always result in smaller slidecast windows than you can manually configure based on spellspeed/network latency.");
            Spacing(!P.Config.IsSlideAuto);
            ImGuiEx.RadioButtonBool("Automatic", "Manual", ref P.Config.IsSlideAuto, true);
            if (!P.Config.IsSlideAuto)
            {
                Spacing();
                ImGui.SetNextItemWidth(200f);
                ImGui.SliderFloat("Unlock at, s", ref P.Config.Threshold, 0.1f, 1f);
            }
            ImGuiEx.Text($"Orbwalking Mode:");
            ImGuiComponents.HelpMarker("Switch between the two modes. \"Slidecast\" mode is the default and simply prevents player movement until the slidecast window is available, locking movement again to begin the next cast. You must be stationary for the first cast in most cases. \"Slidelock\" mode on the otherhand permanently locks the player from moving while in combat and only allows for movement during the slidecast window. The movement release key is the only way to enable movement when this mode is used.");
            Spacing(); 
            if (ImGui.RadioButton("Slidecast", !P.Config.ForceStopMoveCombat))
            {
                P.Config.ForceStopMoveCombat = false;
            }
            ImGui.SameLine();
            if (ImGui.RadioButton("Slidelock", P.Config.ForceStopMoveCombat))
            {
                P.Config.ForceStopMoveCombat = true;
            }
            ImGui.SetNextItemWidth(200f);
            DrawKeybind("Movement Release Key", ref P.Config.ReleaseKey);
            ImGuiComponents.HelpMarker("Bind a key to instantly unlock player movement and cancel any channeling cast. Note that movement is only enabled whilst the key is held, therefore a mouse button is recommended.");
            ImGui.Checkbox($"Permanently Release", ref P.Config.UnlockPermanently);
            ImGuiComponents.HelpMarker("Releases player movement - used primarily by the release key setting above.");
            ImGuiEx.Text($"Release Key Mode:");
            ImGuiComponents.HelpMarker("Switches the movement release key from needing to be held, to becoming a toggle.");
            Spacing(); 
            ImGuiEx.RadioButtonBool("Hold", "Toggle", ref P.Config.IsHoldToRelease, true);

            if (ImGui.Checkbox($"Buffer Initial Cast (BETA)", ref P.Config.Buffer))
            {
                P.Memory.EnableDisableBuffer();
            }
            ImGuiComponents.HelpMarker($"Removes the requirement for the player to be stationary when channeling the first cast by buffering it until movement is halted. This setting may cause strange behavior with plugins such as Redirect or ReAction, or prevent their options from working at all, be warned!");

            ImGui.Checkbox($"Enable Mouse Button Release", ref P.Config.DisableMouseDisabling);
            ImGuiComponents.HelpMarker("Allows emergency movement via holding down MB1 and MB2 simultaneously.");
            ImGuiEx.TextV($"Movement keys:");
            ImGui.SameLine();
            ImGuiEx.SetNextItemWidth(0.8f);
            if (ImGui.BeginCombo($"##movekeys", $"{P.Config.MoveKeys.Print()}"))
            {
                foreach (var x in Svc.KeyState.GetValidVirtualKeys())
                {
                    ImGuiEx.CollectionCheckbox($"{x}", x, P.Config.MoveKeys);
                }
                ImGui.EndCombo();
            }

            ImGuiGroup.EndGroupBox();

            ImGuiEx.Text($"Overlay");
            ImGuiGroup.BeginGroupBox();

            ImGuiEx.Text($"Display Overlay");
            ImGuiComponents.HelpMarker("Choose when to display the Orbwalker overlay when enabled.");
            Spacing(true); ImGui.Checkbox($"In Combat", ref P.Config.DisplayBattle);
            Spacing(true); ImGui.Checkbox($"In Duty", ref P.Config.DisplayDuty);
            Spacing(true); ImGui.Checkbox($"Always", ref P.Config.DisplayAlways);
            Spacing();
            ImGui.SetNextItemWidth(100f);
            ImGui.SliderFloat($"Overlay scale", ref P.Config.SizeMod.ValidateRange(0.5f, 2f), 0.8f, 1.2f);

            ImGuiGroup.EndGroupBox();
        }

        static void Debug()
        {
            ImGui.InputInt($"forceDisableMovementPtr", ref P.Memory.ForceDisableMovement);
            if (Svc.Targets.Target != null)
            {
                var addInfo = stackalloc uint[1];
                ImGuiEx.Text($"{ActionManager.Instance()->GetActionStatus(ActionType.Spell, 16541, Svc.Targets.Target.Struct()->GetObjectID(), outOptExtraInfo: addInfo)} / {*addInfo}");
            }
        }

        static string KeyInputActive = null;
        static bool DrawKeybind(string text, ref Keys key)
        {
            bool ret = false;
            ImGui.PushID(text);
            ImGuiEx.Text($"{text}:");
            ImGui.Dummy(new(20, 1));
            ImGui.SameLine();
            ImGui.SetNextItemWidth(200f);
            if (ImGui.BeginCombo("##inputKey", $"{key}"))
            {
                if (text == KeyInputActive)
                {
                    ImGuiEx.Text(ImGuiColors.DalamudYellow, $"Now press new key...");
                    foreach (var x in Enum.GetValues<Keys>())
                    {
                        if (IsKeyPressed(x))
                        {
                            KeyInputActive = null;
                            key = x;
                            ret = true;
                            break;
                        }
                    }
                }
                else
                {
                    if (ImGui.Selectable("Auto-detect new key", false, ImGuiSelectableFlags.DontClosePopups))
                    {
                        KeyInputActive = text;
                    }
                    ImGuiEx.Text($"Select key manually:");
                    ImGuiEx.SetNextItemFullWidth();
                    ImGuiEx.EnumCombo("##selkeyman", ref key);
                }
                ImGui.EndCombo();
            }
            else
            {
                if (text == KeyInputActive)
                {
                    KeyInputActive = null;
                }
            }
            if (key != Keys.None)
            {
                ImGui.SameLine();
                if (ImGuiEx.IconButton(FontAwesomeIcon.Trash))
                {
                    key = Keys.None;
                    ret = true;
                }
            }
            ImGui.PopID();
            return ret;
        }
    }
}