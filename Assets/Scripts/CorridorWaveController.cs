using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Koridor: düşman ölünce kısa gecikme → ceset kaldır → havuzdan yeni düşman.
/// Toplam N düşman (varsayılan 5) öldürülünce <see cref="onAllEnemiesDefeated"/> tetiklenir (kapı / boss sahnesi buraya bağlanır).
/// </summary>
public class CorridorWaveController : MonoBehaviour
{
    [Header("Havuz")]
    [SerializeField] private EnemySpawnPool spawnPool;

    [Header("Dalga")]
    [Tooltip("Öldürülmesi gereken düşman sayısı (ör. 5).")]
    [SerializeField] private int enemiesTotal = 5;

    [Tooltip("Ölüm animasyonu için bekleme; ardından obje Destroy edilir ve sıradaki spawn olur. 0 girilirse güvenli varsayılan (1.25s) kullanılır.")]
    [SerializeField] private float corpseRemovalDelay = 1.25f;

    [Header("Başlangıç")]
    [Tooltip("Sahneye elle yerleştirdiğin ilk düşman (boşsa aşağıdaki seçenek devreye girer).")]
    [SerializeField] private EnemyHealth startingEnemy;

    [Tooltip("startingEnemy yoksa Play’de havuzdan ilk düşmanı üretir.")]
    [SerializeField] private bool spawnFirstEnemyOnStart = true;

    [Header("Tamamlandı (5. düşman öldükten sonra)")]
    [SerializeField] private UnityEvent onAllEnemiesDefeated;

    private int _killCount;
    private bool _waveComplete;

    /// <summary>5. düşman da öldükten sonra true (kapı geçişi burayı kontrol edebilir).</summary>
    public bool IsWaveComplete => _waveComplete;
    public int KillCount => _killCount;

    /// <summary>Inspector’daki <see cref="onAllEnemiesDefeated"/> ile aynı anda tetiklenir.</summary>
    public event Action WaveCompleted;

    private void Start()
    {
        if (startingEnemy != null)
            RegisterEnemy(startingEnemy, startingEnemy.gameObject);
        else if (spawnFirstEnemyOnStart && spawnPool != null)
        {
            GameObject go = spawnPool.SpawnOne();
            if (go != null)
                RegisterEnemy(go.GetComponentInChildren<EnemyHealth>(true), go);
            else
                Debug.LogWarning("[CorridorWaveController] İlk düşman spawn edilemedi (havuz boş?).", this);
        }
    }

    private void RegisterEnemy(EnemyHealth eh, GameObject ownerRoot)
    {
        if (eh == null)
        {
            Debug.LogWarning("[CorridorWaveController] EnemyHealth bulunamadı.", this);
            return;
        }
        if (ownerRoot == null)
            ownerRoot = eh.gameObject;

        GameObject rootToDestroy = ownerRoot;
        eh.Died += () => OnEnemyDied(eh, rootToDestroy);
    }

    private void OnEnemyDied(EnemyHealth dead, GameObject rootToDestroy)
    {
        if (_waveComplete)
            return;

        _killCount++;
        Transform t = dead != null ? dead.transform : null;
        Vector3 deathPosition = t != null ? t.position : transform.position;
        Quaternion deathRotation = t != null ? t.rotation : transform.rotation;
        StartCoroutine(CorpseThenRespawnRoutine(rootToDestroy, deathPosition, deathRotation));
    }

    private IEnumerator CorpseThenRespawnRoutine(GameObject corpse, Vector3 deathPosition, Quaternion deathRotation)
    {
        float delay = corpseRemovalDelay > 0.01f ? corpseRemovalDelay : 1.25f;
        yield return new WaitForSeconds(delay);

        if (corpse != null)
            Destroy(corpse);

        if (_killCount >= enemiesTotal)
        {
            _waveComplete = true;
            onAllEnemiesDefeated?.Invoke();
            WaveCompleted?.Invoke();
            yield break;
        }

        if (spawnPool == null)
        {
            Debug.LogWarning("[CorridorWaveController] Spawn havuzu atanmadı; sıradaki düşman oluşturulamadı.", this);
            yield break;
        }

        // Her yeni düşman spawn havuzundaki rastgele noktada doğar.
        GameObject next = spawnPool.SpawnOne();
        if (next != null)
            RegisterEnemy(next.GetComponentInChildren<EnemyHealth>(true), next);
        else
            Debug.LogWarning("[CorridorWaveController] Havuzdan spawn başarısız.", this);
    }
}
