// <copyright file="UiToastsHandlers.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text.SeStringHandling;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    private unsafe void ToastHandler(string toastName, int index)
    {
      IntPtr toastByName = GameGui.GetAddonByName(toastName, index);
      if (toastByName != IntPtr.Zero)
      {
        AtkUnitBase* toastByNameMaster = (AtkUnitBase*)toastByName;
        if (toastByNameMaster->IsVisible)
        {
          this.toastDisplayTranslation = true;
          this.toastTranslationTextDimensions.X = toastByNameMaster->RootNode->Width * toastByNameMaster->Scale * 2;
          this.toastTranslationTextDimensions.Y = toastByNameMaster->RootNode->Height * toastByNameMaster->Scale;
          this.toastTranslationTextPosition.X = toastByNameMaster->RootNode->X;
          this.toastTranslationTextPosition.Y = toastByNameMaster->RootNode->Y;
        }
        else
        {
          this.toastDisplayTranslation = false;
        }
      }
      else
      {
        this.toastDisplayTranslation = false;
      }
    }

    private unsafe void QuestToastHandler(string questToastName, int index)
    {
      IntPtr questToastByName = GameGui.GetAddonByName(questToastName, index);
      if (questToastByName != IntPtr.Zero)
      {
        AtkUnitBase* questToastByNameMaster = (AtkUnitBase*)questToastByName;
        if (questToastByNameMaster->IsVisible)
        {
          this.questToastDisplayTranslation = true;
          this.questToastTranslationTextDimensions.X = questToastByNameMaster->RootNode->Width * questToastByNameMaster->Scale * 2;
          this.questToastTranslationTextDimensions.Y = questToastByNameMaster->RootNode->Height * questToastByNameMaster->Scale;
          this.questToastTranslationTextPosition.X = questToastByNameMaster->RootNode->X;
          this.questToastTranslationTextPosition.Y = questToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.questToastDisplayTranslation = false;
        }
      }
      else
      {
        this.questToastDisplayTranslation = false;
      }
    }

    private unsafe void ClassChangeToastHandler(string classChangeToastName, int index)
    {
      // TODO: Rework translation code to async
      if (!this.configuration.TranslateClassChangeToast)
      {
        return;
      }

      IntPtr classChangeToastByName = GameGui.GetAddonByName(classChangeToastName, index);

      if (classChangeToastByName != IntPtr.Zero)
      {
        AtkUnitBase* classChangeToastByNameMaster = (AtkUnitBase*)classChangeToastByName;
        if (classChangeToastByNameMaster->IsVisible)
        {
          AtkTextNode* textNode = null;
          for (int i = 0; i < classChangeToastByNameMaster->UldManager.NodeListCount; i++)
          {
            if (classChangeToastByNameMaster->UldManager.NodeList[i]->Type != NodeType.Text)
            {
              continue;
            }

            textNode = (AtkTextNode*)classChangeToastByNameMaster->UldManager.NodeList[i];
            break;
          }

          if (textNode == null)
          {
            return;
          }

          try
          {
            string messageToTranslate = Marshal.PtrToStringUTF8(new IntPtr(textNode->NodeText.StringPtr));

            if (!this.configuration.UseImGuiForToasts)
            {
#if DEBUG
              PluginLog.Debug("Not Using Imgui - Translate ClassChange toast");
#endif
              this.currentClassChangeToastTranslationId = Environment.TickCount;
              this.currentClassChangeToastTranslation = Resources.WaitingForTranslation;
#if DEBUG
              PluginLog.Debug("Not Using Imgui - Translate ClassChange toast 1");
#endif
              textNode->SetText(Resources.WaitingForTranslation);
#if DEBUG
              PluginLog.Debug("Not Using Imgui - Translate ClassChange toast - 2");
#endif

              Task.Run(() =>
              {
                int messageId = this.currentClassChangeToastTranslationId;

#if DEBUG
                PluginLog.Debug("Not Using Imgui - Translate ClassChange toast - 3");
#endif

                textNode->SetText(this.currentClassChangeToastTranslation);
                this.classChangeToastTranslationSemaphore.Wait();
                if (messageId == this.currentClassChangeToastTranslationId)
                {
                  string messageTranslation = this.Translate(messageToTranslate);
#if DEBUG
                  PluginLog.Debug("Not Using Imgui - Translate ClassChange toast - 4");
#endif
                  textNode->SetText(messageTranslation);
                }

                textNode->SetText(Resources.WaitingForTranslation);
#if DEBUG
                PluginLog.Debug("Not Using Imgui - Translate ClassChange toast - 5");
#endif
                this.classChangeToastTranslationSemaphore.Release();
              });
            }
            else
            {
#if DEBUG
              PluginLog.Debug("Using Imgui - Translate ClassChange toast");
#endif
              this.classChangeToastDisplayTranslation = true;
              this.currentClassChangeToastTranslationId = Environment.TickCount;
              this.currentClassChangeToastTranslation = Resources.WaitingForTranslation;
              Task.Run(() =>
              {
                int messageId = this.currentToastTranslationId;
                string messageTranslation = this.Translate(textNode->NodeText.ToString());
                this.classChangeToastTranslationSemaphore.Wait();
                if (messageId == this.currentClassChangeToastTranslationId)
                {
                  this.currentClassChangeToastTranslation = messageTranslation;
                }

                this.classChangeToastTranslationSemaphore.Release();
              });

              this.classChangeToastTranslationTextDimensions.X = classChangeToastByNameMaster->RootNode->Width * classChangeToastByNameMaster->Scale * 2;
              this.classChangeToastTranslationTextDimensions.Y = classChangeToastByNameMaster->RootNode->Height * classChangeToastByNameMaster->Scale;
              this.classChangeToastTranslationTextPosition.X = classChangeToastByNameMaster->RootNode->X;
              this.classChangeToastTranslationTextPosition.Y = classChangeToastByNameMaster->RootNode->Y;
            }
          }
          catch (Exception e)
          {
            PluginLog.Debug("Exception: " + e.StackTrace);
            throw;
          }
        }
        else
        {
          this.classChangeToastDisplayTranslation = false;
        }
      }
      else
      {
        this.classChangeToastDisplayTranslation = false;
      }
    }

    private unsafe void TextErrorToastHandler(string toastName, int index)
    {
      IntPtr errorToastByName = GameGui.GetAddonByName(toastName, index);

      if (errorToastByName != IntPtr.Zero)
      {
        AtkUnitBase* errorToastByNameMaster = (AtkUnitBase*)errorToastByName;

        // 2729DE6EDE0
        if (errorToastByNameMaster->IsVisible)
        {
          this.errorToastDisplayTranslation = true;

          // TODO: convert all to this approach + async
          /*var errorToastId = errorToastByNameMaster->RootNode->ChildNode->NodeID;
          PluginLog.Debug($"error toast id: {errorToastId}");
          var textNode = (AtkTextNode*)errorToastByNameMaster->UldManager.SearchNodeById(errorToastId);
          //var nodeText = MemoryHelper.ReadString((IntPtr)textNode->NodeText.StringPtr, (int)textNode->NodeText.StringLength);
          PluginLog.Debug(textNode->NodeText.ToString() ?? "sem nada...");
          textNode->SetText("What is a man? A miserable little pile of secrets. But enough talk… Have at you!");*/

          this.errorToastTranslationTextDimensions.X = errorToastByNameMaster->RootNode->Width * errorToastByNameMaster->Scale;
          this.errorToastTranslationTextDimensions.Y = errorToastByNameMaster->RootNode->Height * errorToastByNameMaster->Scale;
          this.errorToastTranslationTextPosition.X = errorToastByNameMaster->RootNode->X;
          this.errorToastTranslationTextPosition.Y = errorToastByNameMaster->RootNode->Y;
        }
        else
        {
          this.errorToastDisplayTranslation = false;
        }
      }
      else
      {
        this.errorToastDisplayTranslation = false;
      }
    }
  }
}