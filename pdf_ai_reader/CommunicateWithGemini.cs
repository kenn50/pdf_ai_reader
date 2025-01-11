namespace pdf_ai_reader
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class CommunicateWithGemini
    {
        // Replace with your actual Gemini API key
        private const string ApiKey = "YOUR_GEMINI_API_KEY";
        private const string GeminiApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent";

        public static async Task Main(string[] args)
        {
            // Simulate a Base64 string obtained from PDF conversion
            string base64ImageFromPdf = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAYAAACNbyblAAAAHElEQVQI12P4//8/w38GIAXDIBKE0DHxgljNBAAO9TXL0Y4OHwAAAABJRU5ErkJggg=="; // Replace with your actual Base64 string
            string mimeType = "image/png"; // Or "image/jpeg", depending on your PDF conversion
            string prompt = "Describe the content of this image.";

            await SendBase64StringToGemini(base64ImageFromPdf, mimeType, prompt);

            Console.ReadKey();
        }

        public static async Task SendBase64StringToGemini(string base64Image, string mimeType, string prompt)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var requestData = new
            {
                contents = new[]
                {
                new
                {
                    parts = new dynamic[]
                    {
                        new { text = prompt },
                        new
                        {
                            inline_data = new
                            {
                                mime_type = mimeType,
                                data = base64Image
                            }
                        }
                    }
                }
            }
            };

            var jsonPayload = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var requestUri = $"{GeminiApiEndpoint}?key={ApiKey}";

            try
            {
                var response = await httpClient.PostAsync(requestUri, content);
                response.EnsureSuccessStatusCode();
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Gemini Response:\n{FormatGeminiResponse(responseContent)}");
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Error communicating with Gemini API: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error parsing Gemini API response: {ex.Message}");
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
