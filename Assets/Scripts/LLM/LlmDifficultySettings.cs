using UnityEngine;

namespace Slavery.LLM
{
    public enum LlmBackend
    {
        Mock = 0,
        Claude = 1
    }

    [CreateAssetMenu(fileName = "LlmDifficultySettings", menuName = "Slavery/LLM Difficulty Settings", order = 0)]
    public class LlmDifficultySettings : ScriptableObject
    {
        [Header("Hangi backend?")]
        public LlmBackend backend = LlmBackend.Mock;

        [Tooltip("Açıkken Inspector'da Claude + API key görünse bile gerçek istek atılmaz; Mock kullanılır.")]
        public bool forceMockDespiteClaudeBackend = true;

        [Header("Claude (Anthropic Messages API)")]
        [Tooltip("Editor'da test için. Dağıtımda güvenli saklama: PlayerPrefs / sunucu proxy önerilir.")]
        public string anthropicApiKey = "";

        public string model = "claude-3-5-haiku-20241022";
        [Min(64)] public int maxTokens = 256;
        public float requestTimeoutSeconds = 45f;

        [TextArea(4, 12)]
        public string systemPromptExtra = "";
    }
}
