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
      // Echoglossian.PluginLog.Information($"EgloAddonHandler called!!");

      this.addonName = addonName;
      // Echoglossian.PluginLog.Information($"AddonName in EgloAddonHandler: {this.addonName}");

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
          };
          break;
        case "_BattleTalk":
          this.addonCharacteristicsInfo = new()
          {
            AddonName = this.addonName,
            IsComplexAddon = false,
            NameNodeId = 4,
            MessageNodeId = 6,
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
          this.addonNodesFlags.Add(3, (TextFlags)(/*(byte)TextFlags.AutoAdjustNodeSize | */(byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        case "_BattleTalk":
          this.addonNodesFlags.Add(6, (TextFlags)(/*(byte)TextFlags.AutoAdjustNodeSize | */(byte)TextFlags.WordWrap | (byte)TextFlags.MultiLine));
          break;
        default:
          break;
      }
    }

    private unsafe void ExploreAddon()
    {
      // Echoglossian.PluginLog.Information($"ExploreAddon called!!");

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

      // Clear translations when re-exploring addon
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

        var textFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr);

        if (string.IsNullOrEmpty(textFromNode))
        {
          continue;
        }

        if (this.addonCharacteristicsInfo.NameNodeId == i)
        {
          Echoglossian.PluginLog.Information($"Text from Node in ExploreAddon NameNode: {textFromNode}");

          if (this.addonName == "Talk")
          {
            this.addonCharacteristicsInfo.TalkMessage = new TalkMessage(
              senderName: textFromNode,
              originalTalkMessage: string.Empty,
              originalSenderNameLang: this.clientLanguage.Humanize(),
              translatedTalkMessage: string.Empty,
              originalTalkMessageLang: this.clientLanguage.Humanize(),
              translationLang: this.langToTranslateTo,
              translationEngine: this.configuration.ChosenTransEngine,
              translatedSenderName: string.Empty,
              createdDate: DateTime.Now,
              updatedDate: DateTime.Now);
          }
          else if (this.addonName == "_BattleTalk")
          {
            this.addonCharacteristicsInfo.BattleTalkMessage = new BattleTalkMessage(
              senderName: textFromNode,
              originalBattleTalkMessage: string.Empty,
              originalSenderNameLang: this.clientLanguage.Humanize(),
              translatedBattleTalkMessage: string.Empty,
              originalBattleTalkMessageLang: this.clientLanguage.Humanize(),
              translationLang: this.langToTranslateTo,
              translationEngine: this.configuration.ChosenTransEngine,
              translatedSenderName: string.Empty,
              createdDate: DateTime.Now,
              updatedDate: DateTime.Now);
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

#if DEBUG
        // Echoglossian.PluginLog.Information($"Text from Node: {textFromNode}");
#endif
        try
        {
          var entry = new TranslationEntry { OriginalText = textFromNode };

          // Add entry only if it doesn't already exist
          if (!this.translations.ContainsKey(i))
          {
            Echoglossian.PluginLog.Warning($"Adding translation entry: {i} - {textFromNode}");
            this.translations[i] = entry;

            // Move database check and translation outside of the unsafe context
            this.CheckAndTranslateAsync(i, textFromNode, entry);
          }
        }
        catch (Exception e)
        {
          Echoglossian.PluginLog.Error($"Error in translation: {e}");
        }
      }
    }

    private async Task CheckAndTranslateAsync(int id, string textFromNode, TranslationEntry entry)
    {
      var dbTranslation = await Task.Run(() => this.CheckDatabaseForTranslation(textFromNode));

      if (dbTranslation != null)
      {
        entry.TranslatedText = dbTranslation;
        entry.IsTranslated = true;
        Echoglossian.PluginLog.Information($"Using cached translation for: {textFromNode}");
      }
      else
      {
        this.FireAndForgetTranslation(id, textFromNode);
      }
    }

    private void FireAndForgetTranslation(int id, string text)
    {
      if (!this.isAddonVisible)
      {
        return;
      }

      Task.Run(() => this.TranslateText(id, text));
      this.SetTranslationToAddon();
    }

    private string CheckDatabaseForTranslation(string originalText)
    {
      if (this.addonName == "Talk")
      {
        if (this.addonCharacteristicsInfo.TalkMessage == null || this.addonCharacteristicsInfo.TalkMessage.SenderName == string.Empty)
        {
          return null;
        }

        var talkMessage = new TalkMessage(
          senderName: this.addonCharacteristicsInfo.TalkMessage.SenderName,
          originalTalkMessage: originalText,
          originalTalkMessageLang: this.clientLanguage.Humanize(),
          originalSenderNameLang: this.clientLanguage.Humanize(),
          translatedSenderName: string.Empty,
          translatedTalkMessage: string.Empty,
          translationLang: this.langToTranslateTo,
          translationEngine: this.configuration.ChosenTransEngine,
          createdDate: DateTime.Now,
          updatedDate: DateTime.Now);

        return Echoglossian.FindTalkMessage(talkMessage) ? Echoglossian.FoundTalkMessage.TranslatedTalkMessage : null;
      }
      else if (this.addonName == "_BattleTalk")
      {
        var battleTalkMessage = new BattleTalkMessage(
          senderName: this.addonCharacteristicsInfo.BattleTalkMessage.SenderName,
          originalBattleTalkMessage: originalText,
          originalBattleTalkMessageLang: this.clientLanguage.Humanize(),
          originalSenderNameLang: this.clientLanguage.Humanize(),
          translatedSenderName: string.Empty,
          translatedBattleTalkMessage: string.Empty,
          translationLang: this.langToTranslateTo,
          translationEngine: this.configuration.ChosenTransEngine,
          createdDate: DateTime.Now,
          updatedDate: DateTime.Now);

        return Echoglossian.FindBattleTalkMessage(battleTalkMessage) ? Echoglossian.FoundBattleTalkMessage.TranslatedBattleTalkMessage : null;
      }

      return null;
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

          await Task.Run(() => this.SaveTranslationToDatabase(text, translation));
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in TranslateText method: {e}");
      }
    }

    private void SaveTranslationToDatabase(string originalText, string translatedText)
    {
      /*      Echoglossian.PluginLog.Information($"Saving translation to database: {originalText} - {translatedText}");*/
      if (this.addonName == "Talk")
      {
        var talkMessage = this.addonCharacteristicsInfo.TalkMessage;

        Echoglossian.PluginLog.Information($"TalkMessage in SaveTranslationToDatabase: {talkMessage}");

        talkMessage.OriginalTalkMessage = originalText;
        talkMessage.TranslatedTalkMessage = translatedText;

        Echoglossian.PluginLog.Information($"Saving translation to database: {talkMessage.OriginalTalkMessage} - {talkMessage.TranslatedTalkMessage}");
        try
        {
          Echoglossian.InsertTalkData(talkMessage);
        }
        catch (Exception e)
        {
          Echoglossian.PluginLog.Error($"Error in InsertTalkData: {e}");
        }
      }
      else if (this.addonName == "_BattleTalk")
      {
        var battleTalkMessage = this.addonCharacteristicsInfo.BattleTalkMessage;

        Echoglossian.PluginLog.Information($"BattleTalkMessage in SaveTranslationToDatabase: {battleTalkMessage}");

        battleTalkMessage.OriginalBattleTalkMessage = originalText;
        battleTalkMessage.TranslatedBattleTalkMessage = translatedText;

        Echoglossian.PluginLog.Information($"Saving translation to database: {battleTalkMessage.OriginalBattleTalkMessage} - {battleTalkMessage.TranslatedBattleTalkMessage}");
        try
        {
          Echoglossian.InsertBattleTalkData(battleTalkMessage);
        }
        catch (Exception e)
        {
          Echoglossian.PluginLog.Error($"Error in InsertBattleTalkData: {e}");
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
        await Task.Delay(100, token); // Add a small delay to avoid tight looping
      }
    }

    private unsafe void SetTranslationToAddon()
    {
      Echoglossian.PluginLog.Information($"Setting translation to addon: {this.addonName}");

      Framework.RunOnTick(() =>
      {
        Echoglossian.PluginLog.Information($"AddonName in SetTranslationToAddon: {this.addonName}");
        var addon = GameGui.GetAddonByName(this.addonName, 1);
        var foundAddon = (AtkUnitBase*)addon;

        if (foundAddon == null || !foundAddon->IsVisible)
        {
          return;
        }

        var nodesQuantity = foundAddon->UldManager.NodeListCount;

        // Echoglossian.PluginLog.Information($"Nodes Quantity in SetTranslationToAddon: {nodesQuantity}");

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

          var textFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr);

          // Echoglossian.PluginLog.Information($"Text from Node in SetTranslationToAddon: {textFromNode}");

          if (this.translations.TryGetValue(i, out var entry))
          {
            Echoglossian.PluginLog.Information($"Entry in SetTranslationToAddon: {entry.OriginalText}");

            if (entry.IsTranslated)
            {
              Echoglossian.PluginLog.Information($"Entry is translated in SetTranslationToAddon: {entry.TranslatedText}");

              if (entry.TranslatedText == textFromNode)
              {
                Echoglossian.PluginLog.Information($"Translated text matches in SetTranslationToAddon!");
                continue;
              }

              if (entry.OriginalText == textFromNode)
              {
                Echoglossian.PluginLog.Information($"Original text matches in SetTranslationToAddon!");

                var sanitizedText = entry.TranslatedText;

                // Echoglossian.PluginLog.Information($"Sanitized text in SetTranslationToAddon: {sanitizedText}");

                if (this.addonCharacteristicsInfo.NameNodeId == i)
                {
                  nodeAsTextNode->SetText(sanitizedText);
                  nodeAsTextNode->ResizeNodeForCurrentText();
                  continue;
                }

                nodeAsTextNode->TextFlags = (byte)this.addonNodesFlags[i];
                nodeAsTextNode->SetText(sanitizedText);
                nodeAsTextNode->ResizeNodeForCurrentText();

                // Echoglossian.PluginLog.Information($"Text set in SetTranslationToAddon!");

                this.translations.TryRemove(i, out _);

                // Echoglossian.PluginLog.Information($"Entry removed in SetTranslationToAddon!");
              }
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
