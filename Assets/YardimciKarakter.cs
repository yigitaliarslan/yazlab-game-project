using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncunun müttefiki olan asker kontrolcüsü.
/// - Canı sonsuzdur (ölmez ve HUD'da can barı gösterilmez).
/// - Sahnede düşman gördüğünde onlara ateş açar ve hasar verir.
/// - Co-op bulmacalarda (ör. 2 Anahtarlı Asma Köprü) emredilen görev noktasına koşup anahtarı tutar.
/// - Serbest durumdayken oyuncuyu akıcı şekilde yürüme/koşma animasyonuyla takip eder.
/// </summary>
public class YardimciKarakter : MonoBehaviour
{
    public static YardimciKarakter Instance { get; private set; }

    public enum DonusModu
    {
        OyuncuyaBak,
        OyuncununBaktigiYon
    }

    [Header("Sağlık Ayarları (Sonsuz Can)")]
    public float maksimumCan = 999999f;
    public float mevcutCan = 999999f;

    [Header("Dönüş ve Takip")]
    public DonusModu donusModu = DonusModu.OyuncuyaBak;
    public float donusHizi = 10.0f;
    public float hareketHizi = 5.5f;
    public float takipMesafesi = 2.5f;
    public Transform hedef;

    [Header("Ateş ve Çatışma Ayarları")]
    [Tooltip("Düşmanı görebileceği ve ateş edebileceği maksimum menzil.")]
    public float atesMenzili = 44f;

    [Tooltip("İki atış arasındaki süre.")]
    public float atesAraligi = 0.62f;

    [Tooltip("Düşmana her vuruşunda vereceği hasar.")]
    public float atesHasari = 34f;

    [Header("Görsel Ayarlar")]
    public Color kiyafetRengi = new Color(0.2f, 0.50f, 0.95f, 1f); // Taktiksel Mavi Müttefik

    private Animator animator;
    private int isRunningHash;
    private bool isRunningParamVar = false;
    private bool suAnHareketEdiyor = false;

    private AudioSource audioSource;
    private CapsuleCollider col;
    private LineRenderer mermiIziRenderer;
    private Light namluIsigi;
    private LineRenderer namluFlasRenderer;
    private Transform namluNoktasi;

    private float sonrakiAtesZamani = 0f;
    private DusmanKontrol hedefDusman;

    // Co-op Görev Noktası Sistemi (2 Anahtarlı Asma Köprü vb. için)
    private bool gorevAktif = false;
    private Vector3 gorevHedefNoktasi;
    private bool gorevNoktasinaUlasti = false;

    // Yan karakterin canı sonsuzdur, asla ölmez
    public bool IsDead => false;
    public bool GorevAktifMi => gorevAktif;
    public bool GorevNoktasindaMi => gorevAktif && gorevNoktasinaUlasti;

    void Awake()
    {
        Instance = this;

        maksimumCan = 999999f;
        mevcutCan = 999999f;

        // Sahnedeki eski serialize edilmiş değerleri ezerek dengeli-güçlü ajan değerlerini uygula
        atesMenzili = 44f;
        atesAraligi = 0.62f;
        atesHasari = 34f;

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        HazirlaAnimator();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
            audioSource.maxDistance = 35f;
        }
        if (audioSource.clip == null)
        {
            audioSource.clip = UretAtesSesi();
        }

