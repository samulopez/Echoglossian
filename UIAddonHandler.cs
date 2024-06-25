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
    private bool isInitialized = false;

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
      if (!this.isInitialized)
      {
        this.addonName = addonName;
        if (string.IsNullOrEmpty(this.addonName))
        {
          return;
        }
        this.DetermineAddonCharacteristics();
        this.AdjustAddonNodesFlags();
        this.isInitialized = true;
      }
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
      var addon = GameGui.GetAddonByName(this.addonName, 1);
      var foundAddon = (AtkUnitBase*)addon;
      if (foundAddon == null)
      {
        return;
      }

      if (!foundAddon->IsVisible) { return; }

      this.isAddonVisible = true;
      this.translations.Clear();

      this.ProcessNode(foundAddon, this.addonCharacteristicsInfo.NameNodeId, true);
      this.ProcessNode(foundAddon, this.addonCharacteristicsInfo.MessageNodeId, false);

      Echoglossian.PluginLog.Information($"Addon {this.addonName} explored and the data generated is: \n{this.addonCharacteristicsInfo}");
      this.CheckDatabaseForTranslation();
    }

    private unsafe void ProcessNode(AtkUnitBase* foundAddon, int nodeId, bool isNameNode)
    {
      var node = foundAddon->GetNodeById((uint)nodeId);
      if (node == null || node->Type != NodeType.Text)
      {
        return;
      }

      var nodeAsTextNode = node->GetAsAtkTextNode();
      if (nodeAsTextNode == null)
      {
        return;
      }

      var textFromNode = Echoglossian.CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr));

      if (string.IsNullOrEmpty(textFromNode) || this.translatedTexts.Contains(textFromNode))
      {
        Echoglossian.PluginLog.Information($"Skipping already translated text: {textFromNode}");
        return;
      }

      if (isNameNode)
      {
        this.SetSenderName(textFromNode);
      }
      else
      {
        this.SetOriginalMessage(textFromNode);
      }
    }

    private void SetSenderName(string textFromNode)
    {
      if (this.addonName == "Talk")
      {
        this.addonCharacteristicsInfo.TalkMessage.SenderName = textFromNode;
        if (!this.configuration.TranslateNpcNames)
        {
          this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = textFromNode;
        }
      }
      else if (this.addonName == "_BattleTalk")
      {
        this.addonCharacteristicsInfo.BattleTalkMessage.SenderName = textFromNode;
        if (!this.configuration.TranslateNpcNames)
        {
          this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = textFromNode;
        }
      }

      if (this.configuration.TranslateNpcNames)
      {
        this.TranslateSenderName(textFromNode);
      }
    }

    private void SetOriginalMessage(string textFromNode)
    {
      if (this.addonName == "Talk")
      {
        this.addonCharacteristicsInfo.TalkMessage.OriginalTalkMessage = textFromNode;
      }
      else if (this.addonName == "_BattleTalk")
      {
        this.addonCharacteristicsInfo.BattleTalkMessage.OriginalBattleTalkMessage = textFromNode;
      }
    }

    private void TranslateSenderName(string senderName)
    {
      Task.Run(async () =>
      {
        var translation = await this.translationService.TranslateAsync(senderName, this.clientLanguage.Humanize(), this.langToTranslateTo);
        if (this.addonName == "Talk")
        {
          this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = translation;
        }
        else if (this.addonName == "_BattleTalk")
        {
          this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = translation;
        }
        this.SetTranslationToAddon();
      });
    }

    private void CheckDatabaseForTranslation()
    {
      if (this.addonName == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;
        if (Echoglossian.FindTalkMessage(talkMessage))
        {
          this.addonCharacteristicsInfo.TalkMessage = Echoglossian.FoundTalkMessage;
          this.SetTranslationToAddon();
        }
        else
        {
          this.TranslateTexts(talkMessage.OriginalTalkMessage, "Talk");
        }
      }
      else if (this.addonName == "_BattleTalk")
      {
        var battleTalkMessage = this.addonCharacteristicsInfo.BattleTalkMessage;
        if (Echoglossian.FindBattleTalkMessage(battleTalkMessage))
        {
          this.addonCharacteristicsInfo.BattleTalkMessage = Echoglossian.FoundBattleTalkMessage;
          this.SetTranslationToAddon();
        }
        else
        {
          this.TranslateTexts(battleTalkMessage.OriginalBattleTalkMessage, "_BattleTalk");
        }
      }
    }

    private void TranslateTexts(string originalText, string addonType)
    {
      Task.Run(async () =>
      {
        var translation = await this.translationService.TranslateAsync(originalText, this.clientLanguage.Humanize(), this.langToTranslateTo);
        if (addonType == "Talk")
        {
          this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = translation;
          this.SaveTranslationToDatabase(this.addonCharacteristicsInfo.TalkMessage);
        }
        else if (addonType == "_BattleTalk")
        {
          this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = translation;
          this.SaveTranslationToDatabase(this.addonCharacteristicsInfo.BattleTalkMessage);
        }
        this.translatedTexts.Add(translation);
        this.translations.TryAdd(this.addonCharacteristicsInfo.MessageNodeId, new TranslationEntry { OriginalText = originalText, TranslatedText = translation, IsTranslated = true });
        this.SetTranslationToAddon();
      });
    }

    private void SaveTranslationToDatabase(TalkMessage talkMessage)
    {
      if (!this.translatedTexts.Contains(talkMessage.TranslatedTalkMessage))
      {
        Echoglossian.InsertTalkData(talkMessage);
        this.translatedTexts.Add(talkMessage.TranslatedTalkMessage);
      }
    }

    private void SaveTranslationToDatabase(BattleTalkMessage battleTalkMessage)
    {
      if (!this.translatedTexts.Contains(battleTalkMessage.TranslatedBattleTalkMessage))
      {
        Echoglossian.InsertBattleTalkData(battleTalkMessage);
        this.translatedTexts.Add(battleTalkMessage.TranslatedBattleTalkMessage);
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

          if (this.addonName == "Talk")
          {
            this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = translation;
            this.SaveTranslationToDatabase(this.addonCharacteristicsInfo.TalkMessage);
          }
          else if (this.addonName == "_BattleTalk")
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = translation;
            this.SaveTranslationToDatabase(this.addonCharacteristicsInfo.BattleTalkMessage);
          }
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in TranslateText method: {e}");
      }
    }

    private unsafe void SetTranslationToAddon()
    {
      var addon = GameGui.GetAddonByName(this.addonName, 1);
      var foundAddon = (AtkUnitBase*)addon;

      if (foundAddon == null || !foundAddon->IsVisible)
      {
        return;
      }

      this.SetNodeText(foundAddon, this.addonCharacteristicsInfo.NameNodeId, this.addonCharacteristicsInfo.TalkMessage?.TranslatedSenderName ?? this.addonCharacteristicsInfo.BattleTalkMessage?.TranslatedSenderName);
      this.SetNodeText(foundAddon, this.addonCharacteristicsInfo.MessageNodeId, this.addonCharacteristicsInfo.TalkMessage?.TranslatedTalkMessage ?? this.addonCharacteristicsInfo.BattleTalkMessage?.TranslatedBattleTalkMessage);

      foreach (var key in this.translations.Keys)
      {
        if (this.translations.TryGetValue(key, out var entry) && entry.IsTranslated)
        {
          this.SetNodeText(foundAddon, key, entry.TranslatedText);
          this.translations.TryRemove(key, out _);
        }
      }
    }

    private unsafe void SetNodeText(AtkUnitBase* addon, int nodeId, string text)
    {
      if (string.IsNullOrEmpty(text))
      {
        return;
      }

      var node = addon->GetNodeById((uint)nodeId);
      if (node == null || node->Type != NodeType.Text)
      {
        return;
      }

      var nodeAsTextNode = node->GetAsAtkTextNode();
      if (nodeAsTextNode == null)
      {
        return;
      }

      nodeAsTextNode->SetText(text);
      nodeAsTextNode->ResizeNodeForCurrentText();
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
