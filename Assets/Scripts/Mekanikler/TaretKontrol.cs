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
    /// Düşman askeri ile birebir aynı kurallarla çalışır:
    /// - Canı vardır (200 HP). Oyuncu tüfeğiyle vurdukça üzerinde düşman askeriyle
    ///   aynı stilde can barı görünür ve hasar aldıkça barı azalır / sarı parlar.
    /// - Hem oyuncuya hem de yan karaktere (yoldaşa) AYNI ANDA çift namlusuyla ateş eder.
    /// - Çift namlulu geri tepme (recoil) animasyonu, mermi izleri, namlu alevleri ve hasar verir.
    /// - İmha edildiğinde patlama ve baş eğme animasyonuyla devre dışı kalır.
    /// </summary>
    public class TaretKontrol : DusmanKontrol
    {
        [Header("Taret Parçaları")]
        public Transform kuleBasi;
        public Transform namluSol;
        public Transform namluSag;
        public Transform namluUcuSol;
        public Transform namluUcuSag;
        public MeshRenderer sensorIsikRenderer;
        public Light sensorPointLight;

        [Header("Geri Tepme (Recoil) Ayarları")]
        public float recoilMiktari = 0.35f;
        public float recoilHizi = 9.0f;

        [Header("Çift Hedef & Eşzamanlı Ateş")]
        public float hasarOyuncu = 8.0f;
        public float hasarYardimci = 8.0f;

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

        // Takip Edilen Hedefler
        private YardimciKarakter yardimciHedef;
        private bool oyuncuGorunurMu = false;
        private bool yardimciGorunurMu = false;
        private bool oyuncuKameraBeniGoruyorMu = false;
        private float sonKameraKontrolZamani = 0f;

        protected override void Awake()
        {
            maksimumCan = 200f;
            mevcutCan = maksimumCan;
            gorusMenzili = 50f;
            atesAraligi = 0.55f;
            atesHasari = 8f;
            donusHizi = 5.5f;
            canBariYukseklikOfseti = 1.15f;
            canBariGenislik = 125f;
            canBariYukseklik = 12f;

            // Parça referanslarını bul ve hiyerarşiyi düzenle
            OtomatikParcalariBul();

            if (namluSol != null) namluSolOrijinalLocalPos = namluSol.localPosition;
            if (namluSag != null) namluSagOrijinalLocalPos = namluSag.localPosition;

            // Vurulabilir Ana Collider: Tüm kuleyi (kaide + baş + namlular) kapsasın
            taretCollider = GetComponent<Collider>();
            if (taretCollider == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 2.2f, 0f);
                box.size = new Vector3(3.6f, 4.4f, 3.6f);
                taretCollider = box;
            }

            // Alt parçalara da HasarHedefi ekle (mermi kaideye veya namluya çarparsa da hasar alsın)
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
            // Oyuncuyu bul
            KarakterHareket p = Object.FindAnyObjectByType<KarakterHareket>();
            if (p != null)
            {
                oyuncuHedef = p.transform;
                oyuncuCaniBileseni = p.GetComponent<OyuncuCani>();
            }

            // Yardımcı Yan Karakteri bul
            yardimciHedef = YardimciKarakter.Instance != null ? YardimciKarakter.Instance : Object.FindAnyObjectByType<YardimciKarakter>();

            // Başlangıçta sensör ışığını kırmızı hazır bekleme durumuna al
            DurumIsiginiAyarla(new Color(0.85f, 0.15f, 0.10f), 1.5f);
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

            if (namluSol == null && kuleBasi != null)
            {
                Transform n1 = kuleBasi.Find("Taret_Namlu1");
                if (n1 != null) namluSol = n1;
            }

            if (namluSag == null && kuleBasi != null)
            {
                Transform n2 = kuleBasi.Find("Taret_Namlu2");
                if (n2 != null) namluSag = n2;
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
                    sensorPointLight.range = 10f;
                    sensorPointLight.intensity = 2.5f;
                }
            }

            // Namlu uçları
            if (namluUcuSol == null && namluSol != null)
            {
                namluUcuSol = namluSol.Find("NamluUcu");
                if (namluUcuSol == null)
                {
                    GameObject u = new GameObject("NamluUcu");
                    u.transform.SetParent(namluSol);
                    u.transform.localPosition = new Vector3(0f, 0f, 1.15f);
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
                    namluUcuSag = u.transform;
                }
            }
        }

        protected override void Update()
        {
            if (oldu) return;

            // Kamera görüş kontrolü (Can barı için periyodik kontrol)
            if (Time.time >= sonKameraKontrolZamani + 0.04f)
            {
                sonKameraKontrolZamani = Time.time;
                oyuncuKameraBeniGoruyorMu = OyuncuKameraBeniGoruyorMu();
            }

            // Hedefleri Güncelle ve Görüş Hatlarını Kontrol Et
            HedefleriGuncelle();

            // Geri Tepme Yaylanma Animasyonu (Her iki namlu da geriden orijinal yerine yumuşakça döner)
            if (namluSol != null)
            {
                namluSol.localPosition = Vector3.MoveTowards(namluSol.localPosition, namluSolOrijinalLocalPos, Time.deltaTime * recoilHizi);
            }
            if (namluSag != null)
            {
                namluSag.localPosition = Vector3.MoveTowards(namluSag.localPosition, namluSagOrijinalLocalPos, Time.deltaTime * recoilHizi);
            }

            Vector3 kuleGoz = kuleBasi != null ? kuleBasi.position : (transform.position + Vector3.up * 3.2f);

            // DURUM 1: HEM OYUNCU HEM DE YAN KARAKTER GÖRÜNÜYOR!
            // Kullanıcı isteği: "hem beni hem yan karaktere aynı anda sıkacak ateş edecek bu taret"
            if (oyuncuGorunurMu && yardimciGorunurMu)
            {
                DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 3.5f);

                Vector3 hedefOyuncu = oyuncuHedef.position + Vector3.up * 1.1f;
                Vector3 hedefYardimci = yardimciHedef.transform.position + Vector3.up * 1.1f;

                // Kule başı ikisinin orta yönüne bakar
                Vector3 yonOyuncu = (hedefOyuncu - kuleGoz).normalized;
                Vector3 yonYardimci = (hedefYardimci - kuleGoz).normalized;
                Vector3 ortaYon = (yonOyuncu + yonYardimci).normalized;

                if (ortaYon.sqrMagnitude > 0.05f && kuleBasi != null)
                {
                    Quaternion hedefRot = Quaternion.LookRotation(ortaYon);
                    kuleBasi.rotation = Quaternion.Slerp(kuleBasi.rotation, hedefRot, Time.deltaTime * donusHizi);
                }

                // Sol namlu Oyuncuya, Sağ namlu Yan Karaktere nişan alır
                if (namluSol != null)
                {
                    Vector3 yon = (hedefOyuncu - namluSol.position).normalized;
                    if (yon.sqrMagnitude > 0.05f) namluSol.rotation = Quaternion.Slerp(namluSol.rotation, Quaternion.LookRotation(yon), Time.deltaTime * donusHizi * 1.5f);
                }
                if (namluSag != null)
                {
                    Vector3 yon = (hedefYardimci - namluSag.position).normalized;
                    if (yon.sqrMagnitude > 0.05f) namluSag.rotation = Quaternion.Slerp(namluSag.rotation, Quaternion.LookRotation(yon), Time.deltaTime * donusHizi * 1.5f);
                }

                // Ateş Zamanı: AYNI ANDA İKİSİNE BİRDEN ATEŞ ET!
                if (Time.time >= sonrakiAtesZamani)
                {
                    sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.04f, 0.05f);
                    AyniAndaCiftAtesEt(hedefOyuncu, hedefYardimci);
                }
            }
            // DURUM 2: SADECE OYUNCU GÖRÜNÜYOR
            else if (oyuncuGorunurMu)
            {
                DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 3.0f);
                Vector3 hedefOyuncu = oyuncuHedef.position + Vector3.up * 1.1f;

                NisanalTekHedefe(hedefOyuncu, kuleGoz);

                if (Time.time >= sonrakiAtesZamani)
                {
                    sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.04f, 0.05f);
                    TekHedefeAtesEt(hedefOyuncu, true);
                }
            }
            // DURUM 3: SADECE YAN KARAKTER GÖRÜNÜYOR
            else if (yardimciGorunurMu)
            {
                DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 3.0f);
                Vector3 hedefYardimci = yardimciHedef.transform.position + Vector3.up * 1.1f;

                NisanalTekHedefe(hedefYardimci, kuleGoz);

                if (Time.time >= sonrakiAtesZamani)
                {
                    sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.04f, 0.05f);
                    TekHedefeAtesEt(hedefYardimci, false);
                }
            }
            // DURUM 4: HİÇBİR HEDEF GÖRÜNMÜYOR (Kapı kapalı veya siper arkasındalar)
            else
            {
                DurumIsiginiAyarla(new Color(0.70f, 0.15f, 0.10f), 1.0f);
                if (kuleBasi != null)
                {
                    // Bekleme modunda kapıya doğru (güney yönüne) baksın
                    Quaternion beklemeRot = Quaternion.Euler(0f, 180f, 0f);
                    kuleBasi.rotation = Quaternion.Slerp(kuleBasi.rotation, beklemeRot, Time.deltaTime * 2.0f);
                }
            }
        }

        private void NisanalTekHedefe(Vector3 hedefNokta, Vector3 kuleGoz)
        {
            Vector3 yon = (hedefNokta - kuleGoz).normalized;
            if (yon.sqrMagnitude > 0.05f && kuleBasi != null)
            {
                Quaternion hedefRot = Quaternion.LookRotation(yon);
                kuleBasi.rotation = Quaternion.Slerp(kuleBasi.rotation, hedefRot, Time.deltaTime * donusHizi);
            }
        }

        private void HedefleriGuncelle()
        {
            if (oyuncuHedef == null)
            {
                KarakterHareket p = Object.FindAnyObjectByType<KarakterHareket>();
                if (p != null)
                {
                    oyuncuHedef = p.transform;
                    oyuncuCaniBileseni = p.GetComponent<OyuncuCani>();
                }
            }

            if (yardimciHedef == null)
            {
                yardimciHedef = YardimciKarakter.Instance != null ? YardimciKarakter.Instance : Object.FindAnyObjectByType<YardimciKarakter>();
            }

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
        /// Taretin hedefle (oyuncu veya yan karakter) arasındaki görüş çizgisini test eder.
        /// Kapalı kapı (FizikselEngelKapisi) veya sur varsa görüş engellenir.
        /// </summary>
        private bool HedefeGorusVarMi(Transform hedef)
        {
            if (hedef == null) return false;

            Vector3 goz = kuleBasi != null ? (kuleBasi.position + Vector3.up * 0.2f) : (transform.position + Vector3.up * 3.4f);

            // Birden fazla noktadan test et (kafa, gövde, bacaklar) siper durumları için
            Vector3[] kontrolNoktalari = new Vector3[]
            {
                hedef.position + Vector3.up * 1.1f, // Gövde
                hedef.position + Vector3.up * 1.6f, // Kafa
                hedef.position + Vector3.up * 0.5f  // Siper altı
            };

            foreach (var nokta in kontrolNoktalari)
            {
                Vector3 dir = (nokta - goz);
                float dist = dir.magnitude;

                RaycastHit hit;
                if (Physics.Raycast(goz, dir.normalized, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform == hedef || hit.transform.IsChildOf(hedef))
                    {
                        return true;
                    }
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
            Vector3 solUcu = namluUcuSol != null ? namluUcuSol.position : (namluSol != null ? namluSol.position + namluSol.forward : transform.position + Vector3.up * 3.2f);
            if (namluSol != null)
            {
                namluSol.localPosition = namluSolOrijinalLocalPos - Vector3.forward * recoilMiktari;
            }
            StartCoroutine(NamluFlasi(namluFlasSol, solUcu));
            StartCoroutine(TracerCiz(taretTracerSol, solUcu, hedefOyuncu + Random.insideUnitSphere * 0.2f));

            if (oyuncuCaniBileseni != null && !oyuncuCaniBileseni.IsDead)
            {
                oyuncuCaniBileseni.HasarAl(hasarOyuncu);
            }

            // 2. Sağ Namlu -> YAN KARAKTERE (YOLDAŞA) ATEŞ EDER
            Vector3 sagUcu = namluUcuSag != null ? namluUcuSag.position : (namluSag != null ? namluSag.position + namluSag.forward : transform.position + Vector3.up * 3.2f);
            if (namluSag != null)
            {
                namluSag.localPosition = namluSagOrijinalLocalPos - Vector3.forward * recoilMiktari;
            }
            StartCoroutine(NamluFlasi(namluFlasSag, sagUcu));
            StartCoroutine(TracerCiz(taretTracerSag, sagUcu, hedefYardimci + Random.insideUnitSphere * 0.2f));

            if (yardimciHedef != null && !yardimciHedef.IsDead)
            {
                yardimciHedef.HasarAl(hasarYardimci);
            }

            // Çift Ateş Sesi
            if (taretAudio != null && sesAtes != null)
            {
                taretAudio.pitch = Random.Range(0.94f, 1.06f);
                taretAudio.PlayOneShot(sesAtes);
            }
        }

        /// <summary>
        /// Yalnızca bir hedef sahnede aktifse her iki namluyla o hedefe ateş eder.
        /// </summary>
        private void TekHedefeAtesEt(Vector3 hedefNokta, bool oyuncuMu)
        {
            if (oldu) return;

            // Her iki namluda da recoil
            if (namluSol != null) namluSol.localPosition = namluSolOrijinalLocalPos - Vector3.forward * recoilMiktari;
            if (namluSag != null) namluSag.localPosition = namluSagOrijinalLocalPos - Vector3.forward * recoilMiktari;

            Vector3 solUcu = namluUcuSol != null ? namluUcuSol.position : transform.position + Vector3.up * 3.2f;
            Vector3 sagUcu = namluUcuSag != null ? namluUcuSag.position : transform.position + Vector3.up * 3.2f;

            StartCoroutine(NamluFlasi(namluFlasSol, solUcu));
            StartCoroutine(NamluFlasi(namluFlasSag, sagUcu));

            StartCoroutine(TracerCiz(taretTracerSol, solUcu, hedefNokta + Random.insideUnitSphere * 0.25f));
            StartCoroutine(TracerCiz(taretTracerSag, sagUcu, hedefNokta + Random.insideUnitSphere * 0.25f));

            if (oyuncuMu && oyuncuCaniBileseni != null && !oyuncuCaniBileseni.IsDead)
            {
                oyuncuCaniBileseni.HasarAl(atesHasari);
            }
            else if (!oyuncuMu && yardimciHedef != null && !yardimciHedef.IsDead)
            {
                yardimciHedef.HasarAl(atesHasari);
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

            // Hasar flaşı
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

        protected override void Olum()
        {
            if (oldu) return;
            oldu = true;

            // Collider'ları devre dışı bırak
            if (taretCollider != null) taretCollider.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }

            // Patlama Sesi
            if (taretAudio != null && sesPatlama != null)
            {
                taretAudio.pitch = 0.9f;
                taretAudio.PlayOneShot(sesPatlama);
            }

            // Sensör ışığını kapat
            DurumIsiginiAyarla(new Color(0.1f, 0.1f, 0.1f), 0f);

            // Başını öne/yere doğru eğme (imha pozu)
            StartCoroutine(ImhaAnimasyonu());

            // Bölüm görevini tamamla
            BolumYoneticisi by = BolumYoneticisi.Ornek;
            if (by != null)
            {
                by.FizikselEngeliAs();
            }

            Debug.Log("[TaretKontrol] Savunma tareti başarıyla imha edildi!");
        }

        private IEnumerator ImhaAnimasyonu()
        {
            if (kuleBasi == null) yield break;

            Quaternion baslangicRot = kuleBasi.rotation;
            Quaternion hedefRot = Quaternion.Euler(38f, kuleBasi.eulerAngles.y, -10f);
            float sure = 0f;

            while (sure < 1.2f)
            {
                sure += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, sure / 1.2f);
                kuleBasi.rotation = Quaternion.Slerp(baslangicRot, hedefRot, t);
                yield return null;
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
            taretTracerSol.startWidth = 0.07f;
            taretTracerSol.endWidth = 0.03f;
            taretTracerSol.positionCount = 2;
            taretTracerSol.material = mat;
            taretTracerSol.enabled = false;

            // Sağ Tracer
            GameObject sagObj = new GameObject("TaretTracer_Sag");
            sagObj.transform.SetParent(transform);
            taretTracerSag = sagObj.AddComponent<LineRenderer>();
            taretTracerSag.startWidth = 0.07f;
            taretTracerSag.endWidth = 0.03f;
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
                flas.intensity = 4.0f;
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
                tracer.SetPosition(0, baslangic);
                tracer.SetPosition(1, bitis);
                tracer.enabled = true;
                yield return new WaitForSeconds(0.05f);
                tracer.enabled = false;
            }
        }

        /// <summary>
        /// Kameranın (oyuncunun) tareti doğrudan görüp görmediğini kontrol eder.
        /// Kapı kapalıyken veya arkasındayken can barı gizlenir; kapı açıldığında görünür.
        /// </summary>
        private bool OyuncuKameraBeniGoruyorMu()
        {
            Camera cam = Camera.main;
            if (cam == null) return false;

            Vector3 camPos = cam.transform.position;
            Vector3 taretNoktasi = (kuleBasi != null ? kuleBasi.position : transform.position + Vector3.up * 3.2f) + Vector3.up * 0.4f;

            // 1. Ekran önünde mi kontrol et
            Vector3 ekranNoktasi = cam.WorldToScreenPoint(taretNoktasi);
            if (ekranNoktasi.z <= 0.1f) return false;

            if (ekranNoktasi.x < -80 || ekranNoktasi.x > Screen.width + 80 ||
                ekranNoktasi.y < -80 || ekranNoktasi.y > Screen.height + 80)
            {
                return false;
            }

            // Mesafe kontrolü (60 metreden uzakta gizle)
            float dist = Vector3.Distance(camPos, taretNoktasi);
            if (dist > 60f) return false;

            // 2. Raycast ile görüş kontrolü (Kapalı kapı veya kalın duvar var mı?)
            Vector3 dir = (taretNoktasi - camPos);
            RaycastHit[] hits = Physics.RaycastAll(camPos, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return true;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.transform == null) continue;
                if (oyuncuHedef != null && (hit.transform == oyuncuHedef || hit.transform.IsChildOf(oyuncuHedef))) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) return true;

                // Başka bir katı nesneye çarptıysa görüş engellidir
                return false;
            }

            return true;
        }

        /// <summary>
        /// KULLANICI İSTEĞİ: Düşman askerdeki gibi üstünde can barı görünür ve vuruldukça azalır!
        /// </summary>
        protected override void OnGUI()
        {
            // Öldüyse veya kapalı kapı arkasındaysa çizme
            if (oldu || !oyuncuKameraBeniGoruyorMu) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Can barı tam taret kulesinin kubbesi / sensörünün üstünde süzülsün
            Vector3 dunyaKonumu = (kuleBasi != null ? kuleBasi.position : transform.position + Vector3.up * 3.2f) + Vector3.up * canBariYukseklikOfseti;
            Vector3 ekranNoktasi = cam.WorldToScreenPoint(dunyaKonumu);
            if (ekranNoktasi.z <= 0.1f) return;

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

            // 6. Can Miktarı Metni: "175 / 200" (Barın ortasında)
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
