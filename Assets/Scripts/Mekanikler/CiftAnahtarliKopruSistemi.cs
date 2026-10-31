using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Yazlab.Yonetim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// 1. Bölümdeki Hendek (Moat) önünde yer alan Çift Anahtarlı Eşzamanlı Askı Kapı (Asma Köprü) Kurgusu.
    /// Köprünün inmesi için Sol Anahtar (Oyuncu) ve Sağ Anahtarın (Yardımcı Karakter)
    /// aynı anda aktif edilmesi gerekir.
    /// - [G] tuşu ile Yardımcı Karakter sağ anahtar sütununa gönderilir.
    /// - Yardımcı karakter sağ anahtarı tutarken Oyuncu sol anahtarın yanında [E] tuşuna basar ve köprü iner!
    /// </summary>
    public class CiftAnahtarliKopruSistemi : MonoBehaviour
    {
        [Header("Bağlı Asma Köprü")]
        public AsmaKopruKontrol bagliKopru;

        [Header("Anahtar Sütunları ve Kolları")]
        public Transform solAnahtarSutunu;
        public Transform sagAnahtarSutunu;
        public Transform solAnahtarKolu;
        public Transform sagAnahtarKolu;
        public MeshRenderer solIsikRenderer;
        public MeshRenderer sagIsikRenderer;

        [Header("Etkileşim Mesafesi")]
        public float etkilesimMesafesi = 3.8f;

        private Transform _oyuncu;
        private YardimciKarakter _yardimci;
        private bool _yardimciGonderildi = false;
        private bool _sagAnahtarTutuluyor = false;
        private bool _cozuldu = false;
        private string _bildirimMesaji = "";
        private float _bildirimBitisZamani = 0f;

        private GUIStyle _panelStil;
        private GUIStyle _baslikStil;
        private GUIStyle _altStil;
        private Texture2D _panelTex;

        private void Start()
        {
            ReferanslariBul();
            IsikRengiAyarla(solIsikRenderer, new Color(0.85f, 0.15f, 0.10f));
            IsikRengiAyarla(sagIsikRenderer, new Color(0.85f, 0.15f, 0.10f));
        }

        private void ReferanslariBul()
        {
            if (_oyuncu == null)
            {
                KarakterHareket kh = Object.FindAnyObjectByType<KarakterHareket>();
                if (kh != null) _oyuncu = kh.transform;
                else
                {
                    GameObject p = GameObject.FindWithTag("Player");
                    if (p != null) _oyuncu = p.transform;
                }
            }

            if (_yardimci == null)
            {
                _yardimci = YardimciKarakter.Instance != null
                    ? YardimciKarakter.Instance
                    : Object.FindAnyObjectByType<YardimciKarakter>();
            }

            if (bagliKopru == null)
            {
                bagliKopru = Object.FindAnyObjectByType<AsmaKopruKontrol>();
            }
        }

        private void Update()
        {
            if (_cozuldu) return;
            if (KapiBulmacaPaneli.MinigameAcik || OllamaNpcDiyalogUI.DiyalogAcikMi) return;

            ReferanslariBul();
            if (_oyuncu == null) return;

            // [G] Tuşu: Yardımcı karakteri Sağ Anahtar Sütununa gönder
            if (GTusuBasildiMi() && !_yardimciGonderildi && _yardimci != null && sagAnahtarSutunu != null)
            {
                _yardimciGonderildi = true;
                Vector3 durusNoktasi = sagAnahtarSutunu.position + new Vector3(0f, 0f, -1.1f);
                _yardimci.GorevNoktasinaGit(durusNoktasi);
                BildirimGoster("Yardımcı Karakter Sağ Anahtar Sütununa ilerliyor!", 3.0f);
            }

            // Yardımcı karakter sağ anahtarın başına ulaştı mı kontrol et
            if (_yardimciGonderildi && _yardimci != null && !_sagAnahtarTutuluyor)
            {
                if (_yardimci.GorevNoktasindaMi)
                {
                    _sagAnahtarTutuluyor = true;
                    IsikRengiAyarla(sagIsikRenderer, new Color(0.95f, 0.80f, 0.15f)); // Hazır (Sarı/Turuncu)
                    if (sagAnahtarKolu != null)
                    {
                        sagAnahtarKolu.localRotation = Quaternion.Euler(40f, 0f, 0f);
                    }
                    BildirimGoster("Yardımcı Karakter Sağ Anahtarı tutuyor! Şimdi Sol Anahtara gidip [E] tuşuna basın!", 4.5f);
                }
            }

            // Oyuncu Sol Anahtarın yanında [E] tuşuna bastı mı?
            if (solAnahtarSutunu != null)
            {
                float solMesafe = Vector3.Distance(_oyuncu.position, solAnahtarSutunu.position);
                if (solMesafe <= etkilesimMesafesi && ETusuBasildiMi())
                {
                    if (!_sagAnahtarTutuluyor)
                    {
                        // Eğer yardımcı henüz gönderilmediyse otomatik gönder ve oyuncuyu bilgilendir
                        if (!_yardimciGonderildi && _yardimci != null && sagAnahtarSutunu != null)
                        {
                            _yardimciGonderildi = true;
                            Vector3 durusNoktasi = sagAnahtarSutunu.position + new Vector3(0f, 0f, -1.1f);
                            _yardimci.GorevNoktasinaGit(durusNoktasi);
                            BildirimGoster("Tek anahtar yetmez! Yardımcı Karakter Sağ Anahtara gönderildi, ulaşınca tekrar [E] basın!", 3.8f);
                        }
                        else
                        {
                            BildirimGoster("Yardımcı Karakterin Sağ Anahtara ulaşmasını bekleyin!", 2.5f);
                        }
                    }
                    else
                    {
                        // HER İKİ ANAHTAR EŞZAMANLI AKTİF! KÖPRÜYÜ İNDİR!
                        EsZamanliKilidiAc();
                    }
                }
            }
        }

        private void EsZamanliKilidiAc()
        {
            _cozuldu = true;

            if (solAnahtarKolu != null)
            {
                solAnahtarKolu.localRotation = Quaternion.Euler(40f, 0f, 0f);
            }

            Color yesil = new Color(0.15f, 0.90f, 0.30f);
            IsikRengiAyarla(solIsikRenderer, yesil);
            IsikRengiAyarla(sagIsikRenderer, yesil);

            if (bagliKopru != null)
            {
                bagliKopru.Ac();
            }

            BildirimGoster("✓ EŞZAMANLI KİLİT AÇILDI! Askı Kapı (Asma Köprü) Hendeğin Üzerine İniyor!", 5.0f);
            StartCoroutine(YardimciyiAvluyaIlerlet());
        }

        private IEnumerator YardimciyiAvluyaIlerlet()
        {
            yield return new WaitForSeconds(2.6f);
            if (_yardimci != null && !_yardimci.IsDead)
            {
                // Köprü indikten sonra yardımcı karakter de köprüden geçip iç avluya girsin
                _yardimci.GorevNoktasinaGit(new Vector3(2.8f, 0.05f, -3.5f));
                yield return new WaitForSeconds(4.0f);
                if (_yardimci != null) _yardimci.GoreviBitirVeTakipEt();
            }
        }

        private void IsikRengiAyarla(MeshRenderer rend, Color renk)
        {
            if (rend == null) return;
            Material mat = rend.material;
            if (mat != null)
            {
                mat.color = renk;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", renk);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", renk * 2.0f);
            }
        }

        private void BildirimGoster(string mesaj, float sure)
        {
            _bildirimMesaji = mesaj;
            _bildirimBitisZamani = Time.time + sure;
        }

        private bool GTusuBasildiMi()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame) return true;