        col = GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.38f;
            col.center = new Vector3(0f, 0.9f, 0f);
        }

        GameObject namluObj = new GameObject("YardimciNamluUcu");
        namluObj.transform.SetParent(transform);
        namluObj.transform.localPosition = new Vector3(0.22f, 1.20f, 0.95f);
        namluNoktasi = namluObj.transform;

        KurMermiEfektleri();
    }

    private void HazirlaAnimator()
    {
        if (animator == null) return;

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        isRunningHash = Animator.StringToHash("IsRunning");
        isRunningParamVar = false;

        if (animator.runtimeAnimatorController != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == "IsRunning" && param.type == AnimatorControllerParameterType.Bool)
                {
                    isRunningParamVar = true;
                    break;
                }
            }
        }
    }

    void Start()
    {
        HedefiBul();
        RengiUygula();
        HazirlaAnimator();
        HareketAnimasyonuGuncelle(false);
    }

    /// <summary>
    /// Yardımcı karakteri belirli bir co-op görev noktasına (ör. Sağ Köprü Anahtarı) yönlendirir.
    /// </summary>
    public void GorevNoktasinaGit(Vector3 nokta)
    {
        gorevAktif = true;
        gorevHedefNoktasi = new Vector3(nokta.x, transform.position.y, nokta.z);
        gorevNoktasinaUlasti = false;
    }

    /// <summary>
    /// Co-op görevi bittiğinde yardımcı karakterin tekrar oyuncuyu takip etmesini sağlar.
    /// </summary>
    public void GoreviBitirVeTakipEt()
    {
        gorevAktif = false;
        gorevNoktasinaUlasti = false;
    }

    public void RengiUygula()
    {
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMaterial != null)
        {
            Material mat = new Material(smr.sharedMaterial);
            mat.name = "US_soldier_companion_instance";
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", kiyafetRengi);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", kiyafetRengi);
            smr.material = mat;
        }
    }

    void Update()
    {
        HedefiBul();

        // 1. Co-op Görev Noktası Hareketi (Öncelikli - Asma Köprü Anahtarı vb.)
        if (gorevAktif)
        {
            Vector3 fark = gorevHedefNoktasi - transform.position;
            fark.y = 0f;
            float mesafe = fark.magnitude;

            if (mesafe > 0.45f)
            {
                gorevNoktasinaUlasti = false;
                Vector3 yon = fark.normalized;
                transform.position += yon * hareketHizi * Time.deltaTime;
                ZeminYuksekliginiHizala(gorevHedefNoktasi.y);

                Quaternion hedefRot = Quaternion.LookRotation(yon);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, Time.deltaTime * donusHizi * 1.5f);
                HareketAnimasyonuGuncelle(true);
                return;
            }
            else
            {
                gorevNoktasinaUlasti = true;
                HareketAnimasyonuGuncelle(false);
                Quaternion ileriRot = Quaternion.Euler(0f, 0f, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, ileriRot, Time.deltaTime * donusHizi);
                return;
            }
        }

        // 2. En yakın canlı düşmanı bul
        hedefDusman = EnYakinCanliDusmaniBul();

        // 3. Oyuncuyu Takip Etme (Hem normalde hem çatışmada oyuncudan kopmaması için)
        bool suKaredeYurudu = false;

        if (hedef != null)
        {
            // Oyuncunun hemen sağ-arkasında doğal bir eskort/takip noktası hesapla
            Vector3 takipOfseti = -hedef.forward * 1.8f + hedef.right * 1.15f;
            takipOfseti.y = 0f;
            Vector3 arzuEdilenNokta = hedef.position + takipOfseti;

            Vector3 oyuncuFark = hedef.position - transform.position;
            oyuncuFark.y = 0f;
            float oyuncuMesafe = oyuncuFark.magnitude;

            Vector3 noktaFark = arzuEdilenNokta - transform.position;
            noktaFark.y = 0f;
            float noktaMesafe = noktaFark.magnitude;

            // Oyuncu henüz hendeğin güneyindeyse (-13.5'in gerisinde) yardımcı da suya girmesin
            bool oyuncuHendekGuneyinde = hedef.position.z < -13.5f && transform.position.z < -13.5f;
            if (oyuncuHendekGuneyinde && arzuEdilenNokta.z > -14.4f)
            {
                arzuEdilenNokta.z = -14.6f;
                noktaFark = arzuEdilenNokta - transform.position;
                noktaFark.y = 0f;
                noktaMesafe = noktaFark.magnitude;
            }

            // Eğer yardımcı karakter çok geride kaldıysa veya bir yere takıldıysa oyuncunun yanına ışınla
            if (oyuncuMesafe > 13.5f && !oyuncuHendekGuneyinde)
            {
                Vector3 kurtarmaKonumu = hedef.position - hedef.forward * 2.0f + hedef.right * 1.0f;
                kurtarmaKonumu.y = hedef.position.y;
                transform.position = kurtarmaKonumu;
                oyuncuMesafe = 2.2f;
                noktaMesafe = 0.3f;
            }

            // Çatışma varken biraz daha esnek durma mesafesi, çatışma yokken yakın takip
            float aktifTakipEsigi = (hedefDusman != null) ? (takipMesafesi + 1.2f) : takipMesafesi;

            if (oyuncuMesafe > aktifTakipEsigi && noktaMesafe > 0.65f)
            {
                Vector3 hareketYonu = EngelsizYonBul(noktaFark.normalized, oyuncuFark.normalized);
                float anlikHiz = oyuncuMesafe > 5.0f ? (hareketHizi * 1.18f) : hareketHizi;

                // Oyuncu henüz hendeği geçmediyse hendek boşluğuna (-14.3 ile -8.5 arası) girmesini önle
                Vector3 yeniKonum = transform.position + hareketYonu * anlikHiz * Time.deltaTime;
                if (oyuncuHendekGuneyinde && yeniKonum.z > -14.3f && yeniKonum.z < -8.5f)
                {
                    yeniKonum.z = transform.position.z;
                }

                transform.position = yeniKonum;
                ZeminYuksekliginiHizala(hedef.position.y);
                suKaredeYurudu = true;

                // Düşman yoksa veya oyuncudan çok uzaklaştıysa yürüdüğü yöne baksın; düşman varsa düşmana dönecek
                if (hedefDusman == null || oyuncuMesafe > 9.5f)
                {
                    if (hareketYonu.sqrMagnitude > 0.001f)
                    {
                        Quaternion kosuRot = Quaternion.LookRotation(hareketYonu);
                        transform.rotation = Quaternion.Slerp(transform.rotation, kosuRot, Time.deltaTime * donusHizi * 1.4f);
                    }
                }
            }
            else
            {
                ZeminYuksekliginiHizala(hedef.position.y);
            }
        }

        // 4. Yürüme / Bekleme Animasyonunu Güncelle
        HareketAnimasyonuGuncelle(suKaredeYurudu);

        // 5. Düşman Varsa Doğrudan Düşmana Yönel ve Ateş Et (Yürürken de ateş edebilir!)
        if (hedefDusman != null)
        {
            Vector3 dusmanYon = hedefDusman.transform.position - transform.position;
            dusmanYon.y = 0f;
            if (dusmanYon.sqrMagnitude > 0.001f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(dusmanYon.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, Time.deltaTime * donusHizi * 2.4f);
            }

            if (Time.time >= sonrakiAtesZamani)
            {
                sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.05f, 0.10f);
                DusmanaAtesEt(hedefDusman, suKaredeYurudu);
            }
        }
        else if (!suKaredeYurudu && hedef != null)
        {
            // Duruyorsa ve düşman yoksa oyuncunun baktığı yöne veya oyuncuya doğru dön
            Vector3 oyuncuFark = hedef.position - transform.position;
            Vector3 bakisYonu = donusModu == DonusModu.OyuncuyaBak ? oyuncuFark : hedef.forward;
            bakisYonu.y = 0f;

            if (bakisYonu.sqrMagnitude > 0.001f)
            {
                Quaternion hedefRotasyon = Quaternion.LookRotation(bakisYonu);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRotasyon, Time.deltaTime * donusHizi);
            }
        }
    }

    /// <summary>
    /// Önünde duvar/kapı sövesi varsa sağa/sola sıyrılarak takılmadan ilerlemesini sağlar.
    /// </summary>
    private Vector3 EngelsizYonBul(Vector3 arzuEdilenYon, Vector3 oyuncuyaDogrudanYon)
    {
        if (!YonEngelliMi(arzuEdilenYon)) return arzuEdilenYon;
        if (!YonEngelliMi(oyuncuyaDogrudanYon)) return oyuncuyaDogrudanYon;

        // 35 ve 65 derece sağ/sol açıları dene
        float[] acilar = { 35f, -35f, 65f, -65f, 90f, -90f };
        foreach (float aci in acilar)
        {
            Vector3 alternatif = Quaternion.Euler(0f, aci, 0f) * oyuncuyaDogrudanYon;
            if (!YonEngelliMi(alternatif))
            {
                return alternatif;
            }
        }

        return oyuncuyaDogrudanYon;
    }

    private bool YonEngelliMi(Vector3 yon)
    {
        Vector3 baslangic = transform.position + Vector3.up * 0.8f;
        RaycastHit[] hits = Physics.SphereCastAll(baslangic, 0.28f, yon, 0.85f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (hedef != null && (h.transform == hedef || h.transform.IsChildOf(hedef))) continue;
            if (h.transform.GetComponent<DusmanKontrol>() != null || h.transform.GetComponentInParent<DusmanKontrol>() != null) continue;
            // Zemin veya merdiven basamağı gibi alçak yüzeyleri engel sayma
            if (h.normal.y > 0.55f) continue;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Yardımcı karakterin merdiven, köprü ve platformlarda zemine tam basmasını sağlar.
    /// </summary>
    private void ZeminYuksekliginiHizala(float referansY)
    {
        float hedefY = referansY;
        Vector3 rayBaslangic = transform.position + Vector3.up * 1.8f;
        RaycastHit[] hits = Physics.RaycastAll(rayBaslangic, Vector3.down, 4.0f, ~0, QueryTriggerInteraction.Ignore);
        float enYakinY = float.MinValue;

        foreach (var h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (hedef != null && (h.transform == hedef || h.transform.IsChildOf(hedef))) continue;
            if (h.point.y > enYakinY && h.point.y <= referansY + 1.2f)
            {
                enYakinY = h.point.y;
            }
        }

        if (enYakinY > -5f)
        {
            hedefY = enYakinY;
        }

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, hedefY, Time.deltaTime * 12f);
        transform.position = pos;
    }

    /// <summary>
    /// KarakterAnimator controller'daki IsRunning bool parametresini ve state geçişini birlikte yönetir.
    /// </summary>
    private void HareketAnimasyonuGuncelle(bool hareketEdiyor)
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            HazirlaAnimator();
        }

        if (isRunningParamVar)
        {
            animator.SetBool(isRunningHash, hareketEdiyor);
        }

        string hedefState = hareketEdiyor ? "infantry_combat_run" : "infantry_combat_idle";
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (hareketEdiyor != suAnHareketEdiyor || (!info.IsName(hedefState) && !animator.IsInTransition(0)))
        {
            animator.CrossFade(hedefState, 0.12f);
        }

        suAnHareketEdiyor = hareketEdiyor;
    }

    private DusmanKontrol EnYakinCanliDusmaniBul()
    {
        DusmanKontrol[] dusmanlar = Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Exclude);
        DusmanKontrol enYakin = null;
        float enKisaMesafe = atesMenzili;

        foreach (var d in dusmanlar)
        {
            if (d == null || d.IsDead) continue;

            // Taret odasının kapısı (FizikselEngelKapisi) henüz kapalıysa tarete duvar arkasından kilitlenme
            if (d is Yazlab.Mekanikler.TaretKontrol)
            {
                GameObject kapiObj = GameObject.Find("FizikselEngelKapisi");
                if (kapiObj != null)
                {
                    var kapi = kapiObj.GetComponent<Yazlab.Mekanikler.KapiKontrol>();
                    if (kapi != null && !kapi.AcikMi) continue;
                }
            }

            float mesafe = Vector3.Distance(transform.position, d.transform.position);
            if (mesafe <= enKisaMesafe && DusmanaAtisHattiAcikMi(d))
            {
                enKisaMesafe = mesafe;
                enYakin = d;
            }
        }

        return enYakin;
    }

    /// <summary>
    /// Yardımcının kendi collider'ını, oyuncuyu ve alçak siperleri yok sayarak düşmana ateş hattı olup olmadığını kontrol eder.
    /// </summary>
    private bool DusmanaAtisHattiAcikMi(DusmanKontrol d)
    {
        Vector3 goz = transform.position + Vector3.up * 1.45f;
        bool taretMi = d is Yazlab.Mekanikler.TaretKontrol;
        float[] testYukseklikleri = taretMi ? new float[] { 3.1f, 2.2f } : new float[] { 1.55f, 1.05f };

        foreach (float yuk in testYukseklikleri)
        {
            Vector3 hedefNokta = d.transform.position + Vector3.up * yuk;
            Vector3 yon = hedefNokta - goz;
            float mesafe = yon.magnitude;
            if (mesafe <= 0.1f) return true;

            RaycastHit[] hits = Physics.RaycastAll(goz, yon.normalized, mesafe, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0) return true;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            bool duvarEngeliVar = false;
            foreach (var hit in hits)
            {
                if (hit.transform == null) continue;
                // Kendi collider'ımızı yok say
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                // Oyuncuyu yok say
                if (hedef != null && (hit.transform == hedef || hit.transform.IsChildOf(hedef))) continue;
                if (hit.transform.CompareTag("Player")) continue;

                // Hedef düşmanın kendisine ulaştıysak görüş açıktır!
                if (hit.transform == d.transform || hit.transform.IsChildOf(d.transform) || hit.collider.GetComponentInParent<DusmanKontrol>() == d)
                {
                    return true;
                }

                // Diğer düşmanları engel sayma
                if (hit.collider.GetComponentInParent<DusmanKontrol>() != null) continue;

                // Kum torbası / alçak siper (yüksekliği 1.45m'den alçak olan siperler) üstünden ateş edebilir
                if (hit.collider.bounds.max.y < transform.position.y + 1.45f) continue;

                duvarEngeliVar = true;
                break;
            }

            if (!duvarEngeliVar) return true;
        }

        return false;
    }

    private AudioClip UretAtesSesi()
    {
        int sampleRate = 44100;
        float duration = 0.12f;
        int totalSamples = (int)(sampleRate * duration);
        float[] data = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(320f, 85f, (float)i / totalSamples);
            float noise = (Random.value * 2f - 1f) * 0.4f;
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.6f;
            float envelope = 1f - ((float)i / totalSamples);
            data[i] = (wave + noise) * envelope * 0.55f;
        }

        AudioClip clip = AudioClip.Create("SesYardimciAtes", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private void DusmanaAtesEt(DusmanKontrol hedef, bool yururkenAtesEdiyor)
    {
        if (hedef == null || hedef.IsDead) return;

        // Yürürken ateş ediyorsa koşma animasyonunu bozma!
        if (animator != null && !yururkenAtesEdiyor)
        {
            animator.CrossFade("infantry_combat_shoot", 0.05f);
        }

        if (audioSource != null)
        {
            audioSource.pitch = Random.Range(0.9f, 1.1f);
            audioSource.Play();
        }

        Vector3 baslangic = namluNoktasi != null ? namluNoktasi.position : (transform.position + Vector3.up * 1.2f + transform.forward * 0.8f);
        float hedefYukseklik = (hedef is Yazlab.Mekanikler.TaretKontrol) ? 2.8f : 1.0f;
        Vector3 hedefNokta = hedef.transform.position + Vector3.up * hedefYukseklik + Random.insideUnitSphere * 0.15f;

        StartCoroutine(NamluFlasiParla(baslangic));
        if (mermiIziRenderer != null)
        {
            StartCoroutine(MermiIziCiz(baslangic, hedefNokta));
        }

        hedef.HasarAl(atesHasari);
    }

    /// <summary>
    /// Yan karakter ölümsüzdür; gelen hasar çağrıları canını azaltmaz.
    /// </summary>
    public void HasarAl(float miktar)
    {
        mevcutCan = maksimumCan;
    }

    private void KurMermiEfektleri()
    {
        GameObject lineObj = new GameObject("YardimciMermiIzi");
        lineObj.transform.SetParent(transform);
        mermiIziRenderer = lineObj.AddComponent<LineRenderer>();
        mermiIziRenderer.startWidth = 0.04f;
        mermiIziRenderer.endWidth = 0.015f;
        mermiIziRenderer.positionCount = 2;
        mermiIziRenderer.useWorldSpace = true;
        mermiIziRenderer.enabled = false;

        Material lineMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.color = new Color(0.3f, 0.75f, 1f, 0.9f);
        mermiIziRenderer.material = lineMat;
        mermiIziRenderer.startColor = new Color(0.3f, 0.75f, 1f, 0.9f);
        mermiIziRenderer.endColor = new Color(0.1f, 0.4f, 1f, 0.2f);

        GameObject lightObj = new GameObject("YardimciNamluIsigi");
        lightObj.transform.SetParent(namluNoktasi != null ? namluNoktasi : transform);
        lightObj.transform.localPosition = Vector3.zero;

        namluIsigi = lightObj.AddComponent<Light>();
        namluIsigi.type = LightType.Point;
        namluIsigi.color = new Color(0.3f, 0.75f, 1f);
        namluIsigi.range = 3.5f;
        namluIsigi.intensity = 4.0f;
        namluIsigi.enabled = false;

        GameObject flashObj = new GameObject("YardimciNamluFlasi");
        flashObj.transform.SetParent(namluNoktasi != null ? namluNoktasi : transform);
        flashObj.transform.localPosition = Vector3.zero;

        namluFlasRenderer = flashObj.AddComponent<LineRenderer>();
        namluFlasRenderer.startWidth = 0.09f;
        namluFlasRenderer.endWidth = 0.02f;
        namluFlasRenderer.positionCount = 2;
        namluFlasRenderer.useWorldSpace = true;
        namluFlasRenderer.enabled = false;

        Material flashMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        flashMat.color = new Color(0.4f, 0.8f, 1f, 1f);
        namluFlasRenderer.material = flashMat;
        namluFlasRenderer.startColor = new Color(0.4f, 0.8f, 1f, 1f);
        namluFlasRenderer.endColor = new Color(0.1f, 0.4f, 1f, 0.1f);
    }

    private IEnumerator NamluFlasiParla(Vector3 namluPos)
    {
        if (namluIsigi != null)
        {
            namluIsigi.transform.position = namluPos;
            namluIsigi.enabled = true;
        }
        if (namluFlasRenderer != null)
        {
            namluFlasRenderer.SetPosition(0, namluPos);
            namluFlasRenderer.SetPosition(1, namluPos + transform.forward * 0.16f);
            namluFlasRenderer.enabled = true;
        }

        yield return new WaitForSeconds(0.05f);

        if (namluIsigi != null) namluIsigi.enabled = false;
        if (namluFlasRenderer != null) namluFlasRenderer.enabled = false;
    }

    private IEnumerator MermiIziCiz(Vector3 baslangic, Vector3 bitis)
    {
        mermiIziRenderer.enabled = true;
        mermiIziRenderer.SetPosition(0, baslangic);
        mermiIziRenderer.SetPosition(1, bitis);

        yield return new WaitForSeconds(0.06f);

        if (mermiIziRenderer != null) mermiIziRenderer.enabled = false;
    }

    private void HedefiBul()
    {
        if (hedef != null) return;
        KarakterHareket kh = FindAnyObjectByType<KarakterHareket>();
        if (kh != null && kh.gameObject != gameObject)
        {
            hedef = kh.transform;
            return;
        }
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player != gameObject)
        {
            hedef = player.transform;
        }
    }
}
