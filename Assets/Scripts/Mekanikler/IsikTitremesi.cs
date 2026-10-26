using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kale meşaleleri, elektrik panoları veya tehlike flaşörleri için
    /// gerçekçi rastgele titreme (flicker) ve parlaklık dalgalanması sağlar.
    /// </summary>
    public class IsikTitremesi : MonoBehaviour
    {
        public enum TitremeModu
        {
            MesaleAtesi,      // Yumuşak, sıcak ve rastgele alev titremesi
            ArizaliFlorasan,  // Anlık kesintiler ve cızırtılı patlamalar
            TehlikeSiren      // Kırmızı ritmik nabız / flaş
        }

        [Header("Mod ve Temel Ayarlar")]
        [SerializeField] private TitremeModu mod = TitremeModu.MesaleAtesi;
        [SerializeField] private Light hedefIsik;

        [Header("Şiddet Sınırları")]
        [SerializeField] private float minSiddet = 0.8f;
        [SerializeField] private float maxSiddet = 2.2f;
        [SerializeField] private float titremeHizi = 8.0f;

        [Header("Görsel Parça (Opsiyonel Alev/Lamba)")]
        [SerializeField] private MeshRenderer lambaRenderer;

        private float tabanSiddet = 1.0f;

        private void Start()
        {
            if (hedefIsik == null)
            {
                hedefIsik = GetComponent<Light>();
            }

            if (hedefIsik != null)
            {
                tabanSiddet = hedefIsik.intensity;
            }
        }

        private void Update()
        {
            if (hedefIsik == null) return;

            switch (mod)
            {
                case TitremeModu.MesaleAtesi:
                    // Perlin noise ile organik alev titremesi
                    float perlin = Mathf.PerlinNoise(Time.time * titremeHizi, 0.0f);
                    hedefIsik.intensity = Mathf.Lerp(minSiddet, maxSiddet, perlin);
                    break;

                case TitremeModu.ArizaliFlorasan:
                    // Rastgele anlık kesinti
                    if (Random.value > 0.92f)
                    {
                        hedefIsik.intensity = Random.Range(minSiddet * 0.2f, minSiddet);
                    }
                    else
                    {
                        hedefIsik.intensity = maxSiddet;
                    }
                    break;

                case TitremeModu.TehlikeSiren:
                    // Ritmik nabız
                    float sinus = (Mathf.Sin(Time.time * titremeHizi) + 1.0f) * 0.5f;
                    hedefIsik.intensity = Mathf.Lerp(minSiddet, maxSiddet, sinus);
                    break;
            }
        }
    }
}
