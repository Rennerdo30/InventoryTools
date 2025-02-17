﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using CriticalCommonLib.Models;
using CriticalCommonLib.Services;
using Dalamud.Interface.Colors;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using InventoryTools.Logic;

namespace InventoryTools
{
    public partial class InventoryToolsUi
    {
        private unsafe void DrawHelpTab()
        {

            if (ImGui.BeginChild("###ivHelpList", new Vector2(150, -1) * ImGui.GetIO().FontGlobalScale, true))
            {
                if (ImGui.Selectable("General", _configuration.SelectedHelpPage == 0))
                {
                    _configuration.SelectedHelpPage = 0;
                }
                if (ImGui.Selectable("Filtering", _configuration.SelectedHelpPage == 1))
                {
                    _configuration.SelectedHelpPage = 1;
                }
                ImGui.EndChild();
            }

            ImGui.SameLine();

            if (ImGui.BeginChild("###ivHelpView", new Vector2(-1, -1), true))
            {
                if (_configuration.SelectedHelpPage == 0)
                {
                    ImGui.Text("Basic Plugin Information:");
                    ImGui.Separator();
                    ImGui.TextWrapped("Inventory Tools will track both your inventory and your retainer inventories. At present it only covers your character's main bag and saddle bags but will be expanded in the future.");
                    ImGui.TextWrapped("I've taken a small amount of inspiration from Teamcraft and full credit to them for the idea of the inventory optimisations that their application provides.");
                    ImGui.TextWrapped("The plugin has been built for speed and such it can't quite do every inventory optimisation that Teamcraft can do but it's getting there.");
                    ImGui.NewLine();
                    ImGui.Text("Concepts:");
                    ImGui.Separator();
                    ImGui.TextWrapped("Filters: At present you can only have 1 filter enabled at a time. There are 2 filters available, one is the window filter and one is the background filter. When a filter is active, it enables highlighting and lets you see the relevant items.");
                    ImGui.TextWrapped("Window Filter: When the inventory tools window is visible, this is the filter that will be used to determine what to highlight.");
                    ImGui.TextWrapped("Background Filter: When the inventory tools window is closed, this is the filter that will be used to determine what to highlight. On top of this, it can be toggled on/off with the commands listed below. The intention is that you could have macros setup to toggle the filters on/off.");
                    ImGui.NewLine();
                    ImGui.Text("Commands:");
                    ImGui.Separator();
                    ImGui.TextWrapped("The below commands will open/close the main inventory tools window.");
                    ImGui.Text("/inventorytools, /inv, /invtools");
                    ImGui.TextWrapped("The below commands will toggle the background filter specified with <name>.");
                    ImGui.Text("/itfiltertoggle <name>, /invf <name>, /ifilter <name>");
                }
                else if (_configuration.SelectedHelpPage == 1)
                {
                    ImGui.Text("Advanced Filtering:");
                    ImGui.Separator();
                    ImGui.TextWrapped("When creating a filter or when searching through the results of a filter it is possible to use a series of operators to make your search more specific. The available operators are dependant on what you searching against but at present support for !, <, >, >=, <=, = is present.");
                    ImGui.TextWrapped("! - Show any results that do not contain what is entered - available for text and numbers.");
                    ImGui.TextWrapped("< - Show any results that have a value less than what is entered - available for numbers.");
                    ImGui.TextWrapped("> - Show any results that have a value greater than what is entered - available for numbers.");
                    ImGui.TextWrapped(">= - Show any results that have a value greater or equal to what is entered - available for numbers.");
                    ImGui.TextWrapped("<= - Show any results that have a value less than or equal to what is entered - available for numbers.");
                    ImGui.TextWrapped("= - Show any results that have a value equal to exactly what is entered - available for text and numbers.");
                }
                ImGui.EndChild();
            }
        }
    }
}