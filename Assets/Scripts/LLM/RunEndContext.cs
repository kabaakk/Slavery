using System;

namespace Slavery.LLM
{
    /// <summary>
    /// Bir dövüş / run bittiğinde LLM veya mock'a giden özet.
    /// </summary>
    [Serializable]
    public struct RunEndContext
    {
        /// <summary>true = boss yenildi, bir sonraki run zorlaşır (tasarımına göre).</summary>
        public bool PlayerWon;

        public float FightDurationSeconds;

        public int BossMaxHealthBefore;
        public int BossAttackDamageBefore;
        public float BossAttackCooldownBefore;
        public float BossMoveSpeedBefore;
        public float BossAttackRangeBefore;
        public float BossAttackRadiusBefore;
        public float BossKickAttackRadiusBefore;
        public float BossFallbackHitPaddingBefore;
        public float BossHitReactionCooldownBefore;
        public int PlayerMaxHealthBefore;

        /// <summary>Run sonunda oyuncunun kalan canı (ölüyse 0).</summary>
        public int PlayerHpRemainingEnd;

        public int PlayerHpMaxEnd;
        public int BossHpRemainingEnd;
        public int BossHpMaxEnd;
        public int HitsTakenByPlayer;
        public int HitsDealtToBoss;
        public int DashCount;
    }
}
