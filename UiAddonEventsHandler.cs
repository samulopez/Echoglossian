// Saving this for backup purposes. This is the original file that was used to handle the UI Addon events.

using ImGuiNET;

using System;
using System.Threading;
using System.Threading.Tasks;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

using static Echoglossian.Echoglossian;

using Dalamud.Memory;
using Humanizer;
using Dalamud;
using Dalamud.Game.Text.Sanitizer;

using System.Collections.Concurrent;

namespace Echoglossian
{
  internal class UIAddonEventsHandler : IDisposable
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

    public UIAddonEventsHandler(
        Config configuration = default,
        ImFontPtr uiFont = default,
        bool fontLoaded = default,
        string langToTranslateTo = default
        )
    {
      this.configuration = configuration;
      this.uiFont = uiFont;
      this.fontLoaded = fontLoaded;
      this.langToTranslateTo = langToTranslateTo;
      this.clientLanguage = ClientState.ClientLanguage;
      this.translationService = new TranslationService(configuration, Echoglossian.PluginLog, new Sanitizer(this.clientLanguage));
      this.translations = new ConcurrentDictionary<int, TranslationEntry>();

      this.cts = new CancellationTokenSource();
      this.translationTask = Task.Run(async () => await this.ProcessTranslations(this.cts.Token));
    }

