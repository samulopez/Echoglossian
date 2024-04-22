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

      List<ToDoItem> questNamesToTranslate = [];
      List<ToDoItem> objectivesToTranslate = [];
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

        var nodeID = todoList->UldManager.NodeList[i]->NodeID;

        // don't translate unneeded fate information
        if (nodeID < 10)
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

          if (nodeID > 60000)
          {
            questNamesToTranslate.Add(new ToDoItem(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr), i, j, nodeID));
          }
          else
          {
            objectivesToTranslate.Add(new ToDoItem(MemoryHelper.ReadSeStringAsString(out _, (nint)originalStep.StringPtr), i, j, nodeID));
          }
        }
      }

      if (questNamesToTranslate.Count == 0)
      {
        return;
      }

      objectivesToTranslate.Reverse();

      this.TranslateTodoItems(questNamesToTranslate, objectivesToTranslate, todoList);
    }

    private List<ToDoItem> GetQuestObjectives(uint currentObjectiveNode, int objectiveIndex, List<ToDoItem> objectivesToTranslate, List<ToDoItem> questObjectives)
    {
      var currentIndex = objectiveIndex + 1;
      if (currentIndex >= objectivesToTranslate.Count)
      {
        return questObjectives;
      }

      var objective = objectivesToTranslate[currentIndex];

      // objectives of the same quest use adjacent node ids
      if (Math.Abs(currentObjectiveNode - objective.NodeID) > 1)
      {
        return questObjectives;
      }

      questObjectives.Add(objective);
      return this.GetQuestObjectives(objective.NodeID, currentIndex, objectivesToTranslate, questObjectives);
    }

    private unsafe void TranslateTodoItems(List<ToDoItem> questNamesToTranslate, List<ToDoItem> objectivesToTranslate, AtkUnitBase* todoList)
    {
      try
      {
        var objectiveIndex = 0;
        foreach (var quest in questNamesToTranslate)
        {
          var currentObjective = objectivesToTranslate[objectiveIndex];
          List<ToDoItem> objectives = [currentObjective];
          objectives = this.GetQuestObjectives(currentObjective.NodeID, objectiveIndex, objectivesToTranslate, objectives);
          objectiveIndex += objectives.Count;

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

            foreach (var objective in objectives)
            {
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
            }

            continue;
          }

          var translatedNameText = Translate(quest.Text);
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
          todoList->UldManager.NodeList[quest.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[quest.IndexJ]->GetAsAtkTextNode()->SetText(translatedNameText);

          foreach (var objective in objectives)
          {
            var translatedObjectiveText = Translate(objective.Text);
            PluginLog.Debug($"Objective translated: {translatedObjectiveText}");
            translatedQuestPlate.Objectives.Add(objective.Text, translatedObjectiveText);
            todoList->UldManager.NodeList[objective.IndexI]->GetAsAtkComponentNode()->Component->UldManager.NodeList[objective.IndexJ]->GetAsAtkTextNode()->SetText(translatedObjectiveText);
          }

          string result = this.InsertQuestPlate(translatedQuestPlate);
          PluginLog.Debug($"Using QuestPlate Replace - QuestPlate DB Insert operation result: {result}");

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

    public uint NodeID { get; set; }

    public ToDoItem(string text, int indexI, int indexJ, uint nodeID)
    {
      this.Text = text;
      this.IndexI = indexI;
      this.IndexJ = indexJ;
      this.NodeID = nodeID;
    }
  }
}
