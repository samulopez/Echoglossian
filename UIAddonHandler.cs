using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
    private Dictionary<int, NodeState> nodeStates;
    private bool databaseChecked = false;
    private bool isInitialized = false;
    private static ConcurrentDictionary<string, bool> addonHashes = new ConcurrentDictionary<string, bool>();

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
      this.databaseChecked = false;

      // Directly access the known node IDs
      this.ProcessNode(foundAddon, this.addonCharacteristicsInfo.NameNodeId);
      this.ProcessNode(foundAddon, this.addonCharacteristicsInfo.MessageNodeId);

      Echoglossian.PluginLog.Information($"Addon {this.addonName} explored and the data generated is: \n{this.addonCharacteristicsInfo.ToString()}");
      this.CheckDatabaseForTranslation();
    }

    private unsafe void ProcessNode(AtkUnitBase* foundAddon, int nodeId)
    {
      var node = foundAddon->GetNodeById((uint)nodeId);

      if (node == null)
      {
        return;
      }

      if (node->Type != NodeType.Text)
      {
        return;
      }

      var nodeAsTextNode = node->GetAsAtkTextNode();
      if (nodeAsTextNode == null)
      {
        return;
      }

      var textFromNode = Echoglossian.CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr));

      if (string.IsNullOrEmpty(textFromNode))
      {
        return;
      }

      if (this.translatedTexts.Contains(textFromNode))
      {
        Echoglossian.PluginLog.Information($"Skipping already translated text: {textFromNode}");
        return;
      }

      if (nodeId == this.addonCharacteristicsInfo.NameNodeId)
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

        if (this.configuration.TranslateNpcNames)
        {
          this.TranslateSenderName(textFromNode, nodeId, this.addonName);
        }
      }

      if (nodeId == this.addonCharacteristicsInfo.MessageNodeId)
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

      if (!this.nodeStates.ContainsKey(nodeId))
      {
        this.nodeStates[nodeId] = new NodeState();
      }

      this.nodeStates[nodeId].OriginalTextExtracted = true;
    }

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

    private void CheckDatabaseForTranslation()
    {
      if (this.databaseChecked)
      {
        return;
      }

      if (this.addonName == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;

        Echoglossian.PluginLog.Warning($"Checking database for translation: {this.addonName}, TalkMessage: {talkMessage}");

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

        Echoglossian.PluginLog.Warning($"Checking database for translation: {this.addonName}, BattleTalkMessage: {battleTalkMessage}");

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

      var addon = GameGui.GetAddonByName(this.addonName, 1);
      var foundAddon = (AtkUnitBase*)addon;

      if (foundAddon == null)
      {
        return;
      }

      if (!foundAddon->IsVisible)
      {
        return;
      }

      this.SetNodeTranslation(foundAddon, this.addonCharacteristicsInfo.NameNodeId, true);
      this.SetNodeTranslation(foundAddon, this.addonCharacteristicsInfo.MessageNodeId, false);
    }

    private unsafe void SetNodeTranslation(AtkUnitBase* foundAddon, int nodeId, bool isNameNode)
    {

      AtkResNode* node = null;

      try
      {
        node = foundAddon->GetNodeById((uint)nodeId);
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in SetNodeTranslation method GetNodeById: {e}");
      }


      if (node == null)
      {
        return;
      }

      if (node->Type != NodeType.Text)
      {
        return;
      }

      AtkTextNode* nodeAsTextNode = null;

      try
      {
        nodeAsTextNode = node->GetAsAtkTextNode();
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in SetNodeTranslation method GetAsTextNode: {e}");
      }

      if (nodeAsTextNode == null)
      {
        return;
      }

      string textFromNode = string.Empty;
      try
      {
        textFromNode = Echoglossian.CleanString(MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr));
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in SetNodeTranslation method ReadSeStringAsString: {e}");
      }

      if (string.IsNullOrEmpty(textFromNode))
      {
        return;
      }

      if (!this.nodeStates.ContainsKey(nodeId))
      {
        return;
      }

      var nodeState = this.nodeStates[nodeId];

      if (nodeState.TranslationSet)
      {
        return;
      }

      if (isNameNode)
      {
        var translatedName = this.addonName == "Talk"
            ? this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName
            : this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName;

        if (this.configuration.TranslateNpcNames && !string.IsNullOrEmpty(translatedName) && textFromNode != translatedName)
        {
          Echoglossian.PluginLog.Warning($"Text from NodeID {nodeId} in SetTranslationToAddon: {textFromNode}, translation: {translatedName}");
          try
          {
            nodeAsTextNode->SetText(translatedName);
            nodeAsTextNode->ResizeNodeForCurrentText();
          }
          catch (Exception e)
          {
            Echoglossian.PluginLog.Error($"Error in SetNodeTranslation method SetText: {e}");
          }

        }
        else if (!this.configuration.TranslateNpcNames)
        {
          if (this.addonName == "Talk")
          {
            this.addonCharacteristicsInfo.TalkMessage.TranslatedSenderName = this.addonCharacteristicsInfo.TalkMessage.SenderName;
          }
          else if (this.addonName == "_BattleTalk")
          {
            this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedSenderName = this.addonCharacteristicsInfo.BattleTalkMessage.SenderName;
          }
        }

        nodeState.TranslationSet = true;
        return;
      }

      var translatedMessage = this.addonName == "Talk"
          ? this.addonCharacteristicsInfo.TalkMessage.TranslatedTalkMessage
          : this.addonCharacteristicsInfo.BattleTalkMessage.TranslatedBattleTalkMessage;

      if (!isNameNode)
      {
        if (textFromNode != string.Empty && textFromNode != translatedMessage && !string.IsNullOrEmpty(translatedMessage))
        {
          Echoglossian.PluginLog.Warning($"Text from NodeID {nodeId} in SetTranslationToAddon: {textFromNode}, translation: {translatedMessage}");

          try
          {
            nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[nodeId];
            nodeAsTextNode->SetText(translatedMessage);
            nodeAsTextNode->ResizeNodeForCurrentText();
          }
          catch (Exception e)
          {
            Echoglossian.PluginLog.Error($"Error in SetNodeTranslation method SetText SetMessageText: {e}");
          }
        }

        nodeState.TranslationSet = true;
        return;
      }

      if (this.translations.TryGetValue(nodeId, out var entry) && entry.IsTranslated)
      {
        if (entry.TranslatedText == textFromNode)
        {
          nodeState.TranslationSet = true;
          return;
        }

        if (entry.OriginalText == textFromNode)
        {
          var sanitizedText = Echoglossian.CleanString(entry.TranslatedText);

          if (nodeId == this.addonCharacteristicsInfo.NameNodeId)
          {
            if (textFromNode != string.Empty && textFromNode != sanitizedText)
            {
              Echoglossian.PluginLog.Information($"Setting translation to addon NameNode if translating: {sanitizedText}");
              try
              {
                nodeAsTextNode->SetText(sanitizedText);
                nodeAsTextNode->ResizeNodeForCurrentText();
              }
              catch (Exception e)
              {
                Echoglossian.PluginLog.Error($"Error in SetNodeTranslation method SetText SetNameText: {e}");
              }
            }

            nodeState.TranslationSet = true;
            return;
          }

          if (nodeId == this.addonCharacteristicsInfo.MessageNodeId)
          {
            if (textFromNode != string.Empty && textFromNode != sanitizedText)
            {
              Echoglossian.PluginLog.Information($"Setting translation to addon MessageNode if translating: {sanitizedText}");
              try
              {
                nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[nodeId];
                nodeAsTextNode->SetText(sanitizedText);
                nodeAsTextNode->ResizeNodeForCurrentText();
              }
              catch (Exception e)
              {
                Echoglossian.PluginLog.Error($"Error in SetNodeTranslation method SetText SetMessageText: {e}");
              }
            }

            nodeState.TranslationSet = true;
            return;
          }

          /*          nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[nodeId];
                    nodeAsTextNode->SetText(sanitizedText);
                    nodeAsTextNode->ResizeNodeForCurrentText();*/

          this.translations.TryRemove(nodeId, out _);
          /*nodeState.TranslationSet = true;*/
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

    private class NodeState
    {
      public bool OriginalTextExtracted { get; set; } = false;

      public bool TranslationSet { get; set; } = false;

      public override string ToString()
      {
        return $"OriginalTextExtracted: {this.OriginalTextExtracted}, TranslationSet: {this.TranslationSet}";
      }
    }

    public static string CalculateHash(string input)
    {
      using var sha256 = SHA256.Create();
      var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
      return Convert.ToBase64String(bytes);
    }

    public static bool IsAddonAlreadyProcessed(string addonHash)
    {
      return addonHashes.ContainsKey(addonHash);
    }

    public static void MarkAddonAsProcessed(string addonHash)
    {
      addonHashes[addonHash] = true;
    }
  }
}
