using System;
using UnityEngine;
using UnityEngine.Events;
using Yazlab.Mekanikler;

namespace Yazlab.Yonetim
{
    /// <summary>
    /// Oyunun ana akışını, aşamalarını ve durum geçişlerini yöneten merkez yöneticidir.
    /// Singleton deseni ve Observer deseni (C# Event/UnityEvent) kullanarak
    /// diğer bileşenlerin birbiriyle gevşek bağlı (loosely coupled) çalışmasını sağlar.
    /// </summary>
    public class BolumYoneticisi : MonoBehaviour
    {
        public static BolumYoneticisi Ornek { get; private set; }

        [Header("Mevcut Durum")]
        [SerializeField] private OyunDurumu mevcutDurum = OyunDurumu.Baslangic;

        [Header("Bölüm Nesneleri")]
        [SerializeField] private KapiKontrol fizikselEngelKapisi;
        [SerializeField] private SalterMekanizmasi salterMekanizmasi;
        [SerializeField] private KapiKontrol npsCikisKapisi;

        [Header("Olaylar")]
        public UnityEvent<OyunDurumu> onDurumDegisti;
        public UnityEvent onFizikselEngelAsildi;
        public UnityEvent onOyunTamamlandi;

        public OyunDurumu MevcutDurum => mevcutDurum;

        private void Awake()
        {
            if (Ornek != null && Ornek != this)
            {
                Destroy(gameObject);
                return;
            }
            Ornek = this;
        }

        private void Start()
        {
            DurumuDegistir(OyunDurumu.Baslangic);

            // Şalter tetiklendiğinde otomatik olarak durumu güncelle
            if (salterMekanizmasi != null)
            {
                salterMekanizmasi.onSalterIndirildi.AddListener(FizikselEngeliAs);
            }

            // Çıkış kapısı açıldığında oyunu tamamla
            if (npsCikisKapisi != null)
            {
                npsCikisKapisi.onKapiAcildi.AddListener(OyunuTamamla);
            }
        }

        public void DurumuDegistir(OyunDurumu yeniDurum)
        {
            if (mevcutDurum == yeniDurum) return;

            mevcutDurum = yeniDurum;
            Debug.Log($"[BolumYoneticisi] Oyun Durumu Değişti: -> {yeniDurum}");
            onDurumDegisti?.Invoke(yeniDurum);
        }

        /// <summary>
        /// Yoldaş karakter şalteri indirdiğinde çağrılır.
        /// </summary>
        public void FizikselEngeliAs()
        {
            if (mevcutDurum == OyunDurumu.FizikselEngelAsildi || mevcutDurum == OyunDurumu.BolumTamamlandi) return;

            DurumuDegistir(OyunDurumu.FizikselEngelAsildi);
            onFizikselEngelAsildi?.Invoke();
            Debug.Log("[BolumYoneticisi] 1. Aşama Başarılı: Fiziksel engel aşıldı! Oyuncu NPC alanına ilerleyebilir.");
        }

        /// <summary>
        /// Oyuncu NPC'nin yakınına geldiğinde çağrılır.
        /// </summary>
        public void NpcEtkilesimBaslat()
        {
            if (mevcutDurum == OyunDurumu.FizikselEngelAsildi)
            {
                DurumuDegistir(OyunDurumu.SosyalEngelAlaninda);
            }
        }

        /// <summary>
        /// LLM NPC ikna edildiğinde veya çıkış kapısı açıldığında çağrılır.
        /// </summary>
        public void OyunuTamamla()
        {
            DurumuDegistir(OyunDurumu.BolumTamamlandi);
            onOyunTamamlandi?.Invoke();
            Debug.Log("[BolumYoneticisi] TEBRİKLER! Oyun başarıyla tamamlandı.");
        }
    }
}
