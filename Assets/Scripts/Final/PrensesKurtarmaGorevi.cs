using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// 4. BÖLÜM (FİNAL) GÖREV YÖNETİCİSİ — PRENSES AYLIN'I KURTARMA:
    ///
    /// SENARYO AKIŞI:
    /// 1. Kuleye Giriş: Kapı açıldıktan sonra oyuncu Kızıl Kule'ye adım atar (Z > 45).
    /// 2. İlk Diyalog: Prenses Aylin ("Beyaz Kuzgun") oyuncuyu karşılar; Komutan Varg'ın onu
    ///    kadim bir Rün Mühürüne hapsettiğini söyler.
    /// 3. Rün Bulmacası: Kehanet Küresi sırayla 3 renk (Kızıl / Mavi / Zümrüt) yanıp söner.
    ///    Oyuncu silahıyla bu 3 kristali doğru sırada vurmalıdır.
    /// 4. Mühür Kırılışı: Rün perdesi söner, kafes parmaklıkları tavana çekilir.
    /// 5. Kale Baskını & Savunma: Mühür kırılınca kalede kırmızı alarm çalar. Prenses hendek su tüneline
    ///    açılan kaçış kapağının zincir tekerleğini çevirirken kule kapısından muhafız askerler baskın yapar.
    ///    Oyuncu ve Yoldaş askerleri püskürtür.
    /// 6. Tünelden Kaçış & Zafer: Kapak açılır; oyuncu tünele atlayarak Prenses ile hendek şelalesine
    ///    ulaşır ve operasyonu başarıyla tamamlar.
    /// </summary>
    public class PrensesKurtarmaGorevi : MonoBehaviour
    {
        public static PrensesKurtarmaGorevi Ornek { get; private set; }

        public enum Asama
        {
            KuleyeGirisBekleniyor,
            PrensesleIlkDiyalog,
            RunBulmacasiGosterim,
            RunBulmacasiAtis,
            MuhurKirildi,
            BaskinSavunmasi,
            KacisaHazir,
            Zafer
        }

        [Header("Görev Durumu")]
        public Asama mevcutAsama = Asama.KuleyeGirisBekleniyor;

        [Header("Kule Nesneleri")]
        public Transform[] kafesCubuklari;
        public GameObject muhurBariyeri;
        public Transform kehanetKuresi;
        public Light kureIsigi;
        public RunKristali[] kristaller; // 0: Kızıl, 1: Mavi, 2: Zümrüt
        public Transform kacisKapagi;
        public GameObject kapakDeligi;
        public Transform zincirTekerlegi;
        public PrensesKarakteri prenses;
        public Light[] alarmIsiklari;

        // Bulmaca Durumu
        private readonly int[] _hedefSira = new int[] { 0, 1, 2 }; // Kızıl -> Mavi -> Zümrüt
        private int _vurulanAdim = 0;
        private bool _gosterimYapiliyor = false;

        // Savunma Durumu
        private readonly List<DusmanKontrol> _saldirganlar = new List<DusmanKontrol>();
        private float _kapakAcilmaOrani = 0f;
        private float _savunmaZamani = 0f;

        // Diyalog & UI
        private string _altYazi = "";
        private float _altYaziBitis = 0f;
        private string _konusan = "PRENSES AYLİN";
        private Color _konusanRenk = new Color(0.85f, 0.75f, 1.0f);
        private Texture2D _texKoyu;
        private AudioSource _ses;

        private Transform _oyuncu;

        private void Awake()
        {
            Ornek = this;
            _ses = gameObject.AddComponent<AudioSource>();
            _ses.playOnAwake = false;
            _ses.spatialBlend = 0f;
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
                    if (_oyuncu != null && _oyuncu.position.z >= 45.0f)
                    {
                        GirisTetiklendi();
                    }
                    break;

                case Asama.PrensesleIlkDiyalog:
                    if (prenses != null && _oyuncu != null)
                    {
                        prenses.BakisYonu(_oyuncu.position);
                    }
                    break;

                case Asama.RunBulmacasiAtis:
                    if (prenses != null && _oyuncu != null)
                    {
                        prenses.BakisYonu(_oyuncu.position);
                    }
                    break;

                case Asama.BaskinSavunmasi:
                    GuncelleBaskinSavunmasi();
                    break;

                case Asama.KacisaHazir:
                    GuncelleKacisKontrolu();
                    break;
            }

            // Kırmızı alarm ışıkları yanıp sönsün
            if (mevcutAsama == Asama.BaskinSavunmasi && alarmIsiklari != null)
            {
                float t = Mathf.Sin(Time.time * 6f) * 0.5f + 0.5f;
                foreach (var l in alarmIsiklari)
                {
                    if (l != null) l.intensity = t * 3.5f;
                }
            }
        }

        // ==========================================================
        // 1. AŞAMA: KULEYE GİRİŞ & İLK DİYALOG
        // ==========================================================
        private void GirisTetiklendi()
        {
            mevcutAsama = Asama.PrensesleIlkDiyalog;
            // BolumYoneticisi'ne görev bilgisini güncelle
            Yonetim.BolumYoneticisi by = Yonetim.BolumYoneticisi.Ornek;
            if (by != null)
            {
                by.DurumuDegistir(Yonetim.OyunDurumu.SosyalEngelAlaninda);
            }

            StartCoroutine(IlkDiyalogAkisi());
        }

        private IEnumerator IlkDiyalogAkisi()
        {
            DiyalogGoster("PRENSES AYLİN", "Sonunda geldin! Ben Prenses Aylin — 'Beyaz Kuzgun'. Gece parolasıyla muhafızı geçtiğini duydum!", 6.0f,
                new Color(0.92f, 0.78f, 1.0f));
            SesCal(520f, 0.25f);
            yield return new WaitForSeconds(6.2f);

            DiyalogGoster("PRENSES AYLİN", "Komutan Varg beni bu Rün Kafesi'ne kilitledi. Mührü kırmak için Kehanet Küresi'nin gösterdiği sırayla kristalleri vurmalısın!", 6.5f,
                new Color(0.92f, 0.78f, 1.0f));
            SesCal(580f, 0.25f);
            yield return new WaitForSeconds(6.6f);

            StartCoroutine(BulmacaGosterimAkisi());
        }

        // ==========================================================
        // 2. AŞAMA: RÜN BULMACASI GÖSTERİMİ & ATIŞ KONTROLÜ
        // ==========================================================
        public void BulmacayiYenidenGoster()
        {
            if (_gosterimYapiliyor || mevcutAsama != Asama.RunBulmacasiAtis) return;
            StartCoroutine(BulmacaGosterimAkisi());
        }

        private IEnumerator BulmacaGosterimAkisi()
        {
            _gosterimYapiliyor = true;
            mevcutAsama = Asama.RunBulmacasiGosterim;
            _vurulanAdim = 0;

            DiyalogGoster("KEHANET KÜRESİ", "Rün dizilimi parlıyor... Sırayı dikkatle izle!", 3.2f, new Color(0.4f, 0.85f, 1.0f));
            yield return new WaitForSeconds(1.5f);

            for (int i = 0; i < _hedefSira.Length; i++)
            {
                int kIdx = _hedefSira[i];
                Color c = RunKristali.RenkAl(kIdx);
                string renkAdi = RunKristali.Adlar[kIdx];

                // Küre parlasın
                if (kureIsigi != null)
                {
                    kureIsigi.color = c;
                    kureIsigi.intensity = 5.0f;
                }
                // İlgili kristal parlasın
                if (kristaller != null && kIdx < kristaller.Length && kristaller[kIdx] != null)
                {
                    kristaller[kIdx].Parla();
                }

                DiyalogGoster("KEHANET KÜRESİ", $"{i + 1}. RÜN: {renkAdi} KRİSTAL!", 1.2f, c);
                SesCal(400f + kIdx * 160f, 0.35f);
                yield return new WaitForSeconds(1.3f);

                if (kureIsigi != null) kureIsigi.intensity = 0.6f;
                yield return new WaitForSeconds(0.4f);
            }

            _gosterimYapiliyor = false;
            mevcutAsama = Asama.RunBulmacasiAtis;
            DiyalogGoster("GÖREV", "Silahınla kristalleri kürenin gösterdiği sırayla VUR: 1. KIZIL  →  2. MAVİ  →  3. ZÜMRÜT", 8.0f,
                new Color(1.0f, 0.90f, 0.30f));
        }

        public void KristalVuruldu(RunKristali kristal)
        {
            if (mevcutAsama != Asama.RunBulmacasiAtis || _gosterimYapiliyor) return;

            int beklenen = _hedefSira[_vurulanAdim];
            kristal.Parla();

            if (kristal.indeks == beklenen)
            {
                // Doğru kristal vuruldu
                _vurulanAdim++;
                SesCal(600f + _vurulanAdim * 150f, 0.28f);

                if (_vurulanAdim >= _hedefSira.Length)
                {
                    // Bulmaca tamamlandı!
                    StartCoroutine(MuhurKirilisAkisi());
                }
                else
                {
                    string sonraki = RunKristali.Adlar[_hedefSira[_vurulanAdim]];
                    DiyalogGoster("GÖREV", $"✓ DOĞRU! Sıradaki: {sonraki} KRİSTAL ({_vurulanAdim}/{_hedefSira.Length})", 3.0f,
                        new Color(0.35f, 0.95f, 0.45f));
                }
            }
            else
            {
                // Yanlış kristal vuruldu — dizilim sıfırlanır ve tekrar izletilir
                SesCal(220f, 0.5f);
                DiyalogGoster("MÜHÜR", "✗ YANLIŞ SIRA! Rün dizilimi sıfırlandı, küre tekrar gösteriyor...", 3.5f,
                    new Color(1.0f, 0.25f, 0.25f));
                StartCoroutine(BulmacaGosterimAkisi());
            }
        }

        // ==========================================================
        // 3. AŞAMA: MÜHÜR KIRILIŞI & ALARM / BASKIN BAŞLANGICI
        // ==========================================================
        private IEnumerator MuhurKirilisAkisi()
        {
            mevcutAsama = Asama.MuhurKirildi;
            SesCal(880f, 0.6f);

            DiyalogGoster("PRENSES AYLİN", "Harika atış! Rün mührü çözülüyor!", 4.0f, new Color(0.95f, 0.85f, 1.0f));

            // Mühür perdesi sönsün
            if (muhurBariyeri != null)
            {
                muhurBariyeri.SetActive(false);
            }
            if (kureIsigi != null)
            {
                kureIsigi.color = new Color(0.3f, 1.0f, 0.45f);
                kureIsigi.intensity = 4.0f;
            }

            // Parmaklıklar yukarı tavana doğru kayarak açılsın
            float sure = 2.2f;
            float gecen = 0f;
            while (gecen < sure)
            {
                gecen += Time.deltaTime;
                float adim = (4.5f / sure) * Time.deltaTime;
                if (kafesCubuklari != null)
                {
                    foreach (var c in kafesCubuklari)
                    {
                        if (c != null) c.position += Vector3.up * adim;
                    }
                }
                yield return null;
            }

            yield return new WaitForSeconds(0.6f);

            // Prenses kürsüden öne doğru bir adım atsın
            if (prenses != null)
            {
                prenses.HedefeGit(new Vector3(0f, PrensesKulesiInsaci.ZeminY + 0.05f, 50.8f));
            }

            DiyalogGoster("PRENSES AYLİN", "Mühür kırıldı! Ama kaledeki muhafızlar alarmı duydu! Hendek su tüneline açılan kaçış kapağını açmalıyız!", 6.5f,
                new Color(1.0f, 0.35f, 0.35f));
            yield return new WaitForSeconds(3.5f);

            // Baskın aşamasına geç
            BaslatBaskinSavunmasi();
        }

        // ==========================================================
        // 4. AŞAMA: KALE BASKINI & TEKERLEK SAVUNMASI
        // ==========================================================
        private void BaslatBaskinSavunmasi()
        {
            mevcutAsama = Asama.BaskinSavunmasi;
            _savunmaZamani = 0f;
            _kapakAcilmaOrani = 0f;

            // Prenses tekerleğe koşsun
            if (prenses != null)
            {
                prenses.HedefeGit(new Vector3(4.3f, PrensesKulesiInsaci.ZeminY + 0.05f, 49.4f));
            }

            // 4 Muhafız Asker spawn et (Kule girişinden saldıranlar)
            DusmanAskerleriSpawnla();

            DiyalogGoster("PRENSES AYLİN", "Ben zincir tekerleğini çeviriyorum, muhafızları durdurun!", 6.0f,
                new Color(1.0f, 0.85f, 0.25f));
            SesCal(350f, 0.5f);
        }

        private void DusmanAskerleriSpawnla()
        {
            _saldirganlar.Clear();
            GameObject sablon = null;
            if (YardimciKarakter.Instance != null) sablon = YardimciKarakter.Instance.gameObject;
            if (sablon == null && _oyuncu != null) sablon = _oyuncu.gameObject;
            if (sablon == null) return;

            Vector3[] spawnNoktalari = new Vector3[]
            {
                new Vector3(-2.2f, PrensesKulesiInsaci.ZeminY, 43.5f),
                new Vector3( 2.2f, PrensesKulesiInsaci.ZeminY, 43.5f),
                new Vector3(-1.0f, PrensesKulesiInsaci.ZeminY, 41.5f),
                new Vector3( 1.0f, PrensesKulesiInsaci.ZeminY, 41.5f)
            };

            bool aktif = sablon.activeSelf;
            sablon.SetActive(false);

            for (int i = 0; i < spawnNoktalari.Length; i++)
            {
                GameObject asker = Instantiate(sablon, spawnNoktalari[i], Quaternion.Euler(0f, 0f, 0f));
                asker.name = $"KuleMuhafizi_Baskin_{i + 1}";
                asker.tag = "Untagged";
                asker.transform.localScale = Vector3.one;

                // Eski oyuncu/müttefik bileşenlerini temizle
                TemizleBilesen<YardimciKarakter>(asker);
                TemizleBilesen<KarakterHareket>(asker);
                TemizleBilesen<KarakterAtes>(asker);
                TemizleBilesen<OyuncuCani>(asker);
                TemizleBilesen<CharacterController>(asker);
                TemizleBilesen<HendekDusmeKontrolu>(asker);

                string[] silinecekler = { "YardimciNamluUcu", "YardimciMermiIzi", "YardimciNamluIsigi", "YardimciNamluFlasi", "NamluUcu", "MermiIzi" };
                foreach (string ad in silinecekler)
                {
                    Transform c = asker.transform.Find(ad);
                    if (c != null) DestroyImmediate(c.gameObject);
                }

                DusmanKontrol dk = asker.GetComponent<DusmanKontrol>() ?? asker.AddComponent<DusmanKontrol>();
                dk.maksimumCan = 95f;
                dk.mevcutCan = 95f;
                dk.oyuncuyaDogruYuru = true;
                dk.herZamanOyuncuyuTakipEt = true;
                dk.yurumeHizi = 3.1f + i * 0.2f;
                dk.durmaMesafesi = 3.5f;
                dk.gorusMenzili = 50f;
                dk.atesAraligi = 1.05f;
                dk.atesHasari = 5.5f;
                dk.kiyafetRengi = new Color(0.85f, 0.15f, 0.15f); // Koyu Kızıl Muhafız
                dk.RengiUygula();

                asker.SetActive(true);
                _saldirganlar.Add(dk);
            }

            sablon.SetActive(aktif);
        }

        private static void TemizleBilesen<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c != null) DestroyImmediate(c);
        }

        private void GuncelleBaskinSavunmasi()
        {
            _savunmaZamani += Time.deltaTime;

            // Prenses tekerleğe vardığında çevirme animasyonunu başlat
            if (prenses != null && prenses.HedefeUlasti)
            {
                prenses.TekerlekCevir(true);
                prenses.BakisYonu(zincirTekerlegi != null ? zincirTekerlegi.position : prenses.transform.position + Vector3.right);
            }

            // Tekerlek dönsün ve kaçış kapağı açılsın (12 saniyede tam açılır)
            _kapakAcilmaOrani = Mathf.Clamp01(_savunmaZamani / 12.0f);

            if (zincirTekerlegi != null)
            {
                zincirTekerlegi.Rotate(0f, 0f, 90f * Time.deltaTime, Space.Self);
            }

            if (kacisKapagi != null)
            {
                // Kapak menteşesi üzerinde 90 derece yana kayarak/açılarak kalkar
                Vector3 menteşe = new Vector3(3.2f, PrensesKulesiInsaci.ZeminY + 0.08f, 48.2f);
                kacisKapagi.position = menteşe + new Vector3(0f, Mathf.Sin(_kapakAcilmaOrani * Mathf.PI * 0.5f) * 0.8f, 0f);
                kacisKapagi.rotation = Quaternion.Euler(-_kapakAcilmaOrani * 85f, 0f, 0f);
            }

            // Tünelin içindeki su ışığı parlasın
            if (kapakDeligi != null)
            {
                Light tl = kapakDeligi.GetComponentInChildren<Light>();
                if (tl != null) tl.intensity = _kapakAcilmaOrani * 3.0f;
            }

            // Düşmanlar temizlendi mi ve kapak açıldı mı kontrol et
            int hayatta = 0;
            foreach (var dk in _saldirganlar)
            {
                if (dk != null && !dk.IsDead) hayatta++;
            }

            if (_kapakAcilmaOrani >= 1.0f && hayatta == 0)
            {
                KacisiHazirla();
            }
        }

        // ==========================================================
        // 5. AŞAMA: KAÇIŞA HAZIR & TÜNEL ETKİLEŞİMİ
        // ==========================================================
        private void KacisiHazirla()
        {
            mevcutAsama = Asama.KacisaHazir;
            if (prenses != null)
            {
                prenses.TekerlekCevir(false);
                // Prenses kapağın kenarına gelsin ve oyuncuyu beklesin
                prenses.HedefeGit(new Vector3(2.4f, PrensesKulesiInsaci.ZeminY + 0.05f, 47.4f));
            }

            // Alarm ışıklarını kapat
            if (alarmIsiklari != null)
            {
                foreach (var l in alarmIsiklari)
                {
                    if (l != null) l.intensity = 0f;
                }
            }

            DiyalogGoster("PRENSES AYLİN", "Geçit açık! Bu tünel doğrudan hendek şelalesine ve özgürlüğe açılıyor! Kapağa yaklaş ve [E] ile atla!", 10.0f,
                new Color(0.35f, 0.95f, 0.55f));
            SesCal(660f, 0.4f);
        }

        private void GuncelleKacisKontrolu()
        {
            if (_oyuncu == null) return;
            Vector3 kapakPos = new Vector3(3.2f, PrensesKulesiInsaci.ZeminY, 47.4f);
            float mesafe = Vector3.Distance(_oyuncu.position, kapakPos);

            if (mesafe <= 2.8f)
            {
                if (EBasildiMi() || mesafe <= 1.4f)
                {
                    ZaferiTetikle();
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

        // ==========================================================
        // 6. AŞAMA: ZAFER EKRANI & OPERASYON SONU
        // ==========================================================
        private void ZaferiTetikle()
        {
            mevcutAsama = Asama.Zafer;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // BolumYoneticisi zaferini tetikle
            Yonetim.BolumYoneticisi by = Yonetim.BolumYoneticisi.Ornek;
            if (by != null)
            {
                by.DurumuDegistir(Yonetim.OyunDurumu.BolumTamamlandi);
            }

            // Prenses tünele adım atıp kaybolsun
            if (prenses != null)
            {
                prenses.Isinla(new Vector3(3.2f, PrensesKulesiInsaci.ZeminY - 1.2f, 47.4f));
            }

            SesCal(880f, 0.8f);
            Debug.Log("[PrensesKurtarma] OPERASYON TAMAMLANDI! Prenses Aylin başarıyla kurtarıldı.");
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
            AudioClip clip = AudioClip.Create("RunBip", samples, 1, sampleRate, false);
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
                _texKoyu.SetPixel(0, 0, new Color(0.04f, 0.06f, 0.10f, 0.94f));
                _texKoyu.Apply();
            }

            // 1. DİYALOG / HEDEF KUTUSU (Ekranın alt ortası)
            if (Time.time < _altYaziBitis && !string.IsNullOrEmpty(_altYazi))
            {
                GUIStyle baslik = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                GoruntuKalitesiYoneticisi.StiliKeskinlestir(baslik, Mathf.RoundToInt(13 * olcek));
                baslik.normal.textColor = _konusanRenk;

                GUIStyle metin = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter
                };
                GoruntuKalitesiYoneticisi.StiliKeskinlestir(metin, Mathf.RoundToInt(14 * olcek));
                metin.normal.textColor = Color.white;

                float w = Mathf.Min(Screen.width - 40f, 720f * olcek);
                float icW = w - 24f * olcek;
                float bH = 20f * olcek;
                float mH = Mathf.Max(36f * olcek, metin.CalcHeight(new GUIContent(_altYazi), icW) + 6f);
                float h = bH + mH + 18f * olcek;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height - h - 56f * olcek;

                GUI.DrawTexture(new Rect(x, y, w, h), _texKoyu);
                GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(x + 12f * olcek, y + 6f * olcek, icW, bH), $"◆ {_konusan} ◆", baslik);
                GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(x + 12f * olcek, y + 6f * olcek + bH, icW, mH), _altYazi, metin);
            }

            // 2. KAÇIŞ KAPAĞI YAKININDA [E] İPUCU
            if (mevcutAsama == Asama.KacisaHazir && _oyuncu != null && Camera.main != null)
            {
                Vector3 kapakPos = new Vector3(3.2f, PrensesKulesiInsaci.ZeminY + 0.6f, 47.4f);
                if (GoruntuKalitesiYoneticisi.DunyaNoktasiniEkranaCevir(Camera.main, kapakPos, out Vector2 scr))
                {
                    float bw = 320f * olcek;
                    float bh = 38f * olcek;
                    Rect r = new Rect(scr.x - bw * 0.5f, scr.y - bh * 0.5f, bw, bh);
                    GUI.DrawTexture(r, _texKoyu);
                    GUIStyle st = new GUIStyle(GUI.skin.label)
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    GoruntuKalitesiYoneticisi.StiliKeskinlestir(st, Mathf.RoundToInt(14 * olcek));
                    st.normal.textColor = new Color(0.35f, 1.0f, 0.55f);
                    GoruntuKalitesiYoneticisi.KeskinEtiketCiz(r, "🌊 [E] TÜNELE ATLA & PRENSESLE KAÇ!", st);
                }
            }

            // 3. FİNAL ZAFER EKRANI (AAA Taktiksel Operasyon Raporu & Epilog)
            if (mevcutAsama == Asama.Zafer)
            {
                CizZaferEkrani(olcek);
            }
        }

        private void CizZaferEkrani(float olcek)
        {
            // Karartma arka planı
            GUI.color = new Color(0f, 0f, 0f, 0.88f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float panelW = Mathf.Min(Screen.width - 60f, 760f * olcek);
            float panelH = Mathf.Min(Screen.height - 60f, 540f * olcek);
            float px = (Screen.width - panelW) * 0.5f;
            float py = (Screen.height - panelH) * 0.5f;

            GUI.DrawTexture(new Rect(px, py, panelW, panelH), _texKoyu);

            // Altın sarısı çerçeve çizgisi
            Color altin = new Color(1.0f, 0.82f, 0.28f);
            GUI.color = altin;
            GUI.DrawTexture(new Rect(px, py, panelW, 3f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(px, py + panelH - 3f, panelW, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUIStyle baslikStil = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(baslikStil, Mathf.RoundToInt(22 * olcek));
            baslikStil.normal.textColor = altin;

            GUIStyle altBaslik = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(altBaslik, Mathf.RoundToInt(13 * olcek));
            altBaslik.normal.textColor = new Color(0.45f, 0.85f, 1.0f);

            GUIStyle epilogStil = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(epilogStil, Mathf.RoundToInt(13 * olcek));
            epilogStil.normal.textColor = new Color(0.92f, 0.94f, 0.98f);

            float pad = 24f * olcek;
            float icW = panelW - pad * 2f;
            float cy = py + 20f * olcek;

            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(px + pad, cy, icW, 32f * olcek),
                "★ OPERASYON BAŞARIYLA TAMAMLANDI ★", baslikStil);
            cy += 36f * olcek;

            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(px + pad, cy, icW, 22f * olcek),
                "KOCAELİ ÜNİVERSİTESİ YAZLAB-I PROJE 1 — TAKTİKSEL İSTİHBARAT OPERASYONU", altBaslik);
            cy += 32f * olcek;

            string epilog =
                "Kraliyet Baş İstihbarat Ajanı \"Beyaz Kuzgun\" — Prenses Aylin, Komutan Varg'ın elindeki kadim Rün Mührü'nden " +
                "başarıyla kurtarıldı.\n\n" +
                "TAMAMLANAN GÖREV ADIMLARI:\n" +
                "  ✓ 1. BÖLÜM: Hendek Kanyonu & Yoldaş ile Eşzamanlı 2 Anahtarlı Askı Kapı ([G] + [E])\n" +
                "  ✓ 2. BÖLÜM: Bağımsız Kablo Odası Şalter Bulmacası & Otomatik Savunma Tareti\n" +
                "  ✓ 3. BÖLÜM: Çok Katlı İç Kule & Ollama Yerel LLM Muhafız Müzakeresi\n" +
                "  ✓ 4. BÖLÜM (FİNAL): Kızıl Kule Kehanet Küresi Rün Bulmacası & Prenses Aylin'in Hendek Su Tünelinden Tahliyesi\n\n" +
                "Prenses Aylin ve Yoldaş Ajan hendek şelalesinin altındaki gizli geçitten güvenli bölgeye ulaştı. Görev başarıyla sonuçlandı!";

            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(px + pad, cy, icW, 240f * olcek), epilog, epilogStil);
            cy += 255f * olcek;

            // Butonlar: Tekrar Oyna & Oyundan Çık
            float btnW = 180f * olcek;
            float btnH = 42f * olcek;
            float bx1 = px + panelW * 0.5f - btnW - 12f * olcek;
            float bx2 = px + panelW * 0.5f + 12f * olcek;

            if (GUI.Button(new Rect(bx1, cy, btnW, btnH), "YENİDEN OYNA"))
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            if (GUI.Button(new Rect(bx2, cy, btnW, btnH), "OYUNDAN ÇIK"))
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
