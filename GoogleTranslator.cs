// <copyright file="GoogleTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

using Dalamud.Plugin.Services;

namespace Echoglossian
{
  public class GoogleTranslator : ITranslator
  {
    private readonly IPluginLog pluginLog;
    private readonly HttpClient httpClient;
    private const string NewGTranslateUrl = "https://translate.google.com/m";
    private const string UaString = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/88.0.4324.104 Safari/537.36";

    public GoogleTranslator(IPluginLog pluginLog)
    {
      this.pluginLog = pluginLog;
      this.httpClient = new HttpClient();
      this.httpClient.DefaultRequestHeaders.Add("User-Agent", UaString);
    }

    string ITranslator.Translate(string text, string sourceLanguage, string targetLanguage)
    {
      this.pluginLog.Debug("inside GoogleTranslator translate method");

      try
      {
        string parsedText = text;
        parsedText = parsedText.Replace("\u200B", string.Empty);

        string url = $"{NewGTranslateUrl}?hl=en&sl={sourceLanguage}&tl={targetLanguage}&q={Uri.EscapeDataString(parsedText)}";

        this.pluginLog.Debug($"URL: {url}");

        var requestResult = this.httpClient.GetStreamAsync(url).Result;
        StreamReader reader = new StreamReader(requestResult ?? throw new Exception());

        return this.FormatStreamReader(reader.ReadToEnd());
      }
      catch (Exception e)
      {
        this.pluginLog.Warning(e.ToString());
        throw;
      }
    }

    async Task<string> ITranslator.TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      this.pluginLog.Debug("inside GoogleTranslator translateAsync method");

      try
      {
        string parsedText = text;
        parsedText = parsedText.Replace("\u200B", string.Empty);

        string url = $"{NewGTranslateUrl}?hl=en&sl={sourceLanguage}&tl={targetLanguage}&q={Uri.EscapeDataString(parsedText)}";

        this.pluginLog.Debug($"URL: {url}");

        var requestResult = await this.httpClient.GetStreamAsync(url);
        StreamReader reader = new StreamReader(requestResult ?? throw new Exception());

        return this.FormatStreamReader(reader.ReadToEnd());
      }
      catch (Exception e)
      {
        this.pluginLog.Warning(e.ToString());
        throw;
      }
    }

    private string FormatStreamReader(string read)
    {
      string finalText;
      if (read.StartsWith("[\""))
      {
        char[] start = { '[', '\"' };
        char[] end = { '\"', ']' };
        var dialogueText = read.TrimStart(start);
        finalText = dialogueText.TrimEnd(end);
      }
      else
      {
        finalText = this.ParseHtml(read);
      }

      finalText = finalText.Replace("\u200B", string.Empty);
      finalText = finalText.Replace("\u005C\u0022", "\u0022");
      finalText = finalText.Replace("\u005C\u002F", "\u002F");
      finalText = finalText.Replace("\\u003C", "<");
      finalText = finalText.Replace("&#39;", "\u0027");
      finalText = Regex.Replace(finalText, @"(?<=.)(─)(?=.)", " \u2015 ");

      this.pluginLog.Debug($"FinalTranslatedText: {finalText}");

      return finalText;
    }

    private string ParseHtml(string html)
    {
      using StringWriter stringWriter = new StringWriter();

      HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
      doc.LoadHtml(html);

      var text = doc.DocumentNode.Descendants()
        .Where(n => n.HasClass("result-container")).ToList();

      var parsedText = text.Single(n => n.InnerText.Length > 0).InnerText;

      HttpUtility.HtmlDecode(parsedText, stringWriter);

      string decodedString = stringWriter.ToString();
      this.pluginLog.Debug($"In parser: " + parsedText);

      return decodedString;
    }
  }
}
