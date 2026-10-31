using System.Collections;
using UnityEngine;

/// <summary>
/// Gelişmiş Düşman Asker Yapay Zekası.
/// - Oyuncuyu algıladığında veya baskın dalgasında oyuncuya doğru yürür ve yürürken ateş eder.
/// - Başının üzerinde süzülen dinamik 3D / GUI Can Barı (Floating Health Bar) gösterir.
/// - Oyuncu ona sıktığında canı azalır.
/// - Oyuncuya ateş ettiğinde oyuncunun sağ alttaki can barını azaltır.
/// - Öldüğünde yere yığılma/ölüm animasyonu ile kaybolur.
/// </summary>
public class DusmanKontrol : MonoBehaviour
{
    [Header("Sağlık Ayarları")]
    [Tooltip("Düşmanın maksimum canı.")]
    public float maksimumCan = 120f;

    [Tooltip("Düşmanın mevcut canı.")]
    public float mevcutCan = 120f;

    [Header("Görüş ve Algılama Ayarları")]
    [Tooltip("Düşmanın oyuncuyu görebileceği maksimum mesafe.")]
    public float gorusMenzili = 35f;

    [Tooltip("Düşmanın oyuncuya doğru dönme hızı.")]
    public float donusHizi = 8.0f;

    [Tooltip("Engel kontrolü için göz hizası yüksekliği.")]
    public float gozYuksekligi = 1.4f;

    [Header("Yürüme ve Baskın Ayarları")]
    [Tooltip("Düşman oyuncuya doğru yürüyerek yaklaşsın mı?")]
    public bool oyuncuyaDogruYuru = true;

    [Tooltip("Düşman askerinin yürüme hızı.")]
    public float yurumeHizi = 3.2f;

    [Tooltip("Oyuncuya ne kadar yaklaşınca durup ateş etmeye devam edeceği.")]
    public float durmaMesafesi = 3.8f;

    [Tooltip("Görüş hattı beklemeksizin doğrudan oyuncunun konumuna ilerlesin mi?")]
    public bool herZamanOyuncuyuTakipEt = true;

    [Header("Ateş Etme Ayarları")]
    [Tooltip("İki atış arasındaki süre (saniye).")]
    public float atesAraligi = 0.95f;

    [Tooltip("Her atışta oyuncuya vereceği hasar.")]
    public float atesHasari = 7f;

    [Tooltip("Atış isabet sapması / doğruluğu (0: tam isabet, 1: hafif sapmalı).")]
    public float isabetSapmasi = 0.25f;

    [Header("Görsel ve Ses")]
    [Tooltip("Düşman üniforma rengi (Kırmızı Komando).")]
    public Color kiyafetRengi = new Color(0.95f, 0.18f, 0.18f, 1f);

    [Tooltip("Düşman mermi izi rengi.")]
    public Color dusmanMermiIziRengi = new Color(1f, 0.25f, 0.15f, 0.9f);

    [Tooltip("Ateş sesi.")]
    public AudioClip atesSesi;

    [Header("Can Barı Görünümü")]
    [Tooltip("Can barının düşmanın ne kadar yukarısında duracağı.")]
    public float canBariYukseklikOfseti = 2.15f;

    [Tooltip("Can barı genişliği.")]
    public float canBariGenislik = 90f;

    [Tooltip("Can barı yüksekliği.")]
    public float canBariYukseklik = 12f;

    // Özel Değişkenler
    protected Transform oyuncuHedef;
    protected OyuncuCani oyuncuCaniBileseni;
    private Animator animator;
    private int isRunningHash;
    private bool isRunningParamVar = false;
    private bool suAnYuruyor = false;

    private AudioSource audioSource;
    private LineRenderer mermiIziRenderer;
    private Light namluIsigi;
    private LineRenderer namluFlasRenderer;
    private Transform namluUcu;
    private CapsuleCollider col;

    protected float sonrakiAtesZamani = 0f;
    protected bool oldu = false;
    protected float sonHasarZamani = 0f;
    protected float gorunenCan = 120f;

    private Texture2D beyazDokusu;
    private GUIStyle canMetniStili;
    private GUIStyle dusmanBaslikStili;