    public void EgloAddonHandler(string addonName, string[] eventsToWatch)
    {
      this.addonName = addonName;

      if (string.IsNullOrEmpty(addonName) || eventsToWatch.Length <= 0)
      {
        return;
      }

      foreach (var eventName in eventsToWatch)
      {
        switch (eventName)
        {
          case "PreSetup":
            AddonLifecycle.RegisterListener(AddonEvent.PreSetup, addonName, this.GrabAddonEventInfo);
            break;
          case "PostSetup":
            AddonLifecycle.RegisterListener(AddonEvent.PostSetup, addonName, this.GrabAddonEventInfo);
            break;
          case "PreUpdate":
            AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PostUpdate":
            AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PreDraw":
            AddonLifecycle.RegisterListener(AddonEvent.PreDraw, addonName, this.GrabAddonEventInfo);
            break;
          case "PostDraw":
            AddonLifecycle.RegisterListener(AddonEvent.PostDraw, addonName, this.GrabAddonEventInfo);
            break;
          case "PreFinalize":
            AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, addonName, this.GrabAddonEventInfo);
            break;
          case "PreRequestedUpdate":
            AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PostRequestedUpdate":
            AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PreRefresh":
            AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, addonName, this.GrabAddonEventInfo);
            break;
          case "PostRefresh":
            AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, addonName, this.GrabAddonEventInfo);
            break;
          default:
            Echoglossian.PluginLog.Error($"Event name not found: {eventName}");
            break;
        }
      }
    }

    private void GrabAddonEventInfo(AddonEvent type, AddonArgs args)
    {
      if (args == null)
      {
        return;
      }

      switch (args.Type)
      {
        case AddonArgsType.Setup:
          this.HandleSetupArgs((AddonSetupArgs)args);
          break;
        case AddonArgsType.Update:
          this.HandleUpdateArgs((AddonUpdateArgs)args);
          break;
        case AddonArgsType.Draw:
          this.HandleDrawArgs((AddonDrawArgs)args);
          break;
        case AddonArgsType.Finalize:
          this.HandleFinalizeArgs((AddonFinalizeArgs)args);
          break;
        case AddonArgsType.RequestedUpdate:
          this.HandleRequestedUpdateArgs((AddonRequestedUpdateArgs)args);
          break;
        case AddonArgsType.Refresh:
          this.HandleRefreshArgs((AddonRefreshArgs)args);
          break;
        default:
          Echoglossian.PluginLog.Error($"AddonArgs type not found: {args.GetType()}");
          break;
      }
    }

    private unsafe void HandleSetupArgs(AddonSetupArgs args)
    {
      if (args == null)
      {
        return;
      }

      Echoglossian.PluginLog.Information($"Addonargs.AddonName in HandleSetupArgs: {args.AddonName}");
      Echoglossian.PluginLog.Information($"Addonargs.AtkValues in HandleSetupArgs: {args.AtkValues}");
      Echoglossian.PluginLog.Information($"Addonargs.Addon in HandleSetupArgs: {args.Addon}");
      Echoglossian.PluginLog.Information($"Addonargs.StringArrayData in HandleSetupArgs: {args.AtkValueSpan.ToString()}");

      var setupAtkValues = (AtkValue*)args.AtkValues;

      if (setupAtkValues == null)
      {
        return;
      }

      try
      {
        // TODO: Figure out how to get the original text from the addon
        if (setupAtkValues[0].String != null)
        {
          // var originalText = Marshal.PtrToStringUTF8(new IntPtr(setupAtkValues[0].String));
        }
        else
        {
          var addonInfo = (AtkUnitBase*)args.Addon;

          var addonName = addonInfo->GetTextNodeById(4);
          var addonText = addonInfo->GetTextNodeById(6);

          var originalName = addonName->NodeText.ToString();
          var originalAddonText = addonText->NodeText.ToString();

          // Additional handling
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in UIBattleTalkAddonHandler HandleArgs: {e}");
      }
    }

    private void HandleUpdateArgs(AddonUpdateArgs args)
    {
      if (args == null)
      {
        return;
      }

      Echoglossian.PluginLog.Information($"Addonargs.AddonName in HandleUpdateArgs: {args.AddonName}");
      Echoglossian.PluginLog.Information($"Addonargs.Addon in HandleUpdateArgs: {args.Addon}");
      Echoglossian.PluginLog.Information($"Addonargs in HandleUpdateArgs: {args.ToString}");
    }

    private void HandleDrawArgs(AddonDrawArgs args)
    {
      if (args == null)
      {
        return;
      }

      Echoglossian.PluginLog.Information($"Addonargs.AddonName in HandleDrawArgs: {args.AddonName}");
      Echoglossian.PluginLog.Information($"Addonargs.Addon in HandleDrawArgs: {args.Addon}");
      Echoglossian.PluginLog.Information($"Addonargs in HandleDrawArgs: {args.ToString}");
    }

    private void HandleFinalizeArgs(AddonFinalizeArgs args)
    {
      if (args == null)
      {
        return;
      }

      Echoglossian.PluginLog.Information($"Addonargs.AddonName in HandleFinalizeArgs: {args.AddonName}");
      Echoglossian.PluginLog.Information($"Addonargs.Addon in HandleFinalizeArgs: {args.Addon}");
      Echoglossian.PluginLog.Information($"Addonargs in HandleFinalizeArgs: {args.ToString}");
    }

    private void HandleRequestedUpdateArgs(AddonRequestedUpdateArgs args)
    {
      if (args == null)
      {
        return;
      }

      Echoglossian.PluginLog.Information($"Addonargs.AddonName in HandleRequestedUpdateArgs: {args.AddonName}");
      Echoglossian.PluginLog.Information($"Addonargs.Addon in HandleRequestedUpdateArgs: {args.Addon}");
      Echoglossian.PluginLog.Information($"Addonargs in HandleRequestedUpdateArgs: {args.ToString}");
      Echoglossian.PluginLog.Information($"Addonargs StringArrayData in HandleRequestedUpdateArgs: {args.StringArrayData.ToString()}");
      Echoglossian.PluginLog.Information($"Addonargs NumberArrayData in HandleRequestedUpdateArgs: {args.NumberArrayData.ToString()}");
    }

    private unsafe void HandleRefreshArgs(AddonRefreshArgs args)
    {
      if (args == null)
      {
        return;
      }

      Echoglossian.PluginLog.Information($"AddonRefreshArgs in HandleRefreshArgs: {args.AddonName}");
      Echoglossian.PluginLog.Information($"Addonargs.Addon in HandleRefreshArgs: {args.Addon}");
      Echoglossian.PluginLog.Information($"Addonargs in HandleRefreshArgs: {args.ToString}");
      Echoglossian.PluginLog.Information($"Addonargs.AtkValues in HandleRefreshArgs: {args.AtkValues}");
      Echoglossian.PluginLog.Information($"Addonargs AtkValueSpan in HandleRefreshArgs: {args.AtkValueSpan.ToString()}");
      Echoglossian.PluginLog.Information($"Addonargs AtkValueCount in HandleRefreshArgs: {args.AtkValueCount}");

      var refreshAtkvalues = (AtkValue*)args.AtkValues;

      var addonInfo = (AtkUnitBase*)args.Addon;
      var nodesQuantity = addonInfo->UldManager.NodeListCount;

      for (var i = 0; i < nodesQuantity; i++)
      {
        Echoglossian.PluginLog.Information($"Addon Node ID: {i}");
        var node = addonInfo->GetNodeById((uint)i);

        if (node == null)
        {
          Echoglossian.PluginLog.Warning("Node Empty");
          continue;
        }

        if (node->Type != NodeType.Text)
        {
          Echoglossian.PluginLog.Warning("Node is not Text");
          continue;
        }

        var nodeAsTextNode = node->GetAsAtkTextNode();

        Echoglossian.PluginLog.Information($"Addon Text Node: {nodeAsTextNode->NodeText}");

        if (nodeAsTextNode != null)
        {
          var textFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr);
          Echoglossian.PluginLog.Information($"Text from Node: {textFromNode}");

          try
          {
            var entry = new TranslationEntry { OriginalText = textFromNode };
            this.translations[i] = entry;
            this.FireAndForgetTranslation(i, textFromNode, this.addonName);
          }
          catch (Exception e)
          {
            Echoglossian.PluginLog.Error($"Error in translation: {e}");
          }
        }
      }
    }

    private void FireAndForgetTranslation(int id, string text, string addonName)
    {
      Task.Run(() => this.TranslateText(id, text));

      this.SetTranslationToAddon(addonName);
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
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in TranslateText method: {e}");
      }
    }

    private async Task ProcessTranslations(CancellationToken token)
    {
      while (!token.IsCancellationRequested)
      {
        foreach (var key in this.translations.Keys)
        {
          if (this.translations.TryGetValue(key, out var entry) && !entry.IsTranslated)
          {
            await this.TranslateText(key, entry.OriginalText);
          }
        }
        await Task.Delay(100, token); // Add a small delay to avoid tight looping
      }
    }

    private unsafe void SetTranslationToAddon(string addonName)
    {
      Echoglossian.PluginLog.Information($"Setting translation to addon: {addonName}");

      Framework.RunOnTick(() =>
      {
        Echoglossian.PluginLog.Information($"AddonName in SetTranslationToAddon: {addonName}");
        var addon = GameGui.GetAddonByName(addonName, 1);

        var foundAddon = (AtkUnitBase*)addon;

        if (foundAddon == null)
        {
          return;
        }

        Echoglossian.PluginLog.Information($"Found addon: {foundAddon->Name->ToString()}");

        if (!foundAddon->IsVisible)
        {
          return;
        }

        var nodesQuantity = foundAddon->UldManager.NodeListCount;

        Echoglossian.PluginLog.Information($"Nodes Quantity in SetTranslationToAddon: {nodesQuantity}");

        for (var i = 0; i < nodesQuantity; i++)
        {
          var node = foundAddon->GetNodeById((uint)i);

          if (node == null)
          {
            continue;
          }

          if (node->Type != NodeType.Text)
          {
            continue;
          }

          var nodeAsTextNode = node->GetAsAtkTextNode();

          if (nodeAsTextNode == null)
          {
            continue;
          }

          if (nodeAsTextNode != null)
          {
            var textFromNode = MemoryHelper.ReadSeStringAsString(out _, (nint)nodeAsTextNode->NodeText.StringPtr);

            Echoglossian.PluginLog.Information($"Text from Node in SetTranslationToAddon: {textFromNode}");

            if (this.translations.TryGetValue(i, out var entry))
            {
              Echoglossian.PluginLog.Information($"Entry in SetTranslationToAddon: {entry.OriginalText}");

              if (entry.IsTranslated)
              {
                Echoglossian.PluginLog.Information($"Entry is translated in SetTranslationToAddon: {entry.TranslatedText}");

                if (entry.OriginalText == textFromNode)
                {
                  Echoglossian.PluginLog.Information($"Original text matches in SetTranslationToAddon!");

                  var sanitizedText = entry.TranslatedText;

                  Echoglossian.PluginLog.Information($"Sanitized text in SetTranslationToAddon: {sanitizedText}");
                  nodeAsTextNode->SetText(sanitizedText);
                  nodeAsTextNode->ResizeNodeForCurrentText();
                }
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

          AddonLifecycle.UnregisterListener(AddonEvent.PreSetup, this.addonName);
          AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, this.addonName);

          AddonLifecycle.UnregisterListener(AddonEvent.PreUpdate, this.addonName);
          AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, this.addonName);

          AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, this.addonName);
          AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, this.addonName);

          AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, this.addonName);

          AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, this.addonName);
          AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, this.addonName);

          AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, this.addonName);
          AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, this.addonName);

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
  }
}
