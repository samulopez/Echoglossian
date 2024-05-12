// <copyright file="UiToastsAsyncHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Threading.Tasks;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private string translatedErrorToastMessage = string.Empty;
    private string translatedToastMessage = string.Empty;

    private unsafe void TranslateErrorToastReplacing(string messageTextToTranslate)
    {
      PluginLog.Debug($"TranslateErrorToastReplacing: {messageTextToTranslate}");
      Task.Run(() =>
        {
          try
          {
            ToastMessage errorToastToHandle = this.FormatToastMessage("Error", messageTextToTranslate);
            ToastMessage foundToastMessage = this.FindErrorToastMessage(errorToastToHandle);

            if (foundToastMessage != null)
            {
              this.translatedErrorToastMessage = foundToastMessage.TranslatedToastMessage;
              PluginLog.Debug($"From database: {foundToastMessage.TranslatedToastMessage}");
              return;
            }

            // if the toast isn't saved
            string translatedErrorMessage = this.Translate(messageTextToTranslate);
            this.translatedErrorToastMessage = translatedErrorMessage;

            ToastMessage translatedToastData = new ToastMessage("Error", messageTextToTranslate, ClientState.ClientLanguage.Humanize(),
              translatedErrorMessage, langDict[languageInt].Code, this.configuration.ChosenTransEngine, DateTime.Now,
              DateTime.Now);
            string result = this.InsertErrorToastMessageData(translatedToastData);
            PluginLog.Debug($"TranslateErrorToastReplacing DB Insert operation result: {result}");
          }
          catch (Exception e)
          {
            PluginLog.Warning("TranslateErrorToastReplacing Exception: " + e.StackTrace);
          }
        });
    }

    private void OnErrorToastAsync(ref SeString message, ref bool ishandled)
    {
      if (!this.configuration.TranslateErrorToast)
      {
        return;
      }

      string messageTextToTranslate = message.TextValue;
      if (!this.configuration.UseImGuiForToasts)
      {
        this.translatedErrorToastMessage = string.Empty;
        message = new string(' ', messageTextToTranslate.Length + 14);
        this.TranslateErrorToastReplacing(messageTextToTranslate);
        return;
      }

      try
      {
        PluginLog.Debug($"OnErrorToastAsync: {messageTextToTranslate}");
        ToastMessage errorToastToHandle = this.FormatToastMessage("Error", messageTextToTranslate);
        ToastMessage foundToastMessage = this.FindErrorToastMessage(errorToastToHandle);

        // if the toast isn't saved
        if (foundToastMessage == null)
        {
          this.currentErrorToastTranslationId = Environment.TickCount;
          this.currentErrorToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            int messageId = this.currentErrorToastTranslationId;
            string messageTranslation = this.Translate(messageTextToTranslate);
            this.errorToastTranslationSemaphore.Wait();
            if (messageId == this.currentErrorToastTranslationId)
            {
              this.currentErrorToastTranslation = messageTranslation;
            }

            this.errorToastTranslationSemaphore.Release();

            if (this.currentErrorToastTranslation != Resources.WaitingForTranslation)
            {
              ToastMessage translatedErrorToastData = new ToastMessage("Error", messageTextToTranslate,
                errorToastToHandle.OriginalLang, this.currentErrorToastTranslation,
                langDict[languageInt].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
              string result = this.InsertErrorToastMessageData(translatedErrorToastData);
              PluginLog.Debug($"OnErrorToastAsync DB Insert operation result: {result}");
            }
          });
          return;
        }

        this.currentErrorToastTranslationId = Environment.TickCount;
        this.currentErrorToastTranslation = Resources.WaitingForTranslation;
        Task.Run(() =>
        {
          int messageId = this.currentErrorToastTranslationId;
          string messageTranslation = foundToastMessage.TranslatedToastMessage;

          this.errorToastTranslationSemaphore.Wait();
          if (messageId == this.currentErrorToastTranslationId)
          {
            this.currentErrorToastTranslation = messageTranslation;
            PluginLog.Debug($"From database: {messageTranslation}");
          }

          this.errorToastTranslationSemaphore.Release();
        });
      }
      catch (Exception e)
      {
        PluginLog.Warning("OnErrorToastAsync Exception: " + e.StackTrace);
      }
    }

    private unsafe AtkTextNode* GetTextNodeForToast()
    {
      var atkStage = AtkStage.Instance();
      var areaTextAddon = atkStage->RaptureAtkUnitManager->GetAddonById(90);
      if (areaTextAddon != null && areaTextAddon->IsVisible)
      {
        var textTreeNode = areaTextAddon->UldManager.SearchNodeById(2);
        if (textTreeNode == null || !textTreeNode->IsVisible())
        {
          return null;
        }

        return textTreeNode->GetComponent()->UldManager.SearchNodeById(2)->GetAsAtkTextNode();
      }

      var wideTextAddon = atkStage->RaptureAtkUnitManager->GetAddonById(92);
      if (wideTextAddon != null && wideTextAddon->IsVisible)
      {
        return wideTextAddon->UldManager.SearchNodeById(3)->GetAsAtkTextNode();
      }

      return null;
    }

    private unsafe void TranslateToastReplacing(string messageTextToTranslate)
    {
      PluginLog.Debug($"TranslateToastReplacing: {messageTextToTranslate}");
      Task.Run(() =>
        {
          try
          {
            ToastMessage errorToastToHandle = this.FormatToastMessage("NonError", messageTextToTranslate);
            ToastMessage foundToastMessage = this.FindToastMessage(errorToastToHandle);

            if (foundToastMessage != null)
            {
              this.translatedToastMessage = foundToastMessage.TranslatedToastMessage;
              PluginLog.Debug($"From database: {foundToastMessage.TranslatedToastMessage}");
              return;
            }

            // if the toast isn't saved
            string toastMessage = this.Translate(messageTextToTranslate);
            this.translatedToastMessage = toastMessage;

            ToastMessage translatedToastData = new ToastMessage("NonError", messageTextToTranslate, ClientState.ClientLanguage.Humanize(),
              toastMessage, langDict[languageInt].Code, this.configuration.ChosenTransEngine, DateTime.Now,
              DateTime.Now);

            string result = this.InsertOtherToastMessageData(translatedToastData);
            PluginLog.Debug($"TranslateToastReplacing DB Insert operation result: {result}");
          }
          catch (Exception e)
          {
            PluginLog.Warning("TranslateToastReplacing Exception: " + e);
          }
        });
    }

    private void OnToastAsync(ref SeString message, ref ToastOptions options, ref bool ishandled)
    {
      if (!this.configuration.TranslateToast)
      {
        return;
      }

      string messageTextToTranslate = message.TextValue;
      if (!this.configuration.UseImGuiForToasts)
      {
        this.translatedToastMessage = string.Empty;
        message = new string(' ', messageTextToTranslate.Length + 14);
        this.TranslateToastReplacing(messageTextToTranslate);
        return;
      }

      try
      {
        ToastMessage toastToHandle = this.FormatToastMessage("NonError", message.TextValue);
        ToastMessage foundToastMessage = this.FindToastMessage(toastToHandle);

        // if the toast isn't saved
        if (foundToastMessage == null)
        {
          this.currentToastTranslationId = Environment.TickCount;
          this.currentToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            int messageId = this.currentToastTranslationId;
            string messageTranslation = this.Translate(messageTextToTranslate);
            this.toastTranslationSemaphore.Wait();
            if (messageId == this.currentToastTranslationId)
            {
              this.currentToastTranslation = messageTranslation;
            }

            this.toastTranslationSemaphore.Release();

            if (this.currentToastTranslation != Resources.WaitingForTranslation)
            {
              ToastMessage translatedToastData = new ToastMessage("NonError", messageTextToTranslate,
                toastToHandle.OriginalLang, this.currentToastTranslation,
                langDict[languageInt].Code, this.configuration.ChosenTransEngine, DateTime.Now, DateTime.Now);
              string result = this.InsertOtherToastMessageData(translatedToastData);
              PluginLog.Debug($"OnToastAsync DB Insert operation result: {result}");
            }
          });
          return;
        }

        if (!this.configuration.UseImGuiForToasts)
        {
          message = foundToastMessage.TranslatedToastMessage;
        }
        else
        {
          this.currentToastTranslationId = Environment.TickCount;
          this.currentToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            int messageId = this.currentToastTranslationId;
            string messageTranslation = foundToastMessage.TranslatedToastMessage;

            this.toastTranslationSemaphore.Wait();
            if (messageId == this.currentToastTranslationId)
            {
              this.currentToastTranslation = messageTranslation;
            }

            this.toastTranslationSemaphore.Release();
          });
        }
      }
      catch (Exception e)
      {
        PluginLog.Warning("OnToastAsync Exception: " + e.StackTrace);
      }
    }

    private void OnQuestToastAsync(ref SeString message, ref QuestToastOptions options, ref bool ishandled)
    {
      if (!this.configuration.TranslateToast || !this.configuration.TranslateQuestToast || !this.configuration.TranslateWideTextToast)
      {
        return;
      }

      try
      {
        string messageTextToTranslate = message.TextValue;
        PluginLog.Debug($"OnQuestToastAsync: {messageTextToTranslate}");

        if (!this.configuration.UseImGuiForToasts)
        {
          string messageTranslatedText = this.Translate(messageTextToTranslate);

          message = messageTranslatedText;
        }
        else
        {
          this.currentQuestToastTranslationId = Environment.TickCount;
          this.currentQuestToastTranslation = Resources.WaitingForTranslation;
          Task.Run(() =>
          {
            int messageId = this.currentQuestToastTranslationId;
            string messageTranslation = this.Translate(messageTextToTranslate);
            this.questToastTranslationSemaphore.Wait();
            if (messageId == this.currentQuestToastTranslationId)
            {
              this.currentQuestToastTranslation = messageTranslation;
            }

            this.questToastTranslationSemaphore.Release();
          });
        }
      }
      catch (Exception e)
      {
        PluginLog.Warning("OnQuestToastAsync Exception: " + e.StackTrace);
      }
    }

    private unsafe void UiTextErrorHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiTextErrorHandler AddonEvent: {type} {args.AddonName}");
      if (!this.configuration.TranslateErrorToast)
      {
        return;
      }

      if (this.configuration.UseImGuiForToasts)
      {
        return;
      }

      var atkStage = AtkStage.Instance();
      var textErrorAddon = atkStage->RaptureAtkUnitManager->GetAddonByName("_TextError");
      if (textErrorAddon == null || !textErrorAddon->IsVisible)
      {
        return;
      }

      var textNode = textErrorAddon->GetTextNodeById(2);
      textNode->SetText(this.translatedErrorToastMessage);
    }

    private unsafe void UiAreaTextHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiAreaTextHandler AddonEvent: {type} {args.AddonName}");
      if (!this.configuration.TranslateToast)
      {
        return;
      }

      if (this.configuration.UseImGuiForToasts)
      {
        return;
      }

      var atkStage = AtkStage.Instance();
      var areaTextAddon = atkStage->RaptureAtkUnitManager->GetAddonById(90);
      if (areaTextAddon == null || !areaTextAddon->IsVisible)
      {
        return;
      }

      var textTreeNode = areaTextAddon->UldManager.SearchNodeById(2);
      if (textTreeNode == null || !textTreeNode->IsVisible())
      {
        return;
      }

      var textNode = textTreeNode->GetComponent()->UldManager.SearchNodeById(2)->GetAsAtkTextNode();
      textNode->SetText(this.translatedToastMessage);
    }

    private unsafe void UiWideTextHandler(AddonEvent type, AddonArgs args)
    {
      PluginLog.Debug($"UiWideTextHandler AddonEvent: {type} {args.AddonName}");
      if (!this.configuration.TranslateToast)
      {
        return;
      }

      if (this.configuration.UseImGuiForToasts)
      {
        return;
      }

      var atkStage = AtkStage.Instance();
      var wideTextAddon = atkStage->RaptureAtkUnitManager->GetAddonById(92);
      if (wideTextAddon == null || !wideTextAddon->IsVisible)
      {
        return;
      }

      var textNode = wideTextAddon->UldManager.SearchNodeById(3)->GetAsAtkTextNode();
      textNode->SetText(this.translatedToastMessage);
    }
  }
}
