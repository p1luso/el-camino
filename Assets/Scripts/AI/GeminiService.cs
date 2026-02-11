using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

namespace ElCamino.AI
{
    public class GeminiService : MonoBehaviour
    {
        public static GeminiService Instance { get; private set; }

        [Header("API Settings")]
        [Tooltip("Enter your Gemini API Key here")]
        public string apiKey = "AIzaSyCsBfHDLYjxiufL35GVvIYgb_hWtsd0NZk"; 
        public string model = "gemini-2.0-flash";

        [Header("Debug")]
        public bool useMockResponses = true;

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void GenerateResponse(string prompt, System.Action<string> callback)
        {
            if (string.IsNullOrEmpty(apiKey) || apiKey.Contains("YOUR_API_KEY"))
            {
                Debug.LogWarning("[GeminiService] API Key missing. Returning Mock response.");
                // Mock response if no key
                StartCoroutine(MockResponseRoutine(prompt, callback));
                return;
            }

            // Real Request
            StartCoroutine(CallGeminiAPI(prompt, callback));
        }

        private IEnumerator MockResponseRoutine(string prompt, System.Action<string> callback)
        {
            yield return new WaitForSeconds(0.5f); 
            
            // Simple logic for mock to verify flow
            string mockReply = "Sistemas en línea. (Modo Mock)";
            
            if (prompt.Contains("BIPO"))
                mockReply = "¡Hola! Soy BIPO. Mis sistemas neuronales están desconectados (Falta API Key), pero te escucho.";
            else if (prompt.Contains("combustible"))
                mockReply = "Nivel de combustible crítico. Buscando estación.";
            
            callback?.Invoke(mockReply);
        }

        private IEnumerator CallGeminiAPI(string prompt, System.Action<string> callback)
        {
            // Simple implementation of Gemini API call
            // Note: In production, do not store API Keys in client code. Use a proxy server.
            
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            
            // Construct JSON
            string jsonBody = "{\"contents\": [{\"parts\": [{\"text\": \"" + EscapeJson(prompt) + "\"}]}]}";
            
            using (UnityWebRequest www = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
                www.uploadHandler = new UploadHandlerRaw(bodyRaw);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Content-Type", "application/json");

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError($"Gemini API Error: {www.error}\nResponse: {www.downloadHandler.text}");
                    callback?.Invoke("Error de conexión con IA.");
                }
                else
                {
                    // Parse Response (Quick & Dirty for prototype)
                    string result = www.downloadHandler.text;
                    string text = ExtractTextFromJson(result);
                    callback?.Invoke(text);
                }
            }
        }

        private string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\"", "\\\"").Replace("\n", "\\n");
        }

        private string ExtractTextFromJson(string json)
        {
            // Very basic extraction to avoid full JSON library dependency if not needed
            // Look for "text": "..."
            int index = json.IndexOf("\"text\": \"");
            if (index != -1)
            {
                int start = index + 9;
                int end = json.IndexOf("\"", start);
                string text = json.Substring(start, end - start);
                return text.Replace("\\n", "\n").Replace("\\\"", "\"");
            }
            return json; // Return raw if parsing fails
        }
    }
}
