using System.Collections;
using UnityEngine;

public class SlimeProjectile : MonoBehaviour
{
    public void Launch(Vector3 start, Vector3 end, float duration, float arcHeight, System.Action onLand)
    {
        transform.position = start;
        StartCoroutine(FlyRoutine(start, end, duration, arcHeight, onLand));
    }

    private IEnumerator FlyRoutine(Vector3 start, Vector3 end, float duration, float arcHeight, System.Action onLand)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 pos = Vector3.Lerp(start, end, t);
            pos.y += arcHeight * Mathf.Sin(Mathf.PI * t);
            transform.position = pos;
            yield return null;
        }
        transform.position = end;
        onLand?.Invoke();
        Destroy(gameObject);
    }
}
