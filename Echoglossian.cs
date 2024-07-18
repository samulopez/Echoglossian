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
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.Properties;

namespace Echoglossian
{
  // TODO: implement multiple fallback translation engines.
  public partial class Echoglossian : IDalamudPlugin
  {
    [PluginService]
    public static IDataManager DManager { get; private set; }

    [PluginService]
    public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

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

    public string Name => Resources.Name;

    private const string SlashCommand = "/eglo";
    private string configDir;
    private static int languageInt = 28;
    private static int fontSize = 24;
    private static int chosenTransEngine;
    private static string transEngineName;

    public static string ScriptCharList { get; set; }

    public static string SpecialFontFilePath { get; set; }

    public static string FontFilePath { get; set; }

    public static string SymbolsFontFilePath { get; set; }

    public static string DummyFontFilePath { get; set; }

    public string LangToTranslateTo = string.Empty;

    private bool pluginAssetsState;
    private static Dictionary<int, LanguageInfo> langDict;
    private bool config;

    private Config configuration;

    public static UINewFontHandler UINewFontHandler;

    public static LanguageInfo SelectedLanguage { get; set; }

    private readonly SemaphoreSlim toastTranslationSemaphore;
    private readonly SemaphoreSlim talkTranslationSemaphore;
    private readonly SemaphoreSlim nameTranslationSemaphore;
    private readonly SemaphoreSlim battleTalkTranslationSemaphore;
    private readonly SemaphoreSlim talkSubtitleTranslationSemaphore;
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

    private AtkTextNodeBufferWrapper AtkTextNodeBufferWrapper;

    private UIAddonHandler uiBattleTalkAddonHandler;
    private UIAddonHandler uiTalkAddonHandler;
    private UIAddonHandler uiTalkSubtitleHandler;

    private TranslationService translationService;

    public List<ToastMessage> ErrorToastsCache { get; set; }

    public List<ToastMessage> QuestToastsCache { get; set; }

    public List<ToastMessage> OtherToastsCache { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Echoglossian"/> class.
    /// </summary>
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

      // Common = new XivCommonBase((DalamudPluginInterface)PluginInterface, Hooks.Talk | Hooks.BattleTalk);
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
        PluginLog.Debug("Eglo database created or used successfully.");
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

      SelectedLanguage = this.languagesDictionary[this.configuration.Lang];

      /*
            PluginInterface.UiBuilder.BuildFonts += this.LoadLanguageComboFont; // needs checking
            PluginInterface.UiBuilder.BuildFonts += this.LoadFont; // needs checking
      */
      // this.ListCultureInfos();
      this.pixImage = TextureProvider.CreateFromImageAsync(Resources.pix).Result; // needs checking
      this.choiceImage = TextureProvider.CreateFromImageAsync(Resources.choice).Result;
      this.cutsceneChoiceImage = TextureProvider.CreateFromImageAsync(Resources.cutscenechoice).Result;
      this.talkImage = TextureProvider.CreateFromImageAsync(Resources.prttws).Result;
      this.logo = TextureProvider.CreateFromImageAsync(Resources.logo).Result;

      PluginInterface.UiBuilder.DisableCutsceneUiHide = this.configuration.ShowInCutscenes;

      PluginInterface.UiBuilder.OpenConfigUi += this.ConfigWindow;

      languageInt = this.configuration.Lang;

      fontSize = this.configuration.FontSize;

      chosenTransEngine = this.configuration.ChosenTransEngine;

      this.LangToTranslateTo = langDict[languageInt].Code;

      MountFontPaths();

      Echoglossian.UINewFontHandler = new UINewFontHandler(this.configuration);

      TransEngines t = (TransEngines)chosenTransEngine;
      transEngineName = t.ToString();
      this.translationService = new TranslationService(this.configuration, PluginLog, sanitizer);

      this.AtkTextNodeBufferWrapper = new AtkTextNodeBufferWrapper();

      this.LoadAllErrorToasts();
      this.LoadAllOtherToasts();

      Framework.Update += this.Tick;

      this.talkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.nameTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.battleTalkTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.senderTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.talkSubtitleTranslationSemaphore = new SemaphoreSlim(1, 1);

      this.toastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.errorToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.classChangeToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.areaToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.wideTextToastTranslationSemaphore = new SemaphoreSlim(1, 1);
      this.questToastTranslationSemaphore = new SemaphoreSlim(1, 1);

      ToastGui.Toast += this.OnToast;
      ToastGui.ErrorToast += this.OnErrorToast;
      ToastGui.QuestToast += this.OnQuestToast;

      this.uiTalkAddonHandler = new UIAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);
      this.uiBattleTalkAddonHandler = new UIAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);
      this.uiTalkSubtitleHandler = new UIAddonHandler(this.configuration, this.UiFont, this.FontLoaded, this.LangToTranslateTo);

