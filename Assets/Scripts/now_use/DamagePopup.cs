using UnityEngine;

/// <summary>轻量世界空间伤害数字，仅负责命中反馈。</summary>
public sealed class DamagePopup : MonoBehaviour
{
    private const float Duration = 0.55f;
    private TextMesh textMesh;
    private float age;

    public static void Spawn(Vector3 worldPosition, float damage)
    {
        GameObject popup = new GameObject("DamagePopup");
        popup.transform.position = worldPosition + Vector3.up * 0.35f;

        TextMesh text = popup.AddComponent<TextMesh>();
        text.text = Mathf.Max(0f, damage).ToString("0.#");
        text.color = new Color(1f, 0.2f, 0.15f, 1f);
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.16f;
        text.fontSize = 48;

        MeshRenderer renderer = popup.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sortingOrder = 300;

        popup.AddComponent<DamagePopup>().textMesh = text;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Duration);
        transform.position += Vector3.up * (0.65f * Time.deltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.8f, t);

        if (textMesh != null)
        {
            Color color = textMesh.color;
            color.a = 1f - t;
            textMesh.color = color;
        }

        if (age >= Duration) Destroy(gameObject);
    }
}
