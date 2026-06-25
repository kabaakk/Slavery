using UnityEngine;

namespace Slavery.LLM
{
    /// <summary>
    /// LLM/mock çıktısını BossHealth, PlayerHealth, BossAI üzerine yazar.
    /// </summary>
    public class DifficultyApplier : MonoBehaviour
    {
        [SerializeField] private BossHealth bossHealth;
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private BossAI bossAI;
        [SerializeField] private RunStatisticsTracker runStatistics;

        private const float FightTimeUnset = -1f;
        private float _fightStartTime = FightTimeUnset;

        public void BeginFightTimer()
        {
            _fightStartTime = Time.time;
        }

        public RunEndContext BuildContext(bool playerWonBattle)
        {
            float duration = runStatistics != null
                ? runStatistics.GetFightDurationSeconds()
                : (_fightStartTime >= 0f ? Time.time - _fightStartTime : 0f);

            var ctx = new RunEndContext
            {
                PlayerWon = playerWonBattle,
                FightDurationSeconds = duration,
                BossMaxHealthBefore = bossHealth != null ? bossHealth.MaxHealth : 0,
                BossAttackDamageBefore = bossAI != null ? bossAI.AttackDamage : 0,
                BossAttackCooldownBefore = bossAI != null ? bossAI.AttackCooldownSeconds : 0f,
                BossMoveSpeedBefore = bossAI != null ? bossAI.MoveSpeed : 0f,
                BossAttackRangeBefore = bossAI != null ? bossAI.AttackRange : 0f,
                BossAttackRadiusBefore = bossAI != null ? bossAI.AttackRadius : 0f,
                BossKickAttackRadiusBefore = bossAI != null ? bossAI.KickAttackRadius : 0f,
                BossFallbackHitPaddingBefore = bossAI != null ? bossAI.FallbackHitPadding : 0f,
                BossHitReactionCooldownBefore = bossHealth != null ? bossHealth.HitReactionCooldown : 0f,
                PlayerMaxHealthBefore = playerHealth != null ? playerHealth.MaxHealth : 0
            };

            if (runStatistics != null)
                runStatistics.ApplyStatsToContext(ref ctx);

            return ctx;
        }

        public void Apply(DifficultyProposalDto p)
        {
            if (p == null) return;
            p = DifficultyProposalClamp.Clamp(p);

            if (bossHealth != null)
                bossHealth.SetMaxHealthForRun(p.bossMaxHealth, refillToFull: true, clearDeadState: true);

            if (playerHealth != null)
                playerHealth.SetMaxHealthForRun(playerHealth.MaxHealth, refillToFull: true, clearDeadState: true);

            if (bossAI != null)
            {
                bossAI.SetCombatTuning(
                    p.bossAttackDamage,
                    p.bossAttackCooldown,
                    p.bossMoveSpeed,
                    p.bossAttackRange,
                    p.bossAttackRadius,
                    p.bossKickAttackRadius,
                    p.bossFallbackHitPadding);
            }

            if (bossHealth != null)
                bossHealth.SetHitReactionCooldownForRun(p.bossHitReactionCooldown);
        }
    }
}
