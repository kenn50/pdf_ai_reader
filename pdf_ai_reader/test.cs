using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class GeminiApiClient
{
    private static readonly string ApiKey = "YOUR_API_KEY"; // Replace with your actual API key
    private static readonly string UploadEndpoint = $"https://generativelanguage.googleapis.com/upload/v1beta/files?key={ApiKey}";
    private static readonly string GenerateContentEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-exp:generateContent?key={ApiKey}";
    private static readonly HttpClient HttpClient = new HttpClient();

    public static async Task Main(string[] args)
    {
        // TODO: Make the following files available on the local file system.
        string[] files = { "Image January 18, 2025 - 8:43PM.png" };
        string[] mimeTypes = { "image/png" };
        List<string> fileUris = new List<string>();

        for (int i = 0; i < files.Length; i++)
        {
            string filePath = files[i];
            string mimeType = mimeTypes[i];

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found: {filePath}");
                return;
            }

            long numBytes = new FileInfo(filePath).Length;

            using (var fileStream = File.OpenRead(filePath))
            {
                var request = new HttpRequestMessage(HttpMethod.Post, UploadEndpoint);
                request.Headers.Add("X-Goog-Upload-Command", "start, upload, finalize");
                request.Headers.Add("X-Goog-Upload-Header-Content-Length", numBytes.ToString());
                request.Headers.Add("X-Goog-Upload-Header-Content-Type", mimeType);
                request.Content = new StringContent(JsonSerializer.Serialize(new { file = new { display_name = Path.GetFileName(filePath) } }), Encoding.UTF8, "application/json");

                // Send the metadata request
                HttpResponseMessage response = await HttpClient.SendAsync(request);
                response.EnsureSuccessStatusCode(); // Throw if not successful

                // Extract the upload URL
                string? uploadUrl = response.Headers.Location?.AbsoluteUri;
                if (string.IsNullOrEmpty(uploadUrl))
                {
                    Console.WriteLine("Error: Upload URL not found in response.");
                    return;
                }

                // Upload the file content
                var uploadRequest = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                uploadRequest.Content = new StreamContent(fileStream);
                uploadRequest.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(mimeType);

                HttpResponseMessage uploadResponse = await HttpClient.SendAsync(uploadRequest);
                uploadResponse.EnsureSuccessStatusCode();

                // TODO: Read the file.uri from the response, store it as FILE_URI_${i}
                string responseContent = await uploadResponse.Content.ReadAsStringAsync();
                try
                {
                    using (JsonDocument document = JsonDocument.Parse(responseContent))
                    {
                        JsonElement root = document.RootElement;
                        if (root.TryGetProperty("fileUri", out JsonElement fileUriElement))
                        {
                            string fileUri = fileUriElement.GetString();
                            fileUris.Add(fileUri);
                            Console.WriteLine($"Uploaded {filePath}, fileUri: {fileUri}");
                        }
                        else
                        {
                            Console.WriteLine($"Error: 'fileUri' not found in upload response for {filePath}");
                        }
                    }
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error parsing upload response for {filePath}: {ex.Message}");
                    Console.WriteLine($"Response content: {responseContent}");
                }
            }
        }

        if (fileUris.Count > 0)
        {
            var generateContentPayload = new
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
                                fileData = new
                                {
                                    fileUri = fileUris[0],
                                    mimeType = "image/png"
                                }
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new { text = "abc" }
                        }
                    }
                },
                systemInstruction = new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = "Abc" }
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

            string jsonPayload = JsonSerializer.Serialize(generateContentPayload);

            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            HttpResponseMessage generateResponse = await HttpClient.PostAsync(GenerateContentEndpoint, content);
            generateResponse.EnsureSuccessStatusCode();

            string generateResponseContent = await generateResponse.Content.ReadAsStringAsync();
            Console.WriteLine("Generate Content Response:");
            Console.WriteLine(generateResponseContent);
        }
    }
}