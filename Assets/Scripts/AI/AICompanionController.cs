using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using ElCamino.Core;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace ElCamino.AI
{
    public class AICompanionController : MonoBehaviour
    {
        [Header("Target Settings")]
        public Transform target; // The player to follow
        public string playerTag = "Player";
        public Vector3 offset = new Vector3(2f, 1.5f, -2f); // Floating behind shoulder
        
        [Header("Movement Settings")]
        public float smoothSpeed = 5f;
        public float rotationSpeed = 10f;
        
        [Header("Bobbing Animation")]
        public float bobFrequency = 2f;
        public float bobAmplitude = 0.2f;

        [Header("Visuals")]
        public Color droneColor = Color.cyan;
        private Transform _mouthTransform;
        private Vector3 _originalMouthScale;

        [Header("AI Brain")]
        public float poiCheckInterval = 5f;
        public float poiDetectionRadius = 30f;
        
        private Vector3 _currentVelocity;
        private float _initialYOffset;
        private float _lastPOICheckTime;
        private POIManager.POIData _lastVisitedPOI;

        // Audio & Speech
        private AudioSource _audioSource;
        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private DictationRecognizer _dictationRecognizer;
        private KeywordRecognizer _keywordRecognizer; // Fallback
        #endif
        private bool _isSpeaking = false;

        void Start()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null) target = player.transform;
            }

            _initialYOffset = offset.y;
            
            // Create Visual if none exists
            if (GetComponent<MeshRenderer>() == null && transform.childCount == 0)
            {
                CreateDroneVisual();
            }

            // Ensure Services Exist
            if (GeminiService.Instance == null) gameObject.AddComponent<GeminiService>();
            if (POIManager.Instance == null) new GameObject("POIManager").AddComponent<POIManager>();
            if (FuelSystem.Instance == null) gameObject.AddComponent<FuelSystem>(); // Or attach to player

            // Setup Audio
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f; // 3D Sound
            _audioSource.minDistance = 2f;
            _audioSource.maxDistance = 20f;

            SetupVoiceRecognition();
        }

        void SetupVoiceRecognition()
        {
            #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Attempt to start Dictation (requires online privacy settings enabled)
            _dictationRecognizer = new DictationRecognizer();
            
            _dictationRecognizer.DictationResult += (text, confidence) =>
            {
                Debug.Log($"[AI] Heard: {text}");
                ProcessVoiceCommand(text);
            };

            _dictationRecognizer.DictationHypothesis += (text) =>
            {
                // Optional: Show what is being heard in UI in real-time
            };

            _dictationRecognizer.DictationComplete += (completionCause) =>
            {
                if (completionCause != DictationCompletionCause.Complete)
                {
                    Debug.LogWarning($"[AI] Dictation stopped: {completionCause}.");
                    // Do NOT auto-restart blindly if it's a permission issue, but here we can try once or check cause
                    if (completionCause == DictationCompletionCause.TimeoutExceeded)
                    {
                         _dictationRecognizer.Start();
                    }
                }
            };

            _dictationRecognizer.DictationError += (error, hresult) =>
            {
                // Downgrade to Warning to avoid alarming the user, since we have a fallback.
                Debug.LogWarning($"[AI] Dictation unavailable (Error: {error}). Switching to Basic Mode.");
                
                // Dispose properly to ensure it's dead
                if (_dictationRecognizer != null)
                {
                    _dictationRecognizer.Dispose();
                    _dictationRecognizer = null;
                }
                
                StartFallbackKeywordRecognizer();
            };

            try
            {
                _dictationRecognizer.Start();
                Debug.Log("[AI] Listening for 'BIPO' + commands (Dictation Mode)...");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AI] Failed to start Dictation: {ex.Message}. Switching to Basic Mode.");
                if (_dictationRecognizer != null)
                {
                    _dictationRecognizer.Dispose();
                    _dictationRecognizer = null;
                }
                StartFallbackKeywordRecognizer();
            }
            #else
            Debug.LogWarning("[AI] Voice Recognition is only supported on Windows Standalone.");
            #endif
        }

        #if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private void StartFallbackKeywordRecognizer()
        {
            if (_keywordRecognizer != null && _keywordRecognizer.IsRunning) return;

            Debug.LogWarning("[AI] Switching to Fallback Keyword Mode (Offline/Restricted).");
            
            // Define simple keywords since we can't do open dictation
            string[] keywords = new string[] { "Bipo", "Hola Bipo", "Ayuda", "Gasolina", "Donde estoy", "Que es esto" };
            
            _keywordRecognizer = new KeywordRecognizer(keywords);
            _keywordRecognizer.OnPhraseRecognized += (args) =>
            {
                Debug.Log($"[AI] Keyword Heard: {args.text}");
                ProcessVoiceCommand(args.text);
            };
            
            _keywordRecognizer.Start();
            Debug.Log("[AI] Fallback Keyword Recognizer Started.");
        }

        private void ProcessVoiceCommand(string text)
        {
            string lowerText = text.ToLower();
            
            // Check trigger word
            if (lowerText.Contains("bipo") || lowerText.Contains("vivo") || lowerText.Contains("people")) // Common mishearings
            {
                // Extract everything AFTER "Bipo"
                string query = text;
                int index = lowerText.IndexOf("bipo");
                if (index == -1) index = lowerText.IndexOf("vivo"); // Handle mishearing
                
                if (index != -1)
                {
                    // If dictation, we have full text. If keyword, we have just the keyword.
                    if (text.Length > index + 4)
                        query = text.Substring(index + 4).Trim();
                    else
                        query = ""; // Just said Bipo
                }
                
                // If using KeywordRecognizer, the text IS the command, so query might be empty if they just said "Bipo"
                // But if they said "Hola Bipo", query is "Hola" (actually logic above strips Bipo)
                // Let's just pass the full text if query is empty/short to be safe
                
                if (string.IsNullOrEmpty(query) || query.Length < 2)
                {
                    // Just said "Bipo"
                    GeminiService.Instance.GenerateResponse("El usuario dijo solo 'BIPO'. Saluda y pregunta qué necesita.", Speak);
                }
                else
                {
                    // Said "Bipo [Query]"
                    Debug.Log($"[AI] Processing Query: {query}");
                    
                    // Inject Context about Game State
                    string context = "";
                    if (POIManager.Instance != null)
                    {
                        var gasStation = POIManager.Instance.GetNearestGasStation(transform.position);
                        if (gasStation != null)
                        {
                            float dist = Vector3.Distance(transform.position, gasStation.position);
                            context += $"(Contexto: Hay una estación de servicio a {dist:F0} metros). ";
                        }
                        
                        if (FuelSystem.Instance != null)
                        {
                            context += $"(Contexto: Combustible al {FuelSystem.Instance.currentFuel:F0}%). ";
                        }
                    }

                    string prompt = $"Eres BIPO, un copiloto IA futurista. El usuario te preguntó: '{query}'. {context} Responde de forma útil, breve y con personalidad.";
                    GeminiService.Instance.GenerateResponse(prompt, Speak);
                }
            }
        }
        #endif

        void CreateDroneVisual()
        {
            // Body
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(transform);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = Vector3.one * 0.5f;
            
            Destroy(body.GetComponent<Collider>());
            
            Renderer r = body.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Sprites/Default")); 
            mat.color = droneColor;
            r.material = mat;

            // Light
            GameObject lightObj = new GameObject("DroneLight");
            lightObj.transform.SetParent(body.transform);
            lightObj.transform.localPosition = Vector3.zero;
            Light l = lightObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = droneColor;
            l.range = 5f;
            l.intensity = 2f;

            // Mouth (Black strip on front)
            GameObject mouth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mouth.name = "Mouth";
            mouth.transform.SetParent(body.transform);
            // Face forward (Z+)
            mouth.transform.localPosition = new Vector3(0, 0, 0.45f); 
            mouth.transform.localScale = new Vector3(0.6f, 0.1f, 0.05f);
            
            Destroy(mouth.GetComponent<Collider>());
            
            Renderer mr = mouth.GetComponent<Renderer>();
            mr.material = new Material(Shader.Find("Sprites/Default"));
            mr.material.color = Color.black;

            _mouthTransform = mouth.transform;
            _originalMouthScale = mouth.transform.localScale;
        }

        void LateUpdate()
        {
            HandleMovement();
            HandleBrain();
            HandleMouthAnimation();
        }

        void HandleMovement()
        {
            if (target == null) return;

            Vector3 desiredPos = target.position + target.TransformDirection(offset);
            
            float bob = Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
            desiredPos.y += bob;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _currentVelocity, 1f / smoothSpeed);

            Vector3 lookTarget = target.position + target.forward * 10f;
            Quaternion targetRotation = Quaternion.LookRotation(lookTarget - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        void HandleBrain()
        {
            if (Time.time - _lastPOICheckTime > poiCheckInterval)
            {
                _lastPOICheckTime = Time.time;
                CheckSurroundings();
            }
        }

        void HandleMouthAnimation()
        {
            if (_mouthTransform == null) return;

            if (_isSpeaking && _audioSource.isPlaying)
            {
                // Simple spectrum analysis
                float[] spectrum = new float[64];
                _audioSource.GetSpectrumData(spectrum, 0, FFTWindow.Rectangular);
                float volume = 0f;
                foreach (var s in spectrum) volume += s;
                
                // Scale mouth Y based on volume
                float scaleY = Mathf.Clamp(volume * 50f, 0.1f, 0.8f);
                Vector3 targetScale = _originalMouthScale;
                targetScale.y = scaleY;
                _mouthTransform.localScale = Vector3.Lerp(_mouthTransform.localScale, targetScale, Time.deltaTime * 20f);
            }
            else
            {
                // Return to idle line
                _mouthTransform.localScale = Vector3.Lerp(_mouthTransform.localScale, _originalMouthScale, Time.deltaTime * 10f);
            }
        }

        void CheckSurroundings()
        {
            if (POIManager.Instance == null) return;

            POIManager.POIData poi = POIManager.Instance.GetNearestPOI(transform.position, poiDetectionRadius);
            
            if (poi != null && poi != _lastVisitedPOI)
            {
                _lastVisitedPOI = poi;
                Debug.Log($"[AI] Passing by: {poi.name}");
                
                string prompt = $"El jugador está pasando frente a un lugar llamado '{poi.name}'. Es un {poi.type}. Dime algo corto e interesante al respecto como si fueras un copiloto futurista.";
                GeminiService.Instance.GenerateResponse(prompt, Speak);
            }
        }

        public void OnLowFuelWarning()
        {
            Debug.Log("[AI] Low Fuel Warning received!");
            
            string prompt = "El vehículo tiene poco combustible (GAS). Adviértele al conductor y sugiere buscar una estación de servicio cercana. Sé urgente pero calmado.";
            
            var station = POIManager.Instance.GetNearestGasStation(transform.position);
            if (station != null)
            {
                float dist = Vector3.Distance(transform.position, station.position);
                prompt += $" Hay una estación a {dist:F0} metros.";
            }
            else
            {
                prompt += " No detecto ninguna estación cercana en el radar inmediato.";
            }

            GeminiService.Instance.GenerateResponse(prompt, Speak);
        }

        public void Speak(string text)
        {
            Debug.Log($"<color=cyan>[AI Companion]: {text}</color>");
            _lastSpokenText = text;
            _lastSpokenTime = Time.time;

            // Trigger TTS
            StopAllCoroutines();
            StartCoroutine(DownloadAndPlayTTS(text));
        }

        private IEnumerator DownloadAndPlayTTS(string text)
        {
            // Google Translate TTS Hack (Use with caution for production)
            string url = "https://translate.google.com/translate_tts?ie=UTF-8&total=1&idx=0&textlen=32&client=tw-ob&q=" + UnityWebRequest.EscapeURL(text) + "&tl=es";
            
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                    _audioSource.clip = clip;
                    _audioSource.Play();
                    _isSpeaking = true;
                    // Reset speaking flag after clip
                    Invoke("StopSpeaking", clip.length);
                }
                else
                {
                    Debug.LogWarning($"TTS Failed: {www.error}");
                    // Fallback: Just display text
                    _isSpeaking = false;
                }
            }
        }

        private void StopSpeaking()
        {
            _isSpeaking = false;
        }

        private string _lastSpokenText = "";
        private float _lastSpokenTime = 0f;
        private float _speechDuration = 5f;

        void OnGUI()
        {
            if (Time.time - _lastSpokenTime < _speechDuration && !string.IsNullOrEmpty(_lastSpokenText))
            {
                Vector3 screenPos = UnityEngine.Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
                if (screenPos.z > 0)
                {
                    float width = 300;
                    float height = 100;
                    Rect rect = new Rect(screenPos.x - width/2, Screen.height - screenPos.y - height, width, height);
                    
                    GUIStyle style = new GUIStyle(GUI.skin.box);
                    style.wordWrap = true;
                    style.alignment = TextAnchor.MiddleCenter;
                    style.fontSize = 14;
                    style.normal.textColor = Color.cyan;
                    style.normal.background = Texture2D.whiteTexture; 
                    
                    GUI.backgroundColor = new Color(0, 0, 0, 0.8f);
                    GUI.Box(rect, _lastSpokenText, style);
                }
            }
        }
    }
}
