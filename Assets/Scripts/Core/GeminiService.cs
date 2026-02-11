using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Networking;

namespace ElCamino.Core
{
    public class GeminiService : MonoBehaviour
    {
        public static GeminiService Instance { get; private set; }

        [Header("Gemini API Settings")]
        [Tooltip("Paste your Gemini API Key here")]
        public string apiKey = "YOUR_API_KEY_HERE";
        public string model = "gemini-1.5-flash"; // Cost-effective model

        private const string API_URL = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void GenerateResponse(string prompt, Action<string> callback)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR_API_KEY"))
            {
                // Mock response if no key
                StartCoroutine(MockResponseRoutine(prompt, callback));
                return;
            }

            StartCoroutine(PostRequest(prompt, callback));
        }

        private IEnumerator MockResponseRoutine(string prompt, Action<string> callback)
        {
            yield return new WaitForSeconds(1.0f); // Simulate network delay
            
            string mockReply = "Soy tu copiloto. No tengo API Key configurada, pero detecto algo interesante.";
            
            if (prompt.Contains("combustible"))
                mockReply = "¡Alerta de combustible! Deberíamos buscar una estación pronto.";
            else if (prompt.Contains("lugar llamado"))
                mockReply = "Ese lugar parece antiguo e interesante. Lástima que mis bases de datos estén offline.";
            
            callback?.Invoke(mockReply);
        }

        private IEnumerator PostRequest(string prompt, Action<string> callback)
        {
            string url = string.Format(API_URL, model, apiKey);

            // Simple JSON structure for Gemini
            string jsonBody = $"{{\"contents\":[{{\"parts\":[{{\"text\":\"{EscapeJson(prompt)}\"}}]}}]}}";
            
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Gemini Error: {request.error}\nResponse: {request.downloadHandler.text}");
                    callback?.Invoke("Error de conexión con el núcleo de IA.");
                }
                else
                {
                    string jsonResponse = request.downloadHandler.text;
                    string text = ParseGeminiResponse(jsonResponse);
                    callback?.Invoke(text);
                }
            }
        }

        // Very basic JSON escaping
        private string EscapeJson(string str)
        {
            return str.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        }

        // Quick and dirty JSON parsing to avoid heavy dependencies
        private string ParseGeminiResponse(string json)
        {
            try
            {
                // Look for "text": "..." inside "candidates"
                string marker = "\"text\": \"";
                int start = json.IndexOf(marker);
                if (start == -1) return "Data corrupted.";
                
                start += marker.Length;
                int end = json.IndexOf("\"", start);
                
                string content = json.Substring(start, end - start);
                return content.Replace("\\n", "\n").Replace("\\\"", "\"");
            }
            catch
            {
                return "Error parsing data.";
            }
        }
    }
}