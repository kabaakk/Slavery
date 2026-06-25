using System;
using UnityEngine;

namespace Slavery.LLM
{
    /// <summary>
    /// API yokken deterministik zorluk adımı. Kazanınca zorlaşır, kaybedince kolaylaşır.
    /// </summary>
    public sealed class MockDifficultyLlmProvider : IDifficultyLlmProvider
    {
        public void RequestAdjustment(
            RunEndContext context,
            MonoBehaviour coroutineHost,
            Action<DifficultyProposalDto> onSuccess,
            Action<string> onError)
        {
            var p = new DifficultyProposalDto
            {
                bossMaxHealth = context.BossMaxHealthBefore,
                bossAttackDamage = context.BossAttackDamageBefore,
                bossAttackCooldown = context.BossAttackCooldownBefore,
                bossMoveSpeed = context.BossMoveSpeedBefore,
                bossAttackRange = context.BossAttackRangeBefore,
                bossAttackRadius = context.BossAttackRadiusBefore,
                bossKickAttackRadius = context.BossKickAttackRadiusBefore,
                bossFallbackHitPadding = context.BossFallbackHitPaddingBefore,
                bossHitReactionCooldown = context.BossHitReactionCooldownBefore,
                playerMaxHealth = context.PlayerMaxHealthBefore
            };

            if (p.bossMaxHealth <= 0) p.bossMaxHealth = 120;
            if (p.bossAttackDamage <= 0) p.bossAttackDamage = 10;
            if (p.bossAttackCooldown <= 0f) p.bossAttackCooldown = 2f;
            if (p.bossMoveSpeed <= 0f) p.bossMoveSpeed = 3.8f;
            if (p.bossAttackRange <= 0f) p.bossAttackRange = 3.2f;
            if (p.bossAttackRadius <= 0f) p.bossAttackRadius = 2.1f;
            if (p.bossKickAttackRadius <= 0f) p.bossKickAttackRadius = 1.9f;
            if (p.bossFallbackHitPadding < 0f) p.bossFallbackHitPadding = 0.8f;
            if (p.bossHitReactionCooldown < 0f) p.bossHitReactionCooldown = 0.2f;
            if (p.playerMaxHealth <= 0) p.playerMaxHealth = 100;

            if (context.PlayerWon)
            {
                p.bossMaxHealth += 25;
                p.bossAttackDamage += 2;
                p.bossAttackCooldown = Mathf.Max(0.5f, p.bossAttackCooldown - 0.15f);
                p.bossMoveSpeed += 0.2f;
                p.bossAttackRange += 0.18f;
                p.bossAttackRadius += 0.12f;
                p.bossKickAttackRadius += 0.12f;
                p.bossFallbackHitPadding += 0.05f;
                p.bossHitReactionCooldown = Mathf.Min(2.5f, p.bossHitReactionCooldown + 0.05f);
                p.playerMaxHealth = context.PlayerMaxHealthBefore;
            }
            else
            {
                p.bossMaxHealth = Mathf.Max(60, p.bossMaxHealth - 30);
                p.bossAttackDamage = Mathf.Max(3, p.bossAttackDamage - 2);
                p.bossAttackCooldown = Mathf.Min(5f, p.bossAttackCooldown + 0.25f);
                p.bossMoveSpeed = Mathf.Max(2f, p.bossMoveSpeed - 0.2f);
                p.bossAttackRange = Mathf.Max(1.2f, p.bossAttackRange - 0.15f);
                p.bossAttackRadius = Mathf.Max(1f, p.bossAttackRadius - 0.12f);
                p.bossKickAttackRadius = Mathf.Max(1f, p.bossKickAttackRadius - 0.12f);
                p.bossFallbackHitPadding = Mathf.Max(0f, p.bossFallbackHitPadding - 0.05f);
                p.bossHitReactionCooldown = Mathf.Max(0f, p.bossHitReactionCooldown - 0.06f);
                p.playerMaxHealth = context.PlayerMaxHealthBefore;
            }

            onSuccess?.Invoke(DifficultyProposalClamp.Clamp(p));
        }
    }
}
