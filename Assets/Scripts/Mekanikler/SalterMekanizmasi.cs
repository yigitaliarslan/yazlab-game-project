using UnityEngine;
using UnityEngine.Events;
using Yazlab.Etkilesim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Otonom Yoldaş Ajanın veya Oyuncunun tetikleyebileceği şalter / baskı plakası mekanizması.
    /// Tek Sorumluluk: Şalterin tetiklenmesini, görsel durumunu ve bağlı kapıyı açma emrini yönetir.
    /// </summary>
    public class SalterMekanizmasi : MonoBehaviour, IEtkilesimli
    {
        public enum TetiklemeYontemi
        {
            ManuelEtkilesim,    // Ajan yanına gelip EtkilesimeGir metodunu çağırır
            AlanaGirmece        // Trigger alanına girdiği anda otomatik tetiklenir (Baskı Plakası)
        }

        [Header("Tetikleme Ayarları")]
        [SerializeField] private TetiklemeYontemi tetiklemeYontemi = TetiklemeYontemi.ManuelEtkilesim;
        [SerializeField] private bool sadeceYoldasTetikleyebilir = false;
        [SerializeField] private bool tekKullanimlik = true;

        [Header("Bağlı Kapı veya Hedef")]
        [SerializeField] private KapiKontrol bagliKapi;

        [Header("Görsel Geri Bildirim (Opsiyonel)")]
        [SerializeField] private Transform salterKolu; // Kol varsa dönecek parça
        [SerializeField] private Vector3 aktifKolAcisi = new Vector3(45f, 0f, 0f);
        [SerializeField] private MeshRenderer durumIsigi; // Kırmızıdan yeşile dönecek ışık
        [SerializeField] private Color aktifRenk = Color.green;
        [SerializeField] private Color pasifRenk = Color.red;

        [Header("Olaylar (Events)")]
        public UnityEvent onSalterIndirildi;

        private bool aktifMi = false;

        public bool EtkilesimeHazirMi => !aktifMi || !tekKullanimlik;
        public string EtkilesimMetni => aktifMi ? "Şalter Zaten İndirildi" : "Şalteri İndir [E]";
        public bool AktifMi => aktifMi;

        private void Start()
        {
            if (durumIsigi != null)
            {
                durumIsigi.material.color = pasifRenk;
            }
        }

        public void EtkilesimeGir(Transform etkilesimiBaslatan)
        {
            if (!EtkilesimeHazirMi) return;

            // Eğer sadece yoldaş tetikleyebilir kuralı varsa kontrol et
            if (sadeceYoldasTetikleyebilir && !etkilesimiBaslatan.CompareTag("Companion") && !etkilesimiBaslatan.name.Contains("Yardimci"))
            {
                Debug.Log("[Salter] Bu şaltere yalnızca Yoldaş Karakter ulaşabilir!");
                return;
            }

            Tetikle();
        }

        public void Tetikle()
        {
            if (!EtkilesimeHazirMi) return;

            aktifMi = true;
            Debug.Log($"[{gameObject.name}] Şalter başarıyla tetiklendi! Kapı açılıyor.");

            // Görsel efektler
            if (salterKolu != null)
            {
                salterKolu.localEulerAngles = aktifKolAcisi;
            }

            if (durumIsigi != null)
            {
                durumIsigi.material.color = aktifRenk;
            }

            // Bağlı kapıyı aç
            if (bagliKapi != null)
            {
                bagliKapi.KilidiAc();
                bagliKapi.KapiyiAc();
            }

            onSalterIndirildi?.Invoke();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (tetiklemeYontemi != TetiklemeYontemi.AlanaGirmece) return;
            if (!EtkilesimeHazirMi) return;

            // Yoldaş veya Oyuncu girdiğinde tetikle
            bool gecerliAktör = other.CompareTag("Player") || other.CompareTag("Companion") || other.name.Contains("Yardimci");
            if (gecerliAktör)
            {
                EtkilesimeGir(other.transform);
            }
        }
    }
}
