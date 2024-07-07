using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game;
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
      this.translationService = new TranslationService(configuration, Echoglossian.PluginLog, new Sanitizer(this.clientLanguage));
      this.translations = new ConcurrentDictionary<int, TranslationEntry>();
      this.configDir = Echoglossian.PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;
      this.cts = new CancellationTokenSource();
      this.translationTask = Task.Run(async () => await this.ProcessTranslations(this.cts.Token));
    }

    public void EgloAddonHandler(string addonName)
    {
      this.addonName = addonName;

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
          Echoglossian.PluginLog.Information($"Addon {this.addonName} not found.");
          return;
        }
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving addon: {ex}");
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
        Echoglossian.PluginLog.Error($"Error checking addon visibility: {ex}");
        return;
      }

      AtkTextNode* nameNodeAsTextNode = null;
      AtkTextNode* messageNodeAsTextNode = null;

      try
      {
        var nameNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.NameNodeId);
        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        Echoglossian.PluginLog.Information($"Addon {this.addonName} name node found.");
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving name node: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);
        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        Echoglossian.PluginLog.Information($"Addon {this.addonName} message node found.");
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving message node: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        var nameText = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr));

        Echoglossian.PluginLog.Information($"Addon {this.addonName} name node text: {nameText}");

        if (!string.IsNullOrEmpty(nameText) && nameText.Contains(TranslationMarker))
        {
          Echoglossian.PluginLog.Information($"Addon {this.addonName} name node has already been processed.");
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

        Echoglossian.PluginLog.Information($"Addon {this.addonName} message node text: {messageText}");

        if (!string.IsNullOrEmpty(messageText) && messageText.Contains(TranslationMarker))
        {
          Echoglossian.PluginLog.Information($"Addon {this.addonName} message node has already been processed.");
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

        if (talkMessage != null && !string.IsNullOrEmpty(talkMessage.SenderName) && !string.IsNullOrEmpty(talkMessage.OriginalTalkMessage))
        {
          if (Echoglossian.FindTalkMessage(talkMessage))
          {
            this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = Echoglossian.FoundTalkMessage.TranslatedTalkMessage + TranslationMarker;

            Echoglossian.PluginLog.Information($"Addon {this.addonName} message node text found in database is {this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage}");
            if (this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = Echoglossian.FoundTalkMessage.TranslatedSenderName + TranslationMarker;
            }
            else
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = Echoglossian.FoundTalkMessage.SenderName + TranslationMarker;
            }

            this.SetTranslationToAddon();
          }
          else
          {
            Echoglossian.PluginLog.Information($"Addon {this.addonName} message node text not found in database.");
            this.TranslateTexts(talkMessage.OriginalTalkMessage, "Talk");
          }
        }
      }
      else if (this.addonName == "_BattleTalk")
      {
        var battleTalkMessage = this.addonCharacteristicsInfo.BattleTalkMessage;

        if (battleTalkMessage != null && !string.IsNullOrEmpty(battleTalkMessage.SenderName) && !string.IsNullOrEmpty(battleTalkMessage.OriginalBattleTalkMessage))
        {
          if (Echoglossian.FindBattleTalkMessage(battleTalkMessage))
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage + TranslationMarker;

            Echoglossian.PluginLog.Information($"Addon {this.addonName} message node text found in database is {this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage}");
            if (this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = Echoglossian.FoundBattleTalkMessage.TranslatedSenderName + TranslationMarker;
            }
            this.SetTranslationToAddon();
          }
          else
          {
            Echoglossian.PluginLog.Information($"Addon {this.addonName} message node text not found in database.");
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
        Echoglossian.PluginLog.Warning($"Translation for addon {this.addonName} added to translations dictionary.");

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
            Echoglossian.InsertTalkData(talkMessage);
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
            Echoglossian.InsertBattleTalkData(battleTalkMessage);
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

        /*await Task.Delay(100, token);*/
        await Task.Yield(); // Remove Task.Delay and yield to other tasks.
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
        Echoglossian.PluginLog.Error($"Error in TranslateText method: {e}");
      }
    }

    private unsafe void SetTranslationToAddon()
    {
      Echoglossian.PluginLog.Information($"Called SetTranslationToAddon for addon {this.addonName}.");
      AtkUnitBase* foundAddon = null;

      try
      {
        var addon = GameGui.GetAddonByName(this.addonName, 1);

        Echoglossian.PluginLog.Information($"Addon {this.addonName} found.");
        foundAddon = (AtkUnitBase*)addon;
        if (foundAddon == null)
        {
          return;
        }
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving addon: {ex}");
        return;
      }

      try
      {
        this.isAddonVisible = foundAddon->IsVisible;

        Echoglossian.PluginLog.Information($"Addon {this.addonName} is visible: {this.isAddonVisible} in SetTranslationToAddon.");
        if (!this.isAddonVisible)
        {
          Echoglossian.PluginLog.Information($"Addon {this.addonName} is not visible in SetTranslationToAddon.");
          return;
        }
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error checking addon visibility in SetTranslationToAddon: {ex}");
        return;
      }

      AtkTextNode* nameNodeAsTextNode = null;
      AtkTextNode* messageNodeAsTextNode = null;

      try
      {
        var nameNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.NameNodeId);
        nameNodeAsTextNode = nameNode->GetAsAtkTextNode();

        Echoglossian.PluginLog.Information($"Addon {this.addonName} name node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving name node in SetTranslationToAddon: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);
        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();

        Echoglossian.PluginLog.Information($"Addon {this.addonName} message node found in SetTranslationToAddon.");
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving message node in SetTranslationToAddon: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        var nameTextFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr);

        Echoglossian.PluginLog.Information($"Addon {this.addonName} name node text in SetTranslationToAddon: {nameTextFromNode}");
        try
        {
          var translatedName = this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName + TranslationMarker;

          Echoglossian.PluginLog.Information($"Addon {this.addonName} translatedName node text in SetTranslationToAddon: {translatedName}");

          Echoglossian.PluginLog.Information($"Comparison to SetTranslationToAddon: 'this.configuration.TranslateNpcNames' is {this.configuration.TranslateNpcNames} and '!translatedName.Contains(TranslationMarker)' is {nameTextFromNode.Contains(TranslationMarker)} and the result is {this.configuration.TranslateNpcNames && nameTextFromNode.Contains(TranslationMarker)}");
          if (this.configuration.TranslateNpcNames && !nameTextFromNode.Contains(TranslationMarker))
          {
            Echoglossian.PluginLog.Warning($"Setting name node text in SetTranslationToAddon.");
            nameNodeAsTextNode->SetText(translatedName);
            nameNodeAsTextNode->ResizeNodeForCurrentText();
          }

        }
        catch (Exception ex)
        {
          Echoglossian.PluginLog.Error($"Error setting name node text in SetTranslationToAddon: {ex}");
        }
      }

      if (messageNodeAsTextNode != null)
      {
        var messageTextFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr);

        Echoglossian.PluginLog.Information($"Addon {this.addonName} message node text in SetTranslationToAddon: {messageTextFromNode}");
        try
        {
          var translatedMessage = this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage + TranslationMarker;

          Echoglossian.PluginLog.Information($"Addon {this.addonName} trasnslatedMessage node text in SetTranslationToAddon: {translatedMessage}");

          Echoglossian.PluginLog.Information($"Comparison to SetTranslationToAddon: '!translatedMessage.Contains(TranslationMarker)' is {!messageTextFromNode.Contains(TranslationMarker)} and the result is {!messageTextFromNode.Contains(TranslationMarker)}");
          if (!messageTextFromNode.Contains(TranslationMarker))
          {
            Echoglossian.PluginLog.Warning($"Setting message node text in SetTranslationToAddon.");

            messageNodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[this.addonCharacteristicsInfo.MessageNodeId];
            messageNodeAsTextNode->SetText(translatedMessage);
            messageNodeAsTextNode->ResizeNodeForCurrentText();
          }

        }
        catch (Exception ex)
        {
          Echoglossian.PluginLog.Error($"Error setting message node text: {ex}");
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
          this.translationTask.Wait();

          this.cts.Dispose();
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
