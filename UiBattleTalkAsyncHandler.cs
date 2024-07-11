// <copyright file="UiBattleTalkAsyncHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Dalamud.Utility;
using Echoglossian.EFCoreSqlite.Models;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private readonly int delayBetweenTriesToTranslateBattleTalk = 50;

    private BattleTalkMessage lastBattleTalkMessage = null;

    private HashSet<string> translatedBattleTalkTexts = new HashSet<string>();

    private unsafe void ManageBattleTalk()
    {
      Task.Run(() =>
        {
          for (int i = 0; i < 5; i++)
          {
            var addon = GameGui.GetAddonByName("_BattleTalk");
            var battleTalkAddon = (AtkUnitBase*)addon;
            if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
            {
              Thread.Sleep(this.delayBetweenTriesToTranslateBattleTalk);
              continue;
            }

            var nameToTranslate = string.Empty;

            var nameNode = battleTalkAddon->GetTextNodeById(4);
            if (nameNode != null && !nameNode->NodeText.IsEmpty)
            {
              nameToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)nameNode->NodeText.StringPtr);
            }

            var textNode = battleTalkAddon->GetTextNodeById(6);
            if (textNode == null || textNode->NodeText.IsEmpty)
            {
              Thread.Sleep(this.delayBetweenTriesToTranslateBattleTalk);
              continue;
            }

            var textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)textNode->NodeText.StringPtr);
            if (this.translatedBattleTalkTexts.Contains(textToTranslate))
            {
              Thread.Sleep(this.delayBetweenTriesToTranslateBattleTalk);
              continue;
            }

            PluginLog.Debug($"ManageBattleTalk text to translate {nameToTranslate}: {textToTranslate}");

            this.lastBattleTalkMessage = new BattleTalkMessage(
                  senderName: nameToTranslate,
                  originalBattleTalkMessage: textToTranslate,
                  originalSenderNameLang: ClientState.ClientLanguage.Humanize(),
                  translatedBattleTalkMessage: string.Empty,
                  originalBattleTalkMessageLang: ClientState.ClientLanguage.Humanize(),
                  translationLang: langDict[languageInt].Code,
                  translationEngine: this.configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now);

            var foundBattleTalk = this.FindAndReturnBattleTalkMessage(this.lastBattleTalkMessage);
            if (foundBattleTalk == null)
            {
              string textTranslation = this.Translate(textToTranslate);
              string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate);
              this.lastBattleTalkMessage.TranslatedSenderName = nameTranslation;
              this.lastBattleTalkMessage.TranslatedBattleTalkMessage = textTranslation;
              translatedBattleTalkTexts.Add(textTranslation);
              InsertBattleTalkData(this.lastBattleTalkMessage);

              return;
            }

            this.lastBattleTalkMessage.TranslatedSenderName = foundBattleTalk.TranslatedSenderName;
            this.lastBattleTalkMessage.TranslatedBattleTalkMessage = foundBattleTalk.TranslatedBattleTalkMessage;
            this.translatedBattleTalkTexts.Add(foundBattleTalk.TranslatedBattleTalkMessage);

            return;
          }
        });
    }

    private unsafe void TranslateBattleTalkReplacing()
    {
      PluginLog.Debug($"TranslateBattleTalkReplacing");

      if (this.lastBattleTalkMessage == null)
      {
        return;
      }

      try
      {
        var addon = GameGui.GetAddonByName("_BattleTalk");
        var battleTalkAddon = (AtkUnitBase*)addon;
        if (battleTalkAddon == null || !battleTalkAddon->IsVisible)
        {
          return;
        }

        var nameNode = battleTalkAddon->GetTextNodeById(4);
        var textNode = battleTalkAddon->GetTextNodeById(6);
        if (textNode == null || textNode->NodeText.IsEmpty)
        {
          return;
        }

        if (this.configuration.TranslateNpcNames && nameNode != null && !nameNode->NodeText.IsEmpty)
        {
          nameNode->SetText(this.lastBattleTalkMessage.TranslatedSenderName);
        }

        textNode->SetText(this.lastBattleTalkMessage.TranslatedBattleTalkMessage);
      }
      catch (Exception e)
      {
        PluginLog.Warning("TranslateBattleTalkReplacing Exception: " + e);
      }
    }

    private unsafe void UiBattleTalkAsyncHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiBattleTalkAsyncHandler: {type} {args.AddonName}");

      if (!this.configuration.TranslateBattleTalk)
      {
        return;
      }

      if (args is not AddonRefreshArgs)
      {
        this.TranslateBattleTalkReplacing();
        return;
      }

      try
      {
        this.lastBattleTalkMessage = null;
        this.ManageBattleTalk();
      }
      catch (Exception e)
      {
        PluginLog.Warning("UiTalkAsyncHandler Exception: " + e);
      }
    }
  }
}