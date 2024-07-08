using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Text.Sanitizer;
using Dalamud.Memory;
using Echoglossian.EFCoreSqlite.Models;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Humanizer;
using ImGuiNET;

using static Echoglossian.Echoglossian;

namespace Echoglossian
{
  internal class UIAddonHandler : IDisposable
  {
    private bool disposedValue;
    private CancellationTokenSource cts;
    private Task translationTask;

    private Config configuration;
    private ImFontPtr uiFont;
    private bool fontLoaded;
    private ClientLanguage clientLanguage;
    private TranslationService translationService;
    private ConcurrentDictionary<int, TranslationEntry> translations;
    private string langToTranslateTo;
    private string addonName = string.Empty;
    private bool isAddonVisible = false;
    private Dictionary<int, TextFlags> addonNodesFlags;
    private AddonCharacteristicsInfo addonCharacteristicsInfo;
    private string configDir;
    private HashSet<string> translatedTexts = new HashSet<string>();
    private const string TranslationMarker = "\u0020\u0020\u0020\u0020\u0020"; // 5 spaces
    private static readonly Dictionary<string, bool> ProcessedAddons = new Dictionary<string, bool>();

    private AddonReceiveEventArgs addonReceiveEventArgs = null;
    private AddonSetupArgs addonSetupArgs = null;
    private AddonUpdateArgs addonUpdateArgs = null;
    private AddonDrawArgs addonDrawArgs = null;
    private AddonFinalizeArgs addonFinalizeArgs = null;
    private AddonRequestedUpdateArgs addonRequestedUpdateArgs = null;
    private AddonRefreshArgs addonRefreshArgs = null;

