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
            mevcutDurum = OyunDurumu.Baslangic;
        }

        private Transform _oyuncuTransform;
        private Transform _npcTransform;
        private OllamaNpcDiyalogUI _ollamaDiyalogUI;

        private void Start()
        {
            ReferanslariKontrolEt();
            PrensesKulesiInsaci.FinalKulesiniKur();

            // Sahne yeniden yüklendiğinde eski bölüm sonu tetikleyicisi ve sağır arka duvarın kapalı olduğundan emin ol
            GameObject eskiTetik = GameObject.Find("BolumSonu_Tetikleyici");
            if (eskiTetik != null) eskiTetik.SetActive(false);

            DurumuDegistir(OyunDurumu.Baslangic);

            // Ollama LLM Diyalog bileşenini hazırla
            _ollamaDiyalogUI = GetComponent<OllamaNpcDiyalogUI>();
            if (_ollamaDiyalogUI == null)
            {
                _ollamaDiyalogUI = gameObject.AddComponent<OllamaNpcDiyalogUI>();
            }

            // Şalter tetiklendiğinde otomatik olarak durumu güncelle
            if (salterMekanizmasi != null)
            {
                salterMekanizmasi.onSalterIndirildi.RemoveListener(FizikselEngeliAs);
                salterMekanizmasi.onSalterIndirildi.AddListener(FizikselEngeliAs);
            }

            // Çıkış kapısı açıldığında 4. Bölüm (Zümrütleri vurma ve Prensesi kurtarma) görevini başlat
            if (npsCikisKapisi != null)
            {
                npsCikisKapisi.onKapiAcildi.RemoveListener(KizilKuleGoreviniBaslat);
                npsCikisKapisi.onKapiAcildi.AddListener(KizilKuleGoreviniBaslat);
            }
        }

        private void Update()
        {
            if (OllamaNpcDiyalogUI.DiyalogAcikMi || PrensesKurtarmaGorevi.OyunBittiMi) return;

            ReferanslariKontrolEt();

            // Oyuncu NPC Muhafızın yakınına geldiyse veya SosyalEngelAlaninda ise [E] tuşu ile Ollama Diyalog Ekranını aç
            bool npcYakininda = false;
            if (_oyuncuTransform != null && _npcTransform != null)
            {
                float mesafe = Vector3.Distance(_oyuncuTransform.position, _npcTransform.position);
                if (mesafe <= 4.8f)
                {
                    npcYakininda = true;
                    if (mevcutDurum != OyunDurumu.SosyalEngelAlaninda && mevcutDurum != OyunDurumu.PrensesKurtarma && mevcutDurum != OyunDurumu.BolumTamamlandi)
                    {
                        NpcEtkilesimBaslat();
                    }
                }
            }

            if (!_muhafizSaldirganMi && (mevcutDurum == OyunDurumu.SosyalEngelAlaninda || npcYakininda) && mevcutDurum != OyunDurumu.PrensesKurtarma && mevcutDurum != OyunDurumu.BolumTamamlandi)
            {
                if (EBasildiMi())
                {
                    if (_ollamaDiyalogUI == null)
                    {
                        _ollamaDiyalogUI = GetComponent<OllamaNpcDiyalogUI>() ?? gameObject.AddComponent<OllamaNpcDiyalogUI>();
                    }
                    _ollamaDiyalogUI.DiyalogPenceresiniAc();
                }
            }
        }

        private void ReferanslariKontrolEt()
        {
            if (_oyuncuTransform == null)
            {
                KarakterHareket kh = FindAnyObjectByType<KarakterHareket>();
                if (kh != null) _oyuncuTransform = kh.transform;
                else
                {
                    GameObject p = GameObject.FindWithTag("Player");
                    if (p != null) _oyuncuTransform = p.transform;
                }
            }

            if (_npcTransform == null)
            {
                GameObject npc = GameObject.Find("NPC_Muhafiz");
                if (npc != null) _npcTransform = npc.transform;
            }

            if (fizikselEngelKapisi == null)
            {
                GameObject fk = GameObject.Find("FizikselEngelKapisi");
                if (fk != null) fizikselEngelKapisi = fk.GetComponent<KapiKontrol>();
            }

            if (npsCikisKapisi == null)
            {
                GameObject nk = GameObject.Find("NpcCikisKapisi");
                if (nk != null)
                {
                    npsCikisKapisi = nk.GetComponent<KapiKontrol>();
                    if (npsCikisKapisi != null)
                    {
                        npsCikisKapisi.onKapiAcildi.RemoveListener(KizilKuleGoreviniBaslat);
                        npsCikisKapisi.onKapiAcildi.AddListener(KizilKuleGoreviniBaslat);
                    }
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
                npcDiyalog = "Muhafız Komutan Varg: 'Buradan geçiş iznin yok ona göre !'";
                diyalogBitisZamani = Time.time + 7.0f;
            }
        }

        private bool _muhafizSaldirganMi = false;

        /// <summary>
        /// Oyuncu NPC Muhafız ile konuşup çok aşamalı müzakere sonunda kapıyı açtırır.
        /// </summary>
        public void KizilKuleGoreviniBaslat()
        {
            PrensesKulesiInsaci.FinalKulesiniKur();

            GameObject arkaDuvar = GameObject.Find("KuleDuvar_Arka");
            if (arkaDuvar != null && arkaDuvar.activeSelf) arkaDuvar.SetActive(false);

            GameObject eskiTetik = GameObject.Find("BolumSonu_Tetikleyici");
            if (eskiTetik != null && eskiTetik.activeSelf) eskiTetik.SetActive(false);

            if (mevcutDurum != OyunDurumu.PrensesKurtarma)
            {
                DurumuDegistir(OyunDurumu.PrensesKurtarma);
            }
            npcDiyalog = "";
            diyalogBitisZamani = 0f;
            if (PrensesKurtarmaGorevi.Ornek != null)
            {
                PrensesKurtarmaGorevi.Ornek.GoreviBaslat();
            }
            Debug.Log("[BolumYoneticisi] 4. Bölüm: Kızıl Kule'ye geçit açıldı, Zümrütleri sırasıyla (Kırmızı -> Mavi -> Yeşil) vurarak Prenses Betül'ü kurtarın!");
        }

        public void MuhafizlaGorusVeKapisiAc()
        {
            ReferanslariKontrolEt();
            PrensesKulesiInsaci.FinalKulesiniKur();

            if (npsCikisKapisi == null)
            {
                GameObject nk = GameObject.Find("NpcCikisKapisi");
                if (nk != null) npsCikisKapisi = nk.GetComponent<KapiKontrol>();
            }

            if (npsCikisKapisi != null)
            {
                npsCikisKapisi.KilidiAc();
                npsCikisKapisi.KapiyiAc();
            }

            KizilKuleGoreviniBaslat();
            Debug.Log("[BolumYoneticisi] NPC Muhafız ikna edildi! Çıkış kapısı açılıyor.");
        }

        /// <summary>
        /// Oyuncu LLM diyaloğunda muhafızı kızdırırsa muhafız ölmez/vurulamaz;
        /// oyuncuyu sertçe uyarır ve kapıyı açmak için tekrar LLM ile ikna edilmeyi bekler.
        /// </summary>
        public void MuhafizSavasBaslat()
        {
            ReferanslariKontrolEt();
            if (_npcTransform != null)
            {
                DusmanKontrol mevcutDk = _npcTransform.GetComponent<DusmanKontrol>();
                if (mevcutDk != null)
                {
                    Destroy(mevcutDk);
                }
            }

            npcDiyalog = "Komutan Varg: 'Bana hakaret ve tehdit sökmez asker! [E] ile tekrar konuşup gece parolasını ('Kızıl Şahin') ve emri düzgünce söyle!'";
            diyalogBitisZamani = Time.time + 7.0f;
            Debug.Log("[BolumYoneticisi] Komutan Varg tehditleri reddetti.");
        }

        /// <summary>
        /// LLM NPC ikna edildiğinde veya çıkış kapısı arkasındaki alana ulaşıldığında çağrılır.
        /// </summary>
        public void OyunuTamamla()
        {
            if (mevcutDurum == OyunDurumu.BolumTamamlandi) return;

            DurumuDegistir(OyunDurumu.BolumTamamlandi);
            onOyunTamamlandi?.Invoke();
            npcDiyalog = "GÖREV BAŞARILI! BÖLÜM VE TÜM ENGELLER TAMAMLANDI!";
            diyalogBitisZamani = Time.time + 15.0f;
            Debug.Log("[BolumYoneticisi] TEBRİKLER! Operasyon başarıyla tamamlandı.");
        }

        private void OnGUI()
        {
            if (KapiBulmacaPaneli.MinigameAcik || OllamaNpcDiyalogUI.DiyalogAcikMi || PrensesKurtarmaGorevi.OyunBittiMi) return;

            float olcek = GoruntuKalitesiYoneticisi.KeskinOlcek;

            if (texKoyuArkaPlan == null)
            {
                texKoyuArkaPlan = new Texture2D(1, 1);
                texKoyuArkaPlan.SetPixel(0, 0, new Color(0.05f, 0.07f, 0.11f, 0.94f));
                texKoyuArkaPlan.Apply();
            }

            // Muhafızın yakınına gelindiğinde başının üzerinde "[E] MUHAFIZ İLE KONUŞ (OLLAMA LLM)" ipucu göster
            if (!_muhafizSaldirganMi && _oyuncuTransform != null && _npcTransform != null && mevcutDurum != OyunDurumu.PrensesKurtarma && mevcutDurum != OyunDurumu.BolumTamamlandi)
            {
                float npcMesafe = Vector3.Distance(_oyuncuTransform.position, _npcTransform.position);
                if (npcMesafe <= 4.8f && Camera.main != null)
                {
                    Vector3 tepeNokta = _npcTransform.position + Vector3.up * 2.25f;
                    if (GoruntuKalitesiYoneticisi.DunyaNoktasiniEkranaCevir(Camera.main, tepeNokta, out Vector2 ekranPos))
                    {
                        float bw = Mathf.Round(390f * olcek);
                        float bh = Mathf.Round(42f * olcek);
                        float bx = Mathf.Clamp(ekranPos.x - bw * 0.5f, 12f, Screen.width - bw - 12f);
                        float byPos = Mathf.Clamp(ekranPos.y - bh * 0.5f, 12f, Screen.height - bh - 12f);
                        GUI.DrawTexture(new Rect(bx, byPos, bw, bh), texKoyuArkaPlan);
                        GUIStyle ipucuStil = new GUIStyle(GUI.skin.label)
                        {
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        };
                        GoruntuKalitesiYoneticisi.StiliKeskinlestir(ipucuStil, Mathf.RoundToInt(14 * olcek));
                        ipucuStil.normal.textColor = new Color(1.0f, 0.88f, 0.25f);
                        GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(bx, byPos, bw, bh), "[E] MUHAFIZ İLE KONUŞ", ipucuStil);
                    }
                }
            }

            if (baslikStil == null)
            {
                baslikStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(baslikStil, Mathf.RoundToInt(15 * olcek));
            baslikStil.normal.textColor = new Color(1.0f, 0.88f, 0.25f);

            if (aciklamaStil == null)
            {
                aciklamaStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(aciklamaStil, Mathf.RoundToInt(13 * olcek));
            aciklamaStil.normal.textColor = new Color(0.96f, 0.98f, 1.0f);

            // 1. SOL ÜST: OPERASYON GÖREV BİLGİ KUTUSU (Dinamik yükseklik ile yazılar asla taşmaz/kesilmez)
            string gorevBaslik = "OPERASYON HEDEFİ:";
            string gorevDetay = "";

            switch (mevcutDurum)
            {
                case OyunDurumu.Baslangic:
                    gorevDetay = "• 1. Bölüm: [G] + [E] ile Askı Kapıyı indirip hendeği geçin.\n• 2. Bölüm: Kablo Odasındaki şalteri açın ve Tareti aşın.";
                    break;
                case OyunDurumu.FizikselEngelAlaninda:
                    gorevDetay = "• 2. Bölüm: Avludaki Otomatik Savunma Taretini imha edin veya\n• Doğu koridorundaki yedek jeneratör şalterini indirin.";
                    break;
                case OyunDurumu.FizikselEngelAsildi:
                    gorevDetay = "• İç Kule Kapısı: [G] ile Yoldaşı 2 açma koluna gönderip kapıyı açtırın.\n• 3. Bölüm: Merdivenlerden 2. kata çıkıp Muhafız Varg'a ilerleyin.";
                    break;
                case OyunDurumu.SosyalEngelAlaninda:
                    gorevDetay = "• [E] Muhafız Komutan Varg ile konuşup kapıyı açtırın.\n• İpucu: Gece parolası 'Kızıl Şahin' + Başkan emri / 500+ altın.";
                    break;
                case OyunDurumu.PrensesKurtarma:
                    int vurulan = PrensesKurtarmaGorevi.Ornek != null ? PrensesKurtarmaGorevi.Ornek.VurulanZumrutSayisi : 0;
                    int toplam = PrensesKurtarmaGorevi.Ornek != null ? PrensesKurtarmaGorevi.Ornek.ToplamZumrutSayisi : 3;
                    gorevBaslik = "4. BÖLÜM (FİNAL) — ZÜMRÜTLERİ SIRASIYLA VUR:";
                    gorevDetay = $"• Silahınızla sırasıyla 1. KIRMIZI → 2. MAVİ → 3. YEŞİL zümrütü vurun ({vurulan}/{toplam}).\n• Sırasıyla vurduğunuzda Prenses Betül kapsülden kurtulacaktır.";
                    break;
                case OyunDurumu.BolumTamamlandi:
                    gorevBaslik = "GÖREV BAŞARILI!";
                    gorevDetay = "• Tebrikler! Tüm zümrütler vuruldu ve Prenses Betül kurtarıldı.\n• Operasyon başarıyla sona erdi.";
                    break;
            }

            float kutuW = Mathf.Min(Screen.width - 36f, 480f * olcek);
            float padX = Mathf.Round(14f * olcek);
            float icW = kutuW - padX * 2f;
            float baslikH = Mathf.Max(22f * olcek, baslikStil.CalcHeight(new GUIContent(gorevBaslik), icW) + 4f);
            float detayH = Mathf.Max(42f * olcek, aciklamaStil.CalcHeight(new GUIContent(gorevDetay), icW) + 8f);
            float kutuH = baslikH + detayH + 18f * olcek;
            float x = 16f;
            float y = 16f;

            Color eski = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, kutuW, kutuH), texKoyuArkaPlan);

            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(x + padX, y + 8f * olcek, icW, baslikH), gorevBaslik, baslikStil);
            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(x + padX, y + 8f * olcek + baslikH + 2f, icW, detayH), gorevDetay, aciklamaStil);

            // 2. ORTA ALT: MUHAFIZ DİYALOG / BİLDİRİM KUTUSU
            if (mevcutDurum != OyunDurumu.PrensesKurtarma && Time.time < diyalogBitisZamani && !string.IsNullOrEmpty(npcDiyalog))
            {
                GUIStyle dStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter
                };
                GoruntuKalitesiYoneticisi.StiliKeskinlestir(dStil, Mathf.RoundToInt(14 * olcek));
                dStil.normal.textColor = new Color(0.35f, 0.98f, 0.50f);

                float dW = Mathf.Min(Screen.width - 40f, 680f * olcek);
                float dIcW = dW - 28f * olcek;
                float dH = Mathf.Max(52f * olcek, dStil.CalcHeight(new GUIContent(npcDiyalog), dIcW) + 18f * olcek);
                float dX = Mathf.Round((Screen.width - dW) * 0.5f);
                float dY = Mathf.Round(Screen.height - dH - 58f * olcek);

                GUI.DrawTexture(new Rect(dX, dY, dW, dH), texKoyuArkaPlan);
                GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(dX + 14f * olcek, dY + 6f * olcek, dIcW, dH - 12f * olcek), npcDiyalog, dStil);
            }

            GUI.color = eski;
        }
    }
}
