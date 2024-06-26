using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Dalamud;
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
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving name node: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);
        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving message node: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        var nameText = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nameNodeAsTextNode->NodeText.StringPtr));

        if (!string.IsNullOrEmpty(nameText) && nameText.Contains(TranslationMarker))
        {
          Echoglossian.PluginLog.Information($"Addon {this.addonName} name node has already been processed.");
          return;
        }

        this.addonCharacteristicsInfo.TalkMessage.SenderName = nameText;
      }

      if (messageNodeAsTextNode != null)
      {
        var messageText = CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)messageNodeAsTextNode->NodeText.StringPtr));

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
            this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = Echoglossian.FoundTalkMessage.TranslatedSenderName + TranslationMarker;
            this.SetTranslationToAddon();
          }
          else
          {
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
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = Echoglossian.FoundBattleTalkMessage.TranslatedSenderName + TranslationMarker;
            this.SetTranslationToAddon();
          }
          else
          {
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
          talkMessage.TranslatedTalkMessage = translatedText + TranslationMarker;
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
          battleTalkMessage.TranslatedBattleTalkMessage = translatedText + TranslationMarker;
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

        await Task.Delay(100, token);
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
      AtkUnitBase* foundAddon = null;

      try
      {
        var addon = GameGui.GetAddonByName(this.addonName, 1);
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
        if (!this.isAddonVisible)
        {
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
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving name node: {ex}");
      }

      try
      {
        var messageNode = foundAddon->GetNodeById((uint)this.addonCharacteristicsInfo.MessageNodeId);
        messageNodeAsTextNode = messageNode->GetAsAtkTextNode();
      }
      catch (Exception ex)
      {
        Echoglossian.PluginLog.Error($"Error retrieving message node: {ex}");
      }

      if (nameNodeAsTextNode != null)
      {
        try
        {
          var translatedName = this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName + TranslationMarker;
          nameNodeAsTextNode->SetText(translatedName);
          nameNodeAsTextNode->ResizeNodeForCurrentText();
        }
        catch (Exception ex)
        {
          Echoglossian.PluginLog.Error($"Error setting name node text: {ex}");
        }
      }

      if (messageNodeAsTextNode != null)
      {
        try
        {
          var translatedMessage = this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage + TranslationMarker;
          messageNodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[this.addonCharacteristicsInfo.MessageNodeId];
          messageNodeAsTextNode->SetText(translatedMessage);
          messageNodeAsTextNode->ResizeNodeForCurrentText();
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
