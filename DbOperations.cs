﻿// <copyright file="DbOperations.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Echoglossian.EFCoreSqlite;
using Echoglossian.EFCoreSqlite.Models;
using Echoglossian.EFCoreSqlite.Models.Journal;
using ImGuiNET;
using Microsoft.EntityFrameworkCore;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public static TalkMessage FoundTalkMessage { get; set; }

    public ToastMessage FoundToastMessage { get; set; }

    public static BattleTalkMessage FoundBattleTalkMessage { get; set; }

    public static TalkSubtitleMessage FoundTalkSubtitleMessage { get; set; }

    public async void CreateOrUseDb()
    {
      using (EchoglossianDbContext context = new EchoglossianDbContext(this.configDir))
      {
        PluginLog.Debug($"Config dir path: {this.configDir}");
        try
        {
          PluginLog.Debug($"Config dir path: {this.configDir}");

          var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

          if (pendingMigrations.Any())
          {
            PluginLog.Debug($"Pending migrations: {pendingMigrations.Count()}");
            await context.Database.MigrateAsync();
          }

          var lastAppliedMigration = (await context.Database.GetAppliedMigrationsAsync()).Last();

          PluginLog.Debug($"Last applied migration: {lastAppliedMigration}");
        }
        catch (Exception e)
        {
          PluginLog.Error($"Error creating or using Db: {e}");
        }
        finally
        {
          PluginLog.Debug($"Db created or used successfully");
        }
      }
    }

    public TalkMessage FindAndReturnTalkMessage(TalkMessage talkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(Echoglossian.PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

      var pluginConfig = Echoglossian.PluginInterface.GetPluginConfig() as Config;

      try
      {
        IQueryable<TalkMessage> existingTalkMessage =
          context.TalkMessage.Where(t =>
            t.SenderName == talkMessage.SenderName &&
            t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
            t.TranslationLang == talkMessage.TranslationLang);
        if (pluginConfig.TranslateAlreadyTranslatedTexts)
        {
          existingTalkMessage = existingTalkMessage.Where(t => t.TranslationEngine == talkMessage.TranslationEngine);
        }

        TalkMessage localFoundTalkMessage = existingTalkMessage.FirstOrDefault();
        if (existingTalkMessage.FirstOrDefault() == null ||
            localFoundTalkMessage?.OriginalTalkMessage != talkMessage.OriginalTalkMessage)
        {
          return null;
        }

        return localFoundTalkMessage;
      }
      catch (Exception e)
      {
        return null;
      }
    }

    public static bool FindTalkMessage(TalkMessage talkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

      PluginLog.Debug($"TalkMessage to be found in DB: {talkMessage}");

      var pluginConfig = PluginInterface.GetPluginConfig() as Config;

      try
      {
        IQueryable<TalkMessage> existingTalkMessage =
          context.TalkMessage.Where(t =>
            t.SenderName == talkMessage.SenderName &&
            t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
            t.TranslationLang == talkMessage.TranslationLang);
        if (pluginConfig.TranslateAlreadyTranslatedTexts)
        {
          existingTalkMessage = existingTalkMessage.Where(t => t.TranslationEngine == talkMessage.TranslationEngine);
        }

        TalkMessage localFoundTalkMessage = existingTalkMessage.FirstOrDefault();
        if (existingTalkMessage.FirstOrDefault() == null ||
            localFoundTalkMessage?.OriginalTalkMessage != talkMessage.OriginalTalkMessage)
        {
          FoundTalkMessage = talkMessage;
          return false;
        }

        FoundTalkMessage = localFoundTalkMessage;

        PluginLog.Debug($"FoundTalkMessage in DB: {FoundTalkMessage}");

        return true;
      }
      catch (Exception e)
      {
        return false;
      }
    }

    public ToastMessage FindToastMessage(ToastMessage toastMessage)
    {
      try
      {
        List<ToastMessage> cache = this.OtherToastsCache;
        if (cache == null || cache.Count == 0)
        {
          this.LoadAllOtherToasts();
          cache = this.OtherToastsCache;

          if (cache == null || cache.Count == 0)
          {
            return null;
          }
        }

        IEnumerable<ToastMessage> existingToastMessage =
          cache.Where(t => t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
                           t.TranslationLang == toastMessage.TranslationLang &&
                           t.ToastType == toastMessage.ToastType);

        if (this.configuration.TranslateAlreadyTranslatedTexts)
        {
          existingToastMessage = existingToastMessage.Where(t => t.TranslationEngine == toastMessage.TranslationEngine);
        }

        ToastMessage localFoundToastMessage = existingToastMessage.FirstOrDefault();

        PluginLog.Debug($"localFoundToasMessage: {localFoundToastMessage}");

        if (localFoundToastMessage == null ||
            localFoundToastMessage.OriginalToastMessage != toastMessage.OriginalToastMessage)
        {
          return null;
        }

        return localFoundToastMessage;
      }
      catch (Exception e)
      {
        PluginLog.Warning($"FindToastMessage exception {e}");
        return null;
      }
    }

    public ToastMessage FindErrorToastMessage(ToastMessage toastMessage)
    {
      try
      {
        List<ToastMessage> cache = this.ErrorToastsCache;
        if (cache == null || cache.Count == 0)
        {
          this.LoadAllErrorToasts();
          cache = this.ErrorToastsCache;

          if (cache == null || cache.Count == 0)
          {
            return null;
          }
        }

        IEnumerable<ToastMessage> existingToastMessage =
          cache.Where(t => t.OriginalToastMessage == toastMessage.OriginalToastMessage &&
                                          t.TranslationLang == toastMessage.TranslationLang &&
                                          t.ToastType == toastMessage.ToastType);

        if (this.configuration.TranslateAlreadyTranslatedTexts)
        {
          existingToastMessage = existingToastMessage.Where(t => t.TranslationEngine == toastMessage.TranslationEngine);
        }

        ToastMessage localFoundToastMessage = existingToastMessage.FirstOrDefault();

        if (localFoundToastMessage == null ||
            localFoundToastMessage.OriginalToastMessage != toastMessage.OriginalToastMessage)
        {
          return null;
        }

        return localFoundToastMessage;
      }
      catch (Exception e)
      {
        PluginLog.Warning($"FindAndReturnErrorToastMessage exception {e}");
        return null;
      }
    }

    public BattleTalkMessage FindAndReturnBattleTalkMessage(BattleTalkMessage battleTalkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

      var pluginConfig = PluginInterface.GetPluginConfig() as Config;

      try
      {
        IQueryable<BattleTalkMessage> existingBattleTalkMessage =
          context.BattleTalkMessage.Where(t =>
            t.SenderName == battleTalkMessage.SenderName &&
            t.OriginalBattleTalkMessage == battleTalkMessage.OriginalBattleTalkMessage &&
            t.TranslationLang == battleTalkMessage.TranslationLang);

        if (pluginConfig.TranslateAlreadyTranslatedTexts)
        {
          existingBattleTalkMessage = existingBattleTalkMessage.Where(t => t.TranslationEngine == battleTalkMessage.TranslationEngine);
        }

        BattleTalkMessage localFoundBattleTalkMessage = existingBattleTalkMessage.FirstOrDefault();
        if (existingBattleTalkMessage.FirstOrDefault() == null ||
            localFoundBattleTalkMessage?.OriginalBattleTalkMessage != battleTalkMessage.OriginalBattleTalkMessage)
        {
          return null;
        }

        FoundBattleTalkMessage = localFoundBattleTalkMessage;

        return localFoundBattleTalkMessage;
      }
      catch (Exception e)
      {
        PluginLog.Debug($"FindAndReturnBattleTalkMessage exception {e}");
        return null;
      }
    }

    public static bool FindBattleTalkMessage(BattleTalkMessage battleTalkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

      PluginLog.Debug($"BattleTalkMessage to be found in DB: {battleTalkMessage}");

      var pluginConfig = PluginInterface.GetPluginConfig() as Config;

      try
      {
        IQueryable<BattleTalkMessage> existingBattleTalkMessage =
          context.BattleTalkMessage.Where(t =>
            t.SenderName == battleTalkMessage.SenderName &&
            t.OriginalBattleTalkMessage == battleTalkMessage.OriginalBattleTalkMessage &&
            t.TranslationLang == battleTalkMessage.TranslationLang);

        if (pluginConfig.TranslateAlreadyTranslatedTexts)
        {
          existingBattleTalkMessage = existingBattleTalkMessage.Where(t => t.TranslationEngine == battleTalkMessage.TranslationEngine);
        }

        BattleTalkMessage localFoundBattleTalkMessage = existingBattleTalkMessage.FirstOrDefault();
        if (existingBattleTalkMessage.FirstOrDefault() == null ||
            localFoundBattleTalkMessage?.OriginalBattleTalkMessage != battleTalkMessage.OriginalBattleTalkMessage)
        {
          FoundBattleTalkMessage = battleTalkMessage;
          return false;
        }

        FoundBattleTalkMessage = localFoundBattleTalkMessage;

        PluginLog.Debug($"FoundBattleTalkMessage in DB: {FoundBattleTalkMessage}");
        return true;
      }
      catch (Exception e)
      {
        return false;
      }
    }

    public QuestPlate FindQuestPlate(QuestPlate questPlate)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      try
      {
        IQueryable<QuestPlate> existingQuestPlate =
          context.QuestPlate.Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.OriginalQuestMessage == questPlate.OriginalQuestMessage &&
            t.TranslationLang == questPlate.TranslationLang);

        if (this.configuration.TranslateAlreadyTranslatedTexts)
        {
          existingQuestPlate = existingQuestPlate.Where(t => t.TranslationEngine == questPlate.TranslationEngine);
        }

        QuestPlate localFoundQuestPlate = existingQuestPlate.FirstOrDefault();
        if (localFoundQuestPlate == null || localFoundQuestPlate.OriginalQuestMessage != questPlate.OriginalQuestMessage)
        {
          return null;
        }

        localFoundQuestPlate.UpdateFieldsFromText();
        return localFoundQuestPlate;
      }
      catch (Exception e)
      {
        return null;
      }
    }

    public QuestPlate FindQuestPlateByName(QuestPlate questPlate)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      try
      {
        IQueryable<QuestPlate> existingQuestPlate =
          context.QuestPlate.Where(t =>
            t.QuestName == questPlate.QuestName &&
            t.TranslationLang == questPlate.TranslationLang);

        if (this.configuration.TranslateAlreadyTranslatedTexts)
        {
          existingQuestPlate = existingQuestPlate.Where(t => t.TranslationEngine == questPlate.TranslationEngine);
        }

        QuestPlate localFoundQuestPlate = existingQuestPlate.FirstOrDefault();

        if (localFoundQuestPlate == null || localFoundQuestPlate.QuestName != questPlate.QuestName)
        {
          return null;
        }

        localFoundQuestPlate.UpdateFieldsFromText();
        return localFoundQuestPlate;
      }
      catch (Exception e)
      {
        return null;
      }
    }

    public TalkSubtitleMessage FindAndReturnTalkSubtitleMessage(TalkSubtitleMessage talkSubtitleMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      try
      {
        IQueryable<TalkSubtitleMessage> existingTalkSubtitleMessage =
          context.TalkSubtitleMessage.Where(t =>
                     t.OriginalTalkSubtitleMessage == talkSubtitleMessage.OriginalTalkSubtitleMessage &&
                                t.TranslationLang == talkSubtitleMessage.TranslationLang);

        if (this.configuration.TranslateAlreadyTranslatedTexts)
        {
          existingTalkSubtitleMessage = existingTalkSubtitleMessage.Where(t => t.TranslationEngine == talkSubtitleMessage.TranslationEngine);
        }

        TalkSubtitleMessage localFoundTalkSubtitleMessage = existingTalkSubtitleMessage.FirstOrDefault();
        if (localFoundTalkSubtitleMessage == null ||
                     localFoundTalkSubtitleMessage.OriginalTalkSubtitleMessage != talkSubtitleMessage.OriginalTalkSubtitleMessage)
        {
          return null;
        }

        return localFoundTalkSubtitleMessage;
      }
      catch (Exception e)
      {
        PluginLog.Debug($"FindAndReturnTalkSubtitleMessage exception {e}");
        return null;
      }
    }

    public static bool FindTalkSubtitleMessage(TalkSubtitleMessage talkSubtitleMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);

      PluginLog.Debug($"TalkSubtitleMessage to be found in DB: {talkSubtitleMessage}");

      var pluginConfig = PluginInterface.GetPluginConfig() as Config;

      try
      {
        IQueryable<TalkSubtitleMessage> existingTalkSubtitleMessage =
          context.TalkSubtitleMessage.Where(t =>
                              t.OriginalTalkSubtitleMessage == talkSubtitleMessage.OriginalTalkSubtitleMessage &&
                                                             t.TranslationLang == talkSubtitleMessage.TranslationLang);

        if (pluginConfig.TranslateAlreadyTranslatedTexts)
        {
          existingTalkSubtitleMessage = existingTalkSubtitleMessage.Where(t => t.TranslationEngine == talkSubtitleMessage.TranslationEngine);
        }

        TalkSubtitleMessage localFoundTalkSubtitleMessage = existingTalkSubtitleMessage.FirstOrDefault();
        if (existingTalkSubtitleMessage.FirstOrDefault() == null ||
                              localFoundTalkSubtitleMessage?.OriginalTalkSubtitleMessage != talkSubtitleMessage.OriginalTalkSubtitleMessage)
        {
          FoundTalkSubtitleMessage = talkSubtitleMessage;
          return false;
        }

        FoundTalkSubtitleMessage = localFoundTalkSubtitleMessage;

        PluginLog.Debug($"FoundTalkSubtitleMessage in DB: {FoundTalkMessage}");
        return true;
      }
      catch (Exception e)
      {
        return false;
      }
    }

    public static string InsertTalkData(TalkMessage talkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);
