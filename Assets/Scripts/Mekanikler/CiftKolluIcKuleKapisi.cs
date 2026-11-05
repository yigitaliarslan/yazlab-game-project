using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Yazlab.Yonetim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// 2. Bölüm sonu (Taret ve baskın düşman askerleri geçildikten sonra) ile 3. Bölüm (LLM Muhafız Komutan Varg)
    /// arasındaki İç Kule Giriş Kapısı ve Çift Açma Kollu Yoldaş Mekanizması.
    ///
    /// - Sahnedeki Sol Açma Kolu (-4.11, 2.21, 21.18) ve Sağ Açma Kolu (4.44, 2.19, 21.16) ile
    ///   bunların durum ışıklarını (Jenerator_DurumIsigi) doğrudan kullanır.
    /// - İç Kule kemerinin altına kilitli "IcKuleGirisKapisi" kapısını yerleştirir.
    /// - Taret ve düşman askerler temizlendikten sonra oyuncu [G] tuşuna bastığında Yoldaş (YardimciKarakter)
    ///   sırasıyla önce 1. Açma Koluna (Sol), ardından 2. Açma Koluna (Sağ) koşup her iki kolu indirir
    ///   ve İç Kule Kapısını açarak oyuncunun LLM Muhafız kısmına çıkmasını sağlar.
    /// </summary>
    public class CiftKolluIcKuleKapisi : MonoBehaviour
    {
        public static CiftKolluIcKuleKapisi Ornek { get; private set; }

        public enum KolGorevDurumu
        {
            Bekliyor,
            BirinciKolaGidiyor,
            BirinciKolIndiriliyor,
            IkinciKolaGidiyor,
            IkinciKolIndiriliyor,
            KapiAcildi
        }

        [Header("İç Kule Giriş Kapısı")]
        public KapiKontrol icKuleKapisi;

        [Header("Sol ve Sağ Açma Kolları & Durum Işıkları")]
        public Transform solKol;
        public Transform sagKol;
        public MeshRenderer solIsikRenderer;
        public MeshRenderer sagIsikRenderer;
        public Light solIsikLight;
        public Light sagIsikLight;

        [Header("Yoldaş Görev Durma Noktaları")]
        public Vector3 solKolDurusNoktasi = new Vector3(-4.11f, 0.05f, 20.15f);
        public Vector3 sagKolDurusNoktasi = new Vector3(4.44f, 0.05f, 20.15f);

        private KolGorevDurumu _durum = KolGorevDurumu.Bekliyor;
        private bool _solKolIndirildi = false;
        private bool _sagKolIndirildi = false;

        private Transform _oyuncu;
        private YardimciKarakter _yardimci;
        private KapiKontrol _birinciBolumKapisi;

        private string _bildirimMesaji = "";
        private float _bildirimBitisZamani = 0f;

        private AudioSource _sesKaynagi;
        private AudioClip _sesKolIndirme;
        private AudioClip _sesKapiAcilma;

        private Texture2D _panelTex;
        private GUIStyle _panelStil;
        private GUIStyle _baslikStil;
        private GUIStyle _altStil;
        private GUIStyle _etiketStil;

        public KolGorevDurumu MevcutDurum => _durum;
        public bool KapiAcildiMi => _durum == KolGorevDurumu.KapiAcildi;
        public bool SolKolIndirildiMi => _solKolIndirildi;
        public bool SagKolIndirildiMi => _sagKolIndirildi;

        private static bool _eventKayitli = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OtomatikKayitVeKurulum()
        {
            if (!_eventKayitli)
            {
                SceneManager.sceneLoaded -= OnSahneYuklendi;
                SceneManager.sceneLoaded += OnSahneYuklendi;
                _eventKayitli = true;
            }
            KurulumuGarantile();
        }

        private static void OnSahneYuklendi(Scene sahne, LoadSceneMode mod)
        {
            KurulumuGarantile();
        }

        /// <summary>
        /// Sahnede İç Kule Giriş Kapısını, Sol/Sağ açma kollarını ve CiftKolluIcKuleKapisi yöneticisini garanti altına alır.
        /// </summary>
        public static CiftKolluIcKuleKapisi KurulumuGarantile()
        {
            if (GameObject.Find("--- HARITA_SEVIYESI ---") == null) return null;

            CiftKolluIcKuleKapisi mevcut = Object.FindAnyObjectByType<CiftKolluIcKuleKapisi>();
            if (mevcut != null)
            {
                mevcut.SahneNesneleriniBaglaVeyaOlustur();
                return mevcut;
            }

            GameObject bolum3 = GameObject.Find("4_Bolum3_IcKaleKulesi_Ve_NpcOdasi");
            GameObject yoneticiObj = new GameObject("IcKule_CiftKolluKapiSistemi");
            if (bolum3 != null)
            {
                yoneticiObj.transform.SetParent(bolum3.transform, false);
            }

            CiftKolluIcKuleKapisi sistem = yoneticiObj.AddComponent<CiftKolluIcKuleKapisi>();
            sistem.SahneNesneleriniBaglaVeyaOlustur();
            return sistem;
        }

        private void Awake()
        {
            Ornek = this;
            _sesKaynagi = GetComponent<AudioSource>();
            if (_sesKaynagi == null)
            {
                _sesKaynagi = gameObject.AddComponent<AudioSource>();
                _sesKaynagi.playOnAwake = false;
                _sesKaynagi.spatialBlend = 0.35f;
            }
            _sesKolIndirme = UretKolSesi();
            _sesKapiAcilma = UretAcilmaSesi();

            SahneNesneleriniBaglaVeyaOlustur();
        }

        private void OnDestroy()
        {
            if (Ornek == this) Ornek = null;
        }

        private void Start()
        {
            SahneNesneleriniBaglaVeyaOlustur();
            ReferanslariBul();
        }

        /// <summary>
        /// Sahnedeki iki açma kolunu (Salter_Kol + Jenerator_DurumIsigi) bağlar,
        /// arka duvar panellerini tamamlar ve İç Kule giriş kemerine kilitli kapıyı (IcKuleGirisKapisi) kurar.
        /// </summary>
        public void SahneNesneleriniBaglaVeyaOlustur()
        {
            GameObject bolum3 = GameObject.Find("4_Bolum3_IcKaleKulesi_Ve_NpcOdasi");
            Transform parentTransform = bolum3 != null ? bolum3.transform : transform;

            // 1. Sahnedeki Sol ve Sağ Açma Kollarını (Salter_Kol) ve Işıklarını (Jenerator_DurumIsigi) bul
            Transform[] tumTransformlar = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach (Transform t in tumTransformlar)
            {
                if (t == null) continue;
                Vector3 p = t.position;

                // İç Kule ön duvarı (Z ≈ 21.16 .. 21.25) üzerindeki kollar ve ışıklar
                if (p.z >= 19.5f && p.z <= 22.5f && Mathf.Abs(p.x) < 8.5f)
                {
                    if (t.name.StartsWith("Salter_Kol"))
                    {
                        if (p.x < 0f && solKol == null) solKol = t;
                        else if (p.x > 0f && sagKol == null) sagKol = t;
                    }
                    else if (t.name.StartsWith("Jenerator_DurumIsigi"))
                    {
                        MeshRenderer mr = t.GetComponent<MeshRenderer>();
                        if (p.x < 0f && solIsikRenderer == null) solIsikRenderer = mr;
                        else if (p.x > 0f && sagIsikRenderer == null) sagIsikRenderer = mr;
                    }
                }
            }

            // Sahnede eksikse varsayılan koordinatlarda oluştur
            if (solKol == null)
            {
                GameObject sk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sk.name = "Salter_Kol";
                sk.transform.SetParent(parentTransform);
                sk.transform.position = new Vector3(-4.11f, 2.21f, 21.18f);
                sk.transform.localScale = new Vector3(0.20f, 0.60f, 0.20f);
                solKol = sk.transform;
            }

            if (solIsikRenderer == null)
            {
                GameObject si = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                si.name = "Jenerator_DurumIsigi";
                si.transform.SetParent(parentTransform);
                si.transform.position = new Vector3(-4.11f, 2.71f, 21.18f);
                si.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                solIsikRenderer = si.GetComponent<MeshRenderer>();
            }

            if (sagKol == null)
            {
                GameObject sgk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sgk.name = "Salter_Kol";
                sgk.transform.SetParent(parentTransform);
                sgk.transform.position = new Vector3(4.44f, 2.19f, 21.16f);
                sgk.transform.localScale = new Vector3(0.20f, 0.60f, 0.20f);
                sagKol = sgk.transform;
            }

            if (sagIsikRenderer == null)
            {
                GameObject sgi = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sgi.name = "Jenerator_DurumIsigi";
                sgi.transform.SetParent(parentTransform);
                sgi.transform.position = new Vector3(4.44f, 2.69f, 21.16f);
                sgi.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
                sagIsikRenderer = sgi.GetComponent<MeshRenderer>();
            }

            // Doğu koridorundaki Jenerator_Ve_GucKaynagi'nin bu kolları ezmesini engelle
            GameObject jeneratorObj = GameObject.Find("Jenerator_Ve_GucKaynagi");
            if (jeneratorObj != null)
            {
                SalterMekanizmasi sm = jeneratorObj.GetComponent<SalterMekanizmasi>();
                if (sm != null)
                {
                    // Jeneratör kendi konumundan (X=19) uzaktaki kolları kontrol etmesin
                    sm.enabled = false;
                }
            }

            // Kolların arkasında duvara monte metal şalter kutusu (havada durmaması ve şık görünmesi için)
            OlusturKolDuvarKutusu(parentTransform, "Sol_AcmaKolu_DuvarKutusu", new Vector3(solKol.position.x, 2.42f, 21.26f));
            OlusturKolDuvarKutusu(parentTransform, "Sag_AcmaKolu_DuvarKutusu", new Vector3(sagKol.position.x, 2.40f, 21.26f));

            // Kolları başlangıçta yukarı/kilitli açıda (-30 derece) ve ışıkları Kırmızı olarak hazırla
            if (!_solKolIndirildi && solKol != null)
            {
                solKol.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }
            if (!_sagKolIndirildi && sagKol != null)
            {
                sagKol.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }

            solIsikLight = IsikKaynagiGarantile(solIsikRenderer);
            sagIsikLight = IsikKaynagiGarantile(sagIsikRenderer);

            if (_durum == KolGorevDurumu.Bekliyor)
            {
                IsikRengiAyarla(solIsikRenderer, solIsikLight, new Color(0.90f, 0.15f, 0.10f));
                IsikRengiAyarla(sagIsikRenderer, sagIsikLight, new Color(0.90f, 0.15f, 0.10f));
            }

            solKolDurusNoktasi = new Vector3(solKol.position.x, 0.05f, 20.15f);
            sagKolDurusNoktasi = new Vector3(sagKol.position.x, 0.05f, 20.15f);

            // 2. İç Kule Giriş Kapısını (IcKuleGirisKapisi) bul veya oluştur (Z = 21.65, Merdivenlerin hemen önü)
            if (icKuleKapisi == null)
            {
                GameObject mevcutKapi = GameObject.Find("IcKuleGirisKapisi");
                if (mevcutKapi != null)
                {
                    icKuleKapisi = mevcutKapi.GetComponent<KapiKontrol>();
                }
            }

            if (icKuleKapisi == null)
            {
                GameObject kapiObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kapiObj.name = "IcKuleGirisKapisi";
                kapiObj.transform.SetParent(parentTransform);
                kapiObj.transform.position = new Vector3(0f, 2.70f, 21.65f);
                kapiObj.transform.localScale = new Vector3(5.95f, 5.40f, 0.42f);

                // FizikselEngelKapisi veya NpcCikisKapisi ile aynı paslı demir kapı materyalini ata
                Material kapiMat = OrnekMateryalBul("FizikselEngelKapisi", new Color(0.42f, 0.30f, 0.22f));
                kapiObj.GetComponent<MeshRenderer>().sharedMaterial = kapiMat;

                NavMeshObstacle obs = kapiObj.AddComponent<NavMeshObstacle>();
                obs.carving = true;

                icKuleKapisi = kapiObj.AddComponent<KapiKontrol>();
                KapiKontrolYapilandir(icKuleKapisi, 5.45f, 2.8f);

                // Kapı üzerine çift kanatlı demir kuşak ve kilit detayları ekle
                SusleIcKuleKapisi(kapiObj.transform);
            }
        }

        private void KapiKontrolYapilandir(KapiKontrol kapi, float acilmaMiktari, float acilmaHizi)
        {
            if (kapi == null) return;
            var tip = typeof(KapiKontrol);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            tip.GetField("kilitliMi", flags)?.SetValue(kapi, true);
            tip.GetField("acilmaMiktari", flags)?.SetValue(kapi, acilmaMiktari);
            tip.GetField("acilmaHizi", flags)?.SetValue(kapi, acilmaHizi);
        }

        private void OlusturKolDuvarKutusu(Transform parent, string ad, Vector3 pos)
        {
            if (parent.Find(ad) != null || GameObject.Find(ad) != null) return;

            GameObject kutu = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kutu.name = ad;
            kutu.transform.SetParent(parent);
            kutu.transform.position = pos;
            kutu.transform.localScale = new Vector3(0.65f, 1.15f, 0.16f);
            Collider c = kutu.GetComponent<Collider>();
            if (c != null) Destroy(c);

            Material koyuMetal = OrnekMateryalBul("Kule_IkinciKat_Platformu", new Color(0.22f, 0.24f, 0.28f));
            kutu.GetComponent<MeshRenderer>().sharedMaterial = koyuMetal;
        }

        private void SusleIcKuleKapisi(Transform kapiTransform)
        {
            if (kapiTransform.Find("Kapi_OrtaDemirKilit") != null) return;

            Material koyuDemir = OrnekMateryalBul("Kule_IkinciKat_Platformu", new Color(0.20f, 0.22f, 0.25f));

            // Çift kanat ayrım dikmesi
            GameObject ortaDikme = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ortaDikme.name = "Kapi_OrtaDemirKilit";
            ortaDikme.transform.SetParent(kapiTransform, false);
            ortaDikme.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            ortaDikme.transform.localScale = new Vector3(0.04f, 0.98f, 0.35f);
            Destroy(ortaDikme.GetComponent<Collider>());
            ortaDikme.GetComponent<MeshRenderer>().sharedMaterial = koyuDemir;

            // Yatay demir kuşaklar
            float[] kusakY = { -0.32f, 0.0f, 0.32f };
            for (int i = 0; i < kusakY.Length; i++)
            {
                GameObject kusak = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kusak.name = $"Kapi_DemirKusak_{i + 1}";
                kusak.transform.SetParent(kapiTransform, false);
                kusak.transform.localPosition = new Vector3(0f, kusakY[i], -0.52f);
                kusak.transform.localScale = new Vector3(0.96f, 0.05f, 0.28f);
                Destroy(kusak.GetComponent<Collider>());
                kusak.GetComponent<MeshRenderer>().sharedMaterial = koyuDemir;
            }
        }

        private Material OrnekMateryalBul(string objeAdi, Color yedekRenk)
        {
            GameObject go = GameObject.Find(objeAdi);
            MeshRenderer mr = go != null ? go.GetComponent<MeshRenderer>() : null;
            if (mr != null && mr.sharedMaterial != null) return mr.sharedMaterial;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(sh) { color = yedekRenk };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", yedekRenk);
            return m;
        }

        private Light IsikKaynagiGarantile(MeshRenderer rend)
        {
            if (rend == null) return null;
            Light l = rend.GetComponent<Light>();
            if (l == null)
            {
                l = rend.gameObject.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 4.5f;
                l.intensity = 2.4f;
                l.shadows = LightShadows.None;
            }
            return l;
        }

        private void IsikRengiAyarla(MeshRenderer rend, Light l, Color renk)
        {
            if (rend != null)
            {
                Material mat = rend.material;
                if (mat != null)
                {
                    mat.color = renk;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", renk);
                    if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", renk * 2.2f);
                }
            }
            if (l != null)
            {
                l.color = renk;
            }
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

            if (_birinciBolumKapisi == null)
            {
                GameObject fk = GameObject.Find("FizikselEngelKapisi");
                if (fk != null) _birinciBolumKapisi = fk.GetComponent<KapiKontrol>();
            }
        }

        /// <summary>
        /// Taretin ve taret patladığında çıkan düşman askerlerin tamamen temizlenip temizlenmediğini kontrol eder.
        /// </summary>
        public bool TaretVeDusmanlarTemizMi()
        {
            // 1. Taret hâlâ hayatta mı?
            TaretKontrol taret = Object.FindAnyObjectByType<TaretKontrol>();
            if (taret != null && !taret.IsDead)
            {
                // Eğer doğu koridorundaki şalterden taret devre dışı bırakılmadıysa taret engeldir
                if (BolumYoneticisi.Ornek == null || BolumYoneticisi.Ornek.MevcutDurum == OyunDurumu.Baslangic || BolumYoneticisi.Ornek.MevcutDurum == OyunDurumu.FizikselEngelAlaninda)
                {
                    return false;
                }
            }

            // 2. Avludaki düşman askerler (TaretBaskin_Askeri vb.) hâlâ hayatta mı?
            DusmanKontrol[] dusmanlar = Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Exclude);
            foreach (var d in dusmanlar)
            {
                if (d == null || d.IsDead) continue;
                if (d is TaretKontrol) continue;
                if (d.gameObject.name == "NPC_Muhafiz") continue;

                // İç Kule kapısı önündeki (Z < 22.5) tüm düşman askerler temizlenmeli
                if (d.transform.position.z < 22.5f)
                {
                    return false;
                }
            }

            return true;
        }

        private void Update()
        {
            if (_durum == KolGorevDurumu.KapiAcildi) return;
            if (KapiBulmacaPaneli.MinigameAcik || OllamaNpcDiyalogUI.DiyalogAcikMi || PrensesKurtarmaGorevi.OyunBittiMi) return;

            ReferanslariBul();
            if (_oyuncu == null) return;

            // Sadece oyuncu hendeği geçtikten sonra (Z >= -2.0) G tuşunu dinle (Hendekteki G tuşu 1. Bölüm köprüsüne aittir)
            if (_oyuncu.position.z >= -2.0f && _durum == KolGorevDurumu.Bekliyor && GTusuBasildiMi())
            {
                KomutGonderVeyaUyar();
            }

            // 1. Kola gidiş kontrolü
            if (_durum == KolGorevDurumu.BirinciKolaGidiyor && _yardimci != null)
            {
                float mesafe = Vector2.Distance(
                    new Vector2(_yardimci.transform.position.x, _yardimci.transform.position.z),
                    new Vector2(solKolDurusNoktasi.x, solKolDurusNoktasi.z));

                if (_yardimci.GorevNoktasindaMi || mesafe <= 0.65f)
                {
                    StartCoroutine(BirinciKoluIndirVeIkinciyeGec());
                }
            }
            // 2. Kola gidiş kontrolü
            else if (_durum == KolGorevDurumu.IkinciKolaGidiyor && _yardimci != null)
            {
                float mesafe = Vector2.Distance(
                    new Vector2(_yardimci.transform.position.x, _yardimci.transform.position.z),
                    new Vector2(sagKolDurusNoktasi.x, sagKolDurusNoktasi.z));

                if (_yardimci.GorevNoktasindaMi || mesafe <= 0.65f)
                {
                    StartCoroutine(IkinciKoluIndirVeKapiyiAc());
                }
            }
        }

        /// <summary>
        /// [G] tuşuna basıldığında veya test/kod tarafından tetiklendiğinde Yoldaşı 2 açma koluna sırayla gönderir.
        /// </summary>
        public void KomutGonderVeyaUyar()
        {
            if (_durum != KolGorevDurumu.Bekliyor) return;

            ReferanslariBul();

            if (_birinciBolumKapisi != null && !_birinciBolumKapisi.AcikMi && _oyuncu != null && _oyuncu.position.z < 14.0f)
            {
                BildirimGoster("Önce Kablo Odasındaki şalteri açıp 1. Kapıdan taret avlusuna geçin!", 3.2f);
                return;
            }

            if (!TaretVeDusmanlarTemizMi())
            {
                BildirimGoster("Önce Otomatik Tareti ve baskın düşman askerlerini etkisiz hale getirin! Ardından [G] ile Yoldaşı gönderin.", 3.8f);
                return;
            }

            if (_yardimci == null)
            {
                BildirimGoster("Yoldaş karakter bulunamadı!", 2.5f);
                return;
            }

            // Yoldaş çok geride kaldıysa avlu girişine yaklaştır ki oyuncuyu bekletmesin
            if (_yardimci.transform.position.z < 10.0f && _oyuncu != null && _oyuncu.position.z >= 13.5f)
            {
                _yardimci.transform.position = new Vector3(-2.0f, 0.05f, 15.5f);
            }

            _durum = KolGorevDurumu.BirinciKolaGidiyor;
            IsikRengiAyarla(solIsikRenderer, solIsikLight, new Color(1.0f, 0.82f, 0.15f)); // Sarı (Hedeflendi)
            _yardimci.GorevNoktasinaGit(solKolDurusNoktasi);
            BildirimGoster("Yoldaş 1. Açma Koluna (Sol Kol) koşuyor... (0/2)", 3.5f);
            Debug.Log("[CiftKolluIcKuleKapisi] [G] basıldı: Yoldaş 1. Açma Koluna (Sol) yönlendirildi.");
        }

        private IEnumerator BirinciKoluIndirVeIkinciyeGec()
        {
            _durum = KolGorevDurumu.BirinciKolIndiriliyor;

            // Yoldaş kola doğru (+Z) dönsün
            if (_yardimci != null)
            {
                _yardimci.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            yield return new WaitForSeconds(0.35f);

            // 1. Kolu indir ve ışığını Yeşile çevir
            _solKolIndirildi = true;
            if (solKol != null)
            {
                solKol.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
            IsikRengiAyarla(solIsikRenderer, solIsikLight, new Color(0.15f, 0.95f, 0.30f));
            if (_sesKaynagi != null && _sesKolIndirme != null) _sesKaynagi.PlayOneShot(_sesKolIndirme);

            BildirimGoster("[1/2] 1. Açma Kolu İndirildi! Yoldaş şimdi 2. Açma Koluna (Sağ Kol) gidiyor...", 3.8f);
            Debug.Log("[CiftKolluIcKuleKapisi] 1. Açma Kolu (Sol) indirildi! Yoldaş 2. Açma Koluna (Sağ) yönlendiriliyor.");

            yield return new WaitForSeconds(0.35f);

            // Şimdi 2. Kola (Sağ Kol) gönder
            _durum = KolGorevDurumu.IkinciKolaGidiyor;
            IsikRengiAyarla(sagIsikRenderer, sagIsikLight, new Color(1.0f, 0.82f, 0.15f)); // Sarı (Hedeflendi)
            if (_yardimci != null)
            {
                _yardimci.GorevNoktasinaGit(sagKolDurusNoktasi);
            }
        }

        private IEnumerator IkinciKoluIndirVeKapiyiAc()
        {
            _durum = KolGorevDurumu.IkinciKolIndiriliyor;

            if (_yardimci != null)
            {
                _yardimci.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            yield return new WaitForSeconds(0.35f);

            // 2. Kolu indir ve ışığını Yeşile çevir
            _sagKolIndirildi = true;
            if (sagKol != null)
            {
                sagKol.localRotation = Quaternion.Euler(45f, 0f, 0f);
            }
            IsikRengiAyarla(sagIsikRenderer, sagIsikLight, new Color(0.15f, 0.95f, 0.30f));
            if (_sesKaynagi != null && _sesKolIndirme != null) _sesKaynagi.PlayOneShot(_sesKolIndirme);

            yield return new WaitForSeconds(0.25f);

            // HER İKİ KOL İNDİRİLDİ -> İÇ KULE KAPISINI AÇ!
            _durum = KolGorevDurumu.KapiAcildi;
            if (icKuleKapisi != null)
            {
                icKuleKapisi.KilidiAc();
                icKuleKapisi.KapiyiAc();
            }
            if (_sesKaynagi != null && _sesKapiAcilma != null) _sesKaynagi.PlayOneShot(_sesKapiAcilma);

            if (BolumYoneticisi.Ornek != null && BolumYoneticisi.Ornek.MevcutDurum != OyunDurumu.SosyalEngelAlaninda &&
                BolumYoneticisi.Ornek.MevcutDurum != OyunDurumu.PrensesKurtarma &&
                BolumYoneticisi.Ornek.MevcutDurum != OyunDurumu.BolumTamamlandi)
            {
                BolumYoneticisi.Ornek.FizikselEngeliAs();
            }

            BildirimGoster("[2/2] HER İKİ AÇMA KOLU İNDİRİLDİ! İç Kule Kapısı Açılıyor - Muhafız Varg'ın Odasına Çıkabilirsiniz!", 5.0f);
            Debug.Log("[CiftKolluIcKuleKapisi] 2. Açma Kolu (Sağ) indirildi! İç Kule Giriş Kapısı açılıyor.");

            yield return new WaitForSeconds(0.45f);
            if (_yardimci != null)
            {
                _yardimci.GoreviBitirVeTakipEt();
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

        private void OnGUI()
        {
            if (_oyuncu == null) return;
            if (KapiBulmacaPaneli.MinigameAcik || OllamaNpcDiyalogUI.DiyalogAcikMi || PrensesKurtarmaGorevi.OyunBittiMi) return;

            bool icKuleAvlusunda = (_durum != KolGorevDurumu.KapiAcildi) &&
                                   (_oyuncu.position.z >= 13.2f && _oyuncu.position.z <= 23.0f);
            bool bildirimAktif = Time.time < _bildirimBitisZamani && !string.IsNullOrEmpty(_bildirimMesaji);

            if (!icKuleAvlusunda && !bildirimAktif) return;

            float olcek = GoruntuKalitesiYoneticisi.KeskinOlcek;

            if (_panelTex == null)
            {
                _panelTex = new Texture2D(1, 1);
                _panelTex.SetPixel(0, 0, new Color(0.05f, 0.08f, 0.13f, 0.93f));
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
            _baslikStil.normal.textColor = new Color(1.0f, 0.85f, 0.28f);
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_baslikStil, Mathf.RoundToInt(14 * olcek));

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
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_altStil, Mathf.RoundToInt(13 * olcek));

            if (_etiketStil == null)
            {
                _etiketStil = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            GoruntuKalitesiYoneticisi.StiliKeskinlestir(_etiketStil, Mathf.RoundToInt(12 * olcek));

            // 1. Sol ve Sağ Açma Kollarının Üzerinde 3B Dünya Etiketi Göster
            if (icKuleAvlusunda && Camera.main != null)
            {
                CizKolEtiketi(solKol, _solKolIndirildi ? "[AÇIK] 1. KOL" : "1. AÇMA KOLU (SOL)", _solKolIndirildi, olcek);
                CizKolEtiketi(sagKol, _sagKolIndirildi ? "[AÇIK] 2. KOL" : "2. AÇMA KOLU (SAĞ)", _sagKolIndirildi, olcek);
            }

            // 2. Alt Orta Görev / Durum Paneli
            string ustYazi = "İÇ KULE GİRİŞ KAPISI - ÇİFT AÇMA KOLLU KİLİT SİSTEMİ";
            string altYazi;

            if (bildirimAktif)
            {
                altYazi = _bildirimMesaji;
            }
            else if (!TaretVeDusmanlarTemizMi())
            {
                altYazi = "İç Kule Kapısını açmak için önce Tareti ve düşman askerleri etkisiz hale getirin!";
            }
            else if (_durum == KolGorevDurumu.Bekliyor)
            {
                altYazi = "[G] YOLDAŞI GÖNDER -> Yoldaş sırasıyla Sol ve Sağ açma kollarını indirip İç Kule Kapısını açar (0/2)";
            }
            else
            {
                int tamamlanan = (_solKolIndirildi ? 1 : 0) + (_sagKolIndirildi ? 1 : 0);
                altYazi = $"Yoldaş açma kollarını indiriyor... ({tamamlanan}/2 Kol Tamamlandı)";
            }

            float genislik = Mathf.Min(Screen.width - 40f, 680f * olcek);
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

        private void CizKolEtiketi(Transform kol, string metin, bool indirildi, float olcek)
        {
            if (kol == null) return;
            Vector3 dunyaNokta = kol.position + Vector3.up * 0.85f;
            if (GoruntuKalitesiYoneticisi.DunyaNoktasiniEkranaCevir(Camera.main, dunyaNokta, out Vector2 ekranPos))
            {
                float bw = 165f * olcek;
                float bh = 26f * olcek;
                Rect r = new Rect(ekranPos.x - bw * 0.5f, ekranPos.y - bh * 0.5f, bw, bh);
                GUI.DrawTexture(r, _panelTex);
                _etiketStil.normal.textColor = indirildi ? new Color(0.25f, 1.0f, 0.45f) : new Color(1.0f, 0.85f, 0.25f);
                GoruntuKalitesiYoneticisi.KeskinEtiketCiz(r, metin, _etiketStil);
            }
        }

        private AudioClip UretKolSesi()
        {
            int sr = 44100;
            int n = (int)(sr * 0.18f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float f = (i < n / 2) ? 260f : 520f;
                float env = 1f - (float)i / n;
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.45f;
            }
            AudioClip c = AudioClip.Create("SesKolIndirme", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }

        private AudioClip UretAcilmaSesi()
        {
            int sr = 44100;
            int n = (int)(sr * 0.45f);
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float oran = (float)i / n;
                float f = Mathf.Lerp(330f, 660f, oran);
                float env = Mathf.Sin(Mathf.PI * oran);
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.45f;
            }
            AudioClip c = AudioClip.Create("SesIcKuleKapiAcilma", n, 1, sr, false);
            c.SetData(data, 0);
            return c;
        }
    }
}
