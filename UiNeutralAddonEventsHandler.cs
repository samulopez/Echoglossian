// Saving this for backup purposes. This is the original file that was used to handle the UI Addon events.

using System;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;

namespace Echoglossian
{
  public partial class Echoglossian
  {

    public void EgloNeutralAddonHandler(string addonName, string[] eventsToWatch)
    {

      if (string.IsNullOrEmpty(addonName) || eventsToWatch.Length <= 0)
      {
        return;
      }

      foreach (var eventName in eventsToWatch)
      {
        Echoglossian.PluginLog.Information($"AddonName in EgloNeutralAddonHandler: {addonName}, eventName: {eventName}");
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
          case "PreReceiveEvent":
            AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, addonName, this.GrabAddonEventInfo);
            break;
          case "PostReceiveEvent":
            AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, addonName, this.GrabAddonEventInfo);
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
        Echoglossian.PluginLog.Error("AddonArgs is null");
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
        case AddonArgsType.ReceiveEvent:
          this.HandleReceiveEvent((AddonReceiveEventArgs)args);
          break;
        default:
          Echoglossian.PluginLog.Error($"AddonArgs type not found: {args.GetType()}");
          break;
      }
    }

    private unsafe void HandleSetupArgs(AddonSetupArgs args)
    {
      Echoglossian.PluginLog.Information($"Addonargs in HandleSetupArgs: {args}");
      if (args == null)
      {
        Echoglossian.PluginLog.Error("AddonSetupArgs is null");
        return;
      }

      try
      {
        switch (args.AddonName)
        {
          case "Talk":
            // this.uiTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            this.uiTalkAddonHandler.SetTranslationToAddon();

            break;
          case "_BattleTalk":
            //this.uiBattleTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            this.uiBattleTalkAddonHandler.SetTranslationToAddon();
            break;
          default:
            Echoglossian.PluginLog.Error($"AddonName not found: {args.AddonName}");
            break;
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in HandleSetupArgs: {e}");
      }
    }

    private unsafe void HandleUpdateArgs(AddonUpdateArgs args)
    {
      if (args == null)
      {
        return;
      }

      try
      {
        switch (args.AddonName)
        {
          case "Talk":

            try
            {
              // this.uiTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
              this.uiTalkAddonHandler.SetTranslationToAddon();
            }
            catch (Exception e)
            {
              Echoglossian.PluginLog.Error($"Error in HandleUpdateArgs: {e}");
            }

            break;
          case "_BattleTalk":
            //this.uiBattleTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            this.uiBattleTalkAddonHandler.SetTranslationToAddon();
            break;
          default:
            Echoglossian.PluginLog.Error($"AddonName not found: {args.AddonName}");
            break;
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in HandleUpdateArgs: {e}");
      }
    }

    private void HandleDrawArgs(AddonDrawArgs args)
    {
      if (args == null)
      {
        return;
      }

      try
      {
        switch (args.AddonName)
        {
          case "Talk":
            this.uiTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            // this.uiTalkAddonHandler.SetTranslationToAddon();
            break;
          case "_BattleTalk":
            this.uiBattleTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            // this.uiBattleTalkAddonHandler.SetTranslationToAddon();
            break;
          default:
            Echoglossian.PluginLog.Error($"AddonName not found: {args.AddonName}");
            break;
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in HandleDrawArgs: {e}");
      }
    }

    private void HandleFinalizeArgs(AddonFinalizeArgs args)
    {
      if (args == null)
      {
        return;
      }

      try
      {
        switch (args.AddonName)
        {
          case "Talk":
            // this.uiTalkAddonHandler.EgloAddonHandler("Talk", args);
            break;
          case "_BattleTalk":
            // this.uiBattleTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            // this.uiBattleTalkAddonHandler.SetTranslationToAddon();
            break;
          default:
            Echoglossian.PluginLog.Error($"AddonName not found: {args.AddonName}");
            break;
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in HandleFinalizeArgs: {e}");
      }
    }

    private void HandleRequestedUpdateArgs(AddonRequestedUpdateArgs args)
    {
      if (args == null)
      {
        return;
      }

      try
      {
        switch (args.AddonName)
        {
          case "Talk":
            // this.uiTalkAddonHandler.EgloAddonHandler("Talk", args);
            this.uiTalkAddonHandler.SetTranslationToAddon();
            break;
          case "_BattleTalk":
            //this.uiBattleTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            this.uiBattleTalkAddonHandler.SetTranslationToAddon();
            break;
          default:
            Echoglossian.PluginLog.Error($"AddonName not found: {args.AddonName}");
            break;
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in HandleRequestedUpdateArgs: {e}");
      }
    }

    private unsafe void HandleRefreshArgs(AddonRefreshArgs args)
    {
      if (args == null)
      {
        return;
      }

      try
      {
        switch (args.AddonName)
        {
          case "Talk":
            // this.uiTalkAddonHandler.EgloAddonHandler("Talk", args);
            this.uiTalkAddonHandler.SetTranslationToAddon();
            break;
          case "_BattleTalk":
            // this.uiBattleTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            this.uiBattleTalkAddonHandler.SetTranslationToAddon();
            break;
          default:
            Echoglossian.PluginLog.Error($"AddonName not found: {args.AddonName}");
            break;
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in HandleRefreshArgs: {e}");
      }
    }

    private void HandleReceiveEvent(AddonReceiveEventArgs args)
    {
      if (args == null)
      {
        return;
      }

      try
      {
        switch (args.AddonName)
        {
          case "Talk":
            // this.uiTalkAddonHandler.EgloAddonHandler("Talk", args);
            this.uiTalkAddonHandler.SetTranslationToAddon();
            break;
          case "_BattleTalk":
            // this.uiBattleTalkAddonHandler.EgloAddonHandler(args.AddonName, args);
            this.uiBattleTalkAddonHandler.SetTranslationToAddon();
            break;
          default:
            Echoglossian.PluginLog.Error($"AddonName not found: {args.AddonName}");
            break;
        }
      }
      catch (Exception e)
      {
        Echoglossian.PluginLog.Error($"Error in HandleReceiveEvent: {e}");
      }
    }
  }
}
