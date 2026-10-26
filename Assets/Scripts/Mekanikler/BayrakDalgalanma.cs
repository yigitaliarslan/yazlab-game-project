using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Rüzgar etkisiyle kale burçlarındaki veya karargah tepesindeki
    /// bayrak ve flamaların hafifçe dalgalanmasını simüle eder.
    /// </summary>
    public class BayrakDalgalanma : MonoBehaviour
    {
        [Header("Dalgalanma Ayarları")]
        [Tooltip("Rüzgar dalgalanma hızı.")]
        [SerializeField] private float dalgaHizi = 3.5f;

        [Tooltip("Rüzgarın sallanma açısı (derece).")]
        [SerializeField] private float dalgaMiktari = 14.0f;

        [Tooltip("Rüzgarın sallanma ekseni.")]
        [SerializeField] private Vector3 dalgaEkseni = Vector3.forward;

        private Quaternion baslangicRotasyon;

        private void Start()
        {
            baslangicRotasyon = transform.localRotation;
        }

        private void Update()
        {
            // Perlin Noise ve Sinüs bileşimi ile doğal rüzgar savrulması
            float ruzgar = Mathf.Sin(Time.time * dalgaHizi) * dalgaMiktari;
            ruzgar += (Mathf.PerlinNoise(Time.time * (dalgaHizi * 0.7f), 0f) - 0.5f) * (dalgaMiktari * 0.5f);

            transform.localRotation = baslangicRotasyon * Quaternion.AngleAxis(ruzgar, dalgaEkseni);
        }
    }
}