#endif
            try { if (Input.GetKeyDown(KeyCode.G)) return true; } catch { }
            return false;
        }

        private bool ETusuBasildiMi()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
#endif
            try { if (Input.GetKeyDown(KeyCode.E)) return true; } catch { }
            return false;
        }

        private void OnGUI()
        {
            if (_oyuncu == null) return;
            if (KapiBulmacaPaneli.MinigameAcik || OllamaNpcDiyalogUI.DiyalogAcikMi) return;

            // Sadece 1. Bölümde (Kale dışında / hendek çevresinde) veya bildirim varken göster
            bool hendekBolgesinde = !_cozuldu && (_oyuncu.position.z < -7.5f);
            bool bildirimAktif = Time.time < _bildirimBitisZamani;

            if (!hendekBolgesinde && !bildirimAktif) return;

            if (_panelTex == null)
            {
                _panelTex = new Texture2D(1, 1);
                _panelTex.SetPixel(0, 0, new Color(0.06f, 0.09f, 0.14f, 0.92f));
                _panelTex.Apply();
            }

            if (_panelStil == null)
            {
                _panelStil = new GUIStyle(GUI.skin.box);
                _panelStil.normal.background = _panelTex;
            }

            if (_baslikStil == null)
            {
                _baslikStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }
            _baslikStil.normal.textColor = new Color(1.0f, 0.84f, 0.28f);

            if (_altStil == null)
            {
                _altStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }
            _altStil.normal.textColor = Color.white;

            float olcek = GoruntuKalitesiYoneticisi.KeskinOlcek;
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_baslikStil, Mathf.RoundToInt(14 * olcek));
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_altStil, Mathf.RoundToInt(13 * olcek));

            string ustYazi;
            string altYazi;

            if (bildirimAktif)
            {
                ustYazi = "★ 1. BÖLÜM: KALE DIŞI & HENDEK GEÇİDİ ★";
                altYazi = _bildirimMesaji;
            }
            else
            {
                string sagDurum = _sagAnahtarTutuluyor
                    ? "SAĞ ANAHTAR: HAZIR (TUTULUYOR)"
                    : (_yardimciGonderildi ? "SAĞ ANAHTAR: YOLDAŞ GİDİYOR..." : "SAĞ ANAHTAR: BEKLİYOR ([G] YOLDAŞI GÖNDER)");

                ustYazi = "1. BÖLÜM: HENDEK ÜZERİ ASKI KAPIYI İNDİR (EŞZAMANLI 2 ANAHTAR)";
                altYazi = $"{sagDurum}   |   SOL ANAHTAR: YAKLAŞIP [E] BASIN";
            }

            // Sol üstteki BolumYoneticisi görev kutusuyla ASLA çakışmaması için ekranın orta-alt kısmında konumlandırılır
            float genislik = Mathf.Min(Screen.width - 40f, 660f * olcek);
            float icGenislik = genislik - 24f * olcek;
            float ustH = Mathf.Max(22f * olcek, _baslikStil.CalcHeight(new GUIContent(ustYazi), icGenislik) + 4f);
            float altH = Mathf.Max(24f * olcek, _altStil.CalcHeight(new GUIContent(altYazi), icGenislik) + 6f);
            float yukseklik = ustH + altH + 18f * olcek;

            float kutuX = Mathf.Round((Screen.width - genislik) * 0.5f);
            float kutuY = Mathf.Round(Screen.height - yukseklik - 56f * olcek);
            Rect kutu = new Rect(kutuX, kutuY, genislik, yukseklik);

            GUI.Box(kutu, GUIContent.none, _panelStil);
            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(kutu.x + 12f * olcek, kutu.y + 6f * olcek, icGenislik, ustH), ustYazi, _baslikStil);
            GoruntuKalitesiYoneticisi.KeskinEtiketCiz(new Rect(kutu.x + 12f * olcek, kutu.y + 8f * olcek + ustH, icGenislik, altH), altYazi, _altStil);
        }
    }
}
