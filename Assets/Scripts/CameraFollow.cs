using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;           // Player
    [SerializeField] private Transform secondaryTarget;    // Boss (midpoint için)

    [Header("Follow Settings")]
    [SerializeField] private bool useMidpoint = true;      // true = player+boss ortası takip
    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -10f); // Midpoint'e göre sabit izometrik offset
    [SerializeField] private float followSpeed = 8f;

    [Header("Bounds (opsiyonel)")]
    [SerializeField] private bool useBounds = false;
    [SerializeField] private float minX = -2f;
    [SerializeField] private float maxX = 12f;
    [SerializeField] private float minZ = -2f;
    [SerializeField] private float maxZ = 12f;

    private void LateUpdate()
    {
        if (target == null)
            return;

        // Midpoint: player ile düşmanın orta noktası (veya sadece player)
        Vector3 focusPoint = target.position;
        if (useMidpoint && secondaryTarget != null)
            focusPoint = (target.position + secondaryTarget.position) * 0.5f;

        // Kamera pozisyonu = midpoint + sabit offset (izometrik)
        Vector3 desiredPosition = focusPoint + offset;

        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minZ, maxZ);
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        // Her zaman midpoint'e bak (izometrik açı)
        transform.LookAt(focusPoint);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    public void SetSecondaryTarget(Transform newSecondary)
    {
        secondaryTarget = newSecondary;
    }
}

