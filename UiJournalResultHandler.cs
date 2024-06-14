// <copyright file="UiJournalResultHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Echoglossian.EFCoreSqlite.Models.Journal;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void UiJournalResultHandler(AddonEvent type, AddonArgs args)
    {
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      if (args is not AddonSetupArgs setupArgs)
      {
        return;
      }

      var setupAtkValues = (AtkValue*)setupArgs.AtkValues;
      if (setupAtkValues == null)
      {
        return;
      }

      try
      {
        if (setupAtkValues[1].Type != FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String || setupAtkValues[1].String == null)
        {
          return;
        }

        var questNameText = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[1].String);
        if (questNameText == string.Empty)
        {
          return;
        }

        QuestPlate questPlate = this.FormatQuestPlate(questNameText, string.Empty);
        QuestPlate foundQuestPlate = this.FindQuestPlateByName(questPlate);
        if (foundQuestPlate != null)
        {
          PluginLog.Debug($"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
          setupAtkValues[1].SetString(foundQuestPlate.TranslatedQuestName);
        }
        else
        {
          var translatedNameText = this.Translate(questNameText);
          PluginLog.Debug($"Name translated: {questNameText} -> {translatedNameText}");
          QuestPlate translatedQuestPlate = new(
            questNameText,
            string.Empty,
            ClientState.ClientLanguage.Humanize(),
            translatedNameText,
            string.Empty,
            string.Empty,
            langDict[languageInt].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);

          string result = this.InsertQuestPlate(translatedQuestPlate);
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
          setupAtkValues[1].SetString(translatedNameText);
        }
      }
      catch (Exception e)
      {
        PluginLog.Warning("UiJournalResultHandler Exception: " + e.StackTrace);
      }
    }
  }
}