      this.EgloAddonHandler();

      PluginInterface.UiBuilder.Draw += this.BuildUi;

      /* if (ClientState.IsLoggedIn)
       {
         this.ParseUi();
       }*/

      // Disabling BattleTalk translation by default if the language is not supported by the game font while we fix the overlays
      this.configuration.TranslateBattleTalk = this.configuration.OverlayOnlyLanguage ? false : true;
      this.configuration.TranslateTalkSubtitle = false; // disabled so we can check in depth
    }

    /// <inheritdoc />
    public void Dispose()
    {
      this.Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
      ToastGui.Toast -= this.OnToast;
      ToastGui.ErrorToast -= this.OnErrorToast;
      ToastGui.QuestToast -= this.OnQuestToast;

      PluginInterface.UiBuilder.OpenConfigUi -= this.ConfigWindow;

      this.nameTranslationSemaphore?.Dispose();
      this.talkTranslationSemaphore?.Dispose();
      this.battleTalkTranslationSemaphore?.Dispose();
      this.senderTranslationSemaphore?.Dispose();
      this.talkSubtitleTranslationSemaphore?.Dispose();
      this.toastTranslationSemaphore?.Dispose();
      this.errorToastTranslationSemaphore?.Dispose();
      this.areaToastTranslationSemaphore?.Dispose();
      this.wideTextToastTranslationSemaphore?.Dispose();
      this.questToastTranslationSemaphore?.Dispose();

      PluginInterface.UiBuilder.Draw -= this.BuildUi;

      this.uiTalkAddonHandler?.Dispose();
      this.uiBattleTalkAddonHandler?.Dispose();
      this.uiTalkSubtitleHandler?.Dispose();

      this.pixImage?.Dispose();
      this.choiceImage?.Dispose();
      this.cutsceneChoiceImage?.Dispose();
      this.talkImage?.Dispose();
      this.logo?.Dispose();

      Framework.Update -= this.Tick;

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
        // PluginLog.Debug("Translations are disabled!");
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
                this.TextErrorToastHandler("_TextError", 1);
                this.ToastHandler("_WideText", 1);
                this.ToastHandler("_TextClassChange", 1);
                this.ToastHandler("_AreaText", 1);
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

          /* PluginInterface.UiBuilder.RebuildFonts();*/
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
        // PluginLog.Debug("Showing BattleTalk Translation Overlay.");
#endif
      }

      if (this.configuration.UseImGuiForTalk && this.configuration.TranslateTalk && this.talkDisplayTranslation)
      {
        this.DrawTranslatedDialogueWindow();
#if DEBUG
        // PluginLog.Debug("Showing Talk Translation Overlay.");
#endif
      }

      if (this.configuration.UseImGuiForTalkSubtitle && this.configuration.TranslateTalkSubtitle && this.talkSubtitleDisplayTranslation)
      {
        this.DrawTranslatedTalkSubtitleWindow();
#if DEBUG
        // PluginLog.Debug("Showing TalkSubtitle Translation Overlay.");
#endif
      }

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
#if DEBUG
      PluginLog.Debug("EgloAddonHandler called.");
#endif

      if (this.configuration.TranslateTalk)
      {
        // this.EgloNeutralAddonHandler("Talk", new string[] {  /* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/ });

        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "Talk", this.UiTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "Talk", this.UiTalkAsyncHandler);
      }

      if (this.configuration.TranslateBattleTalk)
      {
        // this.EgloNeutralAddonHandler("_BattleTalk", new string[] { /* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/});

        AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_BattleTalk", this.UiBattleTalkAsyncHandler);
        AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "_BattleTalk", this.UiBattleTalkAsyncHandler);
      }

      /*if (this.configuration.TranslateTalkSubtitle)
      {*/
      // this.EgloNeutralAddonHandler("TalkSubtitle", new string[] {/* "PreUpdate", "PostUpdate",*/ "PreDraw",/* "PostDraw",  "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate" ,*/ "PreRefresh",/* "PostRefresh"*/});
      /* AddonLifecycle.RegisterListener(AddonEvent.PreRefresh, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
       AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
       AddonLifecycle.RegisterListener(AddonEvent.PreReceiveEvent, "TalkSubtitle", this.UiTalkSubtitleAsyncHandler);
     }*/

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

      /*"PreSetup","PostSetup", "PreUpdate", "PostUpdate", "PreDraw", "PostDraw", "PreFinalize", "PreReceiveEvent", "PostReceiveEvent", "PreRequestedUpdate", "PostRequestedUpdate", "PreRefresh", "PostRefresh" */
    }
  }
}
