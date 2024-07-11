// <copyright file="UiTalkAsyncHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Memory;
using Dalamud.Utility;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private string translatedName = string.Empty;
    private string translatedText = string.Empty;

    private unsafe void TranslateTalk(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalk: {nameToTranslate}: {textToTranslate}");
      Task.Run(() =>
        {
          try
          {
            TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
            TalkMessage foundTalkMessage = this.FindAndReturnTalkMessage(talkMessage);

            if (foundTalkMessage != null)
            {
              this.translatedName = foundTalkMessage.TranslatedSenderName;
              this.translatedText = foundTalkMessage.TranslatedTalkMessage;

              PluginLog.Debug($"From database - Name: {foundTalkMessage.TranslatedSenderName}, Message: {foundTalkMessage.TranslatedTalkMessage}");
            }
            else
            {
              string textTranslation = this.Translate(textToTranslate);
              string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate);

              this.translatedName = nameTranslation;
              this.translatedText = textTranslation;

              TalkMessage translatedTalkData = new TalkMessage(
                 nameToTranslate,
                 textToTranslate,
                 ClientState.ClientLanguage.Humanize(),
                 ClientState.ClientLanguage.Humanize(),
                 nameTranslation,
                 textTranslation,
                 langDict[languageInt].Code,
                 this.configuration.ChosenTransEngine,
                 DateTime.Now,
                 DateTime.Now);
              string result = InsertTalkData(translatedTalkData);
              PluginLog.Debug($"Talk Message DB Insert operation result: {result}");
            }
          }
          catch (Exception e)
          {
            PluginLog.Warning("TranslateTalkReplacing Exception: " + e);
          }
        });
    }

    private unsafe void TranslateTalkReplacing()
    {
      PluginLog.Debug($"TranslateTalkReplacing");

      if (this.configuration.UseImGuiForTalk && !this.configuration.SwapTextsUsingImGui)
      {
        return;
      }

      try
      {
        var addon = GameGui.GetAddonByName("Talk");
        var talkAddon = (AtkUnitBase*)addon;
        if (talkAddon == null || !talkAddon->IsVisible)
        {
          return;
        }

        var nameNode = talkAddon->GetTextNodeById(2);
        var textNode = talkAddon->GetTextNodeById(3);
        if (textNode == null || textNode->NodeText.IsEmpty)
        {
          return;
        }

        if (this.configuration.TranslateNpcNames && nameNode != null && !nameNode->NodeText.IsEmpty)
        {
          nameNode->SetText(this.translatedName);
        }

        var parentNode = talkAddon->GetNodeById(10);
        textNode->TextFlags = (byte)(TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine);
        textNode->SetWidth(parentNode->GetWidth());
        textNode->SetText(this.translatedText);
        textNode->ResizeNodeForCurrentText();
      }
      catch (Exception e)
      {
        PluginLog.Warning("TranslateTalkReplacing Exception: " + e);
      }
    }

    private unsafe void TranslateTalkUsingImGuiAndSwapping(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkUsingImGuiAndSwapping: {nameToTranslate}: {textToTranslate}");

      Task.Run(() =>
      {
        try
        {
          TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
          TalkMessage foundTalkMessage = this.FindAndReturnTalkMessage(talkMessage);

          if (foundTalkMessage == null)
          {
            PluginLog.Debug("Using Swap text for translation");

            string textTranslation = this.Translate(textToTranslate);
            string nameTranslation = nameToTranslate.IsNullOrEmpty() ? string.Empty : this.Translate(nameToTranslate);
            if (this.configuration.TranslateNpcNames)
            {
              this.translatedName = nameTranslation;
              this.translatedText = textTranslation;

              this.currentNameTranslationId = Environment.TickCount;
              this.currentNameTranslation = Resources.WaitingForTranslation;

              int nameId = this.currentNameTranslationId;
              this.nameTranslationSemaphore.Wait();
              if (nameId == this.currentNameTranslationId)
              {
                this.currentNameTranslation = nameToTranslate;
              }

              this.nameTranslationSemaphore.Release();

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;

              int translationId = this.currentTalkTranslationId;
              this.talkTranslationSemaphore.Wait();
              if (translationId == this.currentTalkTranslationId)
              {
                this.currentTalkTranslation = textToTranslate;
              }

              this.talkTranslationSemaphore.Release();
            }
            else
            {
              this.translatedText = textTranslation;

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;

              int translationId = this.currentTalkTranslationId;
              this.talkTranslationSemaphore.Wait();
              if (translationId == this.currentTalkTranslationId)
              {
                this.currentTalkTranslation = textToTranslate;
              }

              this.talkTranslationSemaphore.Release();
            }

            TalkMessage translatedTalkData = new TalkMessage(
               nameToTranslate,
               textToTranslate,
               ClientState.ClientLanguage.Humanize(),
               ClientState.ClientLanguage.Humanize(),
               string.Empty,
               textTranslation,
               langDict[languageInt].Code,
               this.configuration.ChosenTransEngine,
               DateTime.Now,
               DateTime.Now);

            string result = InsertTalkData(translatedTalkData);
            PluginLog.Debug($"Talk Message DB Insert operation result: {result}");

            return;
          }

          PluginLog.Debug($"From database - Name: {foundTalkMessage.TranslatedSenderName}, Message: {foundTalkMessage.TranslatedTalkMessage}");

          if (this.configuration.TranslateNpcNames)
          {
            this.currentNameTranslationId = Environment.TickCount;
            this.currentNameTranslation = Resources.WaitingForTranslation;
            int nameId = this.currentNameTranslationId;
            string nameTranslation = foundTalkMessage.SenderName;
            this.nameTranslationSemaphore.Wait();
            if (nameId == this.currentNameTranslationId)
            {
              this.currentNameTranslation = nameTranslation;
            }

            this.nameTranslationSemaphore.Release();
            this.translatedName = foundTalkMessage.TranslatedSenderName;
          }

          this.currentTalkTranslationId = Environment.TickCount;
          this.currentTalkTranslation = Resources.WaitingForTranslation;
          int id = this.currentTalkTranslationId;
          string translatedTalkMessage = foundTalkMessage.OriginalTalkMessage;
          this.talkTranslationSemaphore.Wait();
          if (id == this.currentTalkTranslationId)
          {
            this.currentTalkTranslation = translatedTalkMessage;
          }

          this.talkTranslationSemaphore.Release();
          this.translatedText = foundTalkMessage.TranslatedTalkMessage;
        }
        catch (Exception e)
        {
          PluginLog.Warning("TranslateTalkUsingImGuiAndSwapping Exception: " + e);
        }
      });
    }

    private unsafe void TranslateTalkUsingImGuiWithoutSwapping(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkUsingImGuiWithoutSwapping: {nameToTranslate}: {textToTranslate}");

      Task.Run(() =>
        {
          try
          {
            TalkMessage talkMessage = this.FormatTalkMessage(nameToTranslate, textToTranslate);
            TalkMessage foundTalkMessage = this.FindAndReturnTalkMessage(talkMessage);

            if (foundTalkMessage == null)
            {
              if (this.configuration.TranslateNpcNames)
              {
                this.currentNameTranslationId = Environment.TickCount;
                this.currentNameTranslation = Resources.WaitingForTranslation;
                int nameId = this.currentNameTranslationId;
                string nameTranslation = this.Translate(nameToTranslate);
                this.nameTranslationSemaphore.Wait();
                if (nameId == this.currentNameTranslationId)
                {
                  this.currentNameTranslation = nameTranslation;
                  // TODO: Implement ImGuiScene for Talk
                  // if (this.configuration.Lang == 2)
                  // {
                  //   this.currentNameTranslationTexture =
                  //     (ImGuiScene.TextureWrap)PluginInterface.UiBuilder.LoadImage(this.TranslationImageConverter(this.DrawText(this.currentNameTranslation)));
                  // }
                }

                this.nameTranslationSemaphore.Release();
              }

              this.currentTalkTranslationId = Environment.TickCount;
              this.currentTalkTranslation = Resources.WaitingForTranslation;
              int translationId = this.currentTalkTranslationId;
              string translation = this.Translate(textToTranslate);
              this.talkTranslationSemaphore.Wait();
              if (translationId == this.currentTalkTranslationId)
              {
                this.currentTalkTranslation = translation;
              }

              this.talkTranslationSemaphore.Release();

              PluginLog.Debug($"Before if talk translation: {this.currentTalkTranslation}");
              if (this.currentNameTranslation != Resources.WaitingForTranslation &&
                  this.currentTalkTranslation != Resources.WaitingForTranslation)
              {
                TalkMessage translatedTalkData = new TalkMessage(
                  nameToTranslate,
                  textToTranslate,
                  ClientState.ClientLanguage.Humanize(),
                  ClientState.ClientLanguage.Humanize(),
                  this.configuration.TranslateNpcNames ? this.currentNameTranslation : string.Empty,
                  this.currentTalkTranslation,
                  langDict[languageInt].Code,
                  this.configuration.ChosenTransEngine,
                  DateTime.Now,
                  DateTime.Now);
                string result = InsertTalkData(translatedTalkData);
                PluginLog.Debug($"Talk Message DB Insert operation result: {result}");
              }

              return;
            }

            if (this.configuration.TranslateNpcNames)
            {
              this.currentNameTranslationId = Environment.TickCount;
              this.currentNameTranslation = Resources.WaitingForTranslation;
              int nameId = this.currentNameTranslationId;
              string nameTranslation = foundTalkMessage.TranslatedSenderName;
              this.nameTranslationSemaphore.Wait();
              if (nameId == this.currentNameTranslationId)
              {
                this.currentNameTranslation = nameTranslation;
              }

              this.nameTranslationSemaphore.Release();
            }

            this.currentTalkTranslationId = Environment.TickCount;
            this.currentTalkTranslation = Resources.WaitingForTranslation;
            int id = this.currentTalkTranslationId;
            string translatedTalkMessage = foundTalkMessage.TranslatedTalkMessage;
            this.talkTranslationSemaphore.Wait();
            if (id == this.currentTalkTranslationId)
            {
              this.currentTalkTranslation = translatedTalkMessage;
            }





            this.talkTranslationSemaphore.Release();
            this.TalkHandler("Talk", 1);
          }
          catch (Exception e)
          {
            PluginLog.Warning("TranslateTalkUsingImGuiWithoutSwapping Exception: " + e);
          }
        });
    }

    private unsafe void TranslateTalkUsingImGui(string nameToTranslate, string textToTranslate)
    {
      PluginLog.Debug($"TranslateTalkUsingImGui: {nameToTranslate}: {textToTranslate}");

      if (this.configuration.SwapTextsUsingImGui)
      {
        this.translatedName = string.Empty;
        this.translatedText = string.Empty;
        this.TranslateTalkUsingImGuiAndSwapping(nameToTranslate, textToTranslate);
        return;
      }

      this.TranslateTalkUsingImGuiWithoutSwapping(nameToTranslate, textToTranslate);
    }

    private unsafe void UiTalkAsyncHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiTalkAsyncHandler: {type} {args.AddonName}");

      if (!this.configuration.TranslateTalk)
      {
        return;
      }

      if (args is not AddonRefreshArgs refreshArgs)
      {
        this.TranslateTalkReplacing();
        return;
      }

      var updateAtkValues = (AtkValue*)refreshArgs.AtkValues;
      if (updateAtkValues == null)
      {
        return;
      }

      try
      {
        string nameToTranslate = updateAtkValues[1].String != null ? MemoryHelper.ReadSeStringAsString(out _, (nint)updateAtkValues[1].String) : string.Empty;
        string textToTranslate = MemoryHelper.ReadSeStringAsString(out _, (nint)updateAtkValues[0].String);

        PluginLog.Debug($"Talk to translate: {nameToTranslate}: {textToTranslate}");

        if (this.configuration.UseImGuiForTalk)
        {
          this.TranslateTalkUsingImGui(nameToTranslate, textToTranslate);
          return;
        }

        // to be sure we don't show text without translating it for a few milliseconds
        this.translatedName = string.Empty;
        this.translatedText = string.Empty;
        PluginLog.Debug($"Talk to translate: {nameToTranslate}: {textToTranslate}");
        this.TranslateTalk(nameToTranslate, textToTranslate);
      }
      catch (Exception e)
      {
        PluginLog.Warning("UiTalkAsyncHandler Exception: " + e);
      }
    }
  }
}