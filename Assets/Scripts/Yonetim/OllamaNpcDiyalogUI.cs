using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Yazlab.Yonetim
{
    /// <summary>
    /// Kocaeli Üniversitesi YazLab-I Proje İsterlerine (Bölüm 3.2, 4.2, 4.3, 4.4) uygun
    /// Serbest Metin Tabanlı Yerel LLM (Ollama) NPC Diyalog ve Karar Arayüzü.
    /// - Hiçbir hazır/scriptlenmiş ikna butonu içermez; oyuncu tamamen kendi serbest metniyle muhafızı ikna etmeye çalışır.
    /// - Modelin ürettiği yapılandırılmış JSON çıktısını (Tepki Türü, Güven Değişimi, Kapı Açma, Savaş Tetikleme) canlı gösterir.
    /// - Muhafız kolay ikna olmaz (en az 2-3 aşamalı sorgulama ve %80+ güven gerekir).
    /// - Gece parolası ve nasıl ikna edileceğine dair dinamik boyutlu, tam sığan İkna Rehberi (İpucu Kutusu) içerir.
    /// - Oyuncu muhafızı kızdırır veya tehdit ederse muhafız düşmana dönüşüp oyuncuya ateş açar (`savasTetikle: true`)!
    /// </summary>
    [RequireComponent(typeof(OllamaIstemcisi))]
    public class OllamaNpcDiyalogUI : MonoBehaviour
    {
        public static bool DiyalogAcikMi { get; private set; } = false;

        private struct SohbetSatiri
        {
            public string konusan;
            public string mesaj;
            public string etiketBilgisi;
            public bool oyuncuMu;
            public bool hataMi;
        }

        private IYerelDilModeliServisi _llmServisi;
        private string _oyuncuGirdisi = "";
        private int _mevcutGuvenSkoru = 30; // Başlangıç güveni %30 (Şüpheci)
        private int _basariliIknaAdimi = 0; // Kapının açılması için en az 2 başarılı ikna adımı gerekir
        private string _sonTepkiTuru = "SupheciRed";
        private bool _iknaTamamlandi = false;
        private bool _savasBasladi = false;
        private Vector2 _kaydirmaPozisyonu = Vector2.zero;
        private bool _odakVerildi = false;

        private readonly List<SohbetSatiri> _sohbetKayitlari = new List<SohbetSatiri>();

        // Görsel Dokular ve Stiller
        private Texture2D _texArkaPlan;
        private Texture2D _texPanelUst;
        private Texture2D _texSohbetKutu;
        private Texture2D _texGuvenBarArka;
        private Texture2D _texIpucuKutu;
        private Texture2D _texMesajMuhafiz;
        private Texture2D _texMesajOyuncu;
        private Texture2D _texMesajHata;

        private GUIStyle _baslikStil;
        private GUIStyle _altBaslikStil;
        private GUIStyle _ipucuBaslikStil;
        private GUIStyle _ipucuStil;
        private GUIStyle _mesajOyuncuStil;
        private GUIStyle _mesajMuhafizStil;
        private GUIStyle _mesajHataStil;
        private GUIStyle _girdiStil;
        private GUIStyle _butonStil;

        private void Awake()
        {
            _llmServisi = GetComponent<IYerelDilModeliServisi>();
            if (_llmServisi == null)
            {
                _llmServisi = gameObject.AddComponent<OllamaIstemcisi>();
            }

            BaslangicMesajiniEkle();
        }

        private void BaslangicMesajiniEkle()
        {
            _sohbetKayitlari.Clear();
            _mevcutGuvenSkoru = 30;
            _basariliIknaAdimi = 0;
            _sonTepkiTuru = "SupheciRed";
            _iknaTamamlandi = false;
            _savasBasladi = false;

            _sohbetKayitlari.Add(new SohbetSatiri
            {
                konusan = "🛡️ Kıdemli Muhafız Komutan Varg",
                mesaj = "Dur bakalım yabancı! Bu kule kapısı doğrudan çıkışa açılır ve Başkomutanın emriyle geçişler tamamen yasaklandı. Kimsin, bu yasak bölgeye nasıl girdin ve gece parolası nedir? Beni ikna edecek çok güçlü bir kanıtın veya teklifin yoksa buradan asla geçemezsin; yanlış bir hareketinde de silahımı ateşlerim!",
                etiketBilgisi = "[Durum: Nöbetçi Sorgusu | Güven: %30 | İkna Aşaması: 0/2]",
                oyuncuMu = false,
                hataMi = false
            });
        }

        public void DiyalogPenceresiniAc()
        {
            if (_iknaTamamlandi || _savasBasladi) return;

            DiyalogAcikMi = true;
            _odakVerildi = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MuhafiziOyuncuyaDondur();
        }

        public void DiyalogPenceresiniKapat()
        {
            DiyalogAcikMi = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void MuhafiziOyuncuyaDondur()
        {
            GameObject npc = GameObject.Find("NPC_Muhafiz");
            KarakterHareket oyuncu = Object.FindAnyObjectByType<KarakterHareket>();
            if (npc != null && oyuncu != null)
            {
                Vector3 yon = oyuncu.transform.position - npc.transform.position;
                yon.y = 0f;
                if (yon.sqrMagnitude > 0.01f)
                {
                    npc.transform.rotation = Quaternion.LookRotation(yon);
                }
            }
        }

        private void Update()
        {
            if (!DiyalogAcikMi) return;

            bool escBasildi = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                escBasildi = true;
            }
#endif
            if (!escBasildi)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Escape)) escBasildi = true;
                }
                catch { }
            }

            if (escBasildi)
            {
                DiyalogPenceresiniKapat();
            }
        }

        private void MesajiGonder()
        {
            if (_llmServisi == null || _llmServisi.IstekDevamEdiyorMu || string.IsNullOrWhiteSpace(_oyuncuGirdisi))
                return;

            string gonderilecek = _oyuncuGirdisi.Trim();
            _oyuncuGirdisi = "";

            _sohbetKayitlari.Add(new SohbetSatiri
            {
                konusan = "🎖️ Sen (Serbest Metin)",
                mesaj = gonderilecek,
                etiketBilgisi = "",
                oyuncuMu = true,
                hataMi = false
            });
            _kaydirmaPozisyonu.y = float.MaxValue;

            _llmServisi.MesajGonderAsenkron(
                gonderilecek,
                _mevcutGuvenSkoru,
                _basariliIknaAdimi,
                onBasarili: KarariUygula,
                onHata: HatayiGoster);
        }

        private void KarariUygula(MuhafizKararVerisi karar)
        {
            MuhafizTepkiTuru tepki = karar.GetTepkiEnum();
            _sonTepkiTuru = tepki.ToString();

            int eskiGuven = _mevcutGuvenSkoru;
            _mevcutGuvenSkoru = Mathf.Clamp(_mevcutGuvenSkoru + karar.guvenDegisimi, 0, 100);

            if (tepki == MuhafizTepkiTuru.IknaEdici && karar.guvenDegisimi > 0)
            {
                _basariliIknaAdimi++;
            }
            else if (tepki == MuhafizTepkiTuru.SaldirganKizdiran && _basariliIknaAdimi > 0)
            {
                _basariliIknaAdimi--;
            }

            string degisimMetni = karar.guvenDegisimi >= 0 ? $"+{karar.guvenDegisimi}" : karar.guvenDegisimi.ToString();
            string jsonOzeti = $"[JSON Kararı -> Tepki: {_sonTepkiTuru} | Güven Değişimi: {degisimMetni} ({eskiGuven}% -> {_mevcutGuvenSkoru}%) | İkna Adımı: {Mathf.Min(_basariliIknaAdimi, 2)}/2 | KapıAç: {karar.kapiyiAc} | Savaş: {karar.savasTetikle}]";

            _sohbetKayitlari.Add(new SohbetSatiri
            {
                konusan = $"🛡️ Komutan Varg (Ollama: {_llmServisi.AktifModelAdi})",
                mesaj = karar.cevap,
                etiketBilgisi = jsonOzeti,
                oyuncuMu = false,
                hataMi = false
            });
            _kaydirmaPozisyonu.y = float.MaxValue;

            // DURUM 1: SAVAŞ TETİKLENDİ (Oyuncu muhafızı kızdırdı, tehdit etti veya güven sıfırlandı)
            if (karar.savasTetikle || _mevcutGuvenSkoru <= 10)
            {
                _savasBasladi = true;
                _sohbetKayitlari.Add(new SohbetSatiri
                {
                    konusan = "⚔️ ALARM - ÇATIŞMA TETİKLENDİ!",
                    mesaj = "Muhafız Komutan Varg ikna olmadı ve düşmanca tavrınız yüzünden silahına sarıldı! Onu etkisiz hale getirerek kapı kilidini kırın!",
                    etiketBilgisi = "[Oyun Durumu Değişti: NPC Muhafız -> Saldırgan Düşman]",
                    oyuncuMu = false,
                    hataMi = true
                });
                _kaydirmaPozisyonu.y = float.MaxValue;

                if (BolumYoneticisi.Ornek != null)
                {
                    BolumYoneticisi.Ornek.MuhafizSavasBaslat();
                }

                Invoke(nameof(DiyalogPenceresiniKapat), 2.5f);
                return;
            }

            // DURUM 2: MUHAFIZ İKNA EDİLDİ (En az 2 aşamalı ikna ve %80+ güven tamamlandı)
            if ((karar.kapiyiAc || _mevcutGuvenSkoru >= 80) && _basariliIknaAdimi >= 2 && _mevcutGuvenSkoru >= 80)
            {
                _iknaTamamlandi = true;
                _sohbetKayitlari.Add(new SohbetSatiri
                {
                    konusan = "✓ MÜZAKERE BAŞARILI - KAPI AÇILIYOR",
                    mesaj = "Komutan Varg ikna oldu ve kule çıkış kapısının kilidini açtı! Şimdi kapıdan geçerek operasyonu tamamlayabilirsiniz.",
                    etiketBilgisi = "[Oyun Durumu Değişti: NpcCikisKapisi -> AÇIK]",
                    oyuncuMu = false,
                    hataMi = false
                });
                _kaydirmaPozisyonu.y = float.MaxValue;

                if (BolumYoneticisi.Ornek != null)
                {
                    BolumYoneticisi.Ornek.MuhafizlaGorusVeKapisiAc();
                }

                Invoke(nameof(DiyalogPenceresiniKapat), 3.2f);
            }
        }

        private void HatayiGoster(string hataMesaji)
        {
            _sohbetKayitlari.Add(new SohbetSatiri
            {
                konusan = "⚠️ HATA YÖNETİMİ (BAĞLANTI / MODEL UYARISI)",
                mesaj = hataMesaji,
                etiketBilgisi = "[Sistem Çökmeden Güvenli Modda Devam Ediyor - Madde 4.3]",
                oyuncuMu = false,
                hataMi = true
            });
            _kaydirmaPozisyonu.y = float.MaxValue;
        }

        private void DokulariVeStilleriHazirla(float olcek)
        {
            if (_texArkaPlan == null)
            {
                _texArkaPlan = RenkDokusu(new Color(0.06f, 0.08f, 0.12f, 0.98f));
                _texPanelUst = RenkDokusu(new Color(0.10f, 0.14f, 0.22f, 1.0f));
                _texSohbetKutu = RenkDokusu(new Color(0.04f, 0.05f, 0.08f, 0.96f));
                _texGuvenBarArka = RenkDokusu(new Color(0.18f, 0.20f, 0.25f, 1.0f));
                _texIpucuKutu = RenkDokusu(new Color(0.09f, 0.13f, 0.19f, 0.98f));
                _texMesajMuhafiz = RenkDokusu(new Color(0.11f, 0.14f, 0.20f, 0.95f));
                _texMesajOyuncu = RenkDokusu(new Color(0.08f, 0.20f, 0.26f, 0.95f));
                _texMesajHata = RenkDokusu(new Color(0.24f, 0.09f, 0.09f, 0.95f));
            }

            if (_baslikStil == null)
            {
                _baslikStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_baslikStil, Mathf.RoundToInt(16 * olcek));
            _baslikStil.normal.textColor = new Color(1.0f, 0.86f, 0.28f);

            if (_altBaslikStil == null)
            {
                _altBaslikStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_altBaslikStil, Mathf.RoundToInt(13 * olcek));
            _altBaslikStil.normal.textColor = new Color(0.88f, 0.94f, 1.0f);

            if (_ipucuBaslikStil == null)
            {
                _ipucuBaslikStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = true
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_ipucuBaslikStil, Mathf.RoundToInt(13 * olcek));
            _ipucuBaslikStil.normal.textColor = new Color(1.0f, 0.86f, 0.28f);

            if (_ipucuStil == null)
            {
                _ipucuStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    padding = new RectOffset(6, 6, 4, 4)
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_ipucuStil, Mathf.RoundToInt(12.5f * olcek));
            _ipucuStil.normal.textColor = new Color(0.88f, 0.95f, 1.0f);

            int padX = Mathf.Max(10, Mathf.RoundToInt(12 * olcek));
            int padY = Mathf.Max(8, Mathf.RoundToInt(8 * olcek));

            if (_mesajOyuncuStil == null)
            {
                _mesajOyuncuStil = new GUIStyle(GUI.skin.box)
                {
                    wordWrap = true,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
            }
            _mesajOyuncuStil.normal.background = _texMesajOyuncu;
            _mesajOyuncuStil.padding = new RectOffset(padX, padX, padY, padY);
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_mesajOyuncuStil, Mathf.RoundToInt(14 * olcek));
            _mesajOyuncuStil.normal.textColor = new Color(0.68f, 0.95f, 1.0f);

            if (_mesajMuhafizStil == null)
            {
                _mesajMuhafizStil = new GUIStyle(GUI.skin.box)
                {
                    wordWrap = true,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
            }
            _mesajMuhafizStil.normal.background = _texMesajMuhafiz;
            _mesajMuhafizStil.padding = new RectOffset(padX, padX, padY, padY);
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_mesajMuhafizStil, Mathf.RoundToInt(14 * olcek));
            _mesajMuhafizStil.normal.textColor = new Color(1.0f, 0.97f, 0.88f);

            if (_mesajHataStil == null)
            {
                _mesajHataStil = new GUIStyle(GUI.skin.box)
                {
                    wordWrap = true,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperLeft
                };
            }
            _mesajHataStil.normal.background = _texMesajHata;
            _mesajHataStil.padding = new RectOffset(padX, padX, padY, padY);
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_mesajHataStil, Mathf.RoundToInt(14 * olcek));
            _mesajHataStil.normal.textColor = new Color(1.0f, 0.52f, 0.52f);

            if (_girdiStil == null)
            {
                _girdiStil = new GUIStyle(GUI.skin.textField)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                    padding = new RectOffset(10, 10, 6, 6)
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_girdiStil, Mathf.RoundToInt(15 * olcek));

            if (_butonStil == null)
            {
                _butonStil = new GUIStyle(GUI.skin.button)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_butonStil, Mathf.RoundToInt(14 * olcek));
        }

        private Texture2D RenkDokusu(Color c)
        {
            Texture2D t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        private void OnGUI()
        {
            if (!DiyalogAcikMi) return;

            // Hem genişlik hem yüksekliğe göre esnek ölçekleme (hiçbir çözünürlükte pencere dışına taşmaz)
            float olcek = Mathf.Clamp(Mathf.Min((Screen.width - 28f) / 860f, (Screen.height - 24f) / 600f), 0.75f, 1.25f);
            DokulariVeStilleriHazirla(olcek);

            Color eskiRenk = GUI.color;

            // Arka plan karartması
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float w = Mathf.Min(Screen.width - 20f, 860f * olcek);
            float h = Mathf.Min(Screen.height - 16f, 600f * olcek);
            float x = Mathf.Round((Screen.width - w) * 0.5f);
            float y = Mathf.Round((Screen.height - h) * 0.5f);
            float kenar = Mathf.Round(16f * olcek);
            float icGenislikTam = w - kenar * 2f;

            // Ana Çerçeve
            GUI.DrawTexture(new Rect(x, y, w, h), _texArkaPlan);
            float ustBarH = Mathf.Round(46f * olcek);
            GUI.DrawTexture(new Rect(x, y, w, ustBarH), _texPanelUst);

            GUI.color = new Color(0.95f, 0.76f, 0.20f, 1f);
            GUI.DrawTexture(new Rect(x, y + ustBarH, w, Mathf.Max(2f, 2f * olcek)), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 1. Üst Başlık ve Kapat Butonu
            float kapatBtnW = Mathf.Round(114f * olcek);
            float kapatBtnH = Mathf.Round(30f * olcek);
            GUI.Label(new Rect(x + kenar, y + 7f * olcek, icGenislikTam - kapatBtnW - 10f * olcek, 32f * olcek),
                $"🛡️ KIDEMLİ MUHAFIZ KOMUTAN VARG — YEREL LLM ({_llmServisi.AktifModelAdi})", _baslikStil);

            if (GUI.Button(new Rect(x + w - kenar - kapatBtnW, y + 8f * olcek, kapatBtnW, kapatBtnH), "✕ KAPAT [ESC]", _butonStil))
            {
                DiyalogPenceresiniKapat();
                GUI.color = eskiRenk;
                return;
            }

            // 2. Üst Bilgi: Güven Skoru, İkna Aşaması ve Son JSON Tepki Türü
            float barY = y + ustBarH + 8f * olcek;
            string durumBilgisi = $"Güven Skoru: %{_mevcutGuvenSkoru}/100 (Hedef: %80+)   |   İkna Aşaması: {Mathf.Min(_basariliIknaAdimi, 2)}/2   |   Son Analiz: {_sonTepkiTuru}";
            GUI.Label(new Rect(x + kenar, barY, icGenislikTam, 22f * olcek), durumBilgisi, _altBaslikStil);

            float barH = Mathf.Round(10f * olcek);
            GUI.DrawTexture(new Rect(x + kenar, barY + 22f * olcek, icGenislikTam, barH), _texGuvenBarArka);

            float dolguW = icGenislikTam * Mathf.Clamp01(_mevcutGuvenSkoru / 100f);
            if (_mevcutGuvenSkoru >= 80) GUI.color = new Color(0.2f, 0.95f, 0.4f);
            else if (_mevcutGuvenSkoru <= 25) GUI.color = new Color(0.95f, 0.25f, 0.2f);
            else GUI.color = new Color(0.95f, 0.75f, 0.2f);

            GUI.DrawTexture(new Rect(x + kenar, barY + 22f * olcek, dolguW, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;

            // 3. Alt İpucu & İkna Rehberi Kutusu (Dinamik yükseklik ile yazının sığmaması %100 engellenir!)
            string ipucuMetni =
                "💡 NASIL İKNA EDİLİR? (Gece Parolası & 2 Aşamalı İkna Rehberi):\n" +
                "• 1. Adım (Gece Parolası & Emir): Gece parolasının \"Kızıl Şahin\" (veya \"Gece Kuzgunu\") olduğunu ve Başkomutanın gizli istihbarat emriyle geldiğinizi yazın.\n" +
                "• 2. Adım (Kanıt veya Rüşvet): Muhafız kanıt sorunca kraliyet mührünü/belgesini gösterin ya da 500+ altın dolu kese teklif edin. (Hakaret/tehdit savaşı başlatır!)";

            float ipucuIcGenislik = icGenislikTam - 16f * olcek;
            float ipucuMetinYuk = _ipucuStil.CalcHeight(new GUIContent(ipucuMetni), ipucuIcGenislik);
            float ipucuKutuH = Mathf.Max(68f * olcek, ipucuMetinYuk + 14f * olcek);
            float ipucuY = y + h - ipucuKutuH - 10f * olcek;

            // 4. Serbest Metin Giriş Alanı (İpucu kutusunun hemen üstünde)
            float girdiH = Mathf.Round(38f * olcek);
            float girdiY = ipucuY - girdiH - 8f * olcek;
            float gonderBtnW = Mathf.Round(145f * olcek);

            // 5. Sohbet Geçmişi Kutusu (Üst bar ile giriş alanı arasındaki tüm boşluğa tam oturur)
            float sohbetY = barY + barH + 28f * olcek;
            float sohbetH = Mathf.Max(110f * olcek, girdiY - sohbetY - 10f * olcek);
            Rect disAlani = new Rect(x + kenar, sohbetY, icGenislikTam, sohbetH);
            GUI.DrawTexture(disAlani, _texSohbetKutu);

            float scrollIcGenislik = disAlani.width - 22f * olcek;
            float mesajKutuGenislik = scrollIcGenislik - 12f * olcek;
            float toplamYukseklik = 12f * olcek;

            foreach (var satir in _sohbetKayitlari)
            {
                GUIStyle s = satir.hataMi ? _mesajHataStil : (satir.oyuncuMu ? _mesajOyuncuStil : _mesajMuhafizStil);
                string tamMetin = string.IsNullOrEmpty(satir.etiketBilgisi)
                    ? $"{satir.konusan}:\n{satir.mesaj}"
                    : $"{satir.konusan}:\n{satir.mesaj}\n{satir.etiketBilgisi}";
                float kutuYuk = s.CalcHeight(new GUIContent(tamMetin), mesajKutuGenislik) + 8f * olcek;
                toplamYukseklik += kutuYuk + 8f * olcek;
            }
            if (_llmServisi.IstekDevamEdiyorMu)
            {
                toplamYukseklik += 52f * olcek;
            }

            Rect icAlani = new Rect(0, 0, scrollIcGenislik, Mathf.Max(sohbetH - 6f, toplamYukseklik));
            _kaydirmaPozisyonu = GUI.BeginScrollView(disAlani, _kaydirmaPozisyonu, icAlani);

            float curY = 6f * olcek;
            foreach (var satir in _sohbetKayitlari)
            {
                GUIStyle s = satir.hataMi ? _mesajHataStil : (satir.oyuncuMu ? _mesajOyuncuStil : _mesajMuhafizStil);
                string tamMetin = string.IsNullOrEmpty(satir.etiketBilgisi)
                    ? $"{satir.konusan}:\n{satir.mesaj}"
                    : $"{satir.konusan}:\n{satir.mesaj}\n{satir.etiketBilgisi}";
                float kutuYuk = s.CalcHeight(new GUIContent(tamMetin), mesajKutuGenislik) + 8f * olcek;
                GUI.Box(new Rect(6f * olcek, curY, mesajKutuGenislik, kutuYuk), tamMetin, s);
                curY += kutuYuk + 8f * olcek;
            }

            // Madde 3.2: Modelin yanıt üretme süresi (latency) boyunca görsel geri bildirim ("Düşünüyor...")
            if (_llmServisi.IstekDevamEdiyorMu)
            {
                int noktaSayisi = Mathf.FloorToInt(Time.time * 3f) % 4;
                string noktalar = new string('.', noktaSayisi);
                string dusunuyorMetni = $"⏳ Komutan Varg düşünüyor{noktalar} (Yerel LLM JSON yanıtı üretiyor, oyun akışı asenkron devam ediyor)";
                float dusYuk = _mesajMuhafizStil.CalcHeight(new GUIContent(dusunuyorMetni), mesajKutuGenislik) + 8f * olcek;
                GUI.Box(new Rect(6f * olcek, curY, mesajKutuGenislik, dusYuk), dusunuyorMetni, _mesajMuhafizStil);
            }

            GUI.EndScrollView();

            // Klavye Enter tuşu kontrolü
            Event ev = Event.current;
            if (ev != null && ev.type == EventType.KeyDown && (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter))
            {
                if (!_llmServisi.IstekDevamEdiyorMu && !_iknaTamamlandi && !_savasBasladi && !string.IsNullOrWhiteSpace(_oyuncuGirdisi))
                {
                    MesajiGonder();
                    ev.Use();
                }
            }

            // Giriş Kutusu Çizimi
            GUI.SetNextControlName("OllamaSerbestMetinKutusu");
            _oyuncuGirdisi = GUI.TextField(
                new Rect(x + kenar, girdiY, icGenislikTam - gonderBtnW - 10f * olcek, girdiH),
                _oyuncuGirdisi, 350, _girdiStil);

            if (!_odakVerildi)
            {
                GUI.FocusControl("OllamaSerbestMetinKutusu");
                _odakVerildi = true;
            }

            GUI.enabled = !_llmServisi.IstekDevamEdiyorMu && !_iknaTamamlandi && !_savasBasladi;
            GUI.color = new Color(0.20f, 0.80f, 0.40f, 1f);
            if (GUI.Button(new Rect(x + w - kenar - gonderBtnW, girdiY, gonderBtnW, girdiH),
                _llmServisi.IstekDevamEdiyorMu ? "DÜŞÜNÜYOR..." : "GÖNDER [ENTER]", _butonStil))
            {
                MesajiGonder();
            }
            GUI.color = Color.white;
            GUI.enabled = true;

            // Alt İpucu & Gece Parolası Rehber Kutusu Çizimi
            Rect ipucuKutuRect = new Rect(x + kenar, ipucuY, icGenislikTam, ipucuKutuH);
            GUI.DrawTexture(ipucuKutuRect, _texIpucuKutu);

            // İpucu kutusu sol altın şeridi
            GUI.color = new Color(1.0f, 0.82f, 0.22f, 1f);
            GUI.DrawTexture(new Rect(ipucuKutuRect.x, ipucuKutuRect.y, 4f * olcek, ipucuKutuRect.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(ipucuKutuRect.x + 10f * olcek, ipucuKutuRect.y + 5f * olcek, ipucuIcGenislik, ipucuKutuH - 8f * olcek),
                ipucuMetni, _ipucuStil);

            GUI.color = eskiRenk;
        }
    }
}
