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
  /// <summary>
  /// Handles the UI Addons for translation purposes.
  /// </summary>
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
    private Dictionary<int, NodeState> nodeStates;
    private bool databaseChecked = false;
    private bool isInitialized = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAddonHandler"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="uiFont">The UI font.</param>
    /// <param name="fontLoaded">Indicates whether the font is loaded.</param>
    /// <param name="langToTranslateTo">The language to translate to.</param>
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
      this.nodeStates = new Dictionary<int, NodeState>();

      this.cts = new CancellationTokenSource();
      this.translationTask = Task.Run(async () => await this.ProcessTranslations(this.cts.Token));
    }

    /// <summary>
    /// Handles the addon for translation purposes.
    /// </summary>
    /// <param name="addonName">Name of the addon.</param>
    public void EgloAddonHandler(string addonName)
    {
      // Ensure initialization logic runs only once
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

    /// <summary>
    /// Determines the characteristics of the addon.
    /// </summary>
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

    /// <summary>
    /// Adjusts the addon nodes flags.
    /// </summary>
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

    /// <summary>
    /// Explores the addon to extract text nodes and manage translations.
    /// </summary>
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
      this.databaseChecked = false;

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

            if (!this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = textFromNode;
            }
          }
          else if (this.addonName == "_BattleTalk")
          {
            Echoglossian.PluginLog.Information($"Text from Node in ExploreAddon _BattleTalk NameNode: {textFromNode}");
            this.addonCharacteristicsInfo.BattleTalkMessage.SenderName = textFromNode;

            if (!this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = textFromNode;
            }
          }

          // Translate NPC names if enabled
          if (this.configuration.TranslateNpcNames)
          {
            this.TranslateSenderName(textFromNode, i, this.addonName);
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

        // Initialize node state if not already present
        if (!this.nodeStates.ContainsKey(i))
        {
          this.nodeStates[i] = new NodeState();
        }

        // Mark node as explored
        this.nodeStates[i].OriginalTextExtracted = true;
      }

      Echoglossian.PluginLog.Information($"Addon {this.addonName} explored and the data generated is: \n{this.addonCharacteristicsInfo.ToString()}");
      this.CheckDatabaseForTranslation();
    }

    /// <summary>
    /// Translates the sender name if the configuration allows it.
    /// </summary>
    /// <param name="senderName">The sender name to be translated.</param>
    /// <param name="nodeId">The node ID.</param>
    /// <param name="addonType">The type of addon.</param>
    private void TranslateSenderName(string senderName, int nodeId, string addonType)
    {
      Task.Run(async () =>
      {
        var translation = await this.translationService.TranslateAsync(senderName, this.clientLanguage.Humanize(), this.langToTranslateTo);

        if (addonType == "Talk")
        {
          this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = translation;
        }
        else if (addonType == "_BattleTalk")
        {
          this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = translation;
        }

        this.SetTranslationToAddon();
      });
    }

    /// <summary>
    /// Checks the database for existing translations.
    /// </summary>
    private void CheckDatabaseForTranslation()
    {
      if (this.databaseChecked)
      {
        return;
      }

      if (this.addonName == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;

        Echoglossian.PluginLog.Warning($"Checking database for translation: {this.addonName}, TalkMessage: {talkMessage} ");

        if (talkMessage != null && !string.IsNullOrEmpty(talkMessage.SenderName) && !string.IsNullOrEmpty(talkMessage.OriginalTalkMessage))
        {
          if (Echoglossian.FindTalkMessage(talkMessage))
          {
            Echoglossian.PluginLog.Warning($"Found talk message in CheckDatabaseForTranslation: {Echoglossian.FoundTalkMessage}");
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

        Echoglossian.PluginLog.Warning($"Checking database for translation: {this.addonName}, BattleTalkMessage: {battleTalkMessage} ");

        if (battleTalkMessage != null && !string.IsNullOrEmpty(battleTalkMessage.SenderName) && !string.IsNullOrEmpty(battleTalkMessage.OriginalBattleTalkMessage))
        {
          if (Echoglossian.FindBattleTalkMessage(battleTalkMessage))
          {
            Echoglossian.PluginLog.Warning($"Found battle talk message in CheckDatabaseForTranslation: {Echoglossian.FoundBattleTalkMessage}");
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

      this.databaseChecked = true;
    }

    /// <summary>
    /// Translates the texts.
    /// </summary>
    /// <param name="originalText">The original text to translate.</param>
    /// <param name="addonType">The type of addon.</param>
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

    /// <summary>
    /// Saves the translation to the database.
    /// </summary>
    /// <param name="originalText">The original text.</param>
    /// <param name="translatedText">The translated text.</param>
    /// <param name="addonType">The type of addon.</param>
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

    /// <summary>
    /// Processes the translations asynchronously.
    /// </summary>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Translates the text for a specific node.
    /// </summary>
    /// <param name="id">The node ID.</param>
    /// <param name="text">The text to translate.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Sets the translations to the addon text nodes.
    /// </summary>
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

          if (!this.nodeStates.ContainsKey(i))
          {
            continue;
          }

          var nodeState = this.nodeStates[i];

          if (nodeState.TranslationSet)
          {
            continue;
          }

          if (Echoglossian.FoundTalkMessage != null && Echoglossian.FoundTalkMessage.TranslatedTalkMessage == textFromNode)
          {
            nodeState.TranslationSet = true;
            continue;
          }

          if (this.addonCharacteristicsInfo.NameNodeId == i)
          {
            if (this.configuration.TranslateNpcNames && Echoglossian.FoundTalkMessage.TranslatedSenderName != string.Empty)
            {
              Echoglossian.PluginLog.Warning($"Text from NodeID {i} in SetTranslationToAddon: {textFromNode}, translation: {Echoglossian.FoundTalkMessage.TranslatedSenderName}");
              nodeAsTextNode->SetText(Echoglossian.FoundTalkMessage.TranslatedSenderName);
              nodeAsTextNode->ResizeNodeForCurrentText();
            }
            else if (!this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = this.addonCharacteristicsInfo.TalkMessage.SenderName;
            }

            nodeState.TranslationSet = true;
            continue;
          }

          if (this.addonCharacteristicsInfo.MessageNodeId == i)
          {
            if (textFromNode != string.Empty && textFromNode != Echoglossian.FoundTalkMessage.TranslatedTalkMessage && Echoglossian.FoundTalkMessage.TranslatedTalkMessage != string.Empty)
            {
              Echoglossian.PluginLog.Warning($"Text from NodeID {i} in SetTranslationToAddon: {textFromNode}, translation: {Echoglossian.FoundTalkMessage.TranslatedTalkMessage}");

              nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
              nodeAsTextNode->SetText(Echoglossian.FoundTalkMessage.TranslatedTalkMessage);
              nodeAsTextNode->ResizeNodeForCurrentText();
            }

            nodeState.TranslationSet = true;
            continue;
          }

          if (Echoglossian.FoundBattleTalkMessage != null && Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage == textFromNode)
          {
            nodeState.TranslationSet = true;
            continue;
          }

          if (this.addonCharacteristicsInfo.NameNodeId == i)
          {
            if (this.configuration.TranslateNpcNames && Echoglossian.FoundBattleTalkMessage.TranslatedSenderName != string.Empty)
            {
              Echoglossian.PluginLog.Warning($"Text from NodeID {i} in SetTranslationToAddon: {textFromNode}, translation: {Echoglossian.FoundBattleTalkMessage.TranslatedSenderName}");
              nodeAsTextNode->SetText(Echoglossian.FoundBattleTalkMessage.TranslatedSenderName);
              nodeAsTextNode->ResizeNodeForCurrentText();
            }
            else if (!this.configuration.TranslateNpcNames)
            {
              this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = this.addonCharacteristicsInfo.BattleTalkMessage.SenderName;
            }

            nodeState.TranslationSet = true;
            continue;
          }

          if (this.addonCharacteristicsInfo.MessageNodeId == i)
          {
            if (textFromNode != string.Empty && textFromNode != Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage && Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage != string.Empty)
            {
              Echoglossian.PluginLog.Warning($"Text from NodeID {i} in SetTranslationToAddon: {textFromNode}, translation: {Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage}");

              nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
              nodeAsTextNode->SetText(Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage);
              nodeAsTextNode->ResizeNodeForCurrentText();
            }

            nodeState.TranslationSet = true;
            continue;
          }

          if (this.translations.TryGetValue(i, out var entry) && entry.IsTranslated)
          {
            if (entry.TranslatedText == textFromNode)
            {
              nodeState.TranslationSet = true;
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

                nodeState.TranslationSet = true;
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

                nodeState.TranslationSet = true;
                continue;
              }

              nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
              nodeAsTextNode->SetText(sanitizedText);
              nodeAsTextNode->ResizeNodeForCurrentText();

              this.translations.TryRemove(i, out _);
              nodeState.TranslationSet = true;
            }
          }
        }
      });
    }

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="disposing">If set to <c>true</c> release managed resources.</param>
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

    /// <summary>
    /// Disposes the instance and suppresses finalization.
    /// </summary>
    public void Dispose()
    {
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Represents a translation entry.
    /// </summary>
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

    /// <summary>
    /// Represents the characteristics of an addon.
    /// </summary>
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

    /// <summary>
    /// Represents the state of a node.
    /// </summary>
    private class NodeState
    {
      public bool OriginalTextExtracted { get; set; } = false;

      public bool TranslationSet { get; set; } = false;

      public override string ToString()
      {
        return $"OriginalTextExtracted: {this.OriginalTextExtracted}, TranslationSet: {this.TranslationSet}";
      }
    }
  }
}
