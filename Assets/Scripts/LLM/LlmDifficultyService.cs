using System;
using UnityEngine;

namespace Slavery.LLM
{
    /// <summary>
    /// Sahneye bir kez koy: ScriptableObject'te Mock / Claude seçimi.
    /// forceMockDespiteClaudeBackend açıksa Claude + key görünse bile Mock çalışır.
    /// </summary>
    public class LlmDifficultyService : MonoBehaviour
    {
        [SerializeField] private LlmDifficultySettings settings;
        [SerializeField] private DifficultyApplier applier;

        private IDifficultyLlmProvider _provider;

        private void Awake()
        {
            _provider = CreateProvider(settings);
        }

        private static IDifficultyLlmProvider CreateProvider(LlmDifficultySettings s)
        {
            if (s == null)
            {
                Debug.LogWarning("[LlmDifficultyService] LlmDifficultySettings atanmadı — Mock kullanılıyor.");
                return new MockDifficultyLlmProvider();
            }

            if (s.forceMockDespiteClaudeBackend || s.backend == LlmBackend.Mock)
                return new MockDifficultyLlmProvider();

            if (s.backend == LlmBackend.Claude)
                return new ClaudeDifficultyLlmProvider(s);

            return new MockDifficultyLlmProvider();
        }

        /// <summary>
        /// Run bitti: kazanıldı mı kaybedildi mi söyle; LLM önerisini uygular (veya hata loglar).
        /// </summary>
        public void RequestAndApplyNextDifficulty(bool playerWonBattle, Action onComplete = null, Action<string> onError = null)
        {
            if (applier == null)
            {
                Debug.LogError("[LlmDifficultyService] DifficultyApplier yok.");
                onError?.Invoke("DifficultyApplier null");
                return;
            }

            if (_provider == null)
            {
                _provider = new MockDifficultyLlmProvider();
            }

            var ctx = applier.BuildContext(playerWonBattle);

            _provider.RequestAdjustment(
                ctx,
                this,
                proposal =>
                {
                    applier.Apply(proposal);
                    onComplete?.Invoke();
                },
                err =>
                {
                    Debug.LogWarning("[LlmDifficultyService] LLM hata: " + err);
                    onError?.Invoke(err);
                });
        }
    }
}