#if DEBUG
      // using StreamWriter logStream = new($"{this.configDir}DbInsertTalkOperationsLog.txt", append: true);
      PluginLog.Debug($"TalkMessage to be saved in DB: {talkMessage}");
#endif

      var pluginConfig = PluginInterface.GetPluginConfig() as Config;

      try
      {
        if (pluginConfig.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(talkMessage.ToString());
        }

        context.TalkMessage.Attach(talkMessage);

        context.SaveChangesAsync();
        return "Data inserted to TalkMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public static string InsertBattleTalkData(BattleTalkMessage battleTalkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);
      /*#if DEBUG
            using StreamWriter logStream = new($"{this.configDir}DbInsertBattleTalkOperationsLog.txt", append: true);
      #endif*/

      var pluginConfig = PluginInterface.GetPluginConfig() as Config;

      try
      {
        context.BattleTalkMessage.Attach(battleTalkMessage);

        if (pluginConfig.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(battleTalkMessage.ToString());
        }

        context.SaveChangesAsync();

        return "Data inserted to BattleTalkMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public static string InsertTalkSubtitleData(TalkSubtitleMessage talkSubtitleMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar);
      /*#if DEBUG
       *            using StreamWriter logStream = new($"{this.configDir}DbInsertTalkSubtitleOperationsLog.txt", append: true);
       *                 #endif*/

      var pluginConfig = PluginInterface.GetPluginConfig() as Config;

      try
      {
        context.TalkSubtitleMessage.Attach(talkSubtitleMessage);

        if (pluginConfig.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(talkSubtitleMessage.ToString());
        }

        context.SaveChangesAsync();

        return "Data inserted to TalkSubtitleMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string InsertErrorToastMessageData(ToastMessage toastMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);

      try
      {
        bool isInThere;
        if (this.ErrorToastsCache != null && this.ErrorToastsCache.Count > 0)
        {
          PluginLog.Debug($"Total ErrorToasts in cache: {this.ErrorToastsCache.Count}");
          isInThere = this.ErrorToastsCache.Exists(t => toastMessage.ToastType == t.ToastType &&
                                                        toastMessage.TranslationLang == t.TranslationLang &&
                                                        toastMessage.OriginalToastMessage == t.OriginalToastMessage &&
                                                        toastMessage.TranslationEngine == t.TranslationEngine);
        }
        else
        {
          isInThere = false;
        }

        if (isInThere)
        {
          return "Data already in the Db.";
        }

        context.ToastMessage.Attach(toastMessage);

        context.SaveChangesAsync();

        this.LoadAllErrorToasts();

        return "Data inserted to ToastMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string InsertOtherToastMessageData(ToastMessage toastMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);

      try
      {
        bool isInThere;
        if (this.OtherToastsCache != null && this.OtherToastsCache.Count > 0)
        {
          PluginLog.Debug($"Total ErrorToasts in cache: {this.OtherToastsCache.Count}");

          isInThere = this.OtherToastsCache.Exists(t => toastMessage.ToastType == t.ToastType &&
                                                        toastMessage.TranslationLang == t.TranslationLang &&
                                                        toastMessage.OriginalToastMessage == t.OriginalToastMessage &&
                                                        toastMessage.TranslationEngine == t.TranslationEngine);
        }
        else
        {
          isInThere = false;
        }

        if (isInThere)
        {
          return "Data already in the Db.";
        }

        context.ToastMessage.Attach(toastMessage);

        context.SaveChangesAsync();

        this.LoadAllOtherToasts();

        return "Data inserted to ToastMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string InsertQuestPlate(QuestPlate questPlate)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);

      try
      {
        questPlate.UpdateFieldsAsText();
        context.QuestPlate.Attach(questPlate);
        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(questPlate.ToString());
        }

        context.SaveChangesAsync();
        return "Data inserted to QuestPlate table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string UpdateQuestPlate(QuestPlate questPlate)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      try
      {
        questPlate.UpdateFieldsAsText();
        context.QuestPlate.Update(questPlate);
        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(questPlate.ToString());
        }

        context.SaveChangesAsync();
        return "Data updated on QuestPlate table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public void LoadAllErrorToasts()
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      this.ErrorToastsCache = new List<ToastMessage>();
      try
      {
        IQueryable<ToastMessage> existingToastMessages =
          context.ToastMessage
            .Where(t => t.ToastType == "Error");

        foreach (ToastMessage t in existingToastMessages)
        {
          this.ErrorToastsCache.Add(t);
        }
      }
      catch (Exception e)
      {
        PluginLog.Warning("Could not find any Error Toasts in Database");
      }
    }

    public void LoadAllOtherToasts()
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      this.OtherToastsCache = new List<ToastMessage>();

      try
      {
        IQueryable<ToastMessage> existingToastMessages =
          context.ToastMessage
            .Where(t => t.ToastType == "NonError");

        foreach (ToastMessage t in existingToastMessages)
        {
          this.OtherToastsCache.Add(t);
        }
      }
      catch (Exception e)
      {
        PluginLog.Warning("Could not find any Other Toasts in Database");
      }
    }
  }
}