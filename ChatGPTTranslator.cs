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
      this.chatClient = new ChatClient(model, apiKey);
      this.pluginLog = pluginLog;
      this.model = model;
      this.temperature = 0.1f;
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return TranslateAsync(text, sourceLanguage, targetLanguage).GetAwaiter().GetResult();
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}";
      if (translationCache.TryGetValue(cacheKey, out string cachedTranslation))
      {
        return cachedTranslation;
      }

      string prompt = @$"As a professional translator and cultural expert, translate the following text from {sourceLanguage} to {targetLanguage}. 
                         Your translation should sound natural and localized, as if it were originally written in {targetLanguage}. 
                         Consider cultural nuances, idiomatic expressions, and context to ensure the translation resonates with native {targetLanguage} speakers.

                          Important: The final translation MUST NOT exceed 256 characters. If the initial translation is longer, carefully adapt and shorten it while preserving the core meaning and context.

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
          ChatMessage.CreateUserMessage(prompt)
        };

        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, chatCompletionOptions);
        string translatedText = completion.ToString().Trim();

        translatedText = translatedText.Trim('"');

        if (translatedText.Length > 256)
        {
          prompt = @$"The following translation exceeds 256 characters. Please adapt and shorten it to fit within 256 characters while preserving the core meaning and context:

                    {translatedText}

                    Provide only the adapted translation, without any explanations, additional comments, or quotation marks.";

          completion = await chatClient.CompleteChatAsync(prompt);
          translatedText = completion.ToString().Trim().Trim('"');
        }

        if (!string.IsNullOrEmpty(translatedText) && translatedText.Length <= 300)
        {
          translationCache[cacheKey] = translatedText;
          return translatedText;
        }
        else
        {
          pluginLog.Error($"Translation failed or exceeded 300 characters: {translatedText.Length} characters");
          return null;
        }
      }
      catch (Exception ex)
      {
        pluginLog.Error($"Translation error: {ex.Message}");
      }

      return null;
    }
  }
}