using ImGuiNET;

using System;
using System.Threading;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Logging;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Runtime.InteropServices;

namespace Echoglossian
{
  internal class UIAddonHandler : IDisposable
  {
    private bool disposedValue;

    private SemaphoreSlim translationSemaphore;
    private volatile int currentTranslationId;

    private Config configuration = Echoglossian.PluginInterface.GetPluginConfig() as Config;
    private ImFontPtr uiFont;
    private bool fontLoaded;

    private string addonName = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="UIAddonHandler"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="uiFont"></param>
    /// <param name="fontLoaded"></param>
    /// <param name="addonName"></param>
    public UIAddonHandler(
        Config configuration = default,
        ImFontPtr uiFont = default,
        bool fontLoaded = default,
        string addonName = null
        )
    {
      this.configuration = configuration;
      this.uiFont = uiFont;
      this.fontLoaded = fontLoaded;
      this.addonName = addonName;
    }

    public void EgloAddonHandler(string addonName, string[] eventsToWatch)
    {
      if (addonName == null || addonName == string.Empty || eventsToWatch.Length <= 0)
      {
        return;
      }

      foreach (var eventName in eventsToWatch)
      {
        switch (eventName)
        {
          case "PreSetup":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PreSetup, addonName, this.GrabAddonEventInfo);
            break;
          case "PostSetup":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, addonName, this.GrabAddonEventInfo);
            break;
          case "PreUpdate":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PostUpdate":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PostUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PreDraw":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, addonName, this.GrabAddonEventInfo);
            break;
          case "PostDraw":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, addonName, this.GrabAddonEventInfo);
            break;
          case "PreFinalize":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, addonName, this.GrabAddonEventInfo);
            break;
          case "PreRequestedUpdate":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PostRequestedUpdate":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, addonName, this.GrabAddonEventInfo);
            break;
          case "PreRefresh":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, addonName, this.GrabAddonEventInfo);
            break;
          case "PostRefresh":
            Echoglossian.AddonLifecycle.RegisterListener(AddonEvent.PostRefresh, addonName, this.GrabAddonEventInfo);
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
          var setupArgs = args;
          this.Handleargs((AddonSetupArgs)setupArgs);
          break;
        case AddonArgsType.Update:
          var updateArgs = args;
          this.HandleUpdateArgs((AddonUpdateArgs)updateArgs);
          break;
        case AddonArgsType.Draw:
          var drawArgs = args;
          this.HandleDrawArgs((AddonDrawArgs)drawArgs);
          break;
        case AddonArgsType.Finalize:
          var finalizeArgs = args;
          this.HandleFinalizeArgs((AddonFinalizeArgs)finalizeArgs);
          break;
        case AddonArgsType.RequestedUpdate:
          var requestedUpdateArgs = args;
          this.HandleRequestedUpdateArgs((AddonRequestedUpdateArgs)requestedUpdateArgs);
          break;
        case AddonArgsType.Refresh:
          var refreshArgs = args;
          this.HandleRefreshArgs((AddonRefreshArgs)refreshArgs);
          break;
        default:
          Echoglossian.PluginLog.Error($"AddonArgs type not found: {args.GetType()}");
          break;
      }
    }

    private unsafe void Handleargs(AddonSetupArgs args)
    {
      if (args == null)
      {
        Echoglossian.PluginLog.Error("AddonSetupArgs is null");
        return;
      }


      Echoglossian.PluginLog.Information($"Addonargs.AddonName in HandleArgs: {args.AddonName}");
      Echoglossian.PluginLog.Information($"Addonargs.AtkValues in HandleArgs: {args.AtkValues}");

      this.translationSemaphore = new SemaphoreSlim(1, 1);

      if (args is not AddonSetupArgs setupArgs)
      {
        return;
      }

      var setupAtkValues = (AtkValue*)args.AtkValues;

      if (setupAtkValues == null)
      {
        return;
      }

      try
      {
        if (setupAtkValues[0].String != null)
        {

          // TODO: Figure out how to get the original text from the addon
          // var originalText = Marshal.PtrToStringUTF8(new IntPtr(setupAtkValues[0].String));
        }
        else
        {
          var addonInfo = (AtkUnitBase*)args.Addon;

          Echoglossian.PluginLog.Information($"Addon Info: {addonInfo->ToString}");



          var addonName = addonInfo->GetTextNodeById(4);

          var addonText = addonInfo->GetTextNodeById(6);
          Echoglossian.PluginLog.Information($"Addon Details----------------: {addonName->NodeText} -> {addonText->NodeText}");

          var originalName = addonName->NodeText.ToString();
          var originalAddonText = addonText->NodeText.ToString();
          Echoglossian.PluginLog.Information($"AddonSetup-----------: {originalName} -> {originalAddonText}");
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in UIAddonHandler HandleArgs: {e}");
      }

      // throw new NotImplementedException();
    }

    private void HandleUpdateArgs(AddonUpdateArgs args)
    {
      Echoglossian.PluginLog.Information($"AddonUpdateArgs: {args.AddonName}");
      // throw new NotImplementedException();
    }

    private void HandleDrawArgs(AddonDrawArgs args)
    {
      Echoglossian.PluginLog.Information($"AddonDrawArgs: {args.AddonName}");
      // throw new NotImplementedException();
    }

    private void HandleFinalizeArgs(AddonFinalizeArgs args)
    {
      Echoglossian.PluginLog.Information($"AddonFinalizeArgs: {args.AddonName}");
      // throw new NotImplementedException();
    }

    private void HandleRequestedUpdateArgs(AddonRequestedUpdateArgs args)
    {
      Echoglossian.PluginLog.Information($"AddonRequestedUpdateArgs: {args.StringArrayData.ToString()}");
      // throw new NotImplementedException();
    }

    private unsafe void HandleRefreshArgs(AddonRefreshArgs args)
    {
      Echoglossian.PluginLog.Information($"AddonRefreshArgs ¨¨¨¨¨¨¨¨¨¨¨¨¨¨¨¨¨¨: {args.AtkValues.ToString()}");
      this.translationSemaphore = new SemaphoreSlim(1, 1);

      var refreshAtkvalues = (AtkValue*)args.AtkValues;

      var aargs = args.AddonName;

      Echoglossian.PluginLog.Information($"AddonRefreshArgs --------------: {aargs}");



      if (aargs != string.Empty)
      {
        Echoglossian.PluginLog.Information($"AddonRefreshArgs: {aargs.ToString()}");
        // TODO: Figure out how to get the original text from the addon
        // var originalText = Marshal.PtrToStringUTF8(new IntPtr(refreshAtkvalues[0].String));
      }
      else
      {

        var addonInfo = (AtkUnitBase*)args.Addon;

        Echoglossian.PluginLog.Information($"Addon Info: {addonInfo->ToString}");



        var addonName = addonInfo->GetTextNodeById(4);

        var addonText = addonInfo->GetTextNodeById(6);
        Echoglossian.PluginLog.Information($"Addon Details----------------: {addonName->NodeText} -> {addonText->NodeText}");

        var originalName = addonName->NodeText.ToString();
        var originalAddonText = addonText->NodeText.ToString();
        Echoglossian.PluginLog.Information($"AddonSetup-----------: {originalName} -> {originalAddonText}");
      }

      // throw new NotImplementedException();
    }




    protected virtual void Dispose(bool disposing)
    {
      if (!this.disposedValue)
      {
        if (disposing)
        {
          this.translationSemaphore.Dispose();
          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PreSetup, this.addonName);
          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, this.addonName);

          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PreUpdate, this.addonName);
          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PostUpdate, this.addonName);

          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, this.addonName);
          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PostDraw, this.addonName);

          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, this.addonName);

          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PreRequestedUpdate, this.addonName);
          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PostRequestedUpdate, this.addonName);

          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PreRefresh, this.addonName);
          Echoglossian.AddonLifecycle.UnregisterListener(AddonEvent.PostRefresh, this.addonName);

        }

        this.disposedValue = true;
      }
    }

    public void Dispose()
    {
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}