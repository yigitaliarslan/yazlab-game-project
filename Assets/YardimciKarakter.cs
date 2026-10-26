using System.Collections;
using UnityEngine;

/// <summary>
/// Oyuncunun müttefiki olan asker kontrolcüsü.
/// - Canı vardır ve hasar alabilir.
/// - Sahnede düşman gördüğünde onlara ateş açar ve hasar verir.
/// - Co-op bulmacalarda (ör. 2 Anahtarlı Asma Köprü) emredilen görev noktasına koşup anahtarı tutar.
/// - Serbest durumdayken oyuncuyu takip eder ve çatışma yokken oyuncuya doğru döner.
/// - Öldüğünde yere yığılır.
/// </summary>
public class YardimciKarakter : MonoBehaviour
{
    public static YardimciKarakter Instance { get; private set; }

    public enum DonusModu
    {
        OyuncuyaBak,
        OyuncununBaktigiYon
    }

    [Header("Sağlık Ayarları")]
    public float maksimumCan = 100f;
    public float mevcutCan = 100f;

    [Header("Dönüş ve Takip")]
    public DonusModu donusModu = DonusModu.OyuncuyaBak;
    public float donusHizi = 8.0f;
    public float hareketHizi = 4.8f;
    public float takipMesafesi = 3.2f;
    public Transform hedef;

    [Header("Ateş ve Çatışma Ayarları")]
    [Tooltip("Düşmanı görebileceği ve ateş edebileceği maksimum menzil.")]
    public float atesMenzili = 30f;

    [Tooltip("İki atış arasındaki süre.")]
    public float atesAraligi = 0.9f;

    [Tooltip("Düşmana her vuruşunda vereceği hasar.")]
    public float atesHasari = 20f;

    [Header("Görsel Ayarlar")]
    public Color kiyafetRengi = new Color(0.2f, 0.50f, 0.95f, 1f); // Taktiksel Mavi Müttefik

    private Animator animator;
    private AudioSource audioSource;
    private CapsuleCollider col;
    private LineRenderer mermiIziRenderer;
    private Light namluIsigi;
    private LineRenderer namluFlasRenderer;
    private Transform namluNoktasi;

    private float sonrakiAtesZamani = 0f;
    private bool oldu = false;
    private DusmanKontrol hedefDusman;

    // Co-op Görev Noktası Sistemi (2 Anahtarlı Asma Köprü vb. için)
    private bool gorevAktif = false;
    private Vector3 gorevHedefNoktasi;
    private bool gorevNoktasinaUlasti = false;

    public bool IsDead => oldu;
    public bool GorevAktifMi => gorevAktif;
    public bool GorevNoktasindaMi => gorevAktif && gorevNoktasinaUlasti;

    void Awake()
    {
        Instance = this;

        OyuncuCani oyuncu = Object.FindAnyObjectByType<OyuncuCani>();
        if (oyuncu != null)
        {
            maksimumCan = oyuncu.maksimumCan;
            mevcutCan = oyuncu.maksimumCan;
        }
        else
        {
            maksimumCan = 100f;
            mevcutCan = 100f;
        }

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.7f;
            audioSource.maxDistance = 35f;
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

    void Start()
    {
        HedefiBul();
        RengiUygula();

        if (animator != null)
        {
            animator.CrossFade("infantry_combat_idle", 0.2f);
        }
    }

    /// <summary>
    /// Yardımcı karakteri belirli bir co-op görev noktasına (ör. Sağ Köprü Anahtarı) yönlendirir.
    /// </summary>
    public void GorevNoktasinaGit(Vector3 nokta)
    {
        if (oldu) return;
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
        if (oldu) return;

        HedefiBul();

        // 1. Co-op Görev Noktası Hareketi (Öncelikli)
        if (gorevAktif)
        {
            Vector3 fark = gorevHedefNoktasi - transform.position;
            fark.y = 0f;
            float mesafe = fark.magnitude;

            if (mesafe > 0.65f)
            {
                gorevNoktasinaUlasti = false;
                Vector3 yon = fark.normalized;
                transform.position += yon * hareketHizi * Time.deltaTime;
                Quaternion hedefRot = Quaternion.LookRotation(yon);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, Time.deltaTime * donusHizi * 1.4f);
                AnimasyonAyarla("infantry_combat_run");
                return;
            }
            else
            {
                gorevNoktasinaUlasti = true;
                AnimasyonAyarla("infantry_combat_idle");
                // Anahtar sütununa doğru bak
                Quaternion ileriRot = Quaternion.Euler(0f, 0f, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, ileriRot, Time.deltaTime * donusHizi);
                return;
            }
        }

        // 2. En yakın canlı düşmanı ara
        hedefDusman = EnYakinCanliDusmaniBul();

        if (hedefDusman != null)
        {
            Vector3 dusmanYon = (hedefDusman.transform.position - transform.position);
            dusmanYon.y = 0f;
            if (dusmanYon.sqrMagnitude > 0.01f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(dusmanYon);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, Time.deltaTime * donusHizi * 1.5f);
            }

            if (Time.time >= sonrakiAtesZamani)
            {
                sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.1f, 0.2f);
                DusmanaAtesEt(hedefDusman);
            }
        }
        else
        {
            // Etrafta düşman yoksa oyuncuya doğru dön
            if (hedef != null)
            {
                Vector3 bakisYonu = donusModu == DonusModu.OyuncuyaBak ? (hedef.position - transform.position) : hedef.forward;
                bakisYonu.y = 0f;

                if (bakisYonu.sqrMagnitude > 0.001f)
                {
                    Quaternion hedefRotasyon = Quaternion.LookRotation(bakisYonu);
                    transform.rotation = Quaternion.Slerp(transform.rotation, hedefRotasyon, Time.deltaTime * donusHizi);
                }
            }
            AnimasyonAyarla("infantry_combat_idle");
        }
    }

    private void AnimasyonAyarla(string stateAdi)
    {
        if (animator == null) return;
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        if (!info.IsName(stateAdi))
        {
            animator.CrossFade(stateAdi, 0.15f);
        }
    }

    private DusmanKontrol EnYakinCanliDusmaniBul()
    {
        DusmanKontrol[] dusmanlar = Object.FindObjectsByType<DusmanKontrol>(FindObjectsInactive.Exclude);
        DusmanKontrol enYakin = null;
        float enKisaMesafe = atesMenzili;

        foreach (var d in dusmanlar)
        {
            if (d == null || d.IsDead) continue;

            float mesafe = Vector3.Distance(transform.position, d.transform.position);
            if (mesafe <= enKisaMesafe)
            {
                Vector3 goz = transform.position + Vector3.up * 1.3f;
                float hedefYukseklik = (d is Yazlab.Mekanikler.TaretKontrol) ? 2.8f : 1.0f;
                Vector3 hedefGovde = d.transform.position + Vector3.up * hedefYukseklik;
                RaycastHit hit;
                if (Physics.Raycast(goz, (hedefGovde - goz).normalized, out hit, atesMenzili))
                {
                    if (hit.transform == d.transform || hit.transform.IsChildOf(d.transform))
                    {
                        enKisaMesafe = mesafe;
                        enYakin = d;
                    }
                }
            }
        }

        return enYakin;
    }

    private void DusmanaAtesEt(DusmanKontrol hedef)
    {
        if (oldu || hedef == null || hedef.IsDead) return;

        if (animator != null)
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

    private float sonHasarZamani = -1f;

    public void HasarAl(float miktar)
    {
        if (oldu) return;

        if (Time.time < sonHasarZamani + 0.18f) return;
        sonHasarZamani = Time.time;

        OyuncuCani oyuncu = Object.FindAnyObjectByType<OyuncuCani>();
        float limit = oyuncu != null ? oyuncu.mermiHasarLimiti : 8f;
        float gercekHasar = Mathf.Min(miktar, limit);

        mevcutCan -= gercekHasar;
        StartCoroutine(HasarFlasGoster());

        if (mevcutCan <= 0f)
        {
            mevcutCan = 0f;
            Olum();
        }
    }

    private IEnumerator HasarFlasGoster()
    {
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null && smr.material != null)
        {
            Material mat = smr.material;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", Color.white);
                yield return new WaitForSeconds(0.08f);
                if (!oldu) mat.SetColor("_BaseColor", kiyafetRengi);
            }
        }
    }

    private void Olum()
    {
        if (oldu) return;
        oldu = true;

        if (col != null) col.enabled = false;
        if (namluIsigi != null) namluIsigi.enabled = false;
        if (namluFlasRenderer != null) namluFlasRenderer.enabled = false;
        if (mermiIziRenderer != null) mermiIziRenderer.enabled = false;

        StartCoroutine(OlumSekansi());
    }

    private IEnumerator OlumSekansi()
    {
        float gecen = 0f;
        float sure = 0.7f;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(-80f, 0f, 0f);
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * 0.4f - transform.forward * 0.5f;

        while (gecen < sure)
        {
            gecen += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, gecen / sure);
            transform.rotation = Quaternion.Slerp(startRot, endRot, t);
            transform.position = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }
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
