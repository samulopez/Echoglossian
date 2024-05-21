// <copyright file="DbOperations.cs" company="lokinmodar">
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
    public TalkMessage FoundTalkMessage { get; set; }

    public ToastMessage FoundToastMessage { get; set; }

    public BattleTalkMessage FoundBattleTalkMessage { get; set; }

    public async void CreateOrUseDb()
    {
      using (EchoglossianDbContext context = new EchoglossianDbContext(this.configDir))
      {
        try
        {
          PluginLog.Verbose($"Config dir path: {this.configDir}");

          var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

          if (pendingMigrations.Any())
          {
            PluginLog.Verbose($"Pending migrations: {pendingMigrations.Count()}");
            await context.Database.MigrateAsync();
          }

          var lastAppliedMigration = (await context.Database.GetAppliedMigrationsAsync()).Last();

          PluginLog.Verbose($"Last applied migration: {lastAppliedMigration}");
        }
        catch (Exception e)
        {
          PluginLog.Error($"Error creating or using Db: {e}");
        }
        finally
        {
          PluginLog.Verbose($"Db created or used successfully");
        }
      }
    }

    public bool FindTalkMessage(TalkMessage talkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      try
      {
        IQueryable<TalkMessage> existingTalkMessage =
          context.TalkMessage.Where(t =>
            t.SenderName == talkMessage.SenderName &&
            t.OriginalTalkMessage == talkMessage.OriginalTalkMessage &&
            t.TranslationLang == talkMessage.TranslationLang);
        if (this.configuration.TranslateAlreadyTranslatedTexts)
        {
          existingTalkMessage = existingTalkMessage.Where(t => t.TranslationEngine == talkMessage.TranslationEngine);
        }

        TalkMessage localFoundTalkMessage = existingTalkMessage.FirstOrDefault();
        if (existingTalkMessage.FirstOrDefault() == null ||
            localFoundTalkMessage?.OriginalTalkMessage != talkMessage.OriginalTalkMessage)
        {
          this.FoundTalkMessage = talkMessage;
          return false;
        }

        this.FoundTalkMessage = localFoundTalkMessage;
        return true;
      }
      catch (Exception e)
      {
        return false;
      }
    }

    public bool FindToastMessage(ToastMessage toastMessage)
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
            return false;
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
          this.FoundToastMessage = null;
          return false;
        }

        this.FoundToastMessage = localFoundToastMessage;
        return true;
      }
      catch (Exception e)
      {
        PluginLog.Debug($"FindToastMessage exception {e}");
        return false;
      }
    }

    public bool FindErrorToastMessage(ToastMessage toastMessage)
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
            return false;
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
          this.FoundToastMessage = null;
          return false;
        }

        this.FoundToastMessage = localFoundToastMessage;
        return true;
      }
      catch (Exception e)
      {
        PluginLog.Debug($"FindErrorToastMessage exception {e}");
        return false;
      }
    }

    public bool FindBattleTalkMessage(BattleTalkMessage battleTalkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      try
      {
        IQueryable<BattleTalkMessage> existingBattleTalkMessage =
          context.BattleTalkMessage.Where(t =>
            t.SenderName == battleTalkMessage.SenderName &&
            t.OriginalBattleTalkMessage == battleTalkMessage.OriginalBattleTalkMessage &&
            t.TranslationLang == battleTalkMessage.TranslationLang);

        if (this.configuration.TranslateAlreadyTranslatedTexts)
        {
          existingBattleTalkMessage = existingBattleTalkMessage.Where(t => t.TranslationEngine == battleTalkMessage.TranslationEngine);
        }

        BattleTalkMessage localFoundBattleTalkMessage = existingBattleTalkMessage.FirstOrDefault();
        if (existingBattleTalkMessage.FirstOrDefault() == null ||
            localFoundBattleTalkMessage?.OriginalBattleTalkMessage != battleTalkMessage.OriginalBattleTalkMessage)
        {
          this.FoundBattleTalkMessage = battleTalkMessage;
          return false;
        }

        this.FoundBattleTalkMessage = localFoundBattleTalkMessage;
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

    public string InsertTalkData(TalkMessage talkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbInsertTalkOperationsLog.txt", append: true);
#endif
      try
      {
#if DEBUG
        if (!this.configuration.UseImGuiForTalk)
        {
          logStream.WriteLineAsync($"Before SaveChanges: {talkMessage}");
        }
#endif
        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(talkMessage.ToString());
        }

        // 1. Attach an entity to context with Added EntityState
        context.TalkMessage.Attach(talkMessage);
#if DEBUG
        if (!this.configuration.UseImGuiForTalk)
        {
          logStream.WriteLineAsync($"Inside Context: {context.TalkMessage.Local}");
        }
#endif

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into table
        context.SaveChangesAsync();
#if DEBUG
        if (!this.configuration.UseImGuiForTalk)
        {
          logStream.WriteLineAsync($"After 'SaveChanges': {context.TalkMessage.Local}");
        }
#endif
        return "Data inserted to TalkMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string InsertBattleTalkData(BattleTalkMessage battleTalkMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbInsertBattleTalkOperationsLog.txt", append: true);
#endif
      try
      {
        context.BattleTalkMessage.Attach(battleTalkMessage);
#if DEBUG
        if (!this.configuration.UseImGuiForBattleTalk)
        {
          logStream.WriteLineAsync($"Inside Context: {context.BattleTalkMessage.Local}");
        }
#endif
        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(battleTalkMessage.ToString());
        }

        context.SaveChangesAsync();
#if DEBUG
        if (!this.configuration.UseImGuiForBattleTalk)
        {
          logStream.WriteLineAsync($"After 'SaveChanges': {context.BattleTalkMessage.Local}");
        }
#endif
        return "Data inserted to BattleTalkMessages table.";
      }
      catch (Exception e)
      {
        return $"ErrorSavingData: {e}";
      }
    }

    public string InsertErrorToastMessageData(ToastMessage toastMessage)
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbInsertToastOperationsLog.txt", append: true);
#endif
      try
      {
        bool isInThere;
        if (this.ErrorToastsCache != null && this.ErrorToastsCache.Count > 0)
        {
#if DEBUG
          PluginLog.Verbose($"Total ErrorToasts in cache: {this.ErrorToastsCache.Count}");
          /* foreach (ToastMessage t in this.ErrorToastsCache)
           {
             PluginLog.Verbose($"{this.ErrorToastsCache.GetEnumerator().Current} :{t}");
           }*/
#endif
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
#if DEBUG
        logStream.WriteLineAsync($"Inside Context: {context.ToastMessage.Local}");
#endif

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into Students table
        context.SaveChangesAsync();
#if DEBUG
        logStream.WriteLineAsync($"After 'SaveChanges': {context.ToastMessage.Local}");
#endif

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
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbInsertToastOperationsLog.txt", append: true);
#endif
      try
      {
        bool isInThere;
        if (this.OtherToastsCache != null && this.OtherToastsCache.Count > 0)
        {
#if DEBUG
          PluginLog.Verbose($"Total ErrorToasts in cache: {this.OtherToastsCache.Count}");
          /* foreach (ToastMessage t in this.OtherToastsCache)
           {
             PluginLog.Verbose($"{this.OtherToastsCache.GetEnumerator().Current} :{t}");
           }*/
#endif
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
#if DEBUG
        logStream.WriteLineAsync($"Inside Context: {context.ToastMessage.Local}");
#endif

        // or the followings are also valid
        // context.Students.Add(std);
        // context.Entry<Student>(std).State = EntityState.Added;
        // context.Attach<Student>(std);

        // 2. Calling SaveChanges to insert a new record into Students table
        context.SaveChangesAsync();
#if DEBUG
        logStream.WriteLineAsync($"After 'SaveChanges': {context.ToastMessage.Local}");
#endif

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
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbInsertQuestPlateOperationsLog.txt", append: true);
#endif
      try
      {
        questPlate.UpdateFieldsAsText();
        context.QuestPlate.Attach(questPlate);
#if DEBUG
        logStream.WriteLineAsync($"Inside Context: {context.QuestPlate.Local}");
#endif
        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(questPlate.ToString());
        }

        context.SaveChangesAsync();
#if DEBUG
        logStream.WriteLineAsync($"After 'SaveChanges': {context.QuestPlate.Local}");
#endif
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
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbUpdateQuestPlateOperationsLog.txt", append: true);
#endif
      try
      {
        questPlate.UpdateFieldsAsText();
        context.QuestPlate.Update(questPlate);
#if DEBUG
        logStream.WriteLineAsync($"Inside Context: {context.QuestPlate.Local}");
#endif
        if (this.configuration.CopyTranslationToClipboard)
        {
          ImGui.SetClipboardText(questPlate.ToString());
        }

        context.SaveChangesAsync();
#if DEBUG
        logStream.WriteLineAsync($"After 'SaveChanges': {context.QuestPlate.Local}");
#endif
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
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbErrorToastListQueryOperationsLog.txt", append: true);
#endif
      try
      {
        IQueryable<ToastMessage> existingToastMessages =
          context.ToastMessage
            .Where(t => t.ToastType == "Error");

        foreach (ToastMessage t in existingToastMessages)
        {
          this.ErrorToastsCache.Add(t);
        }
#if DEBUG
        logStream.WriteLineAsync($"After Toast Messages table query: {this.ErrorToastsCache.ToArray()}");
#endif
      }
      catch (Exception e)
      {
#if DEBUG
        logStream.WriteLineAsync($"Query operation error: {e}");
#endif
        PluginLog.Warning("Could not find any Error Toasts in Database");
      }
    }

    public void LoadAllOtherToasts()
    {
      using EchoglossianDbContext context = new EchoglossianDbContext(this.configDir);
      this.OtherToastsCache = new List<ToastMessage>();
#if DEBUG
      using StreamWriter logStream = new($"{this.configDir}DbOtherToastListQueryOperationsLog.txt", append: true);
#endif
      try
      {
        IQueryable<ToastMessage> existingToastMessages =
          context.ToastMessage
            .Where(t => t.ToastType == "NonError");

        foreach (ToastMessage t in existingToastMessages)
        {
          this.OtherToastsCache.Add(t);
        }
#if DEBUG
        logStream.WriteLineAsync($"After Toast Messages table query: {this.OtherToastsCache.ToArray()}");
#endif
      }
      catch (Exception e)
      {
#if DEBUG
        logStream.WriteLineAsync($"Query operation error: {e}");
#endif
        PluginLog.Warning("Could not find any Other Toasts in Database");
      }
    }
  }
}