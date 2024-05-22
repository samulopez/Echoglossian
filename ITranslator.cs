// <copyright file="ITranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System.Threading.Tasks;

namespace Echoglossian
{
  public interface ITranslator
  {
    string Translate(string text, string sourceLanguage, string targetLanguage);

    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage);
  }
}
