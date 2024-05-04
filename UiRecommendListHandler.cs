// <copyright file="UiRecommendListHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Threading.Tasks;

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
    private unsafe void UpdateRecommendList()
    {
      var atkStage = AtkStage.GetSingleton();
      var recommendList = atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
      if (recommendList == null || !recommendList->IsVisible)
      {
        return;
      }

      try
      {
        // Replace the text in the nodes reading from the DB
        var questListNode = recommendList->GetNodeById(5);
        if (questListNode == null || !questListNode->IsVisible)
        {
          return;
        }

        var questListComponent = questListNode->GetAsAtkComponentNode()->Component;
        for (var i = 0; i < questListComponent->UldManager.NodeListCount; i++)
        {
          if (!questListComponent->UldManager.NodeList[i]->IsVisible)
          {
            continue;
          }

          if (questListComponent->UldManager.NodeList[i]->Type == NodeType.Collision || questListComponent->UldManager.NodeList[i]->Type == NodeType.Res)
          {
            continue;
          }

          var questItemNode = questListComponent->UldManager.NodeList[i]->GetAsAtkComponentNode();
          var questNameNode = questItemNode->Component->UldManager.SearchNodeById(5);
          if (questNameNode == null || !questNameNode->IsVisible || questNameNode->Type != NodeType.Text)
          {
            continue;
          }

          var questName = questNameNode->GetAsAtkTextNode();
          if (questName->NodeText.IsEmpty == 1)
          {
            continue;
          }

          var questNameText = MemoryHelper.ReadSeStringAsString(out _, (nint)questName->NodeText.StringPtr);
          if (this.translatedQuestNames.ContainsKey(questNameText))
          {
            continue;
          }

          QuestPlate questPlate = this.FormatQuestPlate(questNameText, string.Empty);
          QuestPlate foundQuestPlate = this.FindQuestPlateByName(questPlate);
          if (foundQuestPlate != null)
          {
            PluginLog.Debug($"Name from database: {questNameText} -> {foundQuestPlate.TranslatedQuestName}");
            // because we are translating names, it's safer to use SetString instead of SetText
            questName->NodeText.SetString(foundQuestPlate.TranslatedQuestName);
            this.translatedQuestNames.TryAdd(foundQuestPlate.TranslatedQuestName, true);
          }
        }
      }
      catch (Exception e)
      {
        PluginLog.Warning($"Error: {e}");
      }
    }

    private unsafe void TranslateRecommendListHandler()
    {
      var atkStage = AtkStage.GetSingleton();
      var recommendList = atkStage->RaptureAtkUnitManager->GetAddonByName("RecommendList");
      if (recommendList == null || !recommendList->IsVisible)
      {
        return;
      }

      try
      {
        // First we store the non translated quest names in the DB
        var questListNode = recommendList->GetNodeById(5);
        if (questListNode == null || !questListNode->IsVisible)
        {
          return;
        }

        var questListComponent = questListNode->GetAsAtkComponentNode()->Component;
        for (var i = 0; i < questListComponent->UldManager.NodeListCount; i++)
        {
          if (!questListComponent->UldManager.NodeList[i]->IsVisible)
          {
            continue;
          }

          if (questListComponent->UldManager.NodeList[i]->Type == NodeType.Collision || questListComponent->UldManager.NodeList[i]->Type == NodeType.Res)
          {
            continue;
          }

          var questItemNode = questListComponent->UldManager.NodeList[i]->GetAsAtkComponentNode();
          var questNameNode = questItemNode->Component->UldManager.SearchNodeById(5);
          if (questNameNode == null || !questNameNode->IsVisible || questNameNode->Type != NodeType.Text)
          {
            continue;
          }

          var questName = questNameNode->GetAsAtkTextNode();
          if (questName->NodeText.IsEmpty == 1)
          {
            continue;
          }

          var questNameText = MemoryHelper.ReadSeStringAsString(out _, (nint)questName->NodeText.StringPtr);
          if (this.translatedQuestNames.ContainsKey(questNameText))
          {
            continue;
          }

          QuestPlate questPlate = this.FormatQuestPlate(questNameText, string.Empty);
          QuestPlate foundQuestPlate = this.FindQuestPlateByName(questPlate);
          if (foundQuestPlate != null)
          {
            continue;
          }

          var translatedNameText = Translate(questNameText);
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
        }

        // Then we replace the text in the nodes
        this.UpdateRecommendList();
      }
      catch (Exception e)
      {
        PluginLog.Warning($"Error: {e}");
      }
    }

    private unsafe void UiRecommendListHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiRecommendListHandler AddonEvent: {type} {args.AddonName}");
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      this.TranslateRecommendListHandler();
    }

    private unsafe void UiRecommendListHandlerAsync(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiRecommendListHandlerAsync AddonEvent: {type} {args.AddonName}");
      if (!this.configuration.TranslateJournal)
      {
        return;
      }

      // delay added to be sure the nodes are loaded when the player changes zones
      Task.Delay(200).ContinueWith(t => this.TranslateRecommendListHandler());
    }
  }
}
