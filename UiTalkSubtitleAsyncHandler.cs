// <copyright file="UiTalkSubtitleAsyncHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Threading.Tasks;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private string translatedTalkSubtitleText = string.Empty;

    private unsafe void TranslateTalkSubtitle(string textToTranslate)
    {
      PluginLog.Debug("Translating Talk Subtitle: " + textToTranslate);
      Task.Run(() =>
      {
        try
        {
          TalkSubtitleMessage talkSubtitleMessage = this.FormatTalkSubtitleMessage(textToTranslate);
          TalkSubtitleMessage foundTalkSubtitleMessage = this.FindAndReturnTalkSubtitleMessage(talkSubtitleMessage);

          if (foundTalkSubtitleMessage != null)
          {
            this.translatedTalkSubtitleText = foundTalkSubtitleMessage.TranslatedTalkSubtitleMessage;
          }
          else
          {
            string textTranslation = this.Translate(textToTranslate);

            TalkSubtitleMessage translatedTalkSubtitleData = new TalkSubtitleMessage(
              textToTranslate, ClientState.ClientLanguage.Humanize(), textTranslation, langDict[languageInt].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);

            string result = InsertTalkSubtitleData(translatedTalkSubtitleData);
            PluginLog.Debug("TalkSubtitle Insert Result: " + result);
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error("TranslateTalkSubtitle error: " + ex);
        }
      });
    }

    public unsafe void TranslateTalkSubtitleReplacing()
    {
      PluginLog.Debug("TranslateTalkSubtitleReplacing");

      if (this.configuration.UseImGuiForTalkSubtitle && !this.configuration.SwapTextsUsingImGui)
      {
        return;
      }

      try
      {
        var addon = GameGui.GetAddonByName("TalkSubtitle");
        if (addon == IntPtr.Zero)
        {
          return;
        }

        var talkSubtitleAddon = (AtkUnitBase*)addon;
        if (talkSubtitleAddon == null || !talkSubtitleAddon->IsVisible)
        {
          return;
        }

        var textNode = talkSubtitleAddon->GetTextNodeById(2);
        var textNode3 = talkSubtitleAddon->GetTextNodeById(3);
        var textNode4 = talkSubtitleAddon->GetTextNodeById(4);
        if (textNode == null ||
          textNode3 == null ||
          textNode4 == null ||
          textNode->NodeText.IsEmpty ||
          textNode3->NodeText.IsEmpty ||
          textNode4->NodeText.IsEmpty)
        {
          return;
        }

        textNode->SetText(this.translatedTalkSubtitleText);
        textNode3->SetText(this.translatedTalkSubtitleText);
        textNode4->SetText(this.translatedTalkSubtitleText);

      }
      catch (Exception ex)
      {
        PluginLog.Error("TranslateTalkSubtitleReplacing error: " + ex);
      }
    }

    private unsafe void TranslateTalkSubtitleUsingImGuiAndSwapping(string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkSubtitleUsingImGuiAndSwapping: {textToTranslate}");

      Task.Run(() =>
      {
        try
        {
          TalkSubtitleMessage talkMessage = this.FormatTalkSubtitleMessage(textToTranslate);
          TalkSubtitleMessage foundTalkSubtitleMessage = this.FindAndReturnTalkSubtitleMessage(talkMessage);

          if (foundTalkSubtitleMessage == null)
          {
            PluginLog.Debug("Using Swap text for translation");

            string textTranslation = this.Translate(textToTranslate);

            this.translatedText = textTranslation;

            this.currentTalkSubtitleTranslationId = Environment.TickCount;
            this.currentTalkSubtitleTranslation = Resources.WaitingForTranslation;

            int translationId = this.currentTalkSubtitleTranslationId;
            this.talkSubtitleTranslationSemaphore.Wait();
            if (translationId == this.currentTalkSubtitleTranslationId)
            {
              this.currentTalkSubtitleTranslation = textToTranslate;
            }

            this.talkSubtitleTranslationSemaphore.Release();

            this.TalkSubtitleHandler("TalkSubtitle", 1);

            TalkSubtitleMessage translatedTalkSubtitleData = new TalkSubtitleMessage(
               textToTranslate,
               ClientState.ClientLanguage.Humanize(),
               textTranslation,
               langDict[languageInt].Code,
               this.configuration.ChosenTransEngine,
               DateTime.Now,
               DateTime.Now);

            string result = InsertTalkSubtitleData(translatedTalkSubtitleData);
            PluginLog.Debug($"TalkSubtitle Message DB Insert operation result: {result}");

            return;
          }

          PluginLog.Debug($"From database - Message: {foundTalkSubtitleMessage.TranslatedTalkSubtitleMessage}");

          this.currentTalkSubtitleTranslationId = Environment.TickCount;
          this.currentTalkSubtitleTranslation = Resources.WaitingForTranslation;
          int id = this.currentTalkSubtitleTranslationId;
          string translatedTalkSubtitleMessage = foundTalkSubtitleMessage.OriginalTalkSubtitleMessage;
          this.talkSubtitleTranslationSemaphore.Wait();
          if (id == this.currentTalkSubtitleTranslationId)
          {
            this.currentTalkSubtitleTranslation = translatedTalkSubtitleMessage;
          }

          this.talkSubtitleTranslationSemaphore.Release();
          this.translatedText = foundTalkSubtitleMessage.TranslatedTalkSubtitleMessage;
          this.TalkSubtitleHandler("TalkSubtitle", 1);
        }
        catch (Exception e)
        {
          PluginLog.Warning("TranslateTalkSubtitleUsingImGuiAndSwapping Exception: " + e);
        }
      });
    }

    private unsafe void TranslateTalkSubtitleUsingImGuiWithoutSwapping(string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkSubtitleUsingImGuiWithoutSwapping:{textToTranslate}");

      Task.Run(() =>
        {
          try
          {
            TalkSubtitleMessage talkMessage = this.FormatTalkSubtitleMessage(textToTranslate);
            TalkSubtitleMessage foundTalkSubtitleMessage = this.FindAndReturnTalkSubtitleMessage(talkMessage);

            if (foundTalkSubtitleMessage == null)
            {
              this.currentTalkSubtitleTranslationId = Environment.TickCount;
              this.currentTalkSubtitleTranslation = Resources.WaitingForTranslation;
              int translationId = this.currentTalkSubtitleTranslationId;
              string translation = this.Translate(textToTranslate);
              this.talkSubtitleTranslationSemaphore.Wait();
              if (translationId == this.currentTalkSubtitleTranslationId)
              {
                this.currentTalkSubtitleTranslation = translation;
              }

              this.talkSubtitleTranslationSemaphore.Release();

              this.TalkSubtitleHandler("TalkSubtitle", 1);

              PluginLog.Debug($"Before if talk translation: {this.currentTalkSubtitleTranslation}");
              if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                  this.currentTalkSubtitleTranslation != Resources.WaitingForTranslation)
              {
                TalkSubtitleMessage translatedTalkSubtitleData = new TalkSubtitleMessage(
                  textToTranslate,
                  ClientState.ClientLanguage.Humanize(),
                  this.currentTalkSubtitleTranslation,
                  langDict[languageInt].Code,
                  this.configuration.ChosenTransEngine,
                  DateTime.Now,
                  DateTime.Now);
                string result = InsertTalkSubtitleData(translatedTalkSubtitleData);
                PluginLog.Debug($"TalkSubtitle Message DB Insert operation result: {result}");
              }

              return;
            }

            this.currentTalkSubtitleTranslationId = Environment.TickCount;
            this.currentTalkSubtitleTranslation = Resources.WaitingForTranslation;
            int id = this.currentTalkSubtitleTranslationId;
            string translatedTalkSubtitleMessage = foundTalkSubtitleMessage.TranslatedTalkSubtitleMessage;
            this.talkSubtitleTranslationSemaphore.Wait();
            if (id == this.currentTalkSubtitleTranslationId)
            {
              this.currentTalkSubtitleTranslation = translatedTalkSubtitleMessage;
            }

            this.talkSubtitleTranslationSemaphore.Release();
            this.TalkSubtitleHandler("TalkSubtitle", 1);
          }
          catch (Exception e)
          {
            PluginLog.Warning("TranslateTalkSubtitleUsingImGuiWithoutSwapping Exception: " + e);
          }
        });
    }

    private unsafe void TranslateTalkSubtitleUsingImGui(string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkSubtitleUsingImGui: {textToTranslate}");

      if (this.configuration.SwapTextsUsingImGui)
      {
        this.translatedText = string.Empty;
        this.TranslateTalkSubtitleUsingImGuiAndSwapping(textToTranslate);
        return;
      }

      this.TranslateTalkSubtitleUsingImGuiWithoutSwapping(textToTranslate);
    }

    private unsafe void UiTalkSubtitleAsyncHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiTalkSubtitleAsyncHandler: {type} {args.AddonName}");

      if (!this.configuration.TranslateTalkSubtitle)
      {
        return;
      }

      switch (type)
      {
        case AddonEvent.PreReceiveEvent:
          // to be sure we don't show the same text twice

          var addon = GameGui.GetAddonByName("TalkSubtitle");
          var talkSubtitleAddon = (AtkUnitBase*)addon;
          if (talkSubtitleAddon == null || !talkSubtitleAddon->IsVisible)
          { return; }

          var nameNode = talkSubtitleAddon->GetTextNodeById(2);
          var textNode = talkSubtitleAddon->GetTextNodeById(3);
          if (textNode == null || textNode->NodeText.IsEmpty)
          { return; }

          var textNodeText = MemoryHelper.ReadSeStringAsString(out _, (nint)textNode->NodeText.StringPtr);
          if (textNodeText == this.translatedText)
          {
            this.translatedName = string.Empty;
            this.translatedText = string.Empty;
          }

          return;
        case AddonEvent.PreDraw:
          this.TranslateTalkSubtitleReplacing();
          return;
      }

      if (args is not AddonRefreshArgs refreshArgs)
      {
        return;
      }

      var updateAtkValues = (AtkValue*)refreshArgs.AtkValues;
      if (updateAtkValues == null)
      {
        return;
      }

      try
      {
        string textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)updateAtkValues[0].String);


        PluginLog.Debug($"TalkSubtitle to translate: {textToTranslate}");

        if (this.configuration.UseImGuiForTalkSubtitle)
        {
          this.TranslateTalkSubtitleUsingImGui(textToTranslate);
          return;
        }

        // to be sure we don't show text without translating it for a few milliseconds
        this.translatedName = string.Empty;
        this.translatedText = string.Empty;
        PluginLog.Debug($"TalkSubtitle to translate: {textToTranslate}");
        this.TranslateTalkSubtitle(textToTranslate);
      }
      catch (Exception e)
      {
        PluginLog.Warning("UiTalkSubtitleAsyncHandler Exception: " + e);
      }
    }

    private unsafe void TalkSubtitleHandler(string addonName, int index)
    {
      IntPtr talkSubtitle = GameGui.GetAddonByName(addonName, index);
      if (talkSubtitle != IntPtr.Zero)
      {
        AtkUnitBase* talkSubtitleMaster = (AtkUnitBase*)talkSubtitle;
        while (talkSubtitleMaster->IsVisible)
        {
          this.talkSubtitleDisplayTranslation = true;
          this.talkSubtitleTextDimensions.X = talkSubtitleMaster->RootNode->Width * talkSubtitleMaster->Scale;
          this.talkSubtitleTextDimensions.Y = talkSubtitleMaster->RootNode->Height * talkSubtitleMaster->Scale;
          this.talkSubtitleTextPosition.X = talkSubtitleMaster->RootNode->X;
          this.talkSubtitleTextPosition.Y = talkSubtitleMaster->RootNode->Y;
        }

        this.talkSubtitleDisplayTranslation = false;
      }

      this.talkSubtitleDisplayTranslation = false;
    }
  }
}