    public UIAddonHandler(
        Config configuration = default,
        ImFontPtr uiFont = default,
        bool fontLoaded = default,
        string langToTranslateTo = default)
    {
      this.configuration = configuration;
      this.uiFont = uiFont;
      this.fontLoaded = fontLoaded;
      this.langToTranslateTo = langToTranslateTo;
      this.clientLanguage = ClientState.ClientLanguage;
      this.translationService = new TranslationService(configuration, PluginLog, new Sanitizer(this.clientLanguage));
      this.translations = new ConcurrentDictionary<int, TranslationEntry>();
      this.configDir = PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;
      this.cts = new CancellationTokenSource();
      this.translationTask = Task.Run(async () => await this.ProcessTranslations(this.cts.Token));
    }

#nullable enable
    public void EgloAddonHandler(string addonName, AddonSetupArgs? setupArgs = null)
    {
      this.addonName = addonName;
      if (setupArgs != null)
      {
        this.addonSetupArgs = setupArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonReceiveEventArgs? receiveEventArgs = null)
    {
      this.addonName = addonName;
      if (receiveEventArgs != null)
      {
        this.addonReceiveEventArgs = receiveEventArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonUpdateArgs? updateArgs = null)
    {
      this.addonName = addonName;
      if (updateArgs != null)
      {
        this.addonUpdateArgs = updateArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonDrawArgs? drawArgs = null)
    {
      this.addonName = addonName;
      if (drawArgs != null)
      {
        this.addonDrawArgs = drawArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonFinalizeArgs? finalizeArgs = null)
    {
      this.addonName = addonName;
      if (finalizeArgs != null)
      {
        this.addonFinalizeArgs = finalizeArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonRequestedUpdateArgs? requestedUpdateArgs = null)
    {
      this.addonName = addonName;
      if (requestedUpdateArgs != null)
      {
        this.addonRequestedUpdateArgs = requestedUpdateArgs;
      }

      this.HandleCommonLogic();
    }

    public void EgloAddonHandler(string addonName, AddonRefreshArgs? refreshArgs = null)
    {
      this.addonName = addonName;
      if (refreshArgs != null)
      {
        this.addonRefreshArgs = refreshArgs;
      }

      this.HandleCommonLogic();
    }

    private void HandleCommonLogic()
    {
      if (string.IsNullOrEmpty(this.addonName))
      {
        return;
      }

      this.DetermineAddonCharacteristics();
      this.AdjustAddonNodesFlags();
      this.ExploreAddon();
    }

    private void DetermineAddonCharacteristics()
    {
      switch (this.addonName)
      {
        case "Talk":
          this.addonCharacteristicsInfo = new()
          {
            AddonName = this.addonName,
            IsComplexAddon = false,
            NameNodeId = 2,
            MessageNodeId = 3,
            TalkMessage = new TalkMessage(
                  senderName: string.Empty,
                  originalTalkMessage: string.Empty,
                  originalSenderNameLang: this.clientLanguage.Humanize(),
                  translatedTalkMessage: string.Empty,
                  originalTalkMessageLang: this.clientLanguage.Humanize(),
                  translationLang: this.langToTranslateTo,
                  translationEngine: this.configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        case "_BattleTalk":
          this.addonCharacteristicsInfo = new()
          {
            AddonName = this.addonName,
            IsComplexAddon = false,
            NameNodeId = 4,
            MessageNodeId = 6,
            BattleTalkMessage = new BattleTalkMessage(
                  senderName: string.Empty,
                  originalBattleTalkMessage: string.Empty,
                  originalSenderNameLang: this.clientLanguage.Humanize(),
                  translatedBattleTalkMessage: string.Empty,
                  originalBattleTalkMessageLang: this.clientLanguage.Humanize(),
                  translationLang: this.langToTranslateTo,
                  translationEngine: this.configuration.ChosenTransEngine,
                  translatedSenderName: string.Empty,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        case "TalkSubtitle":
          this.addonCharacteristicsInfo = new()
          {
            AddonName = this.addonName,
            IsComplexAddon = false,
            MessageNodeId = 3,
            TalkSubtitleMessage = new TalkSubtitleMessage(
                  originalTalkSubtitleMessage: string.Empty,
                  translatedTalkSubtitleMessage: string.Empty,
                  originalTalkSubtitleMessageLang: this.clientLanguage.Humanize(),
                  translationLang: this.langToTranslateTo,
                  translationEngine: this.configuration.ChosenTransEngine,
                  createdDate: DateTime.Now,
                  updatedDate: DateTime.Now),
          };
          break;
        default:
          break;
      }
    }

    private void AdjustAddonNodesFlags()
    {
      this.addonNodesFlags = new Dictionary<int, TextFlags>();

      switch (this.addonName)
      {
        case "Talk":
          this.addonNodesFlags.Add(3, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        case "_BattleTalk":
          this.addonNodesFlags.Add(6, (TextFlags)((byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        default:
          break;
      }
    }

    private unsafe void ExploreAddon()
    {
      AtkUnitBase* foundAddon = null;

      try
      {
        var addon = GameGui.GetAddonByName(this.addonName, 1);
        foundAddon = (AtkUnitBase*)addon;
        if (foundAddon == null)
        {
          PluginLog.Information($"Addon {this.addonName} not found in ExploreAddon.");
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving addon: {ex}");
        return;
      }

      try
      {
        this.isAddonVisible = foundAddon->IsVisible;
        if (!this.isAddonVisible)
        {
          // Echoglossian.PluginLog.Information($"Addon {this.addonName} is not visible.");
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error checking addon visibility: {ex}");
        return;
      }

      AtkTextNode* nameNodeAsTextNode = null;
      AtkTextNode* messageNodeAsTextNode = null;

      try
      {
        var nameNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Information($"Addon {this.addonName} name node found in ExploreAddon.");
        PluginLog.Information($"Addon {this.addonName} name node text in ExploreAddon: {MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr)}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Information($"Addon {this.addonName} message node found in ExploreAddon.");
        PluginLog.Information($"Addon {this.addonName} message node text in ExploreAddon: {MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr)}");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        var nameText = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr));

        PluginLog.Information($"Addon {this.addonName} name node text in ExploreAddon: {nameText}");

        if (string.IsNullOrEmpty(nameText) || (!string.IsNullOrEmpty(nameText) && nameText.Contains(TranslationMarker)))
        {
          PluginLog.Information($"Addon {this.addonName} name node has already been processed.");
          return;
        }

        this.addonCharacteristicsInfo.TalkMessage.SenderName = nameText;

        if (!this.configuration.TranslateNpcNames)
        {
          this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = nameText;
        }
      }

      if (messageNodeAsTextNode != null)
      {
        var messageText = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr));

        PluginLog.Information($"Addon {this.addonName} message node text in ExploreAddon: {messageText}");

        if (!string.IsNullOrEmpty(messageText) && messageText.Contains(TranslationMarker))
        {
          PluginLog.Information($"Addon {this.addonName} message node has already been processed.");
          return;
        }

        this.addonCharacteristicsInfo.TalkMessage.OriginalTalkMessage = messageText;
      }

      this.CheckDatabaseForTranslation();
    }

    private void CheckDatabaseForTranslation()
    {
      if (this.addonName == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;

        PluginLog.Information($"Checking database for: {this.addonCharacteristicsInfo.TalkMessage.ToString()}");

        if (talkMessage != null && !string.IsNullOrEmpty(talkMessage.SenderName) && !string.IsNullOrEmpty(talkMessage.OriginalTalkMessage))
        {
          if (FindTalkMessage(talkMessage))
          {
            this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = FoundTalkMessage.TranslatedTalkMessage + TranslationMarker;

            PluginLog.Information($"Addon {this.addonName} message node text found in database is {this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage}");
            if (this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = FoundTalkMessage.TranslatedSenderName + TranslationMarker;
              PluginLog.Information($"Addon {this.addonName} sender name node text found in database is {this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName}");
            }
            else
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = FoundTalkMessage.SenderName + TranslationMarker;
            }

            this.SetTranslationToAddon();
          }
          else
          {
            PluginLog.Information($"Current addon {this.addonName} not found in database. Sending to translate!");
            this.TranslateTexts(talkMessage.OriginalTalkMessage, "Talk");
          }
        }
      }
      else if (this.addonName == "_BattleTalk")
      {
        var battleTalkMessage = this.addonCharacteristicsInfo.BattleTalkMessage;

        PluginLog.Information($"Checking database for: {this.addonCharacteristicsInfo.BattleTalkMessage.ToString()}");

        if (battleTalkMessage != null && !string.IsNullOrEmpty(battleTalkMessage.SenderName) && !string.IsNullOrEmpty(battleTalkMessage.OriginalBattleTalkMessage))
        {
          if (FindBattleTalkMessage(battleTalkMessage))
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = FoundBattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;

            PluginLog.Information($"Addon {this.addonName} message node text found in database is {this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage}");
            if (this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = FoundBattleTalkMessage.TranslatedSenderName + TranslationMarker;
              PluginLog.Information($"Addon {this.addonName} sender name node text found in database is {this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName}");
            }
            else
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = FoundBattleTalkMessage.SenderName + TranslationMarker;
            }

            this.SetTranslationToAddon();
          }
          else
          {
            PluginLog.Information($"Current addon {this.addonName} not found in database. Sending to translate!");
            this.TranslateTexts(battleTalkMessage.OriginalBattleTalkMessage, "_BattleTalk");
          }
        }
      }
    }

    private void TranslateTexts(string originalText, string addonType)
    {
      Task.Run(async () =>
      {
        var translation = await this.translationService.TranslateAsync(originalText, this.clientLanguage.Humanize(), this.langToTranslateTo);
        this.SaveTranslationToDatabase(originalText, translation, addonType);
        this.translatedTexts.Add(translation);

        this.translations.TryAdd(this.addonCharacteristicsInfo.MessageNodeId, new TranslationEntry
        {
          OriginalText = originalText,
          TranslatedText = translation,
          IsTranslated = true,
        });
        PluginLog.Warning($"Translation for addon {this.addonName} added to translations dictionary.");

        this.SetTranslationToAddon();
      });
    }

    private void SaveTranslationToDatabase(string originalText, string translatedText, string addonType)
    {
      if (addonType == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;

        if (talkMessage.TranslatedTalkMessage != translatedText)
        {
          talkMessage.OriginalTalkMessage = originalText;
          talkMessage.TranslatedTalkMessage = translatedText;
          if (!this.translatedTexts.Contains(talkMessage.TranslatedTalkMessage))
          {
            InsertTalkData(talkMessage);
            this.translatedTexts.Add(talkMessage.TranslatedTalkMessage);
          }
        }
      }
      else if (addonType == "_BattleTalk")
      {
        var battleTalkMessage = this.addonCharacteristicsInfo.BattleTalkMessage;

        if (battleTalkMessage.TranslatedBattleTalkMessage != translatedText)
        {
          battleTalkMessage.OriginalBattleTalkMessage = originalText;
          battleTalkMessage.TranslatedBattleTalkMessage = translatedText;
          if (!this.translatedTexts.Contains(battleTalkMessage.TranslatedBattleTalkMessage))
          {
            InsertBattleTalkData(battleTalkMessage);
            this.translatedTexts.Add(battleTalkMessage.TranslatedBattleTalkMessage);
          }
        }
      }
    }

    private async Task ProcessTranslations(CancellationToken token)
    {
      while (!token.IsCancellationRequested)
      {
        foreach (var key in this.translations.Keys)
        {
          if (this.translations.TryGetValue(key, out var entry) && !entry.IsTranslated && this.isAddonVisible)
          {
            await this.TranslateText(key, entry.OriginalText);
          }
        }

        try
        {
          await Task.Delay(100, token); // tried using Task.Yeld() but I saw no difference
        }
        catch (TaskCanceledException)
        {
          break;
        }
      }
    }

    private async Task TranslateText(int id, string text)
    {
      try
      {
        var translation = await this.translationService.TranslateAsync(text, this.clientLanguage.Humanize(), this.langToTranslateTo);
        if (this.translations.TryGetValue(id, out var entry))
        {
          entry.TranslatedText = translation;
          entry.IsTranslated = true;
          this.translatedTexts.Add(translation);

          await Task.Run(() => this.SaveTranslationToDatabase(text, translation, this.addonName));
        }
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error in TranslateText method: {e}");
      }
    }

    private unsafe void SetTranslationToAddon()
    {
      PluginLog.Information($"Called SetTranslationToAddon for addon {this.addonName}.");
      AtkUnitBase* foundAddon = null;

      // --- experiment --- //

      /*var atkvalues = (AtkValue*)this.addonDrawArgs.AtkValues;

      if (atkvalues == null)
      {
        return;
      }*/

      /*   try
         {
           Echoglossian.PluginLog.Information($"atkvalues[0].Value: {atkvalues[0].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[1].Value: {atkvalues[1].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[2].Value: {atkvalues[2].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[3].Value: {atkvalues[3].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[4].Value: {atkvalues[4].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[5].Value: {atkvalues[5].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[6].Value: {atkvalues[6].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[7].Value: {atkvalues[7].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[8].Value: {atkvalues[8].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[9].Value: {atkvalues[9].ToString}");
           Echoglossian.PluginLog.Information($"atkvalues[10].Value: {atkvalues[10].ToString}");
         }
         catch (Exception ex)
         {
           PluginLog.Error($"Error in SetTranslationToAddon atkvalues capture: {ex}");
         }*/

      try
      {
        var addon = GameGui.GetAddonByName(this.addonName, 1);

        PluginLog.Warning($"Addon {this.addonName} found in SetTranslationToAddon.");
        foundAddon = (AtkUnitBase*)addon;
        if (foundAddon == null)
        {
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving addon: {ex}");
        return;
      }

      try
      {
        this.isAddonVisible = foundAddon->IsVisible;

        PluginLog.Information($"Addon {this.addonName} is visible: {this.isAddonVisible} in SetTranslationToAddon.");
        if (!this.isAddonVisible)
        {
          PluginLog.Information($"Addon {this.addonName} is not visible in SetTranslationToAddon.");
          return;
        }
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error checking addon visibility in SetTranslationToAddon: {ex}");
        return;
      }

      AtkTextNode* nameNodeAsTextNode = null;
      AtkTextNode* messageNodeAsTextNode = null;

      try
      {
        var nameNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.NameNodeId);

        if (nameNode == null)
        {
          return;
        }

        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        if (nameNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Information($"Addon {this.addonName} name node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving name node in SetTranslationToAddon: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);

        if (messageNode == null)
        {
          return;
        }

        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        if (messageNodeAsTextNode == null)
        {
          return;
        }

        PluginLog.Information($"Addon {this.addonName} message node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        PluginLog.Error($"Error retrieving message node in SetTranslationToAddon: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        var nameTextFromNode = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr));

        PluginLog.Information($"Addon {this.addonName} name node text in SetTranslationToAddon: {nameTextFromNode}");
        try
        {
          var translatedName = this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName + TranslationMarker;

          PluginLog.Information($"Addon {this.addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

          PluginLog.Information($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {this.configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {this.configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
          if (this.configuration.TranslateNpcNames && !nameTextFromNode.Contains(TranslationMarker))
          {
            PluginLog.Warning($"Setting name node text in SetTranslationToAddon.");
            nameNodeAsTextNode->SetText(translatedName);
            nameNodeAsTextNode->ResizeNodeForCurrentText();
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error($"Error setting name node text in SetTranslationToAddon: {ex}");
        }
      }

      if (messageNodeAsTextNode != null)
      {
        var messageTextFromNode = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr));

        PluginLog.Information($"Addon {this.addonName} message node text in SetTranslationToAddon: {messageTextFromNode}");
        try
        {
          var translatedMessage = this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage + TranslationMarker;

          PluginLog.Information($"Addon {this.addonName} trasnslatedMessage node text in SetTranslationToAddon: {translatedMessage}");

          PluginLog.Information($"Comparison to SetTranslationToAddon: '!translatedMessage.Contains(TranslationMarker)' is {!messageTextFromNode.Contains(TranslationMarker)} and the result is {!messageTextFromNode.Contains(TranslationMarker)}");
          if (!messageTextFromNode.Contains(TranslationMarker))
          {
            PluginLog.Warning($"Setting message node text in SetTranslationToAddon.");

            messageNodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[this.addonCharacteristicsInfo.MessageNodeId];
            messageNodeAsTextNode->SetText(translatedMessage);
            messageNodeAsTextNode->ResizeNodeForCurrentText();
          }
        }
        catch (Exception ex)
        {
          PluginLog.Error($"Error setting message node text in SetTranslationToAddon: {ex}");
        }
      }
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!this.disposedValue)
      {
        if (disposing)
        {
          this.cts.Cancel();

          try
          {
            // Wait for the task to complete within a reasonable time frame.
            this.translationTask.Wait(5000); // Adjust timeout as needed.
          }
          catch (AggregateException ae)
          {
            // Handle or log exceptions that may occur when waiting for the task.
            foreach (var ex in ae.InnerExceptions)
            {
              PluginLog.Error($"Exception in Dispose method: {ex}");
            }
          }
          finally
          {
            // Dispose of the task and the cancellation token source.
            this.translationTask.Dispose();
            this.cts.Dispose();
          }
        }

        this.disposedValue = true;
      }
    }

    public void Dispose()
    {
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    private class TranslationEntry
    {
      public string OriginalText { get; set; }

      public string TranslatedText { get; set; }

      public bool IsTranslated { get; set; }

      public override string ToString()
      {
        return $"OriginalText: {this.OriginalText}, TranslatedText: {this.TranslatedText}, IsTranslated: {this.IsTranslated}";
      }
    }

    private class AddonCharacteristicsInfo
    {
      public string AddonName { get; set; }

      public bool IsComplexAddon { get; set; }

      public int NameNodeId { get; set; }

      public int MessageNodeId { get; set; }

      public string ComplexStructure { get; set; }

      public TalkMessage TalkMessage { get; set; }

      public BattleTalkMessage BattleTalkMessage { get; set; }

      public TalkSubtitleMessage TalkSubtitleMessage { get; set; }

      public override string ToString()
      {
        return $"AddonName: {this.AddonName}, IsComplexAddon: {this.IsComplexAddon}, NameNodeId: {this.NameNodeId}, MessageNodeId: {this.MessageNodeId}, ComplexStructure: {this.ComplexStructure}, TalkMessage: {this.TalkMessage}, BattleTalkMessage: {this.BattleTalkMessage}, TalkSubtitleMessage: {this.TalkSubtitleMessage}";
      }
    }
  }
}
