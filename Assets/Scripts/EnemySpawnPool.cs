using UnityEngine;

/// <summary>
/// Birkaç hazır düşman prefab'ından birini seçerek sahneye çıkarır.
/// Onlarca model çeşidi olsa bile spawn için sadece bu havuzdaki prefab'ları kullanırsın (ör. 5 adet).
/// </summary>
public class EnemySpawnPool : MonoBehaviour
{
    public enum PickMode
    {
        Random,
        RoundRobin
    }

    [Header("Havuz")]
    [Tooltip("Animasyon + scriptleri doğru kurulu hazır düşman prefab'ları (ör. 5 varyant).")]
    [SerializeField] private GameObject[] enemyPrefabs;

    [Header("Konum")]
    [Tooltip("Boşsa spawn bu objenin pozisyonunda/rotasyonunda olur.")]
    [SerializeField] private Transform[] spawnPoints;

    [SerializeField] private PickMode pickMode = PickMode.Random;

    private int _roundRobinIndex;

    /// <summary>Rastgele veya sıradaki prefab'tan bir örnek oluşturur.</summary>
    public GameObject SpawnOne()
    {
        GameObject prefab = PickPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[EnemySpawnPool] enemyPrefabs içinde geçerli prefab yok.", this);
            return null;
        }

        GetSpawnPose(out Vector3 pos, out Quaternion rot);
        return Instantiate(prefab, pos, rot);
    }

    /// <summary>Belirtilen dünya pozisyonunda bir örnek oluşturur (pickMode yine geçerli).</summary>
    public GameObject SpawnOneAt(Vector3 position, Quaternion rotation)
    {
        GameObject prefab = PickPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[EnemySpawnPool] enemyPrefabs içinde geçerli prefab yok.", this);
            return null;
        }

        return Instantiate(prefab, position, rotation);
    }

    [ContextMenu("Spawn One (test)")]
    private void SpawnOneContextMenu()
    {
        SpawnOne();
    }

    private GameObject PickPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0)
            return null;

        if (pickMode == PickMode.Random)
        {
            int valid = 0;
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] != null)
                    valid++;
            }

            if (valid == 0)
                return null;

            int nth = Random.Range(0, valid);
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] == null)
                    continue;
                if (nth == 0)
                    return enemyPrefabs[i];
                nth--;
            }

            return null;
        }

        // RoundRobin — null slotları atla
        for (int tries = 0; tries < enemyPrefabs.Length; tries++)
        {
            int i = _roundRobinIndex % enemyPrefabs.Length;
            _roundRobinIndex++;
            if (enemyPrefabs[i] != null)
                return enemyPrefabs[i];
        }

        return null;
    }

    private void GetSpawnPose(out Vector3 position, out Quaternion rotation)
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform t = spawnPoints[Random.Range(0, spawnPoints.Length)];
            if (t != null)
            {
                position = t.position;
                rotation = t.rotation;
                return;
            }
        }

        position = transform.position;
        rotation = transform.rotation;
    }
}
