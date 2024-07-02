// <copyright file="Echoglossian.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;

using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Command;
using Dalamud.Game.Text.Sanitizer;
using Dalamud.Interface.Internal;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;
using XivCommon;

namespace Echoglossian
{
  // TODO: implement multiple fallback translation engines.
  public partial class Echoglossian : IDalamudPlugin
  {
    [PluginService]
    public static IDataManager DManager { get; private set; }

    [PluginService]
    public static DalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    public static ICommandManager CommandManager { get; private set; } = null!;

    [PluginService]
    public static IFramework Framework { get; private set; } = null!;

    [PluginService]
    public static IGameGui GameGui { get; private set; } = null!;

    [PluginService]
    public static IClientState ClientState { get; private set; } = null!;

    [PluginService]
    public static IToastGui ToastGui { get; private set; } = null!;

    [PluginService]
    public static IAddonEventManager EventManager { get; private set; } = null!;

    [PluginService]
    public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;

    [PluginService]
    public static IPluginLog PluginLog { get; private set; } = null!;

    [PluginService]
    public static INotificationManager NotificationManager { get; private set; } = null!;

    [PluginService]
    public static ITextureProvider TextureProvider { get; private set; } = null!;

    private static XivCommonBase Common { get; set; }
    public string Name => Resources.Name;

    private const string SlashCommand = "/eglo";
    private string configDir;
    private static int languageInt = 28;
    private static int fontSize = 24;
    private static int chosenTransEngine;
    private static string transEngineName;
    public string LangToTranslateTo = string.Empty;

    private bool pluginAssetsState;
    private static Dictionary<int, LanguageInfo> langDict;
    private bool config;

    private Config configuration;

    private readonly SemaphoreSlim toastTranslationSemaphore;
    private readonly SemaphoreSlim talkTranslationSemaphore;
    private readonly SemaphoreSlim nameTranslationSemaphore;
    private readonly SemaphoreSlim battleTalkTranslationSemaphore;
    private readonly SemaphoreSlim senderTranslationSemaphore;
    private readonly SemaphoreSlim errorToastTranslationSemaphore;
    private readonly SemaphoreSlim classChangeToastTranslationSemaphore;
    private readonly SemaphoreSlim areaToastTranslationSemaphore;
    private readonly SemaphoreSlim wideTextToastTranslationSemaphore;
    private readonly SemaphoreSlim questToastTranslationSemaphore;

    private readonly IDalamudTextureWrap pixImage;
    private readonly IDalamudTextureWrap choiceImage;
    private readonly IDalamudTextureWrap cutsceneChoiceImage;
    private readonly IDalamudTextureWrap talkImage;
    private readonly IDalamudTextureWrap logo;

    private readonly CultureInfo cultureInfo;

    private static Sanitizer sanitizer;

    private UIAddonHandler uiBattleTalkAddonHandler;
    private UIAddonHandler uiTalkAddonHandler;

    private TranslationService translationService;

    public List<ToastMessage> ErrorToastsCache { get; set; }

    public List<ToastMessage> QuestToastsCache { get; set; }

    public List<ToastMessage> OtherToastsCache { get; set; }