    // Görüş Hattı (Line of Sight) ve Duvar Arkası Gizleme
    private bool oyuncuBeniGoruyorMu = false;
    private float sonGorusKontrolZamani = 0f;
    private const float GORUS_KONTROL_ARALIGI = 0.04f;

    protected Transform aktifHedef;

    public bool IsDead => oldu;

    protected virtual void Awake()
    {
        maksimumCan = 120f;
        mevcutCan = maksimumCan;
        gorunenCan = maksimumCan;
        atesHasari = 7f;
        atesAraligi = 0.90f;

        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        HazirlaAnimator();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
            audioSource.maxDistance = 35f;
        }
        if (atesSesi == null)
        {
            atesSesi = UretVarsayilanAtesSesi();
        }

        col = GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.38f;
            col.center = new Vector3(0f, 0.9f, 0f);
        }

        // Namlu ucu referansı
        GameObject namlu = new GameObject("DusmanNamluUcu");
        namlu.transform.SetParent(transform);
        namlu.transform.localPosition = new Vector3(0.22f, 1.15f, 0.85f);
        namluUcu = namlu.transform;

        KurMermiEfektleri();

        // Varsa eski NavMeshAgent'ı devre dışı bırak (doğrudan akıcı fiziksel yürüyüş kullanıyoruz)
        UnityEngine.AI.NavMeshAgent eskiNav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (eskiNav != null)
        {
            eskiNav.enabled = false;
        }
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

    protected virtual void Start()
    {
        HedefiBul();
        RengiUygula();
        OlusturDokular();
        HazirlaAnimator();
        YurumeAnimasyonuGuncelle(false);
    }

    public void RengiUygula()
    {
        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in renderers)
        {
            if (smr != null && smr.sharedMaterial != null)
            {
                Material mat = new Material(smr.sharedMaterial);
                mat.name = "Enemy_Soldier_Instance";
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", kiyafetRengi);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", kiyafetRengi);
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.25f, 0.05f, 0.05f));
                smr.material = mat;
            }
        }
    }

    protected virtual void Update()
    {
        if (oldu) return;

        // Oyuncunun bu düşmanı görüp görmediğini kontrol et (duvar vb. arkasındaysa can barı gizlensin)
        if (Time.time >= sonGorusKontrolZamani + GORUS_KONTROL_ARALIGI)
        {
            sonGorusKontrolZamani = Time.time;
            oyuncuBeniGoruyorMu = OyuncuDusmaniGoruyorMu();
        }

        // Hedefi belirle (Öncelikle Oyuncu)
        HedefSec();

        bool suKaredeYurudu = false;

        if (aktifHedef != null)
        {
            Vector3 fark = aktifHedef.position - transform.position;
            fark.y = 0f;
            float mesafe = fark.magnitude;

            // 1. Hedefe doğru dön
            if (fark.sqrMagnitude > 0.01f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(fark.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, Time.deltaTime * donusHizi * 1.4f);
            }

            // 2. Oyuncuya doğru yürü
            if (oyuncuyaDogruYuru && mesafe > durmaMesafesi)
            {
                Vector3 ilerlemeYonu = EngelsizYonBul(fark.normalized);
                transform.position += ilerlemeYonu * yurumeHizi * Time.deltaTime;
                ZeminYuksekliginiHizala(aktifHedef.position.y);
                suKaredeYurudu = true;
            }
            else
            {
                ZeminYuksekliginiHizala(aktifHedef.position.y);
            }

            // 3. Yürürken ve dururken oyuncuya ateş et
            bool atesEdebilir = mesafe <= gorusMenzili && (HedefiGoruyorMu(aktifHedef) || mesafe <= 18f);
            if (atesEdebilir && Time.time >= sonrakiAtesZamani)
            {
                sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.08f, 0.15f);
                AtesEt(suKaredeYurudu);
            }
        }

        YurumeAnimasyonuGuncelle(suKaredeYurudu);
    }

    private Vector3 EngelsizYonBul(Vector3 arzuEdilenYon)
    {
        if (!YonEngelliMi(arzuEdilenYon)) return arzuEdilenYon;

        float[] acilar = { 35f, -35f, 65f, -65f, 90f, -90f };
        foreach (float aci in acilar)
        {
            Vector3 alternatif = Quaternion.Euler(0f, aci, 0f) * arzuEdilenYon;
            if (!YonEngelliMi(alternatif))
            {
                return alternatif;
            }
        }

        return arzuEdilenYon;
    }

    private bool YonEngelliMi(Vector3 yon)
    {
        Vector3 baslangic = transform.position + Vector3.up * 0.8f;
        RaycastHit[] hits = Physics.SphereCastAll(baslangic, 0.30f, yon, 0.85f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (oyuncuHedef != null && (h.transform == oyuncuHedef || h.transform.IsChildOf(oyuncuHedef))) continue;
            if (h.transform.GetComponent<YardimciKarakter>() != null || h.transform.GetComponentInParent<YardimciKarakter>() != null) continue;
            if (h.normal.y > 0.55f) continue;
            return true;
        }
        return false;
    }

    private void ZeminYuksekliginiHizala(float referansY)
    {
        float hedefY = referansY;
        Vector3 rayBaslangic = transform.position + Vector3.up * 1.8f;
        RaycastHit[] hits = Physics.RaycastAll(rayBaslangic, Vector3.down, 4.0f, ~0, QueryTriggerInteraction.Ignore);
        float enYakinY = float.MinValue;

        foreach (var h in hits)
        {
            if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
            if (oyuncuHedef != null && (h.transform == oyuncuHedef || h.transform.IsChildOf(oyuncuHedef))) continue;
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

    private void YurumeAnimasyonuGuncelle(bool yuruyor)
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null) return;
            HazirlaAnimator();
        }

        if (isRunningParamVar)
        {
            animator.SetBool(isRunningHash, yuruyor);
        }

        string hedefState = yuruyor ? "infantry_combat_run" : "infantry_combat_idle";
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);

        if (yuruyor != suAnYuruyor || (!info.IsName(hedefState) && !animator.IsInTransition(0)))
        {
            animator.CrossFade(hedefState, 0.12f);
        }

        suAnYuruyor = yuruyor;
    }

    private void HedefSec()
    {
        HedefiBul();

        if (oyuncuHedef != null && (oyuncuCaniBileseni == null || !oyuncuCaniBileseni.IsDead))
        {
            float mesafe = Vector3.Distance(transform.position, oyuncuHedef.position);
            if (herZamanOyuncuyuTakipEt || (mesafe <= gorusMenzili && HedefiGoruyorMu(oyuncuHedef)))
            {
                aktifHedef = oyuncuHedef;
                return;
            }
        }

        aktifHedef = null;
    }

    private bool HedefiGoruyorMu(Transform t)
    {
        if (t == null) return false;
        Vector3 goz = transform.position + Vector3.up * gozYuksekligi;
        Vector3 hedefNokta = t.position + Vector3.up * 1.0f;
        Vector3 yon = hedefNokta - goz;
        float mesafe = yon.magnitude;

        RaycastHit[] hits = Physics.RaycastAll(goz, yon.normalized, Mathf.Min(mesafe + 0.5f, gorusMenzili), ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.transform.GetComponent<DusmanKontrol>() != null || hit.transform.GetComponentInParent<DusmanKontrol>() != null) continue;
            if (hit.transform == t || hit.transform.IsChildOf(t)) return true;
            return false;
        }
        return true;
    }

    /// <summary>
    /// Düşmanın oyuncuya ateş etme mekaniği (yürürken koşma animasyonunu kesmez).
    /// </summary>
    private void AtesEt(bool yururkenAtesEdiyor)
    {
        if (oldu || aktifHedef == null) return;

        // 1. Ateş Animasyonu (Sadece dururken oynatılır, yürürken yürüme animasyonu korunur)
        if (animator != null && !yururkenAtesEdiyor)
        {
            animator.CrossFade("infantry_combat_shoot", 0.05f);
        }

        // 2. Ateş Sesi
        if (audioSource != null)
        {
            audioSource.pitch = Random.Range(0.90f, 1.12f);
            if (atesSesi != null)
            {
                audioSource.PlayOneShot(atesSesi, 0.65f);
            }
            else
            {
                audioSource.Play();
            }
        }

        // 3. Namlu Konumu
        Vector3 baslangic = namluUcu != null ? namluUcu.position : (transform.position + Vector3.up * 1.2f + transform.forward * 0.8f);

        // 4. Hedef Noktası
        Vector3 hedefNokta = aktifHedef.position + Vector3.up * 1.1f;
        if (isabetSapmasi > 0f)
        {
            hedefNokta += Random.insideUnitSphere * isabetSapmasi;
        }

        // 5. Namlu Flaşı ve Işığı
        StartCoroutine(NamluFlasiParla(baslangic));

        // 6. Mermi İzi (Tracer)
        if (mermiIziRenderer != null)
        {
            StartCoroutine(MermiIziCiz(baslangic, hedefNokta));
        }

        // 7. Hasar Ver (Oyuncu)
        if (aktifHedef == oyuncuHedef && oyuncuCaniBileseni != null && !oyuncuCaniBileseni.IsDead)
        {
            oyuncuCaniBileseni.HasarAl(atesHasari);
        }
        else
        {
            aktifHedef.SendMessage("HasarAl", atesHasari, SendMessageOptions.DontRequireReceiver);
        }
    }

    private AudioClip UretVarsayilanAtesSesi()
    {
        int sampleRate = 44100;
        float duration = 0.14f;
        int totalSamples = (int)(sampleRate * duration);
        float[] data = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / sampleRate;
            float freq = Mathf.Lerp(280f, 75f, (float)i / totalSamples);
            float noise = (Random.value * 2f - 1f) * 0.45f;
            float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.55f;
            float envelope = 1f - ((float)i / totalSamples);
            data[i] = (wave + noise) * envelope * 0.6f;
        }

        AudioClip clip = AudioClip.Create("SesDusmanAtes", totalSamples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>
    /// Oyuncunun mermisi düşmana çarptığında KarakterAtes tarafından çağrılır.
    /// </summary>
    public virtual void HasarAl(float miktar)
    {
        if (oldu) return;

        mevcutCan -= miktar;
        sonHasarZamani = Time.time;

        // Hasar alma efekti (Hafif kırmızı parlama & sarsılma)
        StartCoroutine(HasarGorselTepkisi());

        if (mevcutCan <= 0f)
        {
            mevcutCan = 0f;
            Olum();
        }
    }

    private IEnumerator HasarGorselTepkisi()
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

    /// <summary>
    /// Düşman öldüğünde tetiklenen prosedürel yere yığılma ve kaybolma animasyonu.
    /// </summary>
    protected virtual void Olum()
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
        float gecenSure = 0f;
        float dususSuresi = 0.7f;
        Quaternion baslangicRot = transform.rotation;
        Quaternion hedefRot = baslangicRot * Quaternion.Euler(-80f, 0f, 0f);
        Vector3 baslangicPos = transform.position;
        Vector3 hedefPos = baslangicPos + Vector3.down * 0.4f - transform.forward * 0.5f;

        while (gecenSure < dususSuresi)
        {
            gecenSure += Time.deltaTime;
            float t = gecenSure / dususSuresi;
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            transform.rotation = Quaternion.Slerp(baslangicRot, hedefRot, smooth);
            transform.position = Vector3.Lerp(baslangicPos, hedefPos, smooth);
            yield return null;
        }

        yield return new WaitForSeconds(1.2f);

        float yokOlmaSuresi = 0.8f;
        gecenSure = 0f;
        Vector3 baslangicOlcek = transform.localScale;

        while (gecenSure < yokOlmaSuresi)
        {
            gecenSure += Time.deltaTime;
            float t = gecenSure / yokOlmaSuresi;
            transform.localScale = Vector3.Lerp(baslangicOlcek, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void HedefiBul()
    {
        if (oyuncuHedef != null && oyuncuCaniBileseni != null) return;

        KarakterHareket kh = FindAnyObjectByType<KarakterHareket>();
        if (kh != null && kh.gameObject != gameObject)
        {
            oyuncuHedef = kh.transform;
            oyuncuCaniBileseni = kh.GetComponent<OyuncuCani>();
            if (oyuncuCaniBileseni == null)
            {
                oyuncuCaniBileseni = kh.gameObject.AddComponent<OyuncuCani>();
            }
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player != gameObject)
        {
            oyuncuHedef = player.transform;
            oyuncuCaniBileseni = player.GetComponent<OyuncuCani>();
        }
    }

    private void KurMermiEfektleri()
    {
        GameObject lineObj = new GameObject("DusmanMermiIzi");
        lineObj.transform.SetParent(transform);
        mermiIziRenderer = lineObj.AddComponent<LineRenderer>();
        mermiIziRenderer.startWidth = 0.04f;
        mermiIziRenderer.endWidth = 0.015f;
        mermiIziRenderer.positionCount = 2;
        mermiIziRenderer.useWorldSpace = true;
        mermiIziRenderer.enabled = false;

        Material lineMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.color = dusmanMermiIziRengi;
        mermiIziRenderer.material = lineMat;
        mermiIziRenderer.startColor = dusmanMermiIziRengi;
        mermiIziRenderer.endColor = new Color(1f, 0.4f, 0.1f, 0.2f);

        GameObject lightObj = new GameObject("DusmanNamluIsigi");
        lightObj.transform.SetParent(namluUcu != null ? namluUcu : transform);
        lightObj.transform.localPosition = Vector3.zero;

        namluIsigi = lightObj.AddComponent<Light>();
        namluIsigi.type = LightType.Point;
        namluIsigi.color = new Color(1f, 0.4f, 0.1f);
        namluIsigi.range = 3.5f;
        namluIsigi.intensity = 4.0f;
        namluIsigi.enabled = false;

        GameObject flashObj = new GameObject("DusmanNamluFlasi");
        flashObj.transform.SetParent(namluUcu != null ? namluUcu : transform);
        flashObj.transform.localPosition = Vector3.zero;

        namluFlasRenderer = flashObj.AddComponent<LineRenderer>();
        namluFlasRenderer.startWidth = 0.09f;
        namluFlasRenderer.endWidth = 0.02f;
        namluFlasRenderer.positionCount = 2;
        namluFlasRenderer.useWorldSpace = true;
        namluFlasRenderer.enabled = false;

        Material flashMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        flashMat.color = new Color(1f, 0.5f, 0.1f, 1f);
        namluFlasRenderer.material = flashMat;
        namluFlasRenderer.startColor = new Color(1f, 0.5f, 0.1f, 1f);
        namluFlasRenderer.endColor = new Color(1f, 0.2f, 0.0f, 0.1f);
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

    private void OlusturDokular()
    {
        if (beyazDokusu == null)
        {
            beyazDokusu = new Texture2D(1, 1);
            beyazDokusu.SetPixel(0, 0, Color.white);
            beyazDokusu.Apply();
        }
    }

    protected virtual void OnGUI()
    {
        if (oldu || !oyuncuBeniGoruyorMu) return;
        if (Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik || Yazlab.Yonetim.OllamaNpcDiyalogUI.DiyalogAcikMi) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 dunyaKonumu = transform.position + Vector3.up * canBariYukseklikOfseti;
        if (!Yazlab.Yonetim.GoruntuKalitesiYoneticisi.DunyaNoktasiniEkranaCevir(cam, dunyaKonumu, out Vector2 ekranKonumu))
            return;

        float guiX = ekranKonumu.x;
        float guiY = ekranKonumu.y;

        float mesafe = Vector3.Distance(cam.transform.position, dunyaKonumu);
        float olcek = Mathf.Clamp(15f / Mathf.Max(mesafe, 5f), 0.85f, 1.30f);

        float genislik = Mathf.Round(Mathf.Max(96f, canBariGenislik) * olcek);
        float yukseklik = Mathf.Round(Mathf.Max(16f, canBariYukseklik) * olcek);

        float solX = Mathf.Round(guiX - (genislik * 0.5f));
        float ustY = Mathf.Round(guiY - yukseklik);

        if (beyazDokusu == null) OlusturDokular();

        Color eskiRenk = GUI.color;

        // 1. Arka Plan Siyah Çerçeve Paneli
        GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.88f);
        GUI.DrawTexture(new Rect(solX - 2, ustY - 2, genislik + 4, yukseklik + 4), beyazDokusu);

        // 2. Boş Bar Zemini (Koyu kırmızı zemin)
        GUI.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);
        GUI.DrawTexture(new Rect(solX, ustY, genislik, yukseklik), beyazDokusu);

        // 3. Yumuşak Geçişli Can Dolgusu
        gorunenCan = Mathf.Lerp(gorunenCan, mevcutCan, Time.deltaTime * 8.0f);
        float ghostOran = Mathf.Clamp01(gorunenCan / maksimumCan);
        GUI.color = new Color(0.85f, 0.55f, 0.15f, 0.65f);
        GUI.DrawTexture(new Rect(solX, ustY, genislik * ghostOran, yukseklik), beyazDokusu);

        float oran = Mathf.Clamp01(mevcutCan / maksimumCan);
        float dolguGenislik = genislik * oran;

        bool yeniHasar = (Time.time - sonHasarZamani) < 0.15f;
        Color dolguRengi = yeniHasar ? new Color(1f, 0.9f, 0.4f) : new Color(0.95f, 0.15f, 0.15f);

        GUI.color = dolguRengi;
        GUI.DrawTexture(new Rect(solX, ustY, dolguGenislik, yukseklik), beyazDokusu);

        // 4. İnce Kenarlık Çerçevesi
        GUI.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
        GUI.DrawTexture(new Rect(solX, ustY, genislik, 1), beyazDokusu);
        GUI.DrawTexture(new Rect(solX, ustY + yukseklik - 1, genislik, 1), beyazDokusu);
        GUI.DrawTexture(new Rect(solX, ustY, 1, yukseklik), beyazDokusu);
        GUI.DrawTexture(new Rect(solX + genislik - 1, ustY, 1, yukseklik), beyazDokusu);

        // 5. "DÜŞMAN ASKER" Başlığı (Barın üzerinde)
        if (dusmanBaslikStili == null)
        {
            dusmanBaslikStili = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
        Yazlab.Yonetim.GoruntuKalitesiYoneticisi.StiliKeskinlestir(dusmanBaslikStili, Mathf.RoundToInt(12 * olcek));
        dusmanBaslikStili.normal.textColor = new Color(1f, 0.4f, 0.4f, 0.98f);

        GUI.color = Color.white;
        float baslikH = Mathf.Max(20f, 20f * olcek);
        Yazlab.Yonetim.GoruntuKalitesiYoneticisi.KeskinEtiketCiz(
            new Rect(solX - 30f, ustY - baslikH - 2f, genislik + 60f, baslikH), "⚔ DÜŞMAN ASKER", dusmanBaslikStili);

        // 6. Can Miktarı Metni
        if (canMetniStili == null)
        {
            canMetniStili = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
        Yazlab.Yonetim.GoruntuKalitesiYoneticisi.StiliKeskinlestir(canMetniStili, Mathf.RoundToInt(11 * olcek));
        canMetniStili.normal.textColor = Color.white;

        string canYazisi = $"{Mathf.CeilToInt(mevcutCan)} / {Mathf.CeilToInt(maksimumCan)}";
        GUI.Label(new Rect(solX, ustY - 2f, genislik, yukseklik + 4f), canYazisi, canMetniStili);

        GUI.color = eskiRenk;
    }

    private bool OyuncuDusmaniGoruyorMu()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 camPos = cam.transform.position;
        Vector3 kafaKonumu = transform.position + Vector3.up * 1.6f;

        Vector3 ekranNoktasi = cam.WorldToScreenPoint(kafaKonumu);
        if (ekranNoktasi.z <= 0.1f) return false;

        if (ekranNoktasi.x < -60 || ekranNoktasi.x > Screen.width + 60 ||
            ekranNoktasi.y < -60 || ekranNoktasi.y > Screen.height + 60)
        {
            return false;
        }

        float mesafe = Vector3.Distance(camPos, kafaKonumu);
        if (mesafe > 50f) return false;

        Vector3 govdeKonumu = transform.position + Vector3.up * 0.9f;
        return NoktaGorunurMu(camPos, kafaKonumu) || NoktaGorunurMu(camPos, govdeKonumu);
    }

    private bool NoktaGorunurMu(Vector3 baslangic, Vector3 hedef)
    {
        Vector3 yon = hedef - baslangic;
        float mesafe = yon.magnitude;
        if (mesafe <= 0.05f) return true;

        RaycastHit[] hits = Physics.RaycastAll(baslangic, yon.normalized, mesafe, ~0, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
            {
                return true;
            }

            if (hit.transform.CompareTag("Player") ||
                hit.transform.name.Contains("Companion") || hit.transform.name.Contains("Oyuncu") || hit.transform.name.Contains("Yardimci"))
            {
                continue;
            }

            if (hit.transform.GetComponent<DusmanKontrol>() != null || hit.transform.GetComponentInParent<DusmanKontrol>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
