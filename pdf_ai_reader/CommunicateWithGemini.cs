namespace pdf_ai_reader
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Diagnostics;

    public class CommunicateWithGemini
    {
        // Replace with your actual Gemini API key
        private const string ApiKey = "";
        private const string GeminiApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent";
        private const string SystemInstruction = @"You are an expert in converting text, especially from scientific and mathematical documents (like PDFs), into a format suitable for text-to-speech (TTS) software. Your goal is to make the content accessible aurally by converting symbolic representations into spoken language.  Commas , must be pronounced as the word ""comma"" if it is important, for example in math statements, otherwise its fine. Only focus on how the words will be pronounced. Do not use parentheses. Use punctuation and several spaces as symbols to tell the TTS to pause. This is very important.

Specifically, you must:

1. Interpret mathematical notation: Translate mathematical symbols and equations into their spoken equivalents. For instance, ""+"" becomes ""plus"", ""="" becomes ""equals"", ""x^2"" becomes ""x squared"" or ""x to the power of two"", ""√x"" becomes ""the square root of x"", and so on. Handle fractions, integrals, sums, limits, and other mathematical constructs appropriately.
2. Maintain context and clarity: Ensure the spoken version retains the original meaning and context. Consider the surrounding text to disambiguate potentially ambiguous notations.
3. Handle special symbols and formatting: Convert other non-textual elements, such as special symbols (e.g., °, %, §), into their spoken forms (e.g., ""degrees"", ""percent"", ""section"").
4. Do not mention the image: The input is an image of a document, but do not mention the image in your response. Just convert the text. 
5. Don't ever assume the TTS will read something out correctly. 
6. Always after a header or some type of subheader you need to issue a little pause with a ""."" and maybe some spaces.
7. Always consider the text and how it would be naturally read aloud. Dont add weird stuff like ""period"" at the end of a sentence or ""comma"" in the middle or ""colon"" or similar stuff, because humans never read aloud those symbols, but when reading a math function like ""f(x, y)"" it is important that it is read as ""f of x comma y .""

Here are a few examples:

Input: ""Consider the equation: a + b = c^2""
Output: ""Consider the equation.     colon a plus b equals c squared.""

Input: ""The integral ∫₀¹ x² dx evaluates to 1/3.""
Output: ""The integral from zero to one of x squared dx evaluates to one third.""

Input: ""We have the following inequality: x > 5 and y ≤ 10%""
Output: ""We have the following inequality.    colon x is greater than five and y is less than or equal to ten percent.""

Input: ""Calculate the value of √25 + 3!""
Output: ""Calculate the value of the square root of twenty five plus three factorial""

Input: ""f(x) = Σᵢ xᵢ""
Output: ""f of x equals the sum over i of x sub i period""

Input: ""Let θ be an angle of 90°.""
Output: ""Let theta be an angle of ninety degrees period""

Input: ""There once was a man, that was very cool.""
Output: ""There once was a man, that was very cool.""

Input: ""The function HelloWorld(prompt, prefix) does the following""
Output: ""The function hello world, prompt comma prefix, does the following""

Only answer with the output. Never start the sentence with stuff like ""Sure"" or ""I will now convert the text"". Simply answer with the output.

Now, convert the following text from the provided image into a format suitable for text-to-speech.";
        private const string Prompt = "Please convert this image into text. Only output the text, nothing else.";

        public static async Task<string> GetTTSFriendlyText(string base64Image, string mimeType)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestData = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                inlineData = new
                                {
                                    mimeType = mimeType,
                                    data = base64Image
                                }
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = Prompt }
                        }
                    }
                },
                systemInstruction = new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = SystemInstruction }
                    }
                },
                generationConfig = new
                {
                    temperature = 1,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 8192,
                    responseMimeType = "text/plain"
                }
            };

            var jsonPayload = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var requestUri = $"{GeminiApiEndpoint}?key={ApiKey}";

            Debug.WriteLine($"Sending payload: {jsonPayload}"); // Keep this for debugging

            try
            {
                var response = await httpClient.PostAsync(requestUri, content);
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"Gemini API Error: {response.StatusCode} - {errorContent}");
                    return $"Error: {response.StatusCode}";
                }
                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"Gemini Response:\n{FormatGeminiResponse(responseContent)}");
                return FormatGeminiResponse(responseContent);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine($"Error communicating with Gemini API: {ex.Message}");
                return $"Error: {ex.Message}";
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"Error parsing Gemini API response: {ex.Message}");
                return $"Error parsing response";
            }
        }

        // Helper function to format the Gemini response for better readability
        private static string FormatGeminiResponse(string jsonResponse)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(jsonResponse);
                if (document.RootElement.TryGetProperty("candidates", out var candidates) && candidates.EnumerateArray().FirstOrDefault() is { } firstCandidate)
                {
                    if (firstCandidate.TryGetProperty("content", out var content) && content.TryGetProperty("parts", out var parts) && parts.EnumerateArray().FirstOrDefault() is { } firstPart)
                    {
                        if (firstPart.TryGetProperty("text", out var text))
                        {
                            return text.GetString();
                        }
                    }
                }
                return "Could not extract text from response.";
            }
            catch (JsonException)
            {
                return $"Error formatting response: {jsonResponse}";
            }
        }
    }
}