    public Echoglossian()
    {
      this.configuration = PluginInterface.GetPluginConfig() as Config ?? new Config();

      this.configDir = PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;

      CommandManager.AddHandler(SlashCommand, new CommandInfo(this.Command)
      {
        HelpMessage = Resources.HelpMessage,
      });

      sanitizer = PluginInterface.Sanitizer as Sanitizer;

      // Resolver.Initialize();
      // Resolver.GetInstance.SetupSearchSpace();
      // Resolver.GetInstance.Resolve();
      langDict = this.languagesDictionary;
      identifier = Factory.Load($"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Wiki82.profile.xml");

      Common = new XivCommonBase(PluginInterface, Hooks.Talk | Hooks.BattleTalk);
      try
      {
        this.CreateOrUseDb();
      }
      catch (Exception e)
      {
        PluginLog.Error($"Error creating or using database: {e}");
      }
      finally
      {
        PluginLog.Information("Eglo database created or used successfully.");
      }

      this.cultureInfo = new CultureInfo(this.configuration.DefaultPluginCulture);
      this.assetsPath = $"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}";

      this.AssetFiles.Add("NotoSansCJKhk-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKjp-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKkr-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKsc-Regular.otf");
      this.AssetFiles.Add("NotoSansCJKtc-Regular.otf");

#if DEBUG
      // PluginLog.Warning($"Assets state config: {JsonConvert.SerializeObject(this.configuration, Formatting.Indented)}");
#endif
      this.configuration.PluginVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
      if (this.configuration.Version < 5)
      {
        this.FixConfig();
      }

      this.pluginAssetsState = this.configuration.PluginAssetsDownloaded;
#if DEBUG
      PluginLog.Warning($"Assets state config: {this.configuration.PluginAssetsDownloaded}");
      PluginLog.Warning($"Assets state var: {this.pluginAssetsState}");
#endif
      if (!this.pluginAssetsState)
      {
        this.PluginAssetsChecker();
      }

      PluginInterface.UiBuilder.BuildFonts += this.LoadLanguageComboFont; // needs checking
      PluginInterface.UiBuilder.BuildFonts += this.LoadFont; // needs checking

      // this.ListCultureInfos();
      this.pixImage = TextureProvider.CreateFromImageAsync(Resources.pix).Result; // needs checking
      this.choiceImage = PluginInterface.UiBuilder.LoadImage(Resources.choice);
      this.cutsceneChoiceImage = PluginInterface.UiBuilder.LoadImage(Resources.cutscenechoice);
      this.talkImage = PluginInterface.UiBuilder.LoadImage(Resources.prttws);
      this.logo = PluginInterface.UiBuilder.LoadImage(Resources.logo);

      PluginInterface.UiBuilder.DisableCutsceneUiHide = this.configuration.ShowInCutscenes;

      PluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;

      languageInt = this.configuration.Lang;

      fontSize = this.configuration.FontSize;

      chosenTransEngine = this.configuration.ChosenTransEngine;

      this.LangToTranslateTo = langDict[languageInt].Code;

      TransEngines t = (TransEngines)chosenTransEngine;
      transEngineName = t.ToString();
      this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);

      this.LoadAllErrorToasts();
      this.LoadAllOtherToasts();

      Framework.Update += this.Tick;

      this.talkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.nameTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.battleTalkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.senderTranslationSemaphore = new SemaphoreSlim(1, 1);

      this.toastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.errorToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.classChangeToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.areaToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.wideTextToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.questToastTranslationSemaphore = new SemaphoreSlim(1, 1);

      ToastGui.Toast += this.OnToast;
      ToastGui.ErrorToast += this.OnErrorToast;
      ToastGui.QuestToast += this.OnQuestToast;

      // this.HandleTalkAsync();

      // Common.Functions.ChatBubbles.OnChatBubble += this.ChatBubblesOnChatBubble;
      // Common.Functions.Tooltips.OnActionTooltip += this.TooltipsOnActionTooltip;
      Common.Functions.Talk.OnTalk += this.GetTalk;
      Common.Functions.BattleTalk.OnBattleTalk += this.GetBattleTalk;

