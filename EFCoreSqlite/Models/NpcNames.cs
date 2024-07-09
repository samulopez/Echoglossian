// <copyright file="NpcNames.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models
{
  [Table("npcnames")]
  public class NpcNames
  {
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(400)]
    public string OriginalNpcName { get; set; }

    [Required]
    public string OriginalNpcNameLang { get; set; }

    [MaxLength(400)]
    public string TranslatedNpcName { get; set; }

    [Required]
    public string TranslationLang { get; set; }

    [Required]
    public int TranslationEngine { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NpcNames"/> class.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="originalNpcName"></param>
    /// <param name="originalNpcNameLang"></param>
    /// <param name="translatedNpcName"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <param name="createdDate"></param>
    /// <param name="updatedDate"></param>
    public NpcNames(int id, string originalNpcName, string originalNpcNameLang, string translatedNpcName, string translationLang, int translationEngine, DateTime createdDate, DateTime? updatedDate)
    {
      this.Id = id;
      this.OriginalNpcName = originalNpcName;
      this.OriginalNpcNameLang = originalNpcNameLang;
      this.TranslatedNpcName = translatedNpcName;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
    }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, " +
        $"OriginalNpcName: {this.OriginalNpcName}, " +
        $"OriginalNpcNameLang: {this.OriginalNpcNameLang}, " +
        $"TranslatedNpcName: {this.TranslatedNpcName}, " +
        $"TranslationLang: {this.TranslationLang}, " +
        $"TranslationEngine: {this.TranslationEngine}, " +
        $"CreatedDate: {this.CreatedDate}, " +
        $"UpdatedDate: {this.UpdatedDate}";
    }
  }
}
