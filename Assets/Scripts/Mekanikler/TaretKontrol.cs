using System.Collections;
using UnityEngine;
using Yazlab.Yonetim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Vurulabilir alt taret parçaları için hasar iletici bileşen.
    /// Herhangi bir parça vurulduğunda hasarı ana taret bileşenine aktarır.
    /// </summary>
    public class TaretHasarHedefi : MonoBehaviour
    {
        public TaretKontrol anaTaret;

        public void HasarAl(float miktar)
        {
            if (anaTaret != null)
            {
                anaTaret.HasarAl(miktar);
            }
        }
    }

    /// <summary>
    /// Kilitli kapının ardındaki avluda konuşlanan Otomatik Savunma Tareti.
    /// - Canı 380 HP'dir, tüfekle vuruldukça düşman askeri gibi üstünde kırmızı can barı azalır ve sarı parlar.
    /// - Hem oyuncuya hem de yan karaktere (yoldaşa) AYNI ANDA çift namlusuyla ateş eder.
    /// - Taretin silah yüzü ve çift namlusu ateş ederken hedefe tam cepheden dönüktür.
    /// - İmha edildiğinde (canı sıfırlandığında) patlama efektiyle sahneden tamamen yok olur (yok olsun isteği).
    /// </summary>
    public class TaretKontrol : DusmanKontrol
    {
        [Header("Taret Parçaları")]
        public Transform kuleBasi;
        public Transform namluSol;
        public Transform namluSag;
        public Transform namluUcuSol;
        public Transform namluUcuSag;
        public Transform silahYuzuPlate;
        public MeshRenderer sensorIsikRenderer;
        public Light sensorPointLight;

        [Header("Geri Tepme (Recoil) Ayarları")]
        public float recoilMiktari = 0.35f;
        public float recoilHizi = 10.0f;

        [Header("Çift Hedef & Eşzamanlı Ateş")]
        public float hasarOyuncu = 10.0f;
        public float hasarYardimci = 10.0f;

        private Vector3 namluSolOrijinalLocalPos;
        private Vector3 namluSagOrijinalLocalPos;

        private AudioSource taretAudio;
        private AudioClip sesAtes;
        private AudioClip sesPatlama;

        // Çift Namlu İçin Bağımsız Tracer ve Flaşlar
        private LineRenderer taretTracerSol;
        private LineRenderer taretTracerSag;
        private Light namluFlasSol;
        private Light namluFlasSag;
        private Collider taretCollider;

        // Can Barı Dokuları ve Stilleri (Düşman askeriyle birebir aynı)
        private Texture2D taretBeyazTex;
        private GUIStyle taretBaslikStili;
        private GUIStyle taretCanStili;

        // Takip Edilen Hedefler ve Kapı
        private YardimciKarakter yardimciHedef;
        private KapiKontrol kapiKontrolBileseni;
        private bool oyuncuGorunurMu = false;
        private bool yardimciGorunurMu = false;

        protected override void Awake()
        {
            maksimumCan = 380f;
            mevcutCan = maksimumCan;
            gorusMenzili = 55f;
            atesAraligi = 0.36f; // Seri ve agresif çift atış
            atesHasari = 10f;
            donusHizi = 8.5f;   // Çok daha hızlı hedefe kilitlenme
            canBariYukseklikOfseti = 1.15f;
            canBariGenislik = 140f;
            canBariYukseklik = 13f;

            // Parça referanslarını ve silah yüzünü güvenceye al
            OtomatikParcalariBul();

            if (namluSol != null) namluSolOrijinalLocalPos = namluSol.localPosition;
            if (namluSag != null) namluSagOrijinalLocalPos = namluSag.localPosition;

            // Vurulabilir Ana Collider
            taretCollider = GetComponent<Collider>();
            if (taretCollider == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 2.2f, 0f);
                box.size = new Vector3(3.6f, 4.4f, 3.6f);
                taretCollider = box;
            }

            // Alt parçalara da hasar iletici ekle
            ParcalaraHasarIleticiEkle();

            // Ses Kaynağı ve Prosedürel Sesler
            taretAudio = GetComponent<AudioSource>();
            if (taretAudio == null)
            {
                taretAudio = gameObject.AddComponent<AudioSource>();
                taretAudio.playOnAwake = false;
                taretAudio.spatialBlend = 0.85f;
                taretAudio.maxDistance = 60f;
            }
            sesAtes = UretTaretAtesSesi();
            sesPatlama = UretPatlamaSesi();

            // Bağımsız Çift Namlu Tracer & Flaş Sistemleri
            KurCiftTracerVeFlas();
        }

        protected override void Start()
        {
            HedefleriBulVeAyarla();

            // Başlangıçta sensör ışığını kırmızı hazır bekleme durumuna al
            DurumIsiginiAyarla(new Color(0.85f, 0.15f, 0.10f), 1.5f);
        }

        private void HedefleriBulVeAyarla()
        {
            // Oyuncuyu bul
            if (oyuncuHedef == null || oyuncuCaniBileseni == null)
            {
                KarakterHareket kh = Object.FindAnyObjectByType<KarakterHareket>();
                if (kh != null)
                {
                    oyuncuHedef = kh.transform;
                    oyuncuCaniBileseni = kh.GetComponent<OyuncuCani>() ?? kh.gameObject.AddComponent<OyuncuCani>();
                }
                else
                {
                    GameObject pObj = GameObject.FindWithTag("Player");
                    if (pObj != null)
                    {
                        oyuncuHedef = pObj.transform;
                        oyuncuCaniBileseni = pObj.GetComponent<OyuncuCani>() ?? pObj.AddComponent<OyuncuCani>();
                    }
                }
            }

            // Yardımcı Yan Karakteri bul
            if (yardimciHedef == null)
            {
                yardimciHedef = YardimciKarakter.Instance != null ? YardimciKarakter.Instance : Object.FindAnyObjectByType<YardimciKarakter>();
            }

            // 1. Kapıyı (FizikselEngelKapisi) bul
            if (kapiKontrolBileseni == null)
            {
                GameObject kapiObj = GameObject.Find("FizikselEngelKapisi");
                if (kapiObj != null)
                {
                    kapiKontrolBileseni = kapiObj.GetComponent<KapiKontrol>();
                }
            }
        }

        private void ParcalaraHasarIleticiEkle()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                if (c.gameObject != gameObject && c.GetComponent<TaretHasarHedefi>() == null)
                {
                    TaretHasarHedefi h = c.gameObject.AddComponent<TaretHasarHedefi>();
                    h.anaTaret = this;
                }
            }
        }

        private void OtomatikParcalariBul()
        {
            if (kuleBasi == null)
            {
                Transform kb = transform.Find("Taret_KuleBasi");
                if (kb == null)
                {
                    GameObject kbObj = new GameObject("Taret_KuleBasi");
                    kbObj.transform.SetParent(transform);
                    kbObj.transform.localPosition = new Vector3(0f, 3.2f, 0f);
                    kb = kbObj.transform;

                    Transform g = transform.Find("Taret_Govdesi");
                    if (g != null) g.SetParent(kb);
                    Transform n1 = transform.Find("Taret_Namlu1");
                    if (n1 != null) n1.SetParent(kb);
                    Transform n2 = transform.Find("Taret_Namlu2");
                    if (n2 != null) n2.SetParent(kb);
                    Transform isik = transform.Find("Taret_TehditIsigi");
                    if (isik != null) isik.SetParent(kb);
                }
                kuleBasi = kb;
            }

            // Zırhlı Silah Yüzü Plakası (Görsel silah cephesi)
            if (silahYuzuPlate == null && kuleBasi != null)
            {
                Transform sy = kuleBasi.Find("Taret_SilahYuzu");
                if (sy == null)
                {
                    GameObject syObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    syObj.name = "Taret_SilahYuzu";
                    syObj.transform.SetParent(kuleBasi);
                    syObj.transform.localPosition = new Vector3(0f, 0.15f, 0.75f);
                    syObj.transform.localRotation = Quaternion.identity;
                    syObj.transform.localScale = new Vector3(1.5f, 0.55f, 0.45f);

                    Material matMetal = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    matMetal.color = new Color(0.18f, 0.18f, 0.20f);
                    syObj.GetComponent<MeshRenderer>().sharedMaterial = matMetal;
                    sy = syObj.transform;
                }
                silahYuzuPlate = sy;
            }

            // Namlu 1 (Sol Namlu): Ön cepheye (Z = +1.25) doğru hizala
            if (namluSol == null && kuleBasi != null)
            {
                Transform n1 = kuleBasi.Find("Taret_Namlu1");
                if (n1 != null) namluSol = n1;
            }

            // Namlu 2 (Sağ Namlu): Ön cepheye (Z = +1.25) doğru hizala
            if (namluSag == null && kuleBasi != null)
            {
                Transform n2 = kuleBasi.Find("Taret_Namlu2");
                if (n2 != null) namluSag = n2;
            }

            // Namluların yerel konumlarını kesin olarak ön cepheye (+Z) kilitle
            if (namluSol != null)
            {
                namluSol.localPosition = new Vector3(-0.45f, 0.15f, 1.25f);
                namluSol.localRotation = Quaternion.identity;
                namluSol.localScale = new Vector3(0.24f, 0.24f, 2.2f);
                namluSolOrijinalLocalPos = namluSol.localPosition;
            }

            if (namluSag != null)
            {
                namluSag.localPosition = new Vector3(0.45f, 0.15f, 1.25f);
                namluSag.localRotation = Quaternion.identity;
                namluSag.localScale = new Vector3(0.24f, 0.24f, 2.2f);
                namluSagOrijinalLocalPos = namluSag.localPosition;
            }

            if (sensorIsikRenderer == null && kuleBasi != null)
            {
                Transform isik = kuleBasi.Find("Taret_TehditIsigi");
                if (isik != null)
                {
                    sensorIsikRenderer = isik.GetComponent<MeshRenderer>();
                    sensorPointLight = isik.GetComponentInChildren<Light>();
                }
            }

            if (sensorPointLight == null && sensorIsikRenderer != null)
            {
                sensorPointLight = sensorIsikRenderer.GetComponent<Light>();
                if (sensorPointLight == null)
                {
                    sensorPointLight = sensorIsikRenderer.gameObject.AddComponent<Light>();
                    sensorPointLight.type = LightType.Point;
                    sensorPointLight.color = Color.red;
                    sensorPointLight.range = 12f;
                    sensorPointLight.intensity = 3.0f;
                }
            }

            // Namlu uçları (Ateş başlangıç noktaları)
            if (namluUcuSol == null && namluSol != null)
            {
                namluUcuSol = namluSol.Find("NamluUcu");
                if (namluUcuSol == null)
                {
                    GameObject u = new GameObject("NamluUcu");
                    u.transform.SetParent(namluSol);
                    u.transform.localPosition = new Vector3(0f, 0f, 1.15f);
                    u.transform.localRotation = Quaternion.identity;
                    namluUcuSol = u.transform;
                }
            }

            if (namluUcuSag == null && namluSag != null)
            {
                namluUcuSag = namluSag.Find("NamluUcu");
                if (namluUcuSag == null)
                {
                    GameObject u = new GameObject("NamluUcu");
                    u.transform.SetParent(namluSag);
                    u.transform.localPosition = new Vector3(0f, 0f, 1.15f);
                    u.transform.localRotation = Quaternion.identity;
                    namluUcuSag = u.transform;
                }
            }
        }

        protected override void Update()
        {
            if (oldu) return;

            // Hedefleri Güncelle ve Görüş Hatlarını Kontrol Et
            HedefleriGuncelle();

            // Geri Tepme Yaylanma Animasyonu
            if (namluSol != null)
            {
                namluSol.localPosition = Vector3.MoveTowards(namluSol.localPosition, namluSolOrijinalLocalPos, Time.deltaTime * recoilHizi);
                namluSol.localRotation = Quaternion.identity;
            }
            if (namluSag != null)
            {
                namluSag.localPosition = Vector3.MoveTowards(namluSag.localPosition, namluSagOrijinalLocalPos, Time.deltaTime * recoilHizi);
                namluSag.localRotation = Quaternion.identity;
            }

            Transform donenGovde = kuleBasi != null ? kuleBasi : transform;
            Vector3 kuleGoz = donenGovde.position + Vector3.up * 0.2f;

            // DURUM 1: HEM OYUNCU HEM DE YAN KARAKTER GÖRÜNÜYOR!
            // KULLANICI İSTEĞİ: Hem bana hem yan karaktere aynı anda sıkacak & silah yüzü bize doğru olacak
            if (oyuncuGorunurMu && yardimciGorunurMu)
            {
                DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 4.0f);

                Vector3 hedefOyuncu = oyuncuHedef.position + Vector3.up * 1.1f;
                Vector3 hedefYardimci = yardimciHedef.transform.position + Vector3.up * 1.1f;

                // Kule başı ve silah yüzü her iki hedefin tam ortasına kilitlenir
                Vector3 yonOyuncu = (hedefOyuncu - kuleGoz).normalized;
                Vector3 yonYardimci = (hedefYardimci - kuleGoz).normalized;
                Vector3 ortaYon = (yonOyuncu + yonYardimci).normalized;

                if (ortaYon.sqrMagnitude > 0.01f)
                {
                    Quaternion hedefRot = Quaternion.LookRotation(ortaYon);
                    donenGovde.rotation = Quaternion.Slerp(donenGovde.rotation, hedefRot, Time.deltaTime * donusHizi);
                }

                // Ateş Zamanı: AYNI ANDA İKİSİNE BİRDEN ÇİFT NAMLUYLA ATEŞ ET!
                if (Time.time >= sonrakiAtesZamani)
                {
                    sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.02f, 0.03f);
                    AyniAndaCiftAtesEt(hedefOyuncu, hedefYardimci);
                }
            }
            // DURUM 2: SADECE OYUNCU GÖRÜNÜYOR
            else if (oyuncuGorunurMu)
            {
                DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 3.5f);
                Vector3 hedefOyuncu = oyuncuHedef.position + Vector3.up * 1.1f;

                NisanalTekHedefe(hedefOyuncu, kuleGoz, donenGovde);

                if (Time.time >= sonrakiAtesZamani)
                {
                    sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.02f, 0.03f);
                    TekHedefeAtesEt(hedefOyuncu, true);
                }
            }
            // DURUM 3: SADECE YAN KARAKTER GÖRÜNÜYOR
            else if (yardimciGorunurMu)
            {
                DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 3.5f);
                Vector3 hedefYardimci = yardimciHedef.transform.position + Vector3.up * 1.1f;

                NisanalTekHedefe(hedefYardimci, kuleGoz, donenGovde);

                if (Time.time >= sonrakiAtesZamani)
                {
                    sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.02f, 0.03f);
                    TekHedefeAtesEt(hedefYardimci, false);
                }
            }
            // DURUM 4: HİÇBİR HEDEF GÖRÜNMÜYOR (Kapı kapalı veya siper arkasındalar)
            else
            {
                DurumIsiginiAyarla(new Color(0.70f, 0.15f, 0.10f), 1.0f);
                // Bekleme modunda kapıya doğru (güney yönüne, Y=180) silah yüzü dönük dursun
                Quaternion beklemeRot = Quaternion.Euler(0f, 180f, 0f);
                donenGovde.rotation = Quaternion.Slerp(donenGovde.rotation, beklemeRot, Time.deltaTime * 3.0f);
            }
        }

        private void NisanalTekHedefe(Vector3 hedefNokta, Vector3 kuleGoz, Transform donenGovde)
        {
            Vector3 yon = (hedefNokta - kuleGoz).normalized;
            if (yon.sqrMagnitude > 0.01f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(yon);
                donenGovde.rotation = Quaternion.Slerp(donenGovde.rotation, hedefRot, Time.deltaTime * donusHizi);
            }
        }

        private void HedefleriGuncelle()
        {
            HedefleriBulVeAyarla();

            oyuncuGorunurMu = false;
            yardimciGorunurMu = false;

            if (oyuncuHedef != null && (oyuncuCaniBileseni == null || !oyuncuCaniBileseni.IsDead))
            {
                float d = Vector3.Distance(transform.position, oyuncuHedef.position);
                if (d <= gorusMenzili && HedefeGorusVarMi(oyuncuHedef))
                {
                    oyuncuGorunurMu = true;
                }
            }

            if (yardimciHedef != null && !yardimciHedef.IsDead)
            {
                float d = Vector3.Distance(transform.position, yardimciHedef.transform.position);
                if (d <= gorusMenzili && HedefeGorusVarMi(yardimciHedef.transform))
                {
                    yardimciGorunurMu = true;
                }
            }
        }

        /// <summary>
        /// Taretin hedefle (oyuncu veya yan karakter) arasındaki görüş hattını kontrol eder.
        /// Taretin kendi collider'larını atlar, kapalı kapı ve duvarları dikkate alır.
        /// </summary>
        private bool HedefeGorusVarMi(Transform hedef)
        {
            if (hedef == null) return false;

            // Kapı kontrolü: Eğer 1. kapı (FizikselEngelKapisi) sahnedeyse ve hala kapalıysa ateş etme
            if (kapiKontrolBileseni == null)
            {
                GameObject kapiObj = GameObject.Find("FizikselEngelKapisi");
                if (kapiObj != null) kapiKontrolBileseni = kapiObj.GetComponent<KapiKontrol>();
            }

            if (kapiKontrolBileseni != null && !kapiKontrolBileseni.AcikMi)
            {
                return false;
            }

            Vector3 kuleMerkez = kuleBasi != null ? kuleBasi.position : (transform.position + Vector3.up * 3.2f);
            Vector3 goz = kuleMerkez + Vector3.up * 0.35f;

            // Hedef noktaları (kafa, göğüs, bel, bacaklar)
            Vector3[] testNoktalari = new Vector3[]
            {
                hedef.position + Vector3.up * 1.55f, // Kafa
                hedef.position + Vector3.up * 1.15f, // Göğüs
                hedef.position + Vector3.up * 0.75f, // Karın/Bel
                hedef.position + Vector3.up * 0.35f  // Bacaklar
            };

            foreach (var nokta in testNoktalari)
            {
                Vector3 dir = (nokta - goz);
                float dist = dir.magnitude;
                if (dist > gorusMenzili) continue;

                RaycastHit[] hits = Physics.RaycastAll(goz, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
                if (hits == null || hits.Length == 0)
                {
                    // Yol boyunca hiçbir engel yok, görüş doğrudan açık!
                    return true;
                }

                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                bool engelVar = false;
                foreach (var hit in hits)
                {
                    if (hit.transform == null) continue;

                    // 1. Taretin kendi parçalarını (kaide, gövde, namlular vb.) MUTLAKA YOK SAY!
                    if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;

                    // 2. Hedefe (Oyuncu veya Yan Karakter) ulaştıysak görüş KESİNLİKLE AÇIKTIR!
                    bool hedefMi = (hit.transform == hedef || hit.transform.IsChildOf(hedef));
                    if (!hedefMi)
                    {
                        if (hedef == oyuncuHedef)
                        {
                            hedefMi = hit.transform.CompareTag("Player") || hit.collider.GetComponentInParent<KarakterHareket>() != null;
                        }
                        else if (yardimciHedef != null && hedef == yardimciHedef.transform)
                        {
                            hedefMi = hit.collider.GetComponentInParent<YardimciKarakter>() != null;
                        }
                    }

                    if (hedefMi)
                    {
                        return true;
                    }

                    // 3. Eğer kapı açılmışsa yukarı kayan kapıyı atla
                    if (kapiKontrolBileseni != null && (hit.transform == kapiKontrolBileseni.transform || hit.transform.IsChildOf(kapiKontrolBileseni.transform)))
                    {
                        if (kapiKontrolBileseni.AcikMi) continue;
                    }

                    // 4. Trigger olan collider'ları atla
                    if (hit.collider != null && hit.collider.isTrigger) continue;

                    // 5. Arada katı kalın bir duvar veya kapalı kapı var
                    engelVar = true;
                    break;
                }

                if (!engelVar)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// KULLANICI İSTEĞİ: Hem oyuncuya hem de yan karaktere AYNI ANDA çift namluyla sıkar!
        /// </summary>
        private void AyniAndaCiftAtesEt(Vector3 hedefOyuncu, Vector3 hedefYardimci)
        {
            if (oldu) return;

            // 1. Sol Namlu -> OYUNCUYA ATEŞ EDER
            Vector3 solUcu = namluUcuSol != null ? namluUcuSol.position : (namluSol != null ? namluSol.position + namluSol.forward * 1.1f : transform.position + Vector3.up * 3.2f);
            if (namluSol != null)
            {
                namluSol.localPosition = namluSolOrijinalLocalPos - Vector3.forward * recoilMiktari;
            }
            StartCoroutine(NamluFlasi(namluFlasSol, solUcu));
            StartCoroutine(TracerCiz(taretTracerSol, solUcu, hedefOyuncu + Random.insideUnitSphere * 0.15f));

            if (oyuncuCaniBileseni != null && !oyuncuCaniBileseni.IsDead)
            {
                oyuncuCaniBileseni.HasarAl(hasarOyuncu);
            }
            else if (oyuncuHedef != null)
            {
                oyuncuHedef.SendMessage("HasarAl", hasarOyuncu, SendMessageOptions.DontRequireReceiver);
            }

            // 2. Sağ Namlu -> YAN KARAKTERE (YOLDAŞA) ATEŞ EDER
            Vector3 sagUcu = namluUcuSag != null ? namluUcuSag.position : (namluSag != null ? namluSag.position + namluSag.forward * 1.1f : transform.position + Vector3.up * 3.2f);
            if (namluSag != null)
            {
                namluSag.localPosition = namluSagOrijinalLocalPos - Vector3.forward * recoilMiktari;
            }
            StartCoroutine(NamluFlasi(namluFlasSag, sagUcu));
            StartCoroutine(TracerCiz(taretTracerSag, sagUcu, hedefYardimci + Random.insideUnitSphere * 0.15f));

            if (yardimciHedef != null && !yardimciHedef.IsDead)
            {
                yardimciHedef.HasarAl(hasarYardimci);
            }
            else if (yardimciHedef != null)
            {
                yardimciHedef.SendMessage("HasarAl", hasarYardimci, SendMessageOptions.DontRequireReceiver);
            }

            // Çift Ateş Sesi
            if (taretAudio != null && sesAtes != null)
            {
                taretAudio.pitch = Random.Range(0.95f, 1.05f);
                taretAudio.PlayOneShot(sesAtes);
            }
        }

        private void TekHedefeAtesEt(Vector3 hedefNokta, bool oyuncuMu)
        {
            if (oldu) return;

            if (namluSol != null) namluSol.localPosition = namluSolOrijinalLocalPos - Vector3.forward * recoilMiktari;
            if (namluSag != null) namluSag.localPosition = namluSagOrijinalLocalPos - Vector3.forward * recoilMiktari;

            Vector3 solUcu = namluUcuSol != null ? namluUcuSol.position : (namluSol != null ? namluSol.position + namluSol.forward * 1.1f : transform.position + Vector3.up * 3.2f);
            Vector3 sagUcu = namluUcuSag != null ? namluUcuSag.position : (namluSag != null ? namluSag.position + namluSag.forward * 1.1f : transform.position + Vector3.up * 3.2f);

            StartCoroutine(NamluFlasi(namluFlasSol, solUcu));
            StartCoroutine(NamluFlasi(namluFlasSag, sagUcu));

            StartCoroutine(TracerCiz(taretTracerSol, solUcu, hedefNokta + Random.insideUnitSphere * 0.18f));
            StartCoroutine(TracerCiz(taretTracerSag, sagUcu, hedefNokta + Random.insideUnitSphere * 0.18f));

            if (oyuncuMu)
            {
                if (oyuncuCaniBileseni != null && !oyuncuCaniBileseni.IsDead)
                {
                    oyuncuCaniBileseni.HasarAl(atesHasari);
                }
                else if (oyuncuHedef != null)
                {
                    oyuncuHedef.SendMessage("HasarAl", atesHasari, SendMessageOptions.DontRequireReceiver);
                }
            }
            else
            {
                if (yardimciHedef != null && !yardimciHedef.IsDead)
                {
                    yardimciHedef.HasarAl(atesHasari);
                }
                else if (yardimciHedef != null)
                {
                    yardimciHedef.SendMessage("HasarAl", atesHasari, SendMessageOptions.DontRequireReceiver);
                }
            }

            if (taretAudio != null && sesAtes != null)
            {
                taretAudio.pitch = Random.Range(0.95f, 1.05f);
                taretAudio.PlayOneShot(sesAtes);
            }
        }

        public override void HasarAl(float miktar)
        {
            if (oldu) return;

            mevcutCan -= miktar;
            sonHasarZamani = Time.time;

            // Hasar anında sensör flaşı
            StartCoroutine(HasarSensörFlasi());

            if (mevcutCan <= 0f)
            {
                mevcutCan = 0f;
                Olum();
            }
        }

        private IEnumerator HasarSensörFlasi()
        {
            DurumIsiginiAyarla(Color.yellow, 4.5f);
            yield return new WaitForSeconds(0.08f);
            if (!oldu) DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 3.0f);
        }

        /// <summary>
        /// KULLANICI İSTEĞİ: "tareti vurduğumuzda taret yok olsun"
        /// Canı sıfırlandığında patlama sesi/ışığı oluşturup kendini sahneden tamamen yok eder (Destroy).
        /// </summary>
        protected override void Olum()
        {
            if (oldu) return;
            oldu = true;

            // 1. Tüm collider'ları anında kapat
            if (taretCollider != null) taretCollider.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            // 2. Patlama Sesini dünya konumunda çal (Obje silinse bile ses kesilmez)
            if (sesPatlama != null)
            {
                AudioSource.PlayClipAtPoint(sesPatlama, transform.position, 1.0f);
            }

            // 3. Büyük Patlama Efekti (Işık ve enkaz parçaları oluştur)
            UretPatlamaEfekti();

            // 4. Bölüm Yöneticisini bilgilendir (Engel aşıldı görevi tamamlandı)
            BolumYoneticisi by = BolumYoneticisi.Ornek;
            if (by != null)
            {
                by.FizikselEngeliAs();
            }

            Debug.Log("[TaretKontrol] Savunma tareti başarıyla imha edildi ve tamamen yok edildi!");

            // 5. Tareti sahneden tamamen yok et (yok olsun isteği)
            Destroy(gameObject, 0.05f);
        }

        private void UretPatlamaEfekti()
        {
            Vector3 patlamaNoktasi = transform.position + Vector3.up * 2.5f;

            // Anlık devasa patlama ışığı
            GameObject isikObj = new GameObject("TaretPatlamaIsigi");
            isikObj.transform.position = patlamaNoktasi;
            Light patLight = isikObj.AddComponent<Light>();
            patLight.type = LightType.Point;
            patLight.range = 22f;
            patLight.intensity = 8.0f;
            patLight.color = new Color(1.0f, 0.55f, 0.15f);
            Destroy(isikObj, 0.45f);

            // Saçılan metal parçacıkları
            for (int i = 0; i < 6; i++)
            {
                GameObject parcacik = GameObject.CreatePrimitive(PrimitiveType.Cube);
                parcacik.name = "TaretEnkaz";
                parcacik.transform.position = patlamaNoktasi + Random.insideUnitSphere * 0.8f;
                parcacik.transform.localScale = Vector3.one * Random.Range(0.2f, 0.4f);

                Rigidbody rb = parcacik.AddComponent<Rigidbody>();
                rb.mass = 2f;
                rb.AddExplosionForce(450f, patlamaNoktasi, 8f);

                Destroy(parcacik, 2.5f);
            }
        }

        private void DurumIsiginiAyarla(Color renk, float yogunluk)
        {
            if (sensorIsikRenderer != null && sensorIsikRenderer.material != null)
            {
                sensorIsikRenderer.material.color = renk;
                if (sensorIsikRenderer.material.HasProperty("_EmissionColor"))
                {
                    sensorIsikRenderer.material.SetColor("_EmissionColor", renk * yogunluk);
                }
            }

            if (sensorPointLight != null)
            {
                sensorPointLight.color = renk;
                sensorPointLight.intensity = yogunluk;
                sensorPointLight.enabled = yogunluk > 0.01f;
            }
        }

        private void KurCiftTracerVeFlas()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(1.0f, 0.35f, 0.05f, 0.95f);

            // Sol Tracer
            GameObject solObj = new GameObject("TaretTracer_Sol");
            solObj.transform.SetParent(transform);
            taretTracerSol = solObj.AddComponent<LineRenderer>();
            taretTracerSol.startWidth = 0.08f;
            taretTracerSol.endWidth = 0.04f;
            taretTracerSol.positionCount = 2;
            taretTracerSol.material = mat;
            taretTracerSol.enabled = false;

            // Sağ Tracer
            GameObject sagObj = new GameObject("TaretTracer_Sag");
            sagObj.transform.SetParent(transform);
            taretTracerSag = sagObj.AddComponent<LineRenderer>();
            taretTracerSag.startWidth = 0.08f;
            taretTracerSag.endWidth = 0.04f;
            taretTracerSag.positionCount = 2;
            taretTracerSag.material = mat;
            taretTracerSag.enabled = false;

            // Sol Flaş
            GameObject fSol = new GameObject("Flas_Sol");
            fSol.transform.SetParent(transform);
            namluFlasSol = fSol.AddComponent<Light>();
            namluFlasSol.type = LightType.Point;
            namluFlasSol.range = 6f;
            namluFlasSol.color = new Color(1.0f, 0.65f, 0.2f);
            namluFlasSol.intensity = 0f;
            namluFlasSol.enabled = false;

            // Sağ Flaş
            GameObject fSag = new GameObject("Flas_Sag");
            fSag.transform.SetParent(transform);
            namluFlasSag = fSag.AddComponent<Light>();
            namluFlasSag.type = LightType.Point;
            namluFlasSag.range = 6f;
            namluFlasSag.color = new Color(1.0f, 0.65f, 0.2f);
            namluFlasSag.intensity = 0f;
            namluFlasSag.enabled = false;
        }

        private IEnumerator NamluFlasi(Light flas, Vector3 pos)
        {
            if (flas != null)
            {
                flas.transform.position = pos;
                flas.intensity = 4.5f;
                flas.enabled = true;
                yield return new WaitForSeconds(0.04f);
                flas.intensity = 0f;
                flas.enabled = false;
            }
        }

        private IEnumerator TracerCiz(LineRenderer tracer, Vector3 baslangic, Vector3 bitis)
        {
            if (tracer != null)
            {
                tracer.positionCount = 2;
                tracer.SetPosition(0, baslangic);
                tracer.SetPosition(1, bitis);
                tracer.enabled = true;
                yield return new WaitForSeconds(0.06f);
                tracer.enabled = false;
            }
        }

        /// <summary>
        /// KULLANICI İSTEĞİ: Düşman askerdeki gibi üstünde can barı görünür ve vuruldukça azalır!
        /// </summary>
        protected override void OnGUI()
        {
            // Öldüyse çizme
            if (oldu) return;

            // Kapı henüz açılmadıysa can barını gizle
            if (kapiKontrolBileseni != null && !kapiKontrolBileseni.AcikMi) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Can barı tam taret kulesinin kubbesi / sensörünün üstünde süzülsün
            Vector3 dunyaKonumu = (kuleBasi != null ? kuleBasi.position : transform.position + Vector3.up * 3.2f) + Vector3.up * canBariYukseklikOfseti;
            Vector3 ekranNoktasi = cam.WorldToScreenPoint(dunyaKonumu);
            if (ekranNoktasi.z <= 0.1f) return;

            if (ekranNoktasi.x < -80 || ekranNoktasi.x > Screen.width + 80 ||
                ekranNoktasi.y < -80 || ekranNoktasi.y > Screen.height + 80)
            {
                return;
            }

            float guiY = Screen.height - ekranNoktasi.y;
            float guiX = ekranNoktasi.x;

            float mesafe = Vector3.Distance(cam.transform.position, dunyaKonumu);
            float olcek = Mathf.Clamp(15f / Mathf.Max(mesafe, 5f), 0.75f, 1.35f);

            float genislik = canBariGenislik * olcek;
            float yukseklik = canBariYukseklik * olcek;

            float solX = guiX - (genislik * 0.5f);
            float ustY = guiY - yukseklik;

            if (taretBeyazTex == null)
            {
                taretBeyazTex = new Texture2D(1, 1);
                taretBeyazTex.SetPixel(0, 0, Color.white);
                taretBeyazTex.Apply();
            }

            Color eskiRenk = GUI.color;

            // 1. Arka Plan Siyah Çerçeve Paneli (Düşman askeriyle birebir aynı)
            GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
            GUI.DrawTexture(new Rect(solX - 2, ustY - 2, genislik + 4, yukseklik + 4), taretBeyazTex);

            // 2. Boş Bar Zemini (Koyu kırmızı)
            GUI.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);
            GUI.DrawTexture(new Rect(solX, ustY, genislik, yukseklik), taretBeyazTex);

            // 3. Kırmızı Can Dolgusu (Hasar anında sarı/beyaz parlar)
            float oran = Mathf.Clamp01(mevcutCan / maksimumCan);
            float dolguGenislik = genislik * oran;

            bool yeniHasar = (Time.time - sonHasarZamani) < 0.18f;
            Color dolguRengi = yeniHasar ? new Color(1f, 0.9f, 0.35f) : new Color(0.95f, 0.15f, 0.15f);

            GUI.color = dolguRengi;
            GUI.DrawTexture(new Rect(solX, ustY, dolguGenislik, yukseklik), taretBeyazTex);

            // 4. İnce Kenarlık Çerçevesi
            GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            GUI.DrawTexture(new Rect(solX, ustY, genislik, 1), taretBeyazTex);
            GUI.DrawTexture(new Rect(solX, ustY + yukseklik - 1, genislik, 1), taretBeyazTex);
            GUI.DrawTexture(new Rect(solX, ustY, 1, yukseklik), taretBeyazTex);
            GUI.DrawTexture(new Rect(solX + genislik - 1, ustY, 1, yukseklik), taretBeyazTex);

            // 5. "⚔ DÜŞMAN TARET" Başlığı (Barın üzerinde)
            if (taretBaslikStili == null)
            {
                taretBaslikStili = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(10 * olcek),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.LowerCenter
                };
                taretBaslikStili.normal.textColor = new Color(1f, 0.35f, 0.35f, 0.95f);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(solX - 25, ustY - 16 * olcek, genislik + 50, 16 * olcek), "⚔ DÜŞMAN TARET", taretBaslikStili);

            // 6. Can Miktarı Metni: "350 / 380" (Barın ortasında)
            if (taretCanStili == null)
            {
                taretCanStili = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(9 * olcek),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                taretCanStili.normal.textColor = Color.white;
            }
            string canYazisi = $"{Mathf.CeilToInt(mevcutCan)} / {Mathf.CeilToInt(maksimumCan)}";
            GUI.Label(new Rect(solX, ustY, genislik, yukseklik), canYazisi, taretCanStili);

            GUI.color = eskiRenk;
        }

        private AudioClip UretTaretAtesSesi()
        {
            int sampleRate = 44100;
            float duration = 0.16f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(240f, 60f, (float)i / totalSamples);
                float noise = (Random.value * 2f - 1f) * 0.45f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f;
                float envelope = 1f - ((float)i / totalSamples);
                data[i] = (wave + noise) * envelope * 0.7f;
            }

            AudioClip clip = AudioClip.Create("SesTaretAtes", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip UretPatlamaSesi()
        {
            int sampleRate = 44100;
            float duration = 0.65f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(140f, 30f, (float)i / totalSamples);
                float noise = (Random.value * 2f - 1f) * 0.7f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;
                float envelope = Mathf.Pow(1f - ((float)i / totalSamples), 1.5f);
                data[i] = (wave + noise) * envelope * 0.85f;
            }

            AudioClip clip = AudioClip.Create("SesTaretPatlama", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SahneTaretKontrolu()
        {
            // 1. Eski düşman askerleri varsa sahneden kaldır
            GameObject d1 = GameObject.Find("DusmanAsker_1");
            if (d1 != null) Object.Destroy(d1);
            GameObject d2 = GameObject.Find("DusmanAsker_2");
            if (d2 != null) Object.Destroy(d2);

            foreach (var d in Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Include))
            {
                if (d != null && d.gameObject != null && !(d is TaretKontrol) && d.name.Contains("DusmanAsker"))
                {
                    Object.Destroy(d.gameObject);
                }
            }

            // 2. Taret kulesini bul ve TaretKontrol bileşenini güvenceye al
            GameObject taret = GameObject.Find("Otomatik_Taret_Kulesi");
            if (taret != null)
            {
                if (taret.GetComponent<TaretKontrol>() == null)
                {
                    taret.AddComponent<TaretKontrol>();
                }
            }

            // 3. Şalter konsolunu ilk siperin yanına taşı
            GameObject konsol = GameObject.Find("ElektrikSalterKonsolu");
            if (konsol != null)
            {
                if (konsol.transform.position.z > 5f)
                {
                    konsol.transform.position = new Vector3(-3.0f, 0f, -1.5f);
                    konsol.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }
    }
}
