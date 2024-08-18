using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Echoglossian.EFCoreSqlite.Models
{
  [Table("gamewindows")]
  public class GameWindow
  {
    [Key]
    public int Id { get; set; }

    [Required]
    public string WindowAddonName { get; set; }

    [Required]
    public string OriginalWindowStrings { get; set; }

    [Required]
    public string OriginalWindowStringsLang { get; set; }

    public string TranslatedWindowStrings { get; set; }

    public string TranslationLang { get; set; }

    [Required]
    public int TranslationEngine { get; set; }

    [Required]
    public string GameVersion { get; set; }

    [Required]
    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GameWindow"/> class.
    /// </summary>
    /// <param name="windowAddonName"></param>
    /// <param name="originalWindowStrings"></param>
    /// <param name="originalWindowStringsLang"></param>
    /// <param name="translatedWindowStrings"></param>
    /// <param name="translationLang"></param>
    /// <param name="translationEngine"></param>
    /// <param name="gameVersion"></param>
    /// <param name="createdDate"></param>
    /// <param name="updatedDate"></param>
    public GameWindow(string windowAddonName, string originalWindowStrings, string originalWindowStringsLang, string translatedWindowStrings, string translationLang, int translationEngine, string gameVersion, DateTime createdDate, DateTime? updatedDate)
    {
      this.WindowAddonName = windowAddonName;
      this.OriginalWindowStrings = originalWindowStrings;
      this.OriginalWindowStringsLang = originalWindowStringsLang;
      this.TranslatedWindowStrings = translatedWindowStrings;
      this.TranslationLang = translationLang;
      this.TranslationEngine = translationEngine;
      this.GameVersion = gameVersion;
      this.CreatedDate = createdDate;
      this.UpdatedDate = updatedDate;

    }

    public override string ToString()
    {
      return $"GameWindow: Id={this.Id}, WindowAddonName={this.WindowAddonName}, OriginalWindowStrings={this.OriginalWindowStrings}, OriginalWindowStringsLang={this.OriginalWindowStringsLang}, TranslatedWindowStrings={this.TranslatedWindowStrings}, TranslationLang={this.TranslationLang}, TranslationEngine={this.TranslationEngine}, GameVersion={this.GameVersion}, CreatedDate={this.CreatedDate}, UpdatedDate={this.UpdatedDate}";
    }
  }
}
