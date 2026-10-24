using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
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
        public UnityEvent<OyunDurumu> onDurumDegisti = new UnityEvent<OyunDurumu>();
        public UnityEvent onFizikselEngelAsildi = new UnityEvent();
        public UnityEvent onOyunTamamlandi = new UnityEvent();

        public OyunDurumu MevcutDurum => mevcutDurum;

        // NPC Etkileşim ve UI Alanı
        private string npcDiyalog = "";
        private float diyalogBitisZamani = 0f;
        private Texture2D texKoyuArkaPlan;
        private GUIStyle baslikStil;
        private GUIStyle aciklamaStil;

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

        private void Update()
        {
            // Sosyal engel alanındayken (NPC Muhafızın yanında) E tuşu ile etkileşim
            if (mevcutDurum == OyunDurumu.SosyalEngelAlaninda)
            {
                if (EBasildiMi())
                {
                    MuhafizlaGorusVeKapisiAc();
                }
            }
        }

        private bool EBasildiMi()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.E)) return true;
            }
            catch { }
            return false;
        }

        public void DurumuDegistir(OyunDurumu yeniDurum)
        {
            if (mevcutDurum == yeniDurum) return;

            mevcutDurum = yeniDurum;
            Debug.Log($"[BolumYoneticisi] Oyun Durumu Değişti: -> {yeniDurum}");
            onDurumDegisti?.Invoke(yeniDurum);
        }

        /// <summary>
        /// Yoldaş karakter şalteri indirdiğinde veya elektrik bulmacası çözüldüğünde çağrılır.
        /// </summary>
        public void FizikselEngeliAs()
        {
            if (mevcutDurum == OyunDurumu.FizikselEngelAsildi || mevcutDurum == OyunDurumu.BolumTamamlandi) return;

            DurumuDegistir(OyunDurumu.FizikselEngelAsildi);
            onFizikselEngelAsildi?.Invoke();

            // Taret tehdit ışığını yeşile çevir (Savunma sistemi devre dışı)
            GameObject taretIsik = GameObject.Find("Taret_TehditIsigi");
            if (taretIsik != null)
            {
                MeshRenderer rend = taretIsik.GetComponent<MeshRenderer>();
                if (rend != null)
                {
                    rend.material.color = new Color(0.15f, 0.85f, 0.25f);
                }
            }

            Debug.Log("[BolumYoneticisi] 1. Aşama Başarılı: Fiziksel engel aşıldı! Taret devre dışı, oyuncu kuleye ilerleyebilir.");
        }

        /// <summary>
        /// Oyuncu kuledeki NPC Muhafızın yakınına geldiğinde çağrılır.
        /// </summary>
        public void NpcEtkilesimBaslat()
        {
            if (mevcutDurum == OyunDurumu.FizikselEngelAsildi || mevcutDurum == OyunDurumu.Baslangic)
            {
                DurumuDegistir(OyunDurumu.SosyalEngelAlaninda);
                npcDiyalog = "Muhafız: 'Buradan geçiş izniniz yok! Ama yeterli altın veya ikna ile bir yol bulabiliriz...'";
                diyalogBitisZamani = Time.time + 6.0f;
            }
        }

        /// <summary>
        /// Oyuncu NPC Muhafız ile konuşup kapıyı açtırır.
        /// </summary>
        public void MuhafizlaGorusVeKapisiAc()
        {
            if (npsCikisKapisi != null)
            {
                npsCikisKapisi.KilidiAc();
                npsCikisKapisi.KapiyiAc();
            }

            npcDiyalog = "Muhafız: 'Anlaştık! Kapıyı açıyorum, kimseye görünmeden hızlıca çıkışa ilerle!'";
            diyalogBitisZamani = Time.time + 7.0f;
            Debug.Log("[BolumYoneticisi] NPC Muhafız ikna edildi! Çıkış kapısı açılıyor.");
        }

        /// <summary>
        /// LLM NPC ikna edildiğinde veya çıkış kapısı arkasındaki alana ulaşıldığında çağrılır.
        /// </summary>
        public void OyunuTamamla()
        {
            if (mevcutDurum == OyunDurumu.BolumTamamlandi) return;

            DurumuDegistir(OyunDurumu.BolumTamamlandi);
            onOyunTamamlandi?.Invoke();
            npcDiyalog = "OPERASYON BAŞARILI! BÖLÜM VE TÜM ENGELLER TAMAMLANDI!";
            diyalogBitisZamani = Time.time + 15.0f;
            Debug.Log("[BolumYoneticisi] TEBRİKLER! Operasyon başarıyla tamamlandı.");
        }

        private void OnGUI()
        {
            if (KapiBulmacaPaneli.MinigameAcik) return;

            if (texKoyuArkaPlan == null)
            {
                texKoyuArkaPlan = new Texture2D(1, 1);
                texKoyuArkaPlan.SetPixel(0, 0, new Color(0.08f, 0.10f, 0.14f, 0.88f));
                texKoyuArkaPlan.Apply();
            }

            if (baslikStil == null)
            {
                baslikStil = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                baslikStil.normal.textColor = new Color(0.95f, 0.85f, 0.25f);
            }

            if (aciklamaStil == null)
            {
                aciklamaStil = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal
                };
                aciklamaStil.normal.textColor = Color.white;
            }

            // 1. SOL ÜST: OPERASYON GÖREV BİLGİ KUTUSU
            float kutuW = 380f;
            float kutuH = 68f;
            float x = 20f;
            float y = 20f;

            Color eski = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, kutuW, kutuH), texKoyuArkaPlan);

            string gorevBaslik = "OPERASYON HEDEFİ:";
            string gorevDetay = "";

            switch (mevcutDurum)
            {
                case OyunDurumu.Baslangic:
                    gorevDetay = "• Siperleri aşın, düşmanları etkisiz hale getirin.\n• Kilitli Depo Kapısına veya Jeneratör Şalterine ulaşın.";
                    break;
                case OyunDurumu.FizikselEngelAlaninda:
                    gorevDetay = "• Elektrik paneli bulmacasını çözün veya\n• Yoldaş ile gizli yoldan jeneratör şalterini indirin.";
                    break;
                case OyunDurumu.FizikselEngelAsildi:
                    gorevDetay = "• Merdivenlerden kuleye çıkın.\n• Nöbetçi Muhafızın bulunduğu odaya ilerleyin.";
                    break;
                case OyunDurumu.SosyalEngelAlaninda:
                    gorevDetay = "• [E] Rüşvetçi Muhafız ile görüşün ve kapıyı açtırın.\n• Açılan kapıdan çıkış bölgesine ulaşın.";
                    break;
                case OyunDurumu.BolumTamamlandi:
                    gorevBaslik = "✓ GÖREV TAMAMLANDI!";
                    gorevDetay = "• Tebrikler! Tüm taktiksel ve sosyal engeller aşıldı.\n• Operasyon başarıyla sona erdi.";
                    break;
            }

            GUI.Label(new Rect(x + 12, y + 8, kutuW - 24, 20), gorevBaslik, baslikStil);
            GUI.Label(new Rect(x + 12, y + 26, kutuW - 24, 38), gorevDetay, aciklamaStil);

            // 2. ORTA ALT: MUHAFIZ DİYALOG / BİLDİRİM KUTUSU
            if (Time.time < diyalogBitisZamani && !string.IsNullOrEmpty(npcDiyalog))
            {
                float dW = 620f;
                float dH = 48f;
                float dX = (Screen.width - dW) * 0.5f;
                float dY = Screen.height - 110f;

                GUI.DrawTexture(new Rect(dX, dY, dW, dH), texKoyuArkaPlan);
                GUIStyle dStil = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                dStil.normal.textColor = new Color(0.35f, 0.95f, 0.45f);
                GUI.Label(new Rect(dX + 10, dY + 6, dW - 20, dH - 12), npcDiyalog, dStil);
            }

            GUI.color = eski;
        }
    }
}
