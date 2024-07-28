using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;

namespace Echoglossian
{
  public partial class ChatGPTTranslator : ITranslator
  {
    private readonly IPluginLog pluginLog;
    private readonly string apiKey;
    private readonly HttpClient httpClient;

    public ChatGPTTranslator(IPluginLog pluginLog, string apiKey)
    {
      this.pluginLog = pluginLog;
      this.apiKey = apiKey;

      // Simplificando a configuração do HttpClientHandler
      HttpClientHandler handler = new HttpClientHandler
      {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
      };

      this.httpClient = new HttpClient(handler);
      this.httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {this.apiKey}");
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage)
    {
      this.pluginLog.Debug("inside ChatGPTTranslator TranslateAsync method");

      try
      {
        var requestBody = new
        {
          model = "gpt-3.5-turbo",
          messages = new[]
            {
              new
               {
                role = "system",
                content = $"You are a professional translator and localizer. Translate the following text to {targetLanguage} in a natural and fluent manner, making it feel as if it were originally written in that language. Avoid literal translations and ensure the translation is localized, preserving the context, meaning, emotion, tone, and logical and syntactical sense for the target language. " +
                          $"Use your knowledge of the Final Fantasy XIV universe to correctly translate names of cities, characters, locations, items, and use the appropriate pronouns based on the original text. " +
                          $"Translate text inside <> and return the translated text inside <> as well. Only return in <> the text that was already inside the brackets within <> and not the entire text. " +
                          $"Ensure that the translated text does not exceed 400 characters. If the translated text exceeds this limit, reduce it while maintaining the sense and context. " +
                          $"Translate with attention to the context, sentiment, and tone of the original phrase to create a vivid and accurate translation." },
          new
              {
                role = "user",
                content = text
              }
            }
        };

        var requestBodyText = JsonConvert.SerializeObject(requestBody);

        var response = await this.httpClient.PostAsync("https://api.openai.com/v1/chat/completions", new StringContent(
            requestBodyText,
            Encoding.UTF8,
            "application/json"));

        if (response.IsSuccessStatusCode)
        {
          var jsonString = await response.Content.ReadAsStringAsync();
          var gptResponse = JsonConvert.DeserializeObject<ChatGPTResponse>(jsonString);
          var finalTranslatedText = gptResponse.Choices[0].Message.Content;
          this.pluginLog.Warning($"FinalTranslatedText: {finalTranslatedText}");
          return finalTranslatedText;
        }
        else
        {
          this.pluginLog.Warning($"ChatGPTTranslator TranslateAsync error: {response.StatusCode}");
          return text;
        }
      }
      catch (Exception exception)
      {
        this.pluginLog.Warning($"ChatGPTTranslator TranslateAsync: {exception.Message}");
        return text;
      }
    }

    public string Translate(string text, string sourceLanguage, string targetLanguage)
    {
      return this.TranslateAsync(text, sourceLanguage, targetLanguage).Result;
    }
  }

  public class ChatGPTResponse
  {
    public Choice[] Choices { get; set; }
  }

  public class Choice
  {
    public Message Message { get; set; }
  }

  public class Message
  {
    public string Content { get; set; }
  }
}
