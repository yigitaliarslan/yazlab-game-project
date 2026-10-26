using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kale burçları veya kule üzerine yerleştirilen, avludaki siperleri
    /// belirli bir açı aralığında ileri-geri tarayan gözetleme projektörü.
    /// </summary>
    public class GuvenlikProjektoru : MonoBehaviour
    {
        [Header("Tarama Açısı ve Hızı")]
        [Tooltip("Projektörün merkezden sağa ve sola yapacağı maksimum sapma açısı.")]
        [SerializeField] private float taramaGenisligi = 40.0f;

        [Tooltip("Tarama salınımının hızı.")]
        [SerializeField] private float taramaHizi = 0.8f;

        [Tooltip("Projektörün aşağı doğru bakış eğimi (Pitch).")]
        [SerializeField] private float asagiEgimAcisi = 25.0f;

        [Header("Projektör Işığı (Opsiyonel)")]
        [SerializeField] private Light projektorIsigi;
        [SerializeField] private MeshRenderer fenerLensRenderer;
        [SerializeField] private Color acikRenk = Color.white;

        private float merkezYaw = 0f;

        private void Start()
        {
            merkezYaw = transform.localEulerAngles.y;

            if (projektorIsigi == null)
            {
                projektorIsigi = GetComponentInChildren<Light>();
            }

            if (projektorIsigi != null)
            {
                projektorIsigi.color = acikRenk;
            }
        }

        private void Update()
        {
            // Sinüs dalgası ile yumuşak ileri-geri tarama hareketi
            float sapma = Mathf.Sin(Time.time * taramaHizi) * taramaGenisligi;
            float guncelYaw = merkezYaw + sapma;

            transform.localRotation = Quaternion.Euler(asagiEgimAcisi, guncelYaw, 0f);
        }
    }
}
