// <copyright file="UICharacterWindowHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Component.GUI;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void TranslateCharacterWindow()
    {
      /*      var characterW = GameGuiInterface.GetAddonByName("Character");

            var characterWindowA = (AtkUnitBase*)characterW;

            if (characterWindowA == null || !characterWindowA->IsVisible)
            {
              return;
            }

            PluginLog.Debug("Character window is visible using Method A");*/
      var characterWindowAtkValues = new Dictionary<int, string>();


      var atkStg = AtkStage.Instance();
      var characterWB = atkStg->RaptureAtkUnitManager->GetAddonByName("Character");

      if (characterWB == null || !characterWB->IsVisible)
      {
        return;
      }

      // PluginLog.Debug("Character window is visible using Method B");

      var cwAtkVals = characterWB->AtkValues;

      if (cwAtkVals == null)
      {
        return;
      }

      for (var i = 0; i < 100; i++)
      {
        if (cwAtkVals[i].Type == ValueType.String)
        {
          var cwAtkValStr = cwAtkVals[i].String;
          if (cwAtkValStr != null)
          {
            var cwAtkValStrVal = MemoryHelper.ReadSeStringAsString(out _, (nint)cwAtkValStr);
            if (cwAtkValStrVal != null)
            {
              characterWindowAtkValues.Add(i, cwAtkValStrVal);
            }
          }
        }
      }

      if (characterWindowAtkValues.Count > 0)
      {
        foreach (var kvp in characterWindowAtkValues)
        {
          PluginLog.Debug($"Character window AtkValue: {kvp.Key} - {kvp.Value}");
        }
      }



    }
  }
}
