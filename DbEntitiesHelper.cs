﻿// <copyright file="DbEntitiesHelper.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using EFCoreSqlite.Models;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public TalkMessage FormatTalkMessage(string sender, string text)
    {
      return new TalkMessage(sender, text, LangIdentify(text), LangIdentify(sender), string.Empty, string.Empty,
        Codes[this.configuration.Lang]);
    }

    public ToastMessage FormatToastMessage(string type, string text)
    {
      return new ToastMessage(type, text, LangIdentify(text), string.Empty,
        Codes[this.configuration.Lang]);
    }
  }
}
