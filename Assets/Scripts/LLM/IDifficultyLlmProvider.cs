using System;
using UnityEngine;

namespace Slavery.LLM
{
    /// <summary>
    /// Mock veya Claude — aynı imza. coroutineHost: Claude isteği için StartCoroutine.
    /// </summary>
    public interface IDifficultyLlmProvider
    {
        void RequestAdjustment(
            RunEndContext context,
            MonoBehaviour coroutineHost,
            Action<DifficultyProposalDto> onSuccess,
            Action<string> onError);
    }
}
