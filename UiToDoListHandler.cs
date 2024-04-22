// <copyright file="UiToDoListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;

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
    private unsafe void TranslateToDoList()
    {
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      var atkStage = AtkStage.GetSingleton();
      var todoList = atkStage->RaptureAtkUnitManager->GetAddonByName("_ToDoList");
      if (todoList == null || !todoList->IsVisible)
      {
        return;
      }

      PluginLog.Debug($"Language: {ClientState.ClientLanguage.Humanize()}");
      PluginLog.Debug($"Translate _ToDoList");

      // first half will be the quest names, second half will be the quest objectives
      // quest names are in the database, quest objectives are in a local variable.
      // We can't store objectives in the db because they change.
      List<ToDoItem> textsToTranslate = [];

      for (var i = 0; i < todoList->UldManager.NodeListCount; i++)
      {
        if (!todoList->UldManager.NodeList[i]->IsVisible)
        {
          continue;
        }

        if (todoList->UldManager.NodeList[i]->Type == NodeType.Collision || todoList->UldManager.NodeList[i]->Type == NodeType.Res)
        {
          continue;
        }

        var component = todoList->UldManager.NodeList[i]->GetAsAtkComponentNode();
        for (var j = 0; j < component->Component->UldManager.NodeListCount; j++)
        {
          if (!component->Component->UldManager.NodeList[j]->IsVisible)
          {
            continue;
          }

          if (component->Component->UldManager.NodeList[j]->Type != NodeType.Text)
          {
            continue;
          }

          var originalStep = component->Component->UldManager.NodeList[j]->GetAsAtkTextNode()->NodeText;
          if (originalStep.IsEmpty == 1)
          {
            continue;
          }

          if (IsValidTimeFormat(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr)))
          {
            // skip text if time format
#if DEBUG
            PluginLog.Debug($"Skipping time format translation");
#endif
            continue;
          }

          textsToTranslate.Add(new ToDoItem(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr), i, j));
        }
      }

      if (textsToTranslate.Count == 0)
      {
        return;
      }

      this.TranslateTodoItems(textsToTranslate, todoList);
    }

    private unsafe void TranslateTodoItems(List<ToDoItem> textsToTranslate, AtkUnitBase* todoList)
    {
      try
      {
        var startingObjectiveIndex = textsToTranslate.Count / 2;
        for (var i = 0; i < startingObjectiveIndex; i++)
        {
          var quest = textsToTranslate[i];
          var objective = textsToTranslate[startingObjectiveIndex + i];
          if (this.translatedQuestNames.ContainsKey(quest.Text))
          {
            continue;
          }

          QuestPlate questPlate = this.FormatQuestPlate(quest.Text, string.Empty);
          QuestPlate foundQuestPlate = this.FindQuestPlateByName(questPlate);
          if (foundQuestPlate != null)
          {
            PluginLog.Debug($"Name from database: {quest.Text} -> {foundQuestPlate.TranslatedQuestName}");
            todoList->UldManager.NodeList[quest.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[quest.IndexJ]->GetAsAtkTextNode()->SetText(foundQuestPlate.TranslatedQuestName);
            this.translatedQuestNames.TryAdd(foundQuestPlate.TranslatedQuestName, true);

            if (foundQuestPlate.Objectives.TryGetValue(objective.Text, out var storedObjectiveText))
            {
              todoList->UldManager.NodeList[objective.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[objective.IndexJ]->GetAsAtkTextNode()->SetText(storedObjectiveText);
              continue;
            }

            var translatedQuestObjective = Translate(objective.Text);
            foundQuestPlate.Objectives.Add(objective.Text, translatedQuestObjective);
            string resultUpdate = this.UpdateQuestPlate(foundQuestPlate);
            PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Update operation result: {resultUpdate}");
            todoList->UldManager.NodeList[objective.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[objective.IndexJ]->GetAsAtkTextNode()->SetText(translatedQuestObjective);
            continue;
          }

          var translatedNameText = Translate(quest.Text);
          var translatedObjectiveText = Translate(objective.Text);
          PluginLog.Debug($"Name translated: {quest.Text} -> {translatedNameText}");
          QuestPlate translatedQuestPlate = new(
            quest.Text,
            string.Empty,
            ClientState.ClientLanguage.Humanize(),
            translatedNameText,
            string.Empty,
            string.Empty,
            langDict[languageInt].Code,
            this.configuration.ChosenTransEngine,
            DateTime.Now,
            DateTime.Now);
          translatedQuestPlate.Objectives.Add(objective.Text, translatedObjectiveText);
          string result = this.InsertQuestPlate(translatedQuestPlate);
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");
          todoList->UldManager.NodeList[quest.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[quest.IndexJ]->GetAsAtkTextNode()->SetText(translatedNameText);
          todoList->UldManager.NodeList[objective.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[objective.IndexJ]->GetAsAtkTextNode()->SetText(translatedObjectiveText);
          this.translatedQuestNames.TryAdd(translatedNameText, true);
        }
      }
      catch (Exception e)
      {
        PluginLog.Warning(e, "Error translating todo items");
      }
    }

    private unsafe void UiToDoListHandler(AddonEvent type, AddonArgs args)
    {
      this.TranslateToDoList();
    }
  }

  public class ToDoItem
  {
    public string Text { get; set; }

    public int IndexI { get; set; }

    public int IndexJ { get; set; }

    public ToDoItem(string text, int indexI, int indexJ)
    {
      this.Text = text;
      this.IndexI = indexI;
      this.IndexJ = indexJ;
    }
  }
}
