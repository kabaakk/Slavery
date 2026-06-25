using System;
using UnityEngine;

namespace Slavery.LLM
{
    /// <summary>
    /// Claude / mock'un döndürdüğü bir sonraki run parametreleri (JSON alan adlarıyla aynı).
    /// </summary>
    [Serializable]
    public class DifficultyProposalDto
    {
        public int bossMaxHealth;
        public int bossAttackDamage;
        public float bossAttackCooldown;
        public float bossMoveSpeed;
        public float bossAttackRange;
        public float bossAttackRadius;
        public float bossKickAttackRadius;
        public float bossFallbackHitPadding;
        public float bossHitReactionCooldown;
        public int playerMaxHealth;
    }

    public static class DifficultyProposalClamp
    {
        public const int BossHpMin = 50;
        public const int BossHpMax = 800;
        public const int BossDmgMin = 3;
        public const int BossDmgMax = 80;
        public const float BossCdMin = 0.5f;
        public const float BossCdMax = 6f;
        public const float BossMoveSpeedMin = 2f;
        public const float BossMoveSpeedMax = 7f;
        public const float BossRangeMin = 1.2f;
        public const float BossRangeMax = 8f;
        public const float BossRadiusMin = 1f;
        public const float BossRadiusMax = 8f;
        public const float BossFallbackPaddingMin = 0f;
        public const float BossFallbackPaddingMax = 3f;
        public const float BossHitReactCdMin = 0f;
        public const float BossHitReactCdMax = 2.5f;
        public const int PlayerHpMin = 30;
        public const int PlayerHpMax = 300;

        public static DifficultyProposalDto Clamp(DifficultyProposalDto p)
        {
            if (p == null) return Default();
            p.bossMaxHealth = Mathf.Clamp(p.bossMaxHealth, BossHpMin, BossHpMax);
            p.bossAttackDamage = Mathf.Clamp(p.bossAttackDamage, BossDmgMin, BossDmgMax);
            p.bossAttackCooldown = Mathf.Clamp(p.bossAttackCooldown, BossCdMin, BossCdMax);
            if (p.bossMoveSpeed <= 0f) p.bossMoveSpeed = 3.8f;
            if (p.bossAttackRange <= 0f) p.bossAttackRange = 3.2f;
            if (p.bossAttackRadius <= 0f) p.bossAttackRadius = 2.1f;
            if (p.bossKickAttackRadius <= 0f) p.bossKickAttackRadius = 1.9f;
            if (p.bossFallbackHitPadding < 0f) p.bossFallbackHitPadding = 0f;
            if (p.bossHitReactionCooldown < 0f) p.bossHitReactionCooldown = 0.2f;
            p.bossMoveSpeed = Mathf.Clamp(p.bossMoveSpeed, BossMoveSpeedMin, BossMoveSpeedMax);
            p.bossAttackRange = Mathf.Clamp(p.bossAttackRange, BossRangeMin, BossRangeMax);
            p.bossAttackRadius = Mathf.Clamp(p.bossAttackRadius, BossRadiusMin, BossRadiusMax);
            p.bossKickAttackRadius = Mathf.Clamp(p.bossKickAttackRadius, BossRadiusMin, BossRadiusMax);
            p.bossFallbackHitPadding = Mathf.Clamp(p.bossFallbackHitPadding, BossFallbackPaddingMin, BossFallbackPaddingMax);
            p.bossHitReactionCooldown = Mathf.Clamp(p.bossHitReactionCooldown, BossHitReactCdMin, BossHitReactCdMax);
            p.playerMaxHealth = Mathf.Clamp(p.playerMaxHealth, PlayerHpMin, PlayerHpMax);
            return p;
        }

        public static DifficultyProposalDto Default()
        {
            return new DifficultyProposalDto
            {
                bossMaxHealth = 120,
                bossAttackDamage = 10,
                bossAttackCooldown = 2f,
                bossMoveSpeed = 3.8f,
                bossAttackRange = 3.2f,
                bossAttackRadius = 2.1f,
                bossKickAttackRadius = 1.9f,
                bossFallbackHitPadding = 0.8f,
                bossHitReactionCooldown = 0.2f,
                playerMaxHealth = 100
            };
        }
    }
}
