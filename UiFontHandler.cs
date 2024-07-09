// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using ImGuiNET;

namespace Echoglossian;

public partial class Echoglossian
{
  public ImFontPtr ConfigUiFont;
  public static readonly string FontFileName = "NotoSans-Medium.ttf";
  public bool FontLoaded;
  public bool FontLoadFailed;
  public GCHandle? glyphRangeConfigText;

  public GCHandle? glyphRangeMainText;

  public bool LanguageComboFontLoaded;
  public bool LanguageComboFontLoadFailed;

  public static string SpecialFontFileName = string.Empty;
  public ImFontPtr UiFont;

  private static void AdjustLanguageForFontBuild()
  {
#if DEBUG
    PluginLog.Debug("Inside AdjustLanguageForFontBuild method");
#endif

    var lang = SelectedLanguage;
    SpecialFontFileName = lang.FontName;
    ScriptCharList = lang.ExclusiveCharsToAdd;
  }

  public static void MountFontPaths()
  {
    AdjustLanguageForFontBuild();

    SpecialFontFilePath = $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{SpecialFontFileName}";
    FontFilePath =
       $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{FontFileName}";
    SymbolsFontFilePath =
          $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}symbols.ttf";
    DummyFontFilePath =
          $@"{PluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";
  }

  private unsafe void AddCharsFromIntPtr(List<ushort> chars, ushort* ptr)
  {
    while (*ptr != 0)
    {
      chars.Add(*ptr);
      ptr++;
    }
  }
}