// <copyright file="UiJournalAcceptHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Runtime.InteropServices;

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
    private unsafe void UiJournalAcceptHandler(AddonEvent type, AddonArgs args)
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
        string questName = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[5].String);
        string questMessage = MemoryHelper.ReadSeStringAsString(out _, (nint)setupAtkValues[12].String);

        PluginLog.Debug($"Language: {ClientState.ClientLanguage.Humanize()}");
        PluginLog.Debug($"Quest name: {questName}");
        PluginLog.Debug($"Quest message: {questMessage}");

        QuestPlate questPlate = this.FormatQuestPlate(questName, questMessage);
        QuestPlate foundQuestPlate = this.FindQuestPlate(questPlate);

        string translatedQuestName;
        string translatedQuestMessage;

        // If the quest is not saved
        if (foundQuestPlate == null)
        {
          translatedQuestName = this.Translate(questName);
          translatedQuestMessage = this.Translate(questMessage);

          PluginLog.Debug($"Translated quest name: {translatedQuestName}");
          PluginLog.Debug($"Translated quest message: {translatedQuestMessage}");

          QuestPlate translatedQuestPlate = new(
            questName,
            questMessage,
            ClientState.ClientLanguage.Humanize(),
            translatedQuestName,
            translatedQuestMessage,
            string.Empty,
            langDict[languageInt].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);

          string result = this.InsertQuestPlate(translatedQuestPlate);
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
        }
        else
        { // if the data is already in the DB
          translatedQuestName = foundQuestPlate.TranslatedQuestName;
          translatedQuestMessage = foundQuestPlate.TranslatedQuestMessage;
          PluginLog.Debug($"From database - Name: {translatedQuestName}, Message: {translatedQuestMessage}");
        }

        PluginLog.Debug($"Using QuestPlate Replace - {translatedQuestName}: {translatedQuestMessage}");
        setupAtkValues[5].SetString(translatedQuestName);
        setupAtkValues[12].SetString(translatedQuestMessage);
      }
      catch (Exception e)
      {
        PluginLog.Warning("Exception: " + e.StackTrace);
      }
    }
  }
}
