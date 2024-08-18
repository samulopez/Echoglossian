// <copyright file="LocationName.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Echoglossian.EFCoreSqlite.Models
{
  [Table("locationnames")]
  public class LocationName
  {
    [Key]
    public int Id { get; set; }

    [Required]
    public string OriginalLocationName { get; set; }

    [Required]
    public string OriginalLocationNameLang { get; set; }

    public string TranslatedLocationName { get; set; }

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
    /// Initializes a new instance of the <see cref="LocationName"/> class.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="originalLocationName"></param>
    /// <param name="originalLocationNameLang"></param>
    /// <param name="translatedLocationName"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <param name="createdDate"></param>
    /// <param name="updatedDate"></param>
    public LocationName(int id, string originalLocationName, string originalLocationNameLang, string translatedLocationName, string translationLang, int translationEngine, DateTime createdDate, DateTime? updatedDate)
    {
      this.Id = id;
      this.OriginalLocationName = originalLocationName;
      this.OriginalLocationNameLang = originalLocationNameLang;
      this.TranslatedLocationName = translatedLocationName;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;
    }

    public override string ToString()
    {
      return
        $"Id: {this.Id}, " +
        $"OriginalLocationName: {this.OriginalLocationName}, " +
        $"OriginalLocationNameLang: {this.OriginalLocationNameLang}, " +
        $"TranslatedLocationName: {this.TranslatedLocationName}, " +
        $"TranslationLang: {this.TranslationLang}, " +
        $"TranslationEngine: {this.TranslationEngine}, " +
        $"CreatedDate: {this.CreatedDate}, " +
        $"UpdatedDate: {this.UpdatedDate}";
    }
  }
}
