using ImGuiNET;

using System;
using System.Threading;
using System.Threading.Tasks;

using FFXIVClientStructs.FFXIV.Component.GUI;

using static Echoglossian.Echoglossian;

using Dalamud.Memory;
using Humanizer;
using Dalamud;
using Dalamud.Game.Text.Sanitizer;

using System.Collections.Concurrent;
using System.Collections.Generic;

using Echoglossian.EFCoreSqlite.Models;

using System.IO;

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
      var addon = GameGui.GetAddonByName(this.addonName, 1);
      var foundAddon = (AtkUnitBase*)addon;

      if (foundAddon == null)
      {
        return;
      }

      this.isAddonVisible = foundAddon->IsVisible;

      if (!this.isAddonVisible)
      {
        return;
      }

      this.translations.Clear();

      var nodesQuantity = foundAddon->UldManager.NodeListCount;

      for (var i = 0; i < nodesQuantity; i++)
      {
        var node = foundAddon->GetNodeById((uint)i);

        if (node == null || node->Type != NodeType.Text)
        {
          continue;
        }

        var nodeAsTextNode = node->GetAsAtkTextNode();
        if (nodeAsTextNode == null)
        {
          continue;
        }

        var textFromNode = Echoglossian.CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr));

        if (string.IsNullOrEmpty(textFromNode))
        {
          continue;
        }

        if (this.translatedTexts.Contains(textFromNode))
        {
          Echoglossian.PluginLog.Information($"Skipping already translated text: {textFromNode}");
          continue;
        }

        if (this.addonCharacteristicsInfo.NameNodeId == i)
        {
          Echoglossian.PluginLog.Information($"Text from Node in ExploreAddon NameNode: {textFromNode}");

          if (this.addonName == "Talk")
          {
            Echoglossian.PluginLog.Information($"Text from Node in ExploreAddon Talk NameNode: {textFromNode}");
            this.addonCharacteristicsInfo.TalkMessage.SenderName = textFromNode;
          }
          else if (this.addonName == "_BattleTalk")
          {
            Echoglossian.PluginLog.Information($"Text from Node in ExploreAddon _BattleTalk NameNode: {textFromNode}");
            this.addonCharacteristicsInfo.BattleTalkMessage.SenderName = textFromNode;
          }
        }

        if (this.addonCharacteristicsInfo.MessageNodeId == i)
        {
          Echoglossian.PluginLog.Information($"Text from Node in ExploreAddon messageNode: {textFromNode}");

          if (this.addonName == "Talk")
          {
            this.addonCharacteristicsInfo.TalkMessage.OriginalTalkMessage = textFromNode;
          }
          else if (this.addonName == "_BattleTalk")
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.OriginalBattleTalkMessage = textFromNode;
          }
        }
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
            if (!string.IsNullOrEmpty(Echoglossian.FoundTalkMessage.TranslatedTalkMessage))
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage = Echoglossian.FoundTalkMessage.TranslatedTalkMessage;
            }

            if (!string.IsNullOrEmpty(Echoglossian.FoundTalkMessage.TranslatedSenderName))
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = Echoglossian.FoundTalkMessage.TranslatedSenderName;
            }

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
            if (!string.IsNullOrEmpty(Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage))
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage = Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage;
            }

            if (!string.IsNullOrEmpty(Echoglossian.FoundBattleTalkMessage.TranslatedSenderName))
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = Echoglossian.FoundBattleTalkMessage.TranslatedSenderName;
            }

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
      Echoglossian.PluginLog.Information($"Setting translation to addon: {this.addonName}");

      Framework.RunOnTick(() =>
      {
        var addon = GameGui.GetAddonByName(this.addonName, 1);
        var foundAddon = (AtkUnitBase*)addon;

        if (foundAddon == null || !foundAddon->IsVisible)
        {
          return;
        }

        var nodesQuantity = foundAddon->UldManager.NodeListCount;

        for (var i = 0; i < nodesQuantity; i++)
        {
          var node = foundAddon->GetNodeById((uint)i);

          if (node == null || node->Type != NodeType.Text)
          {
            continue;
          }

          var nodeAsTextNode = node->GetAsAtkTextNode();
          if (nodeAsTextNode == null)
          {
            continue;
          }

          var textFromNode = Echoglossian.CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr));

          if (string.IsNullOrEmpty(textFromNode))
          {
            continue;
          }

          if (Echoglossian.FoundTalkMessage != null && Echoglossian.FoundTalkMessage.TranslatedTalkMessage == textFromNode)
          {
            continue;
          }
          else
          {
            Echoglossian.PluginLog.Error($"this.addonCharacteristicsInfo.Talkmessage if from DB: {this.addonCharacteristicsInfo.TalkMessage}");
            Echoglossian.PluginLog.Warning($"this.addonCharacteristicsInfo.BattleTalkMessage if from DB: {this.addonCharacteristicsInfo.BattleTalkMessage}");

            if (this.addonCharacteristicsInfo.NameNodeId == i)
            {
              if (textFromNode != string.Empty && textFromNode != Echoglossian.FoundTalkMessage.TranslatedSenderName)
              {
                Echoglossian.PluginLog.Warning($"Setting translation to addon NameNode: {Echoglossian.FoundTalkMessage.TranslatedSenderName}");
                nodeAsTextNode->SetText(Echoglossian.FoundTalkMessage.TranslatedSenderName);
                nodeAsTextNode->ResizeNodeForCurrentText();
              }

              continue;
            }

            if (this.addonCharacteristicsInfo.MessageNodeId == i)
            {
              if (textFromNode != string.Empty && textFromNode != Echoglossian.FoundTalkMessage.TranslatedTalkMessage)
              {
                Echoglossian.PluginLog.Warning($"Setting translation to addon MessageNode: {Echoglossian.FoundTalkMessage.TranslatedTalkMessage}");
                nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
                nodeAsTextNode->SetText(Echoglossian.FoundTalkMessage.TranslatedTalkMessage);
                nodeAsTextNode->ResizeNodeForCurrentText();
              }

              continue;
            }
          }

          if (Echoglossian.FoundBattleTalkMessage != null && Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage == textFromNode)
          {
            continue;
          }
          else
          {
            Echoglossian.PluginLog.Error($"this.addonCharacteristicsInfo.Talkmessage if from DB: {this.addonCharacteristicsInfo.TalkMessage}");
            Echoglossian.PluginLog.Warning($"this.addonCharacteristicsInfo.BattleTalkMessage if from DB: {this.addonCharacteristicsInfo.BattleTalkMessage}");

            if (this.addonCharacteristicsInfo.NameNodeId == i)
            {
              if (textFromNode != string.Empty && textFromNode != Echoglossian.FoundBattleTalkMessage.TranslatedSenderName)
              {
                Echoglossian.PluginLog.Warning($"Setting translation to addon NameNode: {Echoglossian.FoundBattleTalkMessage.TranslatedSenderName}");
                nodeAsTextNode->SetText(Echoglossian.FoundBattleTalkMessage.TranslatedSenderName);
                nodeAsTextNode->ResizeNodeForCurrentText();
              }

              continue;
            }

            if (this.addonCharacteristicsInfo.MessageNodeId == i)
            {
              if (textFromNode != string.Empty && textFromNode != Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage)
              {
                Echoglossian.PluginLog.Warning($"Setting translation to addon MessageNode: {Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage}");
                nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
                nodeAsTextNode->SetText(Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage);
                nodeAsTextNode->ResizeNodeForCurrentText();
              }

              continue;
            }
          }

          if (this.translations.TryGetValue(i, out var entry) && entry.IsTranslated)
          {
            if (entry.TranslatedText == textFromNode)
            {
              continue;
            }

            if (entry.OriginalText == textFromNode)
            {
              var sanitizedText = Echoglossian.CleanString(entry.TranslatedText);

              Echoglossian.PluginLog.Error($"this.addonCharacteristicsInfo.Talkmessage if translating: {this.addonCharacteristicsInfo.TalkMessage}");
              Echoglossian.PluginLog.Warning($"this.addonCharacteristicsInfo.BattleTalkMessage if translating: {this.addonCharacteristicsInfo.BattleTalkMessage}");

              if (this.addonCharacteristicsInfo.NameNodeId == i)
              {
                if (textFromNode != string.Empty && textFromNode != sanitizedText)
                {
                  Echoglossian.PluginLog.Information($"Setting translation to addon NameNode if translating: {sanitizedText}");
                  nodeAsTextNode->SetText(sanitizedText);
                  nodeAsTextNode->ResizeNodeForCurrentText();
                }

                continue;
              }

              if (this.addonCharacteristicsInfo.MessageNodeId == i)
              {
                if (textFromNode != string.Empty && textFromNode != sanitizedText)
                {
                  Echoglossian.PluginLog.Information($"Setting translation to addon MessageNode if translating: {sanitizedText}");
                  nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
                  nodeAsTextNode->SetText(sanitizedText);
                  nodeAsTextNode->ResizeNodeForCurrentText();
                }

                continue;
              }

              nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
              nodeAsTextNode->SetText(sanitizedText);
              nodeAsTextNode->ResizeNodeForCurrentText();

              this.translations.TryRemove(i, out _);
            }
          }
        }
      });
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
    }
  }
}
