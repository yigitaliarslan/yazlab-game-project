using System.Collections;
using UnityEngine;
using Yazlab.Yonetim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kilitli kapının ardındaki avluda konuşlanan Otomatik Savunma Tareti.
    /// Düşman askeri gibi davranır:
    /// - Canı vardır (200 HP) ve oyuncu/müttefik tarafından vurulabilir.
    /// - Kapı açıldığında oyuncuyu algılar, çift namlusuyla hedefe döner.
    /// - Çift namlulu geri tepme (recoil) animasyonuyla sırayla ateş eder.
    /// - Mermi izi, namlu alevi, ses ve oyuncuya hasar verir.
    /// - Üzerinde dinamik "OTOMATİK SAVUNMA TARETİ" can barı süzülür.
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
        public float recoilMiktari = 0.32f;
        public float recoilHizi = 8.0f;

        private Vector3 namluSolOrijinalLocalPos;
        private Vector3 namluSagOrijinalLocalPos;
        private bool sagNamluSirasi = false;

        private AudioSource taretAudio;
        private AudioClip sesAtes;
        private AudioClip sesPatlama;
        private LineRenderer taretTracer;
        private Light namluFlasIsigi;
        private Collider taretCollider;

        private Texture2D taretBeyazTex;
        private GUIStyle taretBaslikStili;
        private GUIStyle taretCanStili;
        private bool hedefGorunurMu = false;

        protected override void Awake()
        {
            maksimumCan = 200f;
            mevcutCan = maksimumCan;
            gorusMenzili = 45f;
            atesAraligi = 0.42f; // Seri taret atışı
            atesHasari = 9f;
            donusHizi = 5.0f;
            canBariYukseklikOfseti = 2.4f;
            canBariGenislik = 140f;
            canBariYukseklik = 14f;

            // Parça referanslarını otomatik bul / güvenceye al
            OtomatikParcalariBul();

            if (namluSol != null) namluSolOrijinalLocalPos = namluSol.localPosition;
            if (namluSag != null) namluSagOrijinalLocalPos = namluSag.localPosition;

            // Vurulabilir Collider ekle (KarakterAtes raycast'i için)
            taretCollider = GetComponent<Collider>();
            if (taretCollider == null)
            {
                BoxCollider box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 3.2f, 0f);
                box.size = new Vector3(2.6f, 2.2f, 2.6f);
                taretCollider = box;
            }

            // Ses Kaynağı ve Prosedürel Sesler
            taretAudio = GetComponent<AudioSource>();
            if (taretAudio == null)
            {
                taretAudio = gameObject.AddComponent<AudioSource>();
                taretAudio.playOnAwake = false;
                taretAudio.spatialBlend = 0.85f;
                taretAudio.maxDistance = 50f;
            }
            sesAtes = UretTaretAtesSesi();
            sesPatlama = UretPatlamaSesi();

            // Mermi İzi (Tracer)
            KurTracer();
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

            // Başlangıçta sensör ışığını kırmızı hazır bekleme durumuna al
            DurumIsiginiAyarla(new Color(0.85f, 0.15f, 0.10f), 1.5f);
        }

        private void OtomatikParcalariBul()
        {
            if (kuleBasi == null)
            {
                Transform kb = transform.Find("Taret_KuleBasi");
                if (kb == null)
                {
                    // Eğer kule başı yoksa oluştur ve parçaları içine topla
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
                    u.transform.localPosition = new Vector3(0f, 0f, 1.1f);
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
                    u.transform.localPosition = new Vector3(0f, 0f, 1.1f);
                    namluUcuSag = u.transform;
                }
            }
        }

        protected override void Update()
        {
            if (oldu) return;

            // Hedef belirle (Öncelik canlı oyuncu veya müttefik yoldaş)
            HedefSeciminiYap();

            // Geri tepme toparlanma animasyonu (Smooth spring back)
            if (namluSol != null)
            {
                namluSol.localPosition = Vector3.MoveTowards(namluSol.localPosition, namluSolOrijinalLocalPos, Time.deltaTime * recoilHizi);
            }
            if (namluSag != null)
            {
                namluSag.localPosition = Vector3.MoveTowards(namluSag.localPosition, namluSagOrijinalLocalPos, Time.deltaTime * recoilHizi);
            }

            if (aktifHedef != null && hedefGorunurMu)
            {
                // Sensör ışığını aktif tehdit kırmızısına al
                DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 3.0f);

                // Kule başını hedef yönüne doğru yumuşakça çevir (Yaw & Pitch)
                Vector3 hedefNokta = aktifHedef.position + Vector3.up * 1.1f;
                Vector3 kuleGoz = kuleBasi != null ? kuleBasi.position : (transform.position + Vector3.up * 3.2f);
                Vector3 yon = (hedefNokta - kuleGoz);

                if (yon.sqrMagnitude > 0.1f && kuleBasi != null)
                {
                    Quaternion hedefRot = Quaternion.LookRotation(yon);
                    kuleBasi.rotation = Quaternion.Slerp(kuleBasi.rotation, hedefRot, Time.deltaTime * donusHizi);

                    // Namlu kabaca hedefe bakıyorsa ateş et
                    float aciFarki = Quaternion.Angle(kuleBasi.rotation, hedefRot);
                    if (aciFarki < 35f && Time.time >= sonrakiAtesZamani)
                    {
                        sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.04f, 0.06f);
                        AtesEtTaret(hedefNokta);
                    }
                }
            }
            else
            {
                // Hedef görünmüyorsa (ör. kapı kapalı veya oyuncu siperde) bekleme modunda öne baksın
                DurumIsiginiAyarla(new Color(0.70f, 0.15f, 0.10f), 1.0f);
                if (kuleBasi != null)
                {
                    Quaternion beklemeRot = Quaternion.Euler(0f, 180f, 0f);
                    kuleBasi.rotation = Quaternion.Slerp(kuleBasi.rotation, beklemeRot, Time.deltaTime * 2.0f);
                }
            }
        }

        private void HedefSeciminiYap()
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

            aktifHedef = null;
            hedefGorunurMu = false;

            // 1. Oyuncu kontrolü
            if (oyuncuHedef != null && (oyuncuCaniBileseni == null || !oyuncuCaniBileseni.IsDead))
            {
                float mesafe = Vector3.Distance(transform.position, oyuncuHedef.position);
                if (mesafe <= gorusMenzili && TaretHedefiGoruyorMu(oyuncuHedef))
                {
                    aktifHedef = oyuncuHedef;
                    hedefGorunurMu = true;
                    return;
                }
            }

            // 2. Müttefik Karakter kontrolü
            if (YardimciKarakter.Instance != null && !YardimciKarakter.Instance.IsDead)
            {
                Transform yk = YardimciKarakter.Instance.transform;
                float mesafe = Vector3.Distance(transform.position, yk.position);
                if (mesafe <= gorusMenzili && TaretHedefiGoruyorMu(yk))
                {
                    aktifHedef = yk;
                    hedefGorunurMu = true;
                }
            }
        }

        /// <summary>
        /// Taretin hedefle arasındaki görüş hattını kontrol eder.
        /// Eğer aradaki 1. kapı (FizikselEngelKapisi) kapalıysa görüş engellenir!
        /// Kapı açıldığında görüş hattı serbest kalır.
        /// </summary>
        private bool TaretHedefiGoruyorMu(Transform hedef)
        {
            if (hedef == null) return false;

            Vector3 goz = kuleBasi != null ? (kuleBasi.position + Vector3.up * 0.2f) : (transform.position + Vector3.up * 3.4f);
            Vector3 hedefNokta = hedef.position + Vector3.up * 1.1f;
            Vector3 dir = (hedefNokta - goz);
            float dist = dir.magnitude;

            RaycastHit hit;
            if (Physics.Raycast(goz, dir.normalized, out hit, dist))
            {
                if (hit.transform == hedef || hit.transform.IsChildOf(hedef))
                {
                    return true;
                }
            }
            return false;
        }

        private void AtesEtTaret(Vector3 hedefNokta)
        {
            if (oldu) return;

            // Çift namlu arasında sırayla geçiş yap
            sagNamluSirasi = !sagNamluSirasi;
            Transform aktifNamlu = sagNamluSirasi ? namluSag : namluSol;
            Transform aktifNamluUcu = sagNamluSirasi ? namluUcuSag : namluUcuSol;

            Vector3 baslangicNoktasi;
            if (aktifNamluUcu != null)
            {
                baslangicNoktasi = aktifNamluUcu.position;
            }
            else if (aktifNamlu != null)
            {
                baslangicNoktasi = aktifNamlu.position + aktifNamlu.forward * 1.0f;
            }
            else
            {
                baslangicNoktasi = transform.position + Vector3.up * 3.2f + transform.forward * 1.5f;
            }

            // Geri Tepme (Recoil) Animasyonu: Namlu geriye zıplar
            if (aktifNamlu != null)
            {
                Vector3 orijinal = sagNamluSirasi ? namluSagOrijinalLocalPos : namluSolOrijinalLocalPos;
                aktifNamlu.localPosition = orijinal - Vector3.forward * recoilMiktari;
            }

            // Ateş Sesi
            if (taretAudio != null && sesAtes != null)
            {
                taretAudio.pitch = Random.Range(0.92f, 1.08f);
                taretAudio.PlayOneShot(sesAtes);
            }

            // Namlu Işık Flaşı
            StartCoroutine(TaretNamluFlasi(baslangicNoktasi));

            // Hedefe hafif sapma ver (Zorlayıcı ama adil)
            Vector3 vurusNoktasi = hedefNokta + Random.insideUnitSphere * 0.35f;

            // Mermi İzi (Tracer) Çiz
            if (taretTracer != null)
            {
                StartCoroutine(TaretTracerCiz(baslangicNoktasi, vurusNoktasi));
            }

            // Hasar Uygula (Oyuncu veya Müttefik)
            if (aktifHedef == oyuncuHedef && oyuncuCaniBileseni != null && !oyuncuCaniBileseni.IsDead)
            {
                oyuncuCaniBileseni.HasarAl(atesHasari);
            }
            else if (aktifHedef != null)
            {
                aktifHedef.SendMessage("HasarAl", atesHasari, SendMessageOptions.DontRequireReceiver);
            }
        }

        public override void HasarAl(float miktar)
        {
            if (oldu) return;

            mevcutCan -= miktar;
            sonHasarZamani = Time.time;

            // Hasar anında anlık sensör flaşı
            StartCoroutine(HasarSensörFlasi());

            if (mevcutCan <= 0f)
            {
                mevcutCan = 0f;
                Olum();
            }
        }

        private IEnumerator HasarSensörFlasi()
        {
            DurumIsiginiAyarla(Color.yellow, 4.0f);
            yield return new WaitForSeconds(0.08f);
            if (!oldu) DurumIsiginiAyarla(new Color(1f, 0.05f, 0.05f), 2.5f);
        }

        protected override void Olum()
        {
            if (oldu) return;
            oldu = true;

            // Collider'ı kaldır
            if (taretCollider != null) taretCollider.enabled = false;

            // Patlama Sesi
            if (taretAudio != null && sesPatlama != null)
            {
                taretAudio.pitch = 0.9f;
                taretAudio.PlayOneShot(sesPatlama);
            }

            // Sensör ışığını söndür (Devre dışı)
            DurumIsiginiAyarla(new Color(0.12f, 0.12f, 0.12f), 0f);

            // Taret başını eğ / yere indir (Devre dışı kalma pozu)
            StartCoroutine(ImhaAnimasyonu());

            // Bölüm Yöneticisini bilgilendir
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
            Quaternion hedefRot = Quaternion.Euler(38f, kuleBasi.eulerAngles.y, -12f);
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

        private void KurTracer()
        {
            GameObject tracerObj = new GameObject("TaretTracer");
            tracerObj.transform.SetParent(transform);
            taretTracer = tracerObj.AddComponent<LineRenderer>();
            taretTracer.startWidth = 0.08f;
            taretTracer.endWidth = 0.04f;
            taretTracer.positionCount = 2;
            taretTracer.enabled = false;

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            mat.color = new Color(1.0f, 0.25f, 0.05f, 0.95f);
            taretTracer.material = mat;

            // Namlu ışık flaşı
            GameObject flasObj = new GameObject("TaretFlasIsik");
            flasObj.transform.SetParent(transform);
            namluFlasIsigi = flasObj.AddComponent<Light>();
            namluFlasIsigi.type = LightType.Point;
            namluFlasIsigi.range = 7f;
            namluFlasIsigi.intensity = 0f;
            namluFlasIsigi.color = new Color(1.0f, 0.6f, 0.2f);
            namluFlasIsigi.enabled = false;
        }

        private IEnumerator TaretNamluFlasi(Vector3 pos)
        {
            if (namluFlasIsigi != null)
            {
                namluFlasIsigi.transform.position = pos;
                namluFlasIsigi.intensity = 4.5f;
                namluFlasIsigi.enabled = true;
                yield return new WaitForSeconds(0.04f);
                namluFlasIsigi.intensity = 0f;
                namluFlasIsigi.enabled = false;
            }
        }

        private IEnumerator TaretTracerCiz(Vector3 baslangic, Vector3 bitis)
        {
            taretTracer.SetPosition(0, baslangic);
            taretTracer.SetPosition(1, bitis);
            taretTracer.enabled = true;
            yield return new WaitForSeconds(0.05f);
            taretTracer.enabled = false;
        }

        protected override void OnGUI()
        {
            // Taret imha edildiyse veya oyuncu tareti göremiyorsa (ör. kapı kapalıyken) bar çizilmez
            if (oldu || !hedefGorunurMu) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 dunyaKonumu = (kuleBasi != null ? kuleBasi.position : transform.position) + Vector3.up * canBariYukseklikOfseti;
            Vector3 ekranNoktasi = cam.WorldToScreenPoint(dunyaKonumu);
            if (ekranNoktasi.z <= 0.1f) return;

            float guiY = Screen.height - ekranNoktasi.y;
            float guiX = ekranNoktasi.x;

            float mesafe = Vector3.Distance(cam.transform.position, dunyaKonumu);
            float olcek = Mathf.Clamp(18f / Mathf.Max(mesafe, 6f), 0.75f, 1.4f);

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

            Color eski = GUI.color;

            // 1. Dış Koyu Çerçeve
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(solX - 3, ustY - 3, genislik + 6, yukseklik + 6), taretBeyazTex);

            // 2. Koyu Zemin
            GUI.color = new Color(0.25f, 0.06f, 0.06f, 0.9f);
            GUI.DrawTexture(new Rect(solX, ustY, genislik, yukseklik), taretBeyazTex);

            // 3. Can Dolgusu (Hasar anında sarı parlar)
            float oran = Mathf.Clamp01(mevcutCan / maksimumCan);
            bool yeniHasar = (Time.time - sonHasarZamani) < 0.14f;
            Color dolguRengi = yeniHasar ? new Color(1f, 0.92f, 0.35f) : new Color(0.95f, 0.18f, 0.18f);

            GUI.color = dolguRengi;
            GUI.DrawTexture(new Rect(solX, ustY, genislik * oran, yukseklik), taretBeyazTex);

            // 4. Kenarlık
            GUI.color = new Color(0.85f, 0.25f, 0.25f, 0.8f);
            GUI.DrawTexture(new Rect(solX, ustY, genislik, 1), taretBeyazTex);
            GUI.DrawTexture(new Rect(solX, ustY + yukseklik - 1, genislik, 1), taretBeyazTex);
            GUI.DrawTexture(new Rect(solX, ustY, 1, yukseklik), taretBeyazTex);
            GUI.DrawTexture(new Rect(solX + genislik - 1, ustY, 1, yukseklik), taretBeyazTex);

            // 5. Başlık
            if (taretBaslikStili == null)
            {
                taretBaslikStili = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(11 * olcek),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.LowerCenter
                };
                taretBaslikStili.normal.textColor = new Color(1f, 0.35f, 0.35f);
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(solX - 30, ustY - 18 * olcek, genislik + 60, 18 * olcek), "🚨 OTOMATİK SAVUNMA TARETİ", taretBaslikStili);

            // 6. Can Değeri
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
            GUI.Label(new Rect(solX, ustY - 1, genislik, yukseklik), $"{Mathf.CeilToInt(mevcutCan)} / {maksimumCan}", taretCanStili);

            GUI.color = eski;
        }

        private AudioClip UretTaretAtesSesi()
        {
            int sampleRate = 44100;
            float duration = 0.14f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(220f, 65f, (float)i / totalSamples);
                float noise = (Random.value * 2f - 1f) * 0.45f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f;
                float envelope = 1f - ((float)i / totalSamples);
                data[i] = (wave + noise) * envelope * 0.65f;
            }

            AudioClip clip = AudioClip.Create("SesTaretAtes", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip UretPatlamaSesi()
        {
            int sampleRate = 44100;
            float duration = 0.6f;
            int totalSamples = (int)(sampleRate * duration);
            float[] data = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(140f, 35f, (float)i / totalSamples);
                float noise = (Random.value * 2f - 1f) * 0.7f;
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.3f;
                float envelope = Mathf.Pow(1f - ((float)i / totalSamples), 1.5f);
                data[i] = (wave + noise) * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create("SesTaretPatlama", totalSamples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SahneTaretKontrolu()
        {
            // 1. Eski düşman askerleri varsa sahneden kaldır (kullanıcı isteği)
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
                // Eğer hala kapının önündeyse (Z > 5f), ilk sipere al
                if (konsol.transform.position.z > 5f)
                {
                    konsol.transform.position = new Vector3(-3.0f, 0f, -1.5f);
                    konsol.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }
    }
}
