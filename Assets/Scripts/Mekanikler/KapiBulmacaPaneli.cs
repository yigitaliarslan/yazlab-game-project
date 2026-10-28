using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Yazlab.Yonetim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Among Us tarzı elektrik sigortaları / şalter bulmaca konsolu.
    /// Başlangıçta harita karanlık/loş bir elektrik kesintisi atmosferindedir.
    /// Bulmaca açıldığında karakter ve kamera kilitlenir, fare ile şalterler kontrol edilir.
    /// Bulmaca çözüldüğünde jeneratör devreye girer: Güneş ve tam aydınlatma gelir, 1. kapı açılır.
    /// </summary>
    public class KapiBulmacaPaneli : MonoBehaviour
    {
        public static bool MinigameAcik { get; private set; } = false;

        [Header("Bağlı Kapı")]
        [Tooltip("Bulmaca çözüldüğünde açılacak kapı.")]
        public KapiKontrol bagliKapi;

        [Header("Etkileşim Ayarları")]
        [Tooltip("Oyuncunun konsolu açabileceği maksimum mesafe.")]
        public float etkilesimMenzili = 3.5f;

        [Header("Görsel Durum Işığı")]
        public Light durumIsigi;
        public MeshRenderer durumLambaMesh;

        // Bulmaca Durumu
        private bool bulmacaCozuldu = false;
        private Transform oyuncuTransform;

        // Among Us Şalter Durumları (5 Sigorta)
        private bool[] salterler = new bool[5] { false, true, false, false, true };
        private float cozulmeZamani = 0f;

        // Aydınlatma Yönetimi (Başta loş/karanlık, çözülünce güneş)
        private Light anaGunesIsigi;
        private float orijinalGunesSiddeti = 2.0f;
        private Color orijinalGunesRengi = Color.white;
        private Color orijinalAmbientRengi;
        private float orijinalAmbientSiddeti = 1.0f;

        // GUI Dokuları ve Stilleri
        private Texture2D texBeyaz;
        private Texture2D texSiyah;
        private GUIStyle baslikStili;
        private GUIStyle altBaslikStili;
        private GUIStyle durumStili;
        private GUIStyle dugmeStili;
        private GUIStyle salterDugmeStili;

        // Sesler (Prosedürel)
        private AudioSource audioSource;
        private AudioClip sesTik;
        private AudioClip sesBasari;
        private AudioClip sesGucDalgasi;

        public bool BulmacaCozuldu => bulmacaCozuldu;

        void Awake()
        {
            // Ses kaynağı
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.4f;
            }

            // Prosedürel sesler
            sesTik = SesUret(880f, 0.05f, 0.35f);
            sesBasari = SesUretIkiTonlu(523.25f, 659.25f, 0.45f);
            sesGucDalgasi = SesUretKalin(110f, 220f, 0.8f, 0.4f);

            // Bağlı 1. kapıyı bul
            if (bagliKapi == null)
            {
                GameObject kapiObj = GameObject.Find("FizikselEngelKapisi");
                if (kapiObj != null)
                {
                    bagliKapi = kapiObj.GetComponent<KapiKontrol>();
                }
            }

            // 3D Konsol Görsellerini Kontrol Et / Oluştur
            KonsolGorselleriniHazirla();
        }

        void Start()
        {
            DurumIsiginiGuncelle(false);

            // Başlangıçta oyunu loş / elektrik kesintisi moduna al
            LosAydinlatmayiAyarla();
        }

        /// <summary>
        /// Oyun başladığında haritayı loş, karanlık acil durum moduna alır.
        /// </summary>
        private void LosAydinlatmayiAyarla()
        {
            // Sahnede Directional Light'ı (Güneş) bul
            Light[] isiklar = Object.FindObjectsByType<Light>();
            foreach (var l in isiklar)
            {
                if (l.type == LightType.Directional)
                {
                    anaGunesIsigi = l;
                    orijinalGunesSiddeti = l.intensity;
                    orijinalGunesRengi = l.color;
                    break;
                }
            }

            // Orijinal ortam ışıklarını kaydet
            orijinalAmbientRengi = RenderSettings.ambientLight;
            orijinalAmbientSiddeti = RenderSettings.ambientIntensity;

            if (anaGunesIsigi != null)
            {
                // Güneşi çok loş, acil durum gecesi tonuna getir
                anaGunesIsigi.intensity = 0.06f;
                anaGunesIsigi.color = new Color(0.20f, 0.28f, 0.45f);
            }

            // Ortam ışığını da loş yap
            RenderSettings.ambientLight = new Color(0.08f, 0.10f, 0.15f);
            RenderSettings.ambientIntensity = 0.15f;
        }

        void Update()
        {
            // Oyuncuyu bul
            if (oyuncuTransform == null)
            {
                KarakterHareket kh = Object.FindAnyObjectByType<KarakterHareket>();
                if (kh != null) oyuncuTransform = kh.transform;
                else
                {
                    GameObject p = GameObject.FindWithTag("Player");
                    if (p != null) oyuncuTransform = p.transform;
                }
            }

            if (oyuncuTransform == null) return;

            float mesafe = Vector3.Distance(transform.position, oyuncuTransform.position);

            // Panel kapalıyken [E] tuşu ile açma
            if (!MinigameAcik && !bulmacaCozuldu && mesafe <= etkilesimMenzili)
            {
                if (EBasildiMi())
                {
                    PaneliAc();
                }
            }
            // Panel açıkken ESC veya [E] tuşu ile kapatma
            else if (MinigameAcik)
            {
                if (EscBasildiMi() || EBasildiMi())
                {
                    PaneliKapat();
                    return;
                }

                KlavyeSigortaKisayollariniKontrolEt();

                // Oyuncu uzaklaşırsa paneli otomatik kapat
                if (mesafe > etkilesimMenzili + 2.0f)
                {
                    PaneliKapat();
                }
            }
        }

        public void PaneliAc()
        {
            MinigameAcik = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (sesTik != null) audioSource.PlayOneShot(sesTik);
        }

        public void PaneliKapat()
        {
            MinigameAcik = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
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

        private bool EscBasildiMi()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
#endif
            try
            {
                if (Input.GetKeyDown(KeyCode.Escape)) return true;
            }
            catch { }
            return false;
        }

        private void KlavyeSigortaKisayollariniKontrolEt()
        {
            if (bulmacaCozuldu) return;
            for (int i = 0; i < 5; i++)
            {
                bool basildi = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                {
                    if (i == 0 && Keyboard.current.digit1Key.wasPressedThisFrame) basildi = true;
                    else if (i == 1 && Keyboard.current.digit2Key.wasPressedThisFrame) basildi = true;
                    else if (i == 2 && Keyboard.current.digit3Key.wasPressedThisFrame) basildi = true;
                    else if (i == 3 && Keyboard.current.digit4Key.wasPressedThisFrame) basildi = true;
                    else if (i == 4 && Keyboard.current.digit5Key.wasPressedThisFrame) basildi = true;
                }
#endif
                if (!basildi)
                {
                    try { if (Input.GetKeyDown(KeyCode.Alpha1 + i)) basildi = true; } catch { }
                }
                if (basildi)
                {
                    salterler[i] = !salterler[i];
                    if (sesTik != null && audioSource != null) audioSource.PlayOneShot(sesTik);
                }
            }
        }

        void OnGUI()
        {
            DokulariOlustur();

            // 1. DÜNYA İÇİNDE [E] ETKİLEŞİM İPUCU (Konsola Yakındayken)
            if (!MinigameAcik && !bulmacaCozuldu && oyuncuTransform != null)
            {
                float mesafe = Vector3.Distance(transform.position, oyuncuTransform.position);
                if (mesafe <= etkilesimMenzili)
                {
                    CizEtkilesimIpucu();
                }
            }

            // 2. YÜKSEK ÇÖZÜNÜRLÜKLÜ AMONG US BULMACA ARAYÜZÜ (Panel Açıkken)
            if (MinigameAcik)
            {
                CizYuksekCozunurlukluAmongUsPaneli();
            }
        }

        private void CizEtkilesimIpucu()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 hedefNokta = transform.position + Vector3.up * 1.45f;
            Vector3 ekran = cam.WorldToScreenPoint(hedefNokta);
            if (ekran.z <= 0.1f) return;

            float x = ekran.x;
            float y = Screen.height - ekran.y;

            float w = 320f;
            float h = 50f;

            Color eski = GUI.color;

            // Koyu yüksek kontrastlı zemin
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
            GUI.DrawTexture(new Rect(x - w * 0.5f, y - h * 0.5f, w, h), texBeyaz);

            // Altın sarısı parlak çerçeve
            GUI.color = new Color(1.0f, 0.80f, 0.15f, 1f);
            GUI.DrawTexture(new Rect(x - w * 0.5f, y - h * 0.5f, w, 3), texBeyaz);
            GUI.DrawTexture(new Rect(x - w * 0.5f, y + h * 0.5f - 3, w, 3), texBeyaz);
            GUI.DrawTexture(new Rect(x - w * 0.5f, y - h * 0.5f, 3, h), texBeyaz);
            GUI.DrawTexture(new Rect(x + w * 0.5f - 3, y - h * 0.5f, 3, h), texBeyaz);

            if (altBaslikStili == null)
            {
                altBaslikStili = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            altBaslikStili.normal.textColor = Color.white;

            GUI.color = Color.white;
            GUI.Label(new Rect(x - w * 0.5f, y - h * 0.5f, w, h), "⚡ [E] ELEKTRİK ŞALTERİNİ AÇ", altBaslikStili);

            GUI.color = eski;
        }

        private void CizYuksekCozunurlukluAmongUsPaneli()
        {
            Color eski = GUI.color;

            // Karartılmış Sinematik Arka Plan
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), texBeyaz);

            // Ekran boyutuna göre duyarlı ölçekleme (Responsive Scaling)
            float olcek = Mathf.Clamp(Screen.height / 850f, 0.80f, 1.35f);

            float panelW = 820f * olcek;
            float panelH = 550f * olcek;
            float panelX = (Screen.width - panelW) * 0.5f;
            float panelY = (Screen.height - panelH) * 0.5f;

            // 1. Dış Metal Kutu Gövdesi (Among Us Endüstriyel Çelik)
            GUI.color = new Color(0.14f, 0.16f, 0.20f, 0.98f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, panelH), texBeyaz);

            // 2. Sarı/Siyah Endüstriyel Tehlike Kenarlıkları
            GUI.color = new Color(0.95f, 0.75f, 0.10f, 1f);
            GUI.DrawTexture(new Rect(panelX, panelY, panelW, 8 * olcek), texBeyaz);
            GUI.DrawTexture(new Rect(panelX, panelY + panelH - 8 * olcek, panelW, 8 * olcek), texBeyaz);
            GUI.DrawTexture(new Rect(panelX, panelY, 8 * olcek, panelH), texBeyaz);
            GUI.DrawTexture(new Rect(panelX + panelW - 8 * olcek, panelY, 8 * olcek, panelH), texBeyaz);

            // 3. Başlık Kartı
            float baslikH = 65f * olcek;
            GUI.color = new Color(0.09f, 0.10f, 0.13f, 1f);
            GUI.DrawTexture(new Rect(panelX + 12 * olcek, panelY + 12 * olcek, panelW - 24 * olcek, baslikH), texBeyaz);

            // Başlık Yazısı (Büyük, Kalın ve Çok Net)
            if (baslikStili == null)
            {
                baslikStili = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            baslikStili.fontSize = Mathf.RoundToInt(20 * olcek);
            baslikStili.normal.textColor = new Color(1.0f, 0.85f, 0.20f);

            GUI.color = Color.white;
            GUI.Label(new Rect(panelX + 20 * olcek, panelY + 14 * olcek, panelW - 140 * olcek, 32 * olcek), "⚡ ELEKTRİK KONTROL ÜNİTESİ // 1. KAPI SİGORTALARI", baslikStili);

            // Alt Açıklama
            if (durumStili == null)
            {
                durumStili = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter
                };
            }
            durumStili.fontSize = Mathf.RoundToInt(14 * olcek);
            durumStili.normal.textColor = new Color(0.85f, 0.90f, 0.95f);
            GUI.Label(new Rect(panelX + 20 * olcek, panelY + 44 * olcek, panelW - 140 * olcek, 26 * olcek), "Tüm sigortaları (Fare veya [1-5] tuşlarıyla) AÇIK konuma getirerek ana jeneratörü devreye sokun. [ESC: Çıkış]", durumStili);

            // Sağ Üst [X] ÇIKIŞ Butonu
            float cikisBtnW = 95 * olcek;
            float cikisBtnH = 42 * olcek;
            GUI.color = new Color(0.85f, 0.22f, 0.22f, 1f);
            if (GUI.Button(new Rect(panelX + panelW - cikisBtnW - 20 * olcek, panelY + 22 * olcek, cikisBtnW, cikisBtnH), "✕ ÇIKIŞ (ESC)"))
            {
                PaneliKapat();
                return;
            }

            // 4. 5 ADET BÜYÜK VE NET ŞALTER KARTI
            float alanY = panelY + 90 * olcek;
            float alanH = 320 * olcek;
            float paddingX = 25 * olcek;
            float toplamSalterAlani = panelW - (paddingX * 2);
            float salterW = toplamSalterAlani / 5f;

            bool hepsiAcik = true;

            for (int i = 0; i < 5; i++)
            {
                float sx = panelX + paddingX + (i * salterW);
                bool acik = salterler[i];
                if (!acik) hepsiAcik = false;

                // Şalter Yuva Zemin Kutusu
                GUI.color = new Color(0.07f, 0.08f, 0.10f, 0.96f);
                GUI.DrawTexture(new Rect(sx + 6 * olcek, alanY, salterW - 12 * olcek, alanH), texBeyaz);

                // Yuva Kenarlığı
                GUI.color = acik ? new Color(0.20f, 0.80f, 0.35f, 0.9f) : new Color(0.70f, 0.20f, 0.20f, 0.9f);
                GUI.DrawTexture(new Rect(sx + 6 * olcek, alanY, salterW - 12 * olcek, 3 * olcek), texBeyaz);
                GUI.DrawTexture(new Rect(sx + 6 * olcek, alanY + alanH - 3 * olcek, salterW - 12 * olcek, 3 * olcek), texBeyaz);
                GUI.DrawTexture(new Rect(sx + 6 * olcek, alanY, 3 * olcek, alanH), texBeyaz);
                GUI.DrawTexture(new Rect(sx + salterW - 9 * olcek, alanY, 3 * olcek, alanH), texBeyaz);

                // LED Gösterge Işığı (Büyük ve Parlak)
                float ledBoyut = 28 * olcek;
                float ledX = sx + (salterW - ledBoyut) * 0.5f;
                Color ledRenk = acik ? new Color(0.10f, 1.0f, 0.30f) : new Color(1.0f, 0.15f, 0.15f);
                GUI.color = ledRenk;
                GUI.DrawTexture(new Rect(ledX, alanY + 16 * olcek, ledBoyut, ledBoyut), texBeyaz);

                // LED Durum Yazısı (AÇIK / KAPALI)
                durumStili.fontSize = Mathf.RoundToInt(15 * olcek);
                durumStili.fontStyle = FontStyle.Bold;
                durumStili.normal.textColor = acik ? new Color(0.25f, 1.0f, 0.40f) : new Color(1.0f, 0.35f, 0.35f);
                GUI.color = Color.white;
                GUI.Label(new Rect(sx, alanY + 48 * olcek, salterW, 26 * olcek), acik ? "AÇIK (ON)" : "KAPALI (OFF)", durumStili);

                // Şalter Numarası
                durumStili.fontSize = Mathf.RoundToInt(13 * olcek);
                durumStili.fontStyle = FontStyle.Normal;
                durumStili.normal.textColor = Color.gray;
                GUI.Label(new Rect(sx, alanY + alanH - 35 * olcek, salterW, 24 * olcek), $"SİGORTA #{i + 1}", durumStili);

                // Şalter Kolu (Büyük Tıklanabilir Buton)
                float kolW = salterW - 28 * olcek;
                float kolH = 95 * olcek;
                float kolX = sx + 14 * olcek;
                // Açıkken yukarıda (y = alanY + 80), kapalıyken aşağıda (y = alanY + 175)
                float kolY = acik ? (alanY + 80 * olcek) : (alanY + 175 * olcek);

                Color btnRenk = acik ? new Color(0.18f, 0.65f, 0.30f, 1f) : new Color(0.55f, 0.18f, 0.18f, 1f);
                GUI.color = btnRenk;

                string butonYazi = acik ? "▲\n\nAÇIK" : "KAPALI\n\n▼";
                if (GUI.Button(new Rect(kolX, kolY, kolW, kolH), butonYazi))
                {
                    if (!bulmacaCozuldu)
                    {
                        salterler[i] = !salterler[i];
                        if (sesTik != null) audioSource.PlayOneShot(sesTik);
                    }
                }
            }

            // 5. ALT DURUM VE TAMAMLAMA ALANI
            float altH = 80 * olcek;
            float altY = panelY + panelH - altH - 18 * olcek;

            GUI.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(panelX + 16 * olcek, altY, panelW - 32 * olcek, altH), texBeyaz);

            if (!bulmacaCozuldu && hepsiAcik)
            {
                BulmacayiTamamla();
                return;
            }

            if (!bulmacaCozuldu)
            {
                // Büyük Şalter Butonu (Tıklandığı an şalter açılır ve menü anında kapanır)
                GUI.color = new Color(0.18f, 0.85f, 0.35f, 1f);
                string anaButonYazi = "⚡ ŞALTERİ AÇ & KAPIYI KALDIR (MENÜYÜ KAPAT)";
                if (GUI.Button(new Rect(panelX + 24 * olcek, altY + 10 * olcek, panelW - 48 * olcek, altH - 20 * olcek), anaButonYazi))
                {
                    BulmacayiTamamla();
                }
            }
            else
            {
                // Başarı Bildirimi
                GUI.color = Color.green;
                baslikStili.fontSize = Mathf.RoundToInt(20 * olcek);
                baslikStili.normal.textColor = new Color(0.20f, 1.0f, 0.40f);
                GUI.Label(new Rect(panelX + 20 * olcek, altY + 22 * olcek, panelW - 40 * olcek, 36 * olcek), "✓ GÜÇ VERİLDİ! GÜNEŞ DOĞUYOR VE 1. KAPI AÇILIYOR...", baslikStili);
            }

            GUI.color = eski;
        }

        private void BulmacayiTamamla()
        {
            if (bulmacaCozuldu) return;

            bulmacaCozuldu = true;
            cozulmeZamani = Time.time;

            if (sesBasari != null) audioSource.PlayOneShot(sesBasari);
            if (sesGucDalgasi != null) audioSource.PlayOneShot(sesGucDalgasi);

            // Konsol tepe ışığını yeşile çevir
            DurumIsiginiGuncelle(true);

            // Bağlı 1. kapıyı aç
            if (bagliKapi != null)
            {
                bagliKapi.KilidiAc();
                bagliKapi.KapiyiAc();
                Debug.Log("[KapiBulmacaPaneli] Among Us bulmacası tamamlandı! 1. Kapı açılıyor.");
            }

            // GÜNEŞİ VE GÜNDÜZ AYDINLATMASINI BAŞLAT!
            StartCoroutine(GunesiVeAydinligiGetir());

            // Bölüm Yöneticisi durumunu güncelle
            BolumYoneticisi by = BolumYoneticisi.Ornek;
            if (by != null)
            {
                by.FizikselEngeliAs();
            }

            // KULLANICI İSTEĞİ: Şalter açıldığı an menü ekranı önümüzden hemen gitsin!
            PaneliKapat();
        }

        /// <summary>
        /// Şalter indirildiğinde haritayı karanlıktan parlak güneşli gündüze yumuşakça geçiren sinematik aydınlatma.
        /// </summary>
        private IEnumerator GunesiVeAydinligiGetir()
        {
            float gecisSuresi = 3.5f;
            float gecenSure = 0f;

            float baslangicGunesSiddeti = anaGunesIsigi != null ? anaGunesIsigi.intensity : 0.06f;
            Color baslangicGunesRengi = anaGunesIsigi != null ? anaGunesIsigi.color : new Color(0.20f, 0.28f, 0.45f);

            float hedefGunesSiddeti = 2.2f;
            Color hedefGunesRengi = new Color(1.0f, 0.96f, 0.88f); // Sıcak gün ışığı

            float baslangicAmbientSiddeti = RenderSettings.ambientIntensity;
            Color baslangicAmbientRengi = RenderSettings.ambientLight;

            float hedefAmbientSiddeti = 1.0f;
            Color hedefAmbientRengi = new Color(0.35f, 0.38f, 0.45f);

            while (gecenSure < gecisSuresi)
            {
                gecenSure += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, gecenSure / gecisSuresi);

                if (anaGunesIsigi != null)
                {
                    anaGunesIsigi.intensity = Mathf.Lerp(baslangicGunesSiddeti, hedefGunesSiddeti, t);
                    anaGunesIsigi.color = Color.Lerp(baslangicGunesRengi, hedefGunesRengi, t);
                }

                RenderSettings.ambientIntensity = Mathf.Lerp(baslangicAmbientSiddeti, hedefAmbientSiddeti, t);
                RenderSettings.ambientLight = Color.Lerp(baslangicAmbientRengi, hedefAmbientRengi, t);

                yield return null;
            }

            // Son değerleri tam güneşe kilitle
            if (anaGunesIsigi != null)
            {
                anaGunesIsigi.intensity = hedefGunesSiddeti;
                anaGunesIsigi.color = hedefGunesRengi;
            }
            RenderSettings.ambientIntensity = hedefAmbientSiddeti;
            RenderSettings.ambientLight = hedefAmbientRengi;

            Debug.Log("[KapiBulmacaPaneli] Elektrik devreye girdi! Güneş ışığı ve aydınlatma tam kapasite aktif.");
        }

        private IEnumerator KapanisGecikmesi()
        {
            yield return new WaitForSeconds(1.8f);
            PaneliKapat();
        }

        private void DurumIsiginiGuncelle(bool aktif)
        {
            Color renk = aktif ? Color.green : Color.red;

            if (durumIsigi != null)
            {
                durumIsigi.color = renk;
            }

            if (durumLambaMesh != null && durumLambaMesh.material != null)
            {
                durumLambaMesh.material.color = renk;
                if (durumLambaMesh.material.HasProperty("_EmissionColor"))
                {
                    durumLambaMesh.material.SetColor("_EmissionColor", renk * 2.0f);
                }
            }
        }

        private void KonsolGorselleriniHazirla()
        {
            if (transform.Find("KonsolGovde") != null) return;

            // 1. Ayak Kaidesi
            GameObject ayak = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ayak.name = "KonsolGovde";
            ayak.transform.SetParent(transform);
            ayak.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            ayak.transform.localScale = new Vector3(0.55f, 1.0f, 0.45f);

            Material matDuvar = Resources.Load<Material>("Mat_Duvar");
            if (matDuvar == null)
            {
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                m.color = new Color(0.22f, 0.25f, 0.28f);
                matDuvar = m;
            }
            ayak.GetComponent<MeshRenderer>().sharedMaterial = matDuvar;

            // 2. Eğik Panel Ünitesi (Among Us Ekranı)
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "KonsolEkran";
            panel.transform.SetParent(transform);
            panel.transform.localPosition = new Vector3(0f, 1.05f, 0.05f);
            panel.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            panel.transform.localScale = new Vector3(0.6f, 0.35f, 0.25f);

            Material matEkran = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            matEkran.color = new Color(0.12f, 0.15f, 0.18f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = matEkran;

            // 3. Tepe Durum Lambası (Kırmızı/Yeşil)
            GameObject lamba = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamba.name = "DurumLambasi";
            lamba.transform.SetParent(transform);
            lamba.transform.localPosition = new Vector3(0f, 1.25f, -0.05f);
            lamba.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);

            Material matLamba = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            matLamba.color = Color.red;
            lamba.GetComponent<MeshRenderer>().sharedMaterial = matLamba;
            durumLambaMesh = lamba.GetComponent<MeshRenderer>();

            // Tepe Işık Kaynağı (Karanlıkta konsolu belli eden nokta ışık)
            GameObject isikObj = new GameObject("TepeIsigi");
            isikObj.transform.SetParent(lamba.transform);
            isikObj.transform.localPosition = Vector3.zero;
            Light lt = isikObj.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.range = 5.0f;
            lt.intensity = 2.5f;
            lt.color = Color.red;
            durumIsigi = lt;

            // Tetikleyici Collider
            SphereCollider col = GetComponent<SphereCollider>();
            if (col == null) col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = etkilesimMenzili;
        }

        private void DokulariOlustur()
        {
            if (texBeyaz == null)
            {
                texBeyaz = new Texture2D(1, 1);
                texBeyaz.SetPixel(0, 0, Color.white);
                texBeyaz.Apply();
            }
            if (texSiyah == null)
            {
                texSiyah = new Texture2D(1, 1);
                texSiyah.SetPixel(0, 0, Color.black);
                texSiyah.Apply();
            }
        }

        private AudioClip SesUret(float frekans, float sure, float sesSeviyesi)
        {
            int ornekleme = 44100;
            int toplamOrnek = (int)(ornekleme * sure);
            float[] data = new float[toplamOrnek];

            for (int i = 0; i < toplamOrnek; i++)
            {
                float t = (float)i / ornekleme;
                float dalga = Mathf.Sin(2 * Mathf.PI * frekans * t);
                float zarf = 1f - ((float)i / toplamOrnek);
                data[i] = dalga * zarf * sesSeviyesi;
            }

            AudioClip clip = AudioClip.Create("SesProsedurel", toplamOrnek, 1, ornekleme, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip SesUretIkiTonlu(float f1, float f2, float sure, float sesSeviyesi = 0.35f)
        {
            int ornekleme = 44100;
            int toplamOrnek = (int)(ornekleme * sure);
            int yariOrnek = toplamOrnek / 2;
            float[] data = new float[toplamOrnek];

            for (int i = 0; i < toplamOrnek; i++)
            {
                float t = (float)i / ornekleme;
                float frekans = (i < yariOrnek) ? f1 : f2;
                float dalga = Mathf.Sin(2 * Mathf.PI * frekans * t);
                float zarf = 1f - ((float)(i % yariOrnek) / yariOrnek);
                data[i] = dalga * zarf * sesSeviyesi;
            }

            AudioClip clip = AudioClip.Create("SesBasari", toplamOrnek, 1, ornekleme, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip SesUretKalin(float baslangicFrekans, float bitisFrekans, float sure, float sesSeviyesi = 0.4f)
        {
            int ornekleme = 44100;
            int toplamOrnek = (int)(ornekleme * sure);
            float[] data = new float[toplamOrnek];

            for (int i = 0; i < toplamOrnek; i++)
            {
                float oran = (float)i / toplamOrnek;
                float frekans = Mathf.Lerp(baslangicFrekans, bitisFrekans, oran);
                float t = (float)i / ornekleme;
                float dalga = Mathf.Sin(2 * Mathf.PI * frekans * t);
                float zarf = Mathf.Sin(Mathf.PI * oran);
                data[i] = dalga * zarf * sesSeviyesi;
            }

            AudioClip clip = AudioClip.Create("SesGucDalgasi", toplamOrnek, 1, ornekleme, false);
            clip.SetData(data, 0);
            return clip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OtomatikKonsolOlusturucu()
        {
            if (Object.FindAnyObjectByType<KapiBulmacaPaneli>() != null) return;

            // Sahnede 1. Kapıyı (FizikselEngelKapisi) bul
            GameObject kapiObj = GameObject.Find("FizikselEngelKapisi");
            if (kapiObj != null)
            {
                GameObject konsol = new GameObject("ElektrikSalterKonsolu");
                // 1. Kapının hemen sağ önünde oyuncunun görebileceği konum (Z = 13.8, X = 3.6)
                konsol.transform.position = new Vector3(-3.2f, 0f, -1.8f);
                konsol.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

                KapiBulmacaPaneli panel = konsol.AddComponent<KapiBulmacaPaneli>();
                panel.bagliKapi = kapiObj.GetComponent<KapiKontrol>();
                Debug.Log("[KapiBulmacaPaneli] Among Us Elektrik Şalter Konsolu kapının yanına otomatik kuruldu.");
            }
        }
    }
}
