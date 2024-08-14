// <copyright file="ChatGPTTranslator.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Dalamud.Plugin.Services;
using OpenAI.Chat;

namespace Echoglossian
{
  public class ChatGPTTranslator : ITranslator
  {
    private readonly ChatClient chatClient;
    private readonly IPluginLog pluginLog;
    private readonly string model;
    private readonly float temperature;
    private Dictionary<string, string> translationCache = new Dictionary<string, string>();

    public ChatGPTTranslator(IPluginLog pluginLog, string apiKey, string model = "gpt-4o-mini")
    {
      this.pluginLog = pluginLog;
      this.model = model;
      this.temperature = 0.1f;

      if (string.IsNullOrWhiteSpace(apiKey))
      {
        this.pluginLog.Warning("API Key is empty or invalid. ChatGPT transaltion will not be available.");
        this.chatClient = null;
      }
      else
      {
        try
        {
          this.chatClient = new ChatClient(model, apiKey);
        }
        catch (Exception ex)
        {
          this.pluginLog.Error($"Failed to initialize GPT ChatClient: {ex.Message}");
          this.chatClient = null;
        }
      }
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).GetAwaiter().GetResult();
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {

      if (this.chatClient == null)
      {
        return "[ChatGPT translation unavailable. Please check your API key.]";
      }

      string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
      if (this.translationCache.TryGetValue(cacheKey, out string cachedTranslation))
      {
        return cachedTranslation;
      }

      string prompt = @$"As a professional translator and cultural expert, translate the following text from {sourceLanguage} to {targetLanguage}. 
                         Your translation should sound natural and localized, as if it were originally written in {targetLanguage}. 
                         Consider cultural nuances, idiomatic expressions, and context to ensure the translation resonates with native {targetLanguage} speakers.

                          Text to translate: ""{text}""

                          Provide only the translated text in your response, without any explanations, additional comments, or quotation marks.";

      try
      {
        var chatCompletionOptions = new ChatCompletionOptions
        {
          Temperature = this.temperature,
        };

        var messages = new List<ChatMessage>
        {
          ChatMessage.CreateUserMessage(prompt),
        };

        ChatCompletion completion = await this.chatClient.CompleteChatAsync(messages, chatCompletionOptions);
        string translatedText = completion.ToString().Trim();

        translatedText = translatedText.Trim('"');

        if (!string.IsNullOrEmpty(translatedText))
        {
          this.translationCache[cacheKey] = translatedText;
          return translatedText;
        }
      }
      catch (Exception ex)
      {
        this.pluginLog.Error($"Translation error: {ex.Message}");
        return $"[Translation Error: {ex.Message}]";
      }

      return null;
    }
  }
}