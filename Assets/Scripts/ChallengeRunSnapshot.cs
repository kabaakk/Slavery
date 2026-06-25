using System;

/// <summary>
/// Dövüş bittiğinde UI ve LLM için tek paket özet.
/// </summary>
[Serializable]
public struct ChallengeRunSnapshot
{
    public bool PlayerWon;
    public float FightDurationSeconds;
    public int HitsDealtToBoss;
    public int EnemiesDefeated;
    public int HitsTakenByPlayer;
    public int DashCount;
    public int PlayerHpRemaining;
    public int PlayerHpMax;
    public int BossHpRemaining;
    public int BossHpMax;
}