      this.uiTalkAddonHandler = new UIAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);
      this.uiBattleTalkAddonHandler = new UIAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);

      this.EgloAddonHandler();


      PluginInterface.UiBuilder.Draw += this.BuildUi;

      /* if (ClientState.IsLoggedIn)
       {
         this.ParseUi();
       }*/
    }

    /// <inheritdoc />
    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      Common.Functions.Talk.OnTalk -= this.GetTalk; // broken
      Common.Functions.BattleTalk.OnBattleTalk -= this.GetBattleTalk; // broken - xivcommon is outdated with no new nuget version so cannot be used anymore!!
      // Common.Functions.ChatBubbles.OnChatBubble -= this.ChatBubblesOnChatBubble;
      // Common.Functions.Tooltips.OnActionTooltip -= this.TooltipsOnActionTooltip;
      Common.Functions.Talk.Dispose();
      Common.Functions.BattleTalk.Dispose();

      // Common.Functions.ChatBubbles.Dispose();
      // Common.Functions.Tooltips.Dispose();
      /*Common.Functions.Dispose();

      Common?.Dispose();*/

      ToastGui.Toast -= this.OnToast;
      ToastGui.ErrorToast -= this.OnErrorToast;
      ToastGui.QuestToast -= this.OnQuestToast;

      PluginInterface.UiBuilder.OpenConfigUi -= this.ConfigWindow;

      this.nameTranslationSemaphore?.Dispose();
      this.talkTranslationSemaphore?.Dispose();
      this.battleTalkTranslationSemaphore?.Dispose();
      this.senderTranslationSemaphore?.Dispose();

      this.toastTranslationSemaphore?.Dispose();
      this.errorToastTranslationSemaphore?.Dispose();
      this.areaToastTranslationSemaphore?.Dispose();
      this.wideTextToastTranslationSemaphore?.Dispose();
      this.questToastTranslationSemaphore?.Dispose();

      PluginInterface.UiBuilder.Draw -= this.BuildUi;

      this.pixImage?.Dispose();
      this.choiceImage?.Dispose();
      this.cutsceneChoiceImage?.Dispose();
      this.talkImage?.Dispose();

      Framework.Update -= this.Tick;

      PluginInterface.UiBuilder.BuildFonts -= this.LoadFont; // needs checking
      PluginInterface.UiBuilder.BuildFonts -= this.LoadLanguageComboFont; // needs checking
      this.glyphRangeMainText?.Free();
      this.glyphRangeConfigText?.Free();
      this.glyphRangeMainText = null;
      this.glyphRangeConfigText = null;

      CommandManager.RemoveHandler(SlashCommand);
    }

    private void Tick(IFramework tFramework)
    {
      if (!this.configuration.Translate)
      {
#if DEBUG
        // PluginLog.Information("Translations are disabled!");
#endif
        return;
      }

      switch (this.configuration.UseImGuiForTalk || this.configuration.UseImGuiForBattleTalk ||
              this.configuration.UseImGuiForToasts)
      {
        case true when !this.FontLoaded || this.FontLoadFailed:
          return;
        case true:
          {
            switch (ClientState.IsLoggedIn)
            {
              case true:

                /*this.uiTalkAddonHandler.EgloAddonHandler("Talk");
                this.uiBattleTalkAddonHandler.EgloAddonHandler("_BattleTalk");*/

                // this.TalkHandler("Talk", 1);  tentative fix for stuttering

                // this.BattleTalkHandler("_BattleTalk", 1); tentative fix for stuttering
                this.TextErrorToastHandler("_TextError", 1);

                this.ToastHandler("_WideText", 1);

                // this.ClassChangeToastHandler("_WideText", 1);
                //
                // this.ClassChangeToastHandler("_WideText", 2);
                this.ToastHandler("_TextClassChange", 1);
                this.ToastHandler("_AreaText", 1);

                // this.QuestToastHandler("_ScreenText", 1);
                break;
            }

            break;
          }

        default:
          // this.DisableAllTranslations();
          break;
      }
    }

    private void BuildUi()
    {
      if (!this.configuration.PluginAssetsDownloaded)
      {
        // this.PluginAssetsChecker();
        return;
      }

      if (!this.LanguageComboFontLoaded && !this.LanguageComboFontLoadFailed)
      {
        PluginInterface.UiBuilder.RebuildFonts();
        return;
      }

      if (!this.FontLoaded && !this.FontLoadFailed)
      {
        PluginInterface.UiBuilder.RebuildFonts();
        return;
      }

      if (this.config)
      {
        this.EchoglossianConfigUi();
      }

      if (this.configuration.FontChangeTime > 0)
      {
        if (DateTime.Now.Ticks - 10000000 > this.configuration.FontChangeTime)
        {
          this.configuration.FontChangeTime = 0;
          this.FontLoadFailed = false;

          PluginInterface.UiBuilder.RebuildFonts();
        }
      }

      if (!this.configuration.Translate)
      {
        return;
      }

      if (this.configuration.UseImGuiForBattleTalk && this.configuration.TranslateBattleTalk && this.battleTalkDisplayTranslation)
      {
        this.DrawTranslatedBattleDialogueWindow();
#if DEBUG
        // PluginLog.Verbose("Showing BattleTalk Translation Overlay.");
#endif
      }

      if (this.configuration.UseImGuiForTalk && this.configuration.TranslateTalk && this.talkDisplayTranslation)
      {
        this.DrawTranslatedDialogueWindow();
#if DEBUG
        // PluginLog.Verbose("Showing Talk Translation Overlay.");
#endif
      }

#if DEBUG
      /* PluginLog.Warning($"Toast Draw Vars: !UseImGuiForToasts - {!this.configuration.UseImGuiForToasts}" +
                    $", TranslateErrorToast - {this.configuration.TranslateErrorToast}" +
                    $", errorToastDisplayTranslation - {this.errorToastDisplayTranslation}" +
                    $" equals? {!this.configuration.UseImGuiForToasts && this.configuration.TranslateErrorToast && this.errorToastDisplayTranslation}");*/
#endif
      if (this.configuration.UseImGuiForToasts && this.configuration.TranslateErrorToast && this.errorToastDisplayTranslation)
      {
        this.DrawTranslatedErrorToastWindow();
#if DEBUG
        // PluginLog.Warning("Showing Error Toast Translation Overlay.");
#endif
      }

      if (this.configuration.UseImGuiForToasts && this.configuration.TranslateToast && this.toastDisplayTranslation)
      {
        this.DrawTranslatedToastWindow();
#if DEBUG
        // PluginLog.Warning("Showing Error Toast Translation Overlay.");
#endif
      }

      /*if (!this.configuration.UseImGuiForToasts && this.configuration.TranslateClassChangeToast && this.classChangeToastDisplayTranslation)
      {
        this.DrawTranslatedClassChangeToastWindow();
        *//*#if DEBUG
                PluginLog.Warning("Showing Error Toast Translation Overlay.");
        #endif*//*
      }*/
    }

    private void ConfigWindow()
    {
      this.config = true;
    }

    private void Command(string command, string arguments)
    {
      this.config = true;
    }

    private void EgloAddonHandler()
    {
      Echoglossian.PluginLog.Information("EgloAddonHandler called.");


      AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "JournalResult", this.UiJournalResultHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PostReceiveEvent, "RecommendList", this.UiRecommendListHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "RecommendList", this.UiRecommendListHandlerAsync);
      AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "AreaMap", this.UiAreaMapHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "ScenarioTree", this.UiScenarioTreeHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PreUpdate, "Journal", this.UiJournalQuestHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "Journal", this.UiJournalDetailHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PreRequestedUpdate, "JournalDetail", this.UiJournalDetailHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "JournalAccept", this.UiJournalAcceptHandler);
      AddonLifecycle.RegisterListener(AddonEvent.PostRequestedUpdate, "_ToDoList", this.UiToDoListHandler);

      this.EgloNeutralAddonHandler("Talk", new string[] {/* "PreSetup", "PostSetup",*/ "PreUpdate", /* "PostUpdate", "PreDraw", "PostDraw", "PreFinalize", "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate", "PreRefresh", "PostRefresh" */});
      // this.EgloNeutralAddonHandler("_BattleTalk", new string[] { "PreSetup", "PostSetup", "PreUpdate", "PostUpdate", "PreDraw", "PostDraw", "PreFinalize", "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate", "PreRefresh", "PostRefresh" });





      /*      AddonLifecycle.RegisterListener(AddonEvent.PreSetup, "TalkSubtitle", this.UpdateUI);
            AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "TalkSubtitle", this.UpdateUI);*/
    }
  }
}
