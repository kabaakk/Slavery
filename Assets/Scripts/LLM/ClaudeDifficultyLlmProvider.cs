using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Slavery.LLM
{
    /// <summary>
    /// Anthropic Messages API. Ayarlarda backend = Claude + API key doldurulunca kullanılır.
    /// </summary>
    public sealed class ClaudeDifficultyLlmProvider : IDifficultyLlmProvider
    {
        private const string ApiUrl = "https://api.anthropic.com/v1/messages";

        private readonly LlmDifficultySettings _settings;

        public ClaudeDifficultyLlmProvider(LlmDifficultySettings settings)
        {
            _settings = settings;
        }

        public void RequestAdjustment(
            RunEndContext context,
            MonoBehaviour coroutineHost,
            Action<DifficultyProposalDto> onSuccess,
            Action<string> onError)
        {
            if (coroutineHost == null)
            {
                onError?.Invoke("ClaudeDifficultyLlmProvider: coroutineHost null.");
                return;
            }

            if (_settings == null || string.IsNullOrWhiteSpace(_settings.anthropicApiKey))
            {
                onError?.Invoke("Claude: anthropicApiKey boş (LlmDifficultySettings).");
                return;
            }

            coroutineHost.StartCoroutine(SendCoroutine(context, onSuccess, onError));
        }

        private IEnumerator SendCoroutine(
            RunEndContext context,
            Action<DifficultyProposalDto> onSuccess,
            Action<string> onError)
        {
            string userPrompt = BuildUserPrompt(context);
            string jsonBody = BuildRequestJson(userPrompt);

            using (var req = new UnityWebRequest(ApiUrl, UnityWebRequest.kHttpVerbPOST))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                req.uploadHandler = new UploadHandlerRaw(bodyRaw);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("x-api-key", _settings.anthropicApiKey.Trim());
                req.SetRequestHeader("anthropic-version", "2023-06-01");
                req.timeout = Mathf.RoundToInt(_settings.requestTimeoutSeconds);

                yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onError?.Invoke($"Claude HTTP hata: {req.error}\n{req.downloadHandler.text}");
                    yield break;
                }

                string responseText = req.downloadHandler.text;
                if (!TryParseProposal(responseText, out var proposal, out string parseErr))
                {
                    onError?.Invoke(parseErr + "\nRaw: " + responseText);
                    yield break;
                }

                onSuccess?.Invoke(DifficultyProposalClamp.Clamp(proposal));
            }
        }

        private string BuildUserPrompt(RunEndContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You adjust combat difficulty for the NEXT run of a single-boss arena game.");
            sb.AppendLine("Output rules: respond with ONE single-line JSON object only. No markdown, no explanation.");
            sb.AppendLine("Keys and types exactly: ");
            sb.AppendLine("{\"bossMaxHealth\":int,\"bossAttackDamage\":int,\"bossAttackCooldown\":float,\"bossMoveSpeed\":float,\"bossAttackRange\":float,\"bossAttackRadius\":float,\"bossKickAttackRadius\":float,\"bossFallbackHitPadding\":float,\"bossHitReactionCooldown\":float,\"playerMaxHealth\":int}");
            sb.AppendLine();
            sb.AppendLine("Safe ranges (stay inside): ");
            sb.AppendLine($"bossMaxHealth {DifficultyProposalClamp.BossHpMin}-{DifficultyProposalClamp.BossHpMax}, ");
            sb.AppendLine($"bossAttackDamage {DifficultyProposalClamp.BossDmgMin}-{DifficultyProposalClamp.BossDmgMax}, ");
            sb.AppendLine($"bossAttackCooldown {DifficultyProposalClamp.BossCdMin}-{DifficultyProposalClamp.BossCdMax}, ");
            sb.AppendLine($"bossMoveSpeed {DifficultyProposalClamp.BossMoveSpeedMin}-{DifficultyProposalClamp.BossMoveSpeedMax}, ");
            sb.AppendLine($"bossAttackRange {DifficultyProposalClamp.BossRangeMin}-{DifficultyProposalClamp.BossRangeMax}, ");
            sb.AppendLine($"bossAttackRadius {DifficultyProposalClamp.BossRadiusMin}-{DifficultyProposalClamp.BossRadiusMax}, ");
            sb.AppendLine($"bossKickAttackRadius {DifficultyProposalClamp.BossRadiusMin}-{DifficultyProposalClamp.BossRadiusMax}, ");
            sb.AppendLine($"bossFallbackHitPadding {DifficultyProposalClamp.BossFallbackPaddingMin}-{DifficultyProposalClamp.BossFallbackPaddingMax}, ");
            sb.AppendLine($"bossHitReactionCooldown {DifficultyProposalClamp.BossHitReactCdMin}-{DifficultyProposalClamp.BossHitReactCdMax}, ");
            sb.AppendLine($"playerMaxHealth {DifficultyProposalClamp.PlayerHpMin}-{DifficultyProposalClamp.PlayerHpMax}.");
            sb.AppendLine();
            sb.AppendLine("Design intent: if player WON last fight, make next run harder. If player LOST, make next run easier.");
            if (!string.IsNullOrWhiteSpace(_settings.systemPromptExtra))
            {
                sb.AppendLine();
                sb.AppendLine("Extra design notes from developer:");
                sb.AppendLine(_settings.systemPromptExtra);
            }

            sb.AppendLine();
            sb.AppendLine("Last run summary:");
            sb.AppendLine($"playerWon: {ctx.PlayerWon}");
            sb.AppendLine($"fightDurationSeconds: {ctx.FightDurationSeconds:F1}");
            sb.AppendLine($"currentBossMaxHealth: {ctx.BossMaxHealthBefore}");
            sb.AppendLine($"currentBossAttackDamage: {ctx.BossAttackDamageBefore}");
            sb.AppendLine($"currentBossAttackCooldown: {ctx.BossAttackCooldownBefore:F2}");
            sb.AppendLine($"currentBossMoveSpeed: {ctx.BossMoveSpeedBefore:F2}");
            sb.AppendLine($"currentBossAttackRange: {ctx.BossAttackRangeBefore:F2}");
            sb.AppendLine($"currentBossAttackRadius: {ctx.BossAttackRadiusBefore:F2}");
            sb.AppendLine($"currentBossKickAttackRadius: {ctx.BossKickAttackRadiusBefore:F2}");
            sb.AppendLine($"currentBossFallbackHitPadding: {ctx.BossFallbackHitPaddingBefore:F2}");
            sb.AppendLine($"currentBossHitReactionCooldown: {ctx.BossHitReactionCooldownBefore:F2}");
            sb.AppendLine($"currentPlayerMaxHealth: {ctx.PlayerMaxHealthBefore}");
            sb.AppendLine($"hitsTakenByPlayer: {ctx.HitsTakenByPlayer}");
            sb.AppendLine($"hitsDealtToBoss: {ctx.HitsDealtToBoss}");
            sb.AppendLine($"dashCount: {ctx.DashCount}");
            sb.AppendLine($"playerHpRemainingEnd: {ctx.PlayerHpRemainingEnd}/{ctx.PlayerHpMaxEnd}");
            sb.AppendLine($"bossHpRemainingEnd: {ctx.BossHpRemainingEnd}/{ctx.BossHpMaxEnd}");
            return sb.ToString();
        }

        private string BuildRequestJson(string userContent)
        {
            return "{\"model\":\"" + JsonEscape(_settings.model) + "\",\"max_tokens\":" + _settings.maxTokens +
                   ",\"messages\":[{\"role\":\"user\",\"content\":\"" + JsonEscape(userContent) + "\"}]}";
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        [Serializable]
        private class AnthropicResponse
        {
            public AnthropicContent[] content;
        }

        [Serializable]
        private class AnthropicContent
        {
            public string type;
            public string text;
        }

        private static bool TryParseProposal(string responseJson, out DifficultyProposalDto proposal, out string error)
        {
            proposal = null;
            error = null;

            try
            {
                var wrap = JsonUtility.FromJson<AnthropicResponse>(responseJson);
                if (wrap?.content == null || wrap.content.Length == 0 || string.IsNullOrWhiteSpace(wrap.content[0].text))
                {
                    error = "Claude yanıtında content[0].text yok.";
                    return false;
                }

                string text = wrap.content[0].text.Trim();
                if (text.StartsWith("```"))
                {
                    int nl = text.IndexOf('\n');
                    if (nl > 0) text = text.Substring(nl + 1).Trim();
                    int end = text.LastIndexOf("```", StringComparison.Ordinal);
                    if (end > 0) text = text.Substring(0, end).Trim();
                }

                proposal = JsonUtility.FromJson<DifficultyProposalDto>(text);
                if (proposal == null)
                {
                    error = "JSON parse DifficultyProposalDto null.";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                error = "JSON parse exception: " + e.Message;
                return false;
            }
        }
    }
}
