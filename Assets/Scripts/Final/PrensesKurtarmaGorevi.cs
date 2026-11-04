using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Yazlab.Yonetim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// 4. BÖLÜM (FİNAL) GÖREV YÖNETİCİSİ — ZÜMRÜTLERİ SIRASIYLA VURMA & PRENSES BETÜL'Ü KURTARMA:
    ///
    /// SENARYO AKIŞI:
    /// 1. Komutanı Geçiş & Kuleye Giriş: Çıkış kapısı açıldığında ekranın altında
    ///    "Sırasıyla KIRMIZI → MAVİ → YEŞİL zümrütü vur!" talimatı gösterilir.
    /// 2. Zümrütlere Sırasıyla Vurma Görevi: Oyuncu kendi silahıyla (Sol Tık / F)
    ///    sırasıyla 1. KIRMIZI, 2. MAVİ ve 3. YEŞİL zümrütü vurmalıdır.
    ///    Yanlış sırada vurursa zümrütler sıfırlanır ve tekrar denemesi gerektiği söylenir.
    /// 3. Kapsülden Kurtuluş: 3 zümrüt doğru sırayla vurulduğunda kapsül açılır.
    /// 4. Yanımıza Gelip Teşekkür Etmesi: Prenses Betül kapsülden çıkar, oyuncunun yanına yürür,
    ///    oyuncuya dönüp teşekkür konuşmasını yapar.
    /// 5. Kapanış Ekranı ("GÖREV BAŞARILI"): Teşekkürün ardından "GÖREV BAŞARILI" kapanış ekranı açılır.
    /// </summary>
    public class PrensesKurtarmaGorevi : MonoBehaviour
    {
        private static PrensesKurtarmaGorevi _ornek;
        public static PrensesKurtarmaGorevi Ornek
        {
            get
            {
                if (_ornek == null) _ornek = UnityEngine.Object.FindAnyObjectByType<PrensesKurtarmaGorevi>();
                return _ornek;
            }
            set => _ornek = value;
        }

        public static bool OyunBittiMi => Ornek != null && Ornek.mevcutAsama == Asama.Zafer;

        public enum Asama
        {
            KuleyeGirisBekleniyor,
            ZumrutleriVurmaGorevi,
            KapsulAciliyor,
            PrensesYaninaGeliyor,
            PrensesTesekkurEdiyor,
            Zafer
        }

        [Header("Görev Durumu")]
        public Asama mevcutAsama = Asama.KuleyeGirisBekleniyor;

        [Header("Kule Nesneleri")]
        public Transform[] kafesCubuklari;
        public GameObject muhurBariyeri;
        public Transform kehanetKuresi;
        public Light kureIsigi;
        public RunKristali[] kristaller; // 0: Kırmızı, 1: Mavi, 2: Yeşil
        public Transform kacisKapagi;
        public GameObject kapakDeligi;
        public Transform zincirTekerlegi;
        public PrensesKarakteri prenses;
        public Light[] alarmIsiklari;

        // Doğru Vuruş Sırası: 0 (Kırmızı) -> 1 (Mavi) -> 2 (Yeşil)
        private readonly int[] _hedefSira = new int[] { 0, 1, 2 };
        private int _vurulanAdim = 0;
        private int _vurulanZumrutSayisi = 0;
        private bool _ilkMesajGosterildi = false;

        public int VurulanZumrutSayisi => _vurulanZumrutSayisi;
        public int ToplamZumrutSayisi => kristaller != null ? kristaller.Length : 3;

        // Diyalog & UI
        private string _altYazi = "";
        private float _altYaziBitis = 0f;
        private string _konusan = "PRENSES BETÜL";
        private Color _konusanRenk = new Color(0.85f, 0.75f, 1.0f);
        private Texture2D _texKoyu;
        private AudioSource _ses;

        private Transform _oyuncu;

        private void Awake()
        {
            Ornek = this;
            mevcutAsama = Asama.KuleyeGirisBekleniyor;
            _ses = gameObject.AddComponent<AudioSource>();
            _ses.playOnAwake = false;
            _ses.spatialBlend = 0f;
        }

        private void OnDestroy()
        {
            if (Ornek == this)
            {
                Ornek = null;
            }
        }

        private void Start()
        {
            OyuncuyuBul();
        }

        private void OyuncuyuBul()
        {
            if (_oyuncu != null) return;
            KarakterHareket kh = Object.FindAnyObjectByType<KarakterHareket>();
            if (kh != null) _oyuncu = kh.transform;
            else
            {
                GameObject p = GameObject.FindWithTag("Player");
                if (p != null) _oyuncu = p.transform;
            }
        }

        private void Update()
        {
            OyuncuyuBul();

            switch (mevcutAsama)
            {
                case Asama.KuleyeGirisBekleniyor:
                    bool kapiAcildi = BolumYoneticisi.Ornek != null && BolumYoneticisi.Ornek.MevcutDurum == OyunDurumu.PrensesKurtarma;
                    if (_oyuncu != null && (_oyuncu.position.z >= 42.5f || kapiAcildi))
                    {
                        GoreviBaslat();
                    }
                    break;

                case Asama.ZumrutleriVurmaGorevi:
                    if (prenses != null && _oyuncu != null)
                    {
                        prenses.BakisYonu(_oyuncu.position);
                    }
                    if (!_ilkMesajGosterildi && _oyuncu != null && _oyuncu.position.z >= 44.0f)
                    {
                        _ilkMesajGosterildi = true;
                        DiyalogGoster("PRENSES BETÜL",
                            "Sonunda geldin! Kapsülü açmak için silahınla sırasıyla 1. KIRMIZI, 2. MAVİ ve 3. YEŞİL zümrütü vur!",
                            8.0f, new Color(1.0f, 0.88f, 0.30f));
                        SesCal(540f, 0.25f);
                    }
                    break;

                case Asama.PrensesYaninaGeliyor:
                    if (prenses != null && _oyuncu != null)
                    {
                        Vector3 hedefKonum = OyuncuYaniHedefKonumu();
                        prenses.HedefeGit(hedefKonum);
                    }
                    break;

                case Asama.PrensesTesekkurEdiyor:
                    if (prenses != null && _oyuncu != null)
                    {
                        prenses.BakisYonu(_oyuncu.position);
                    }
                    break;

                case Asama.Zafer:
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    break;
            }
        }

        private Vector3 OyuncuYaniHedefKonumu()
        {
            if (_oyuncu == null) return new Vector3(0f, PrensesKulesiInsaci.ZeminY, 49.0f);

            Vector3 oyuncuPos = _oyuncu.position;
            Vector3 prensesPos = prenses != null ? prenses.transform.position : new Vector3(0f, PrensesKulesiInsaci.ZeminY, 53.4f);

            Vector3 yon = prensesPos - oyuncuPos;
            yon.y = 0f;
            if (yon.sqrMagnitude < 0.01f)
            {
                yon = _oyuncu.forward;
                yon.y = 0f;
            }
            yon.Normalize();

            Vector3 hedef = oyuncuPos + yon * 1.75f;
            hedef.y = PrensesKulesiInsaci.ZeminY;
            return hedef;
        }

        public void GoreviBaslat()
        {
            if (mevcutAsama != Asama.KuleyeGirisBekleniyor) return;

            mevcutAsama = Asama.ZumrutleriVurmaGorevi;

            BolumYoneticisi by = BolumYoneticisi.Ornek;
            if (by != null && by.MevcutDurum != OyunDurumu.PrensesKurtarma && by.MevcutDurum != OyunDurumu.BolumTamamlandi)
            {
                by.DurumuDegistir(OyunDurumu.PrensesKurtarma);
            }

            DiyalogGoster("GÖREV — ZÜMRÜTLERİ SIRASIYLA VUR",
                "Prenses Betül'ü kurtarmak için silahınla sırasıyla 1. KIRMIZI  →  2. MAVİ  →  3. YEŞİL zümrütü vur!",
                9.0f, new Color(1.0f, 0.88f, 0.30f));
        }

        // ==========================================================
        // SİLAHLA ZÜMRÜT VURULDUĞUNDA SIRA KONTROLÜ (KIRMIZI -> MAVİ -> YEŞİL)
        // ==========================================================
        public void KristalVuruldu(RunKristali kristal)
        {
            if (kristal == null || kristal.VurulduMu) return;

            if (mevcutAsama == Asama.KapsulAciliyor ||
                mevcutAsama == Asama.PrensesYaninaGeliyor ||
                mevcutAsama == Asama.PrensesTesekkurEdiyor ||
                mevcutAsama == Asama.Zafer)
            {
                return;
            }

            if (mevcutAsama == Asama.KuleyeGirisBekleniyor)
            {
                mevcutAsama = Asama.ZumrutleriVurmaGorevi;
                if (BolumYoneticisi.Ornek != null)
                {
                    BolumYoneticisi.Ornek.DurumuDegistir(OyunDurumu.PrensesKurtarma);
                }
            }

            int beklenenIndeks = _hedefSira[_vurulanAdim];

            if (kristal.indeks == beklenenIndeks)
            {
                // DOĞRU SIRA!
                kristal.DogruVurusKapat();
                _vurulanAdim++;
                _vurulanZumrutSayisi = _vurulanAdim;
                SesCal(580f + _vurulanAdim * 160f, 0.30f);

                if (kureIsigi != null)
                {
                    kureIsigi.color = RunKristali.RenkAl(kristal.indeks);
                    kureIsigi.intensity = 4.0f;
                }

                if (_vurulanAdim >= _hedefSira.Length)
                {
                    // Kırmızı -> Mavi -> Yeşil sırasıyla tamamlandı!
                    StartCoroutine(KurtulusVeTesekkurAkisi());
                }
                else
                {
                    string vurulanAd = RunKristali.Adlar[kristal.indeks];
                    string siradakiAd = RunKristali.Adlar[_hedefSira[_vurulanAdim]];
                    DiyalogGoster("DOĞRU SIRA!",
                        $"[{_vurulanAdim}/3] {vurulanAd} ZÜMRÜT VURULDU! Sırada: {_vurulanAdim + 1}. {siradakiAd} ZÜMRÜT",
                        6.0f, new Color(0.35f, 1.0f, 0.55f));
                }
            }
            else
            {
                // YANLIŞ SIRA! Zümrütleri sıfırla ve oyuncuya tekrar denemesi gerektiğini söyle
                _vurulanAdim = 0;
                _vurulanZumrutSayisi = 0;

                if (kristaller != null)
                {
                    foreach (var k in kristaller)
                    {
                        if (k != null) k.Sifirla();
                    }
                }

                if (kureIsigi != null)
                {
                    kureIsigi.color = new Color(1.0f, 0.25f, 0.25f);
                    kureIsigi.intensity = 3.5f;
                }

                SesCal(210f, 0.48f);
                string yanlisAd = RunKristali.Adlar[kristal.indeks];
                DiyalogGoster("YANLIŞ SIRA - TEKRAR DENE!",
                    $"Yanlışlıkla {yanlisAd} zümrütü vurdun! Tekrar denemelisin! Sırasıyla: 1. KIRMIZI  →  2. MAVİ  →  3. YEŞİL zümrütü vur!",
                    7.5f, new Color(1.0f, 0.30f, 0.30f));
            }
        }

        // ==========================================================
        // KAPSÜLDEN KURTULUŞ -> YANIMIZA GELİŞ -> TEŞEKKÜR -> GÖREV BAŞARILI
        // ==========================================================
        private IEnumerator KurtulusVeTesekkurAkisi()
        {
            mevcutAsama = Asama.KapsulAciliyor;
            SesCal(880f, 0.55f);

            DiyalogGoster("PRENSES BETÜL",
                "Harika atış! Kırmızı, Mavi ve Yeşil zümrütleri doğru sırayla vurdun, kapsülün mührü kırıldı!",
                3.5f, new Color(0.45f, 1.0f, 0.65f));

            // 1. Kapsül enerji bariyeri sönsün
            if (muhurBariyeri != null)
            {
                muhurBariyeri.SetActive(false);
            }
            if (kureIsigi != null)
            {
                kureIsigi.color = new Color(0.3f, 1.0f, 0.55f);
                kureIsigi.intensity = 4.5f;
            }

            // 2. Kapsül parmaklıklarının collider'larını hemen kapat ve yukarı kaydırarak aç
            if (kafesCubuklari != null)
            {
                foreach (var c in kafesCubuklari)
                {
                    if (c == null) continue;
                    Collider col = c.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                }
            }

            float acilmaSuresi = 1.2f;
            float gecen = 0f;
            while (gecen < acilmaSuresi)
            {
                gecen += Time.deltaTime;
                float adim = (4.8f / acilmaSuresi) * Time.deltaTime;
                if (kafesCubuklari != null)
                {
                    foreach (var c in kafesCubuklari)
                    {
                        if (c != null) c.position += Vector3.up * adim;
                    }
                }
                yield return null;
            }

            if (kafesCubuklari != null)
            {
                foreach (var c in kafesCubuklari)
                {
                    if (c != null) c.gameObject.SetActive(false);
                }
            }

            // 3. Prenses Betül kapsülden çıkıp oyuncunun yanına gelsin
            mevcutAsama = Asama.PrensesYaninaGeliyor;
            DiyalogGoster("PRENSES BETÜL",
                "Kapsülden kurtuldum! Yanına geliyorum...",
                3.5f, new Color(0.92f, 0.82f, 1.0f));

            float maksYurumeSuresi = 5.0f;
            float yurumeGecen = 0f;
            while (yurumeGecen < maksYurumeSuresi)
            {
                yurumeGecen += Time.deltaTime;
                if (prenses == null || _oyuncu == null) break;

                Vector3 yatayFark = _oyuncu.position - prenses.transform.position;
                yatayFark.y = 0f;
                if (yatayFark.magnitude <= 2.15f)
                {
                    prenses.Dur();
                    break;
                }
                yield return null;
            }

            // Eğer oyuncu çok uzaktaysa Prenses tam yanına yetişsin
            if (prenses != null && _oyuncu != null)
            {
                Vector3 yatayFark = _oyuncu.position - prenses.transform.position;
                yatayFark.y = 0f;
                if (yatayFark.magnitude > 2.6f)
                {
                    prenses.Isinla(OyuncuYaniHedefKonumu());
                }
                prenses.Dur();
            }

            // 4. Prenses Betül yanımıza ulaştı -> Bize dönüp teşekkür etsin
            mevcutAsama = Asama.PrensesTesekkurEdiyor;
            if (prenses != null)
            {
                prenses.TesekkurEt(true);
            }

            SesCal(740f, 0.35f);
            DiyalogGoster("PRENSES BETÜL",
                "Beni o kapsülden kurtardığın için çok teşekkür ederim! Zümrütleri doğru sırayla kırıp hayatımı kurtardın, sana minnettarım!",
                4.2f, new Color(0.45f, 1.0f, 0.65f));

            yield return new WaitForSeconds(4.0f);

            // 5. Kapanış Ekranı: "GÖREV BAŞARILI"
            ZaferiTetikle();
        }

        // ==========================================================
        // FİNAL KAPANIŞ EKRANI ("GÖREV BAŞARILI")
        // ==========================================================
        private void ZaferiTetikle()
        {
            mevcutAsama = Asama.Zafer;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            BolumYoneticisi by = BolumYoneticisi.Ornek;
            if (by != null)
            {
                by.DurumuDegistir(OyunDurumu.BolumTamamlandi);
            }

            SesCal(880f, 0.7f);
            Debug.Log("[PrensesKurtarma] GÖREV BAŞARILI! Zümrütler sırasıyla kırıldı, Prenses Betül kapsülden kurtarıldı.");
        }

        // ==========================================================
        // UI & SES YARDIMCILARI
        // ==========================================================
        public void DiyalogGoster(string konusan, string metin, float sure, Color renk)
        {
            _konusan = konusan;
            _altYazi = metin;
            _altYaziBitis = Time.time + sure;
            _konusanRenk = renk;
        }

        private void SesCal(float frekans, float sure)
        {
            if (_ses == null) return;
            int sampleRate = 44100;
            int samples = Mathf.RoundToInt(sampleRate * sure);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / sampleRate;
                float env = 1f - (float)i / samples;
                data[i] = Mathf.Sin(2f * Mathf.PI * frekans * t) * env * 0.28f;
            }
            AudioClip clip = AudioClip.Create("ZumrutSes", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            _ses.PlayOneShot(clip);
        }

        private void OnGUI()
        {
            if (KapiBulmacaPaneli.MinigameAcik || OllamaNpcDiyalogUI.DiyalogAcikMi) return;

            float olcek = GoruntuKalitesiYoneticisi.KeskinOlcek;

            if (_texKoyu == null)
            {
                _texKoyu = new Texture2D(1, 1);
                _texKoyu.SetPixel(0, 0, new Color(0.04f, 0.07f, 0.11f, 0.94f));
                _texKoyu.Apply();
            }

            // 1. FİNAL KAPANIŞ EKRANI ("GÖREV BAŞARILI")
            if (mevcutAsama == Asama.Zafer)
            {
                CizGorevBasariliEkrani(olcek);
                return;
            }

            // 2. VURULMAMIŞ ZÜMRÜT KRİSTALLERİN ÜZERİNDE RENKLİ HEDEF İŞARETÇİSİ
            if (mevcutAsama == Asama.ZumrutleriVurmaGorevi && kristaller != null && Camera.main != null)
            {
                GUIStyle hedefStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                GoruntuKalitesiYoneticisi.StiliKeskinlestir(hedefStil, Mathf.RoundToInt(12 * olcek));

                for (int i = 0; i < kristaller.Length; i++)
                {
                    RunKristali k = kristaller[i];
                    if (k == null || k.VurulduMu) continue;

                    Vector3 ustNokta = k.transform.position + Vector3.up * 0.65f;
                    if (GoruntuKalitesiYoneticisi.DunyaNoktasiniEkranaCevir(Camera.main, ustNokta, out Vector2 scr))
                    {
                        float bw = 165f * olcek;
                        float bh = 26f * olcek;
                        Rect r = new Rect(scr.x - bw * 0.5f, scr.y - bh * 0.5f, bw, bh);
                        GUI.DrawTexture(r, _texKoyu);
                        hedefStil.normal.textColor = RunKristali.RenkAl(k.indeks);
                        GoruntuKalitesiYoneticisi.KeskinEtiketCiz(r, $"{RunKristali.Adlar[k.indeks]} ZÜMRÜT", hedefStil);
                    }
                }
            }

            // 3. PRENSES BETÜL TEŞEKKÜR EDERKEN BAŞININ ÜZERİNDE KONUŞMA BALONU
            if (mevcutAsama == Asama.PrensesTesekkurEdiyor && prenses != null && Camera.main != null)
            {
                Vector3 tepe = prenses.transform.position + Vector3.up * 2.15f;
                if (GoruntuKalitesiYoneticisi.DunyaNoktasiniEkranaCevir(Camera.main, tepe, out Vector2 scr))
                {
                    float bw = 340f * olcek;
                    float bh = 44f * olcek;
                    Rect r = new Rect(
                        Mathf.Clamp(scr.x - bw * 0.5f, 12f, Screen.width - bw - 12f),
                        Mathf.Clamp(scr.y - bh * 0.5f, 12f, Screen.height - bh - 12f),
                        bw, bh);
                    GUI.DrawTexture(r, _texKoyu);

                    GUIStyle balonStil = new GUIStyle(GUI.skin.label)
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        wordWrap = true
                    };
                    GoruntuKalitesiYoneticisi.StiliKeskinlestir(balonStil, Mathf.RoundToInt(13 * olcek));
                    balonStil.normal.textColor = new Color(0.45f, 1.0f, 0.65f);
                    GoruntuKalitesiYoneticisi.KeskinEtiketCiz(r, "Prenses Betül: \"Beni kurtardığın için çok teşekkür ederim!\"", balonStil);
                }
            }

            // 4. ALT ORTA DİYALOG / SIRA TALİMATI KUTUSU
            // Komutanı geçtikten sonra zümrüt vurma aşaması boyunca altta her zaman sıra talimatı veya son geri bildirim görünür
            string gosterilecekBaslik = _konusan;
            string gosterilecekMetin = _altYazi;
            Color gosterilecekRenk = _konusanRenk;
            bool kutuGoster = Time.time < _altYaziBitis && !string.IsNullOrEmpty(_altYazi);

            if (!kutuGoster && mevcutAsama == Asama.ZumrutleriVurmaGorevi)
            {
                kutuGoster = true;
                string siradaki = RunKristali.Adlar[_hedefSira[Mathf.Clamp(_vurulanAdim, 0, _hedefSira.Length - 1)]];
                gosterilecekBaslik = "GÖREV — ZÜMRÜTLERİ SIRASIYLA VUR";
                gosterilecekMetin = $"Sırasıyla KIRMIZI → MAVİ → YEŞİL zümrütü silahınla vur!  (Şu an vurulacak: {_vurulanAdim + 1}. {siradaki})";
                gosterilecekRenk = new Color(1.0f, 0.88f, 0.30f);
            }

            if (kutuGoster)
            {
                GUIStyle baslik = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                GoruntuKalitesiYoneticisi.StiliKeskinlestir(baslik, Mathf.RoundToInt(13 * olcek));
                baslik.normal.textColor = gosterilecekRenk;

                GUIStyle metin = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter
                };
                GoruntuKalitesiYoneticisi.StiliKeskinlestir(metin, Mathf.RoundToInt(14 * olcek));
                metin.normal.textColor = Color.white;

                float w = Mathf.Min(Screen.width - 40f, 740f * olcek);
                float icW = w - 24f * olcek;
                float bH = 20f * olcek;
                float mH = Mathf.Max(36f * olcek, metin.CalcHeight(new GUIContent(gosterilecekMetin), icW) + 6f);
                float h = bH + mH + 18f * olcek;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height - h - 56f * olcek;

                GUI.DrawTexture(new Rect(x, y, w, h), _texKoyu);
                GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(x + 12f * olcek, y + 6f * olcek, icW, bH), $"◆ {gosterilecekBaslik} ◆", baslik);
                GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(x + 12f * olcek, y + 6f * olcek + bH, icW, mH), gosterilecekMetin, metin);
            }
        }

        private void CizGorevBasariliEkrani(float olcek)
        {
            // Karartma arka planı
            GUI.color = new Color(0f, 0.02f, 0.05f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelW = Mathf.Min(Screen.width - 60f, 740f * olcek);
            float panelH = Mathf.Min(Screen.height - 60f, 500f * olcek);
            float px = (Screen.width - panelW) * 0.5f;
            float py = (Screen.height - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(px, py, panelW, panelH), _texKoyu);

            // Zümrüt yeşili & altın üst-alt çerçeve çizgisi
            Color zumrut = new Color(0.25f, 0.98f, 0.55f);
            Color altin = new Color(1.0f, 0.84f, 0.28f);
            GUI.color = zumrut;
            GUI.DrawTexture(new Rect(px, py, panelW, 4f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px, py + panelH - 4f, panelW, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle anaBaslikStil = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(anaBaslikStil, Mathf.RoundToInt(28 * olcek));
            anaBaslikStil.normal.textColor = zumrut;

            GUIStyle altBaslik = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(altBaslik, Mathf.RoundToInt(14 * olcek));
            altBaslik.normal.textColor = altin;

            GUIStyle ozetStil = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(ozetStil, Mathf.RoundToInt(13 * olcek));
            ozetStil.normal.textColor = new Color(0.92f, 0.95f, 0.98f);

            float pad = 28f * olcek;
            float icW = panelW - pad * 2f;
            float cy = py + 24f * olcek;

            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(px + pad, cy, icW, 38f * olcek),
                "GÖREV BAŞARILI", anaBaslikStil);
            cy += 42f * olcek;

            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(px + pad, cy, icW, 24f * olcek),
                "PRENSES BETÜL KAPSÜLDEN KURTARILDI & OPERASYON TAMAMLANDI", altBaslik);
            cy += 34f * olcek;

            string epilog =
                "Prenses Betül (\"Beyaz Kuzgun\"): \"Beni o kapsülden kurtardığın için çok teşekkür ederim! Silahınla Kırmızı, Mavi ve Yeşil zümrütleri doğru sırayla kırıp bana özgürlüğümü geri verdin.\"\n\n" +
                "TAMAMLANAN GÖREV ADIMLARI:\n" +
                "  [OK] 1. BÖLÜM: Hendek Kanyonu & Eşzamanlı 2 Anahtarlı Askı Kapı ([G] + [E])\n" +
                "  [OK] 2. BÖLÜM: Kablo Odası Elektrik Şalteri, Taret & Baskın Askerleri, [G] Çift Kollu İç Kule Kapısı\n" +
                "  [OK] 3. BÖLÜM: İç Kule Muhafızı (Ollama Yerel LLM) Müzakeresi & Çıkış Kapısı\n" +
                "  [OK] 4. BÖLÜM (FİNAL): Kırmızı -> Mavi -> Yeşil Zümrütlerin Vurulması & Prenses Betül'ün Kurtarılması\n\n" +
                "Tebrikler! Tüm engelleri aşarak oyunu başarıyla tamamladınız.";

            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(px + pad, cy, icW, 230f * olcek), epilog, ozetStil);
            cy += 245f * olcek;

            // Butonlar: Yeniden Oyna & Oyundan Çık
            GUIStyle butonStil = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(butonStil, Mathf.RoundToInt(14 * olcek));

            float btnW = 190f * olcek;
            float btnH = 44f * olcek;
            float bx1 = px + panelW * 0.5f - btnW - 14f * olcek;
            float bx2 = px + panelW * 0.5f + 14f * olcek;

            if (GUI.Button(new Rect(bx1, cy, btnW, btnH), "YENİDEN OYNA", butonStil))
            {
                mevcutAsama = Asama.KuleyeGirisBekleniyor;
                Ornek = null;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            if (GUI.Button(new Rect(bx2, cy, btnW, btnH), "OYUNDAN ÇIK", butonStil))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
