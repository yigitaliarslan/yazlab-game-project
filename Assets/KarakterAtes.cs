using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Ana karakter için gelişmiş ateş etme kontrolcüsü.
/// Fare sol tıkı veya 'F' tuşu ile ateş eder (Sol Ctrl eğilme tuşu olduğu için ateş etmez).
/// Namlu ışığı (Muzzle Flash), mermi izi (Bullet Tracer), hem 3. şahıs hem 1. şahıs nişangahı (crosshair) ve ateş animasyonunu yönetir.
/// </summary>
public class KarakterAtes : MonoBehaviour
{
    [Header("Ateş Ayarları")]
    [Tooltip("İki atış arasındaki minimum süre (saniye cinsinden).")]
    public float atesAraligi = 0.2f;

    [Tooltip("Merminin maksimum gidebileceği menzil (metre).")]
    public float menzil = 100f;

    [Tooltip("Vurulan fiziksel nesnelere (Rigidbody) uygulanacak itme kuvveti.")]
    public float vurusKuvveti = 250f;

    [Tooltip("Basılı tutulduğunda seri ateş edilsin mi?")]
    public bool seriAtes = true;

    [Tooltip("Merminin çarpabileceği katmanlar.")]
    public LayerMask vurulabilirKatmanlar = ~0;

    [Header("Referanslar")]
    [Tooltip("Nişan yönü için kullanılacak kamera. Boş bırakılırsa Main Camera kullanılır.")]
    public Transform kamera;

    [Tooltip("Merminin çıkacağı namlu noktası. Boş bırakılırsa silahtan otomatik hesaplanır.")]
    public Transform namluNoktasi;

    [Header("Namlu Pozisyon Ayarları (Silahın Namlu Ucu)")]
    [Tooltip("3. şahısta merminin çıkacağı namlu ucunun karaktere göre ofseti (X: sağ, Y: yukarı, Z: ileri).")]
    public Vector3 namluOfset3P = new Vector3(0.22f, 1.20f, 0.95f);

    [Tooltip("1. şahısta (FPS / Counter-Strike) namlu ucunun kameraya göre ofseti (X: sağ, Y: yukarı/aşağı, Z: ileri).")]
    public Vector3 namluOfset1P = new Vector3(0.20f, -0.32f, 0.55f);

    [Header("Görsel Efektler")]
    [Tooltip("Ekranın ortasında nişangah (crosshair) gösterilsin mi?")]
    public bool nisangahGoster = true;

    [Tooltip("Nişangah rengi.")]
    public Color nisangahRengi = new Color(0.95f, 1f, 0.95f, 0.92f);

    [Tooltip("Mermi izi (Tracer) rengi.")]
    public Color mermiIziRengi = new Color(1f, 0.85f, 0.35f, 0.9f);

    [Header("Ses Efektleri (İsteğe Bağlı)")]
    [Tooltip("Ateş edildiğinde çalacak ses efekti.")]
    public AudioClip atesSesi;

    private Animator animator;
    private AudioSource audioSource;
    private LineRenderer mermiIziRenderer;
    private LineRenderer namluFlasRenderer;
    private Light namluIsigi;
    private float sonrakiAtesZamani = 0f;
    private static readonly int ShootHash = Animator.StringToHash("Shoot");
    private bool hasShootTrigger = false;
    private Texture2D beyazTexture;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == "Shoot")
                {
                    hasShootTrigger = true;
                    break;
                }
            }
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.5f;
        }

        if (namluNoktasi == null)
        {
            GameObject namluObj = new GameObject("NamluNoktasi");
            namluObj.transform.SetParent(transform);
            namluObj.transform.localPosition = namluOfset3P;
            namluObj.transform.localRotation = Quaternion.identity;
            namluNoktasi = namluObj.transform;
        }

        KurLineRenderer();
        KurNamluIsigi();
        KurNamluFlasi();
    }

    public Vector3 GetNamluKonumu()
    {
        if (KameraTakip.IsFirstPerson && kamera != null)
        {
            return kamera.position
                + (kamera.right * namluOfset1P.x)
                + (kamera.up * namluOfset1P.y)
                + (kamera.forward * namluOfset1P.z);
        }
        else
        {
            float egilmeCarpani = KarakterHareket.EgiliyorMu ? 0.65f : 1.0f;
            return transform.position
                + (transform.right * namluOfset3P.x)
                + (transform.up * (namluOfset3P.y * egilmeCarpani))
                + (transform.forward * namluOfset3P.z);
        }
    }

    void Start()
    {
        if (kamera == null && Camera.main != null)
        {
            kamera = Camera.main.transform;
        }
    }

    void Update()
    {
        if (Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik) return;

        if (AtesGirdisiVarMi() && Time.time >= sonrakiAtesZamani)
        {
            sonrakiAtesZamani = Time.time + atesAraligi;
            AtesEt();
        }
    }

    void LateUpdate()
    {
        Vector3 namluPos = GetNamluKonumu();
        if (namluNoktasi != null)
        {
            namluNoktasi.position = namluPos;
            if (kamera != null)
            {
                namluNoktasi.rotation = KameraTakip.IsFirstPerson ? kamera.rotation : transform.rotation;
            }
        }
    }

    private bool AtesGirdisiVarMi()
    {
        // NOT: Sol Ctrl ve C tuşları EĞİLME (Crouch) içindir, ateş etmez!
        // Ateş yalnızca Sol Fare Tıkı veya F tuşu ile yapılır.
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            bool fareTiklandi = seriAtes ? Mouse.current.leftButton.isPressed : Mouse.current.leftButton.wasPressedThisFrame;
            if (fareTiklandi) return true;
        }

        if (Keyboard.current != null)
        {
            var kb = Keyboard.current;
            bool tusBasildi = seriAtes ? kb.fKey.isPressed : kb.fKey.wasPressedThisFrame;
            if (tusBasildi) return true;
        }
#endif

        try
        {
            bool fareBasildi = seriAtes ? Input.GetMouseButton(0) : Input.GetMouseButtonDown(0);
            if (fareBasildi) return true;

            bool tusBasildi = seriAtes ? Input.GetKey(KeyCode.F) : Input.GetKeyDown(KeyCode.F);
            if (tusBasildi) return true;
        }
        catch { }

        return false;
    }

    public void AtesEt()
    {
        if (KameraTakip.IsFirstPerson)
        {
            transform.rotation = Quaternion.Euler(0f, KameraTakip.CurrentYaw, 0f);
        }
        else if (kamera != null)
        {
            Vector3 kameraIleri = kamera.forward;
            kameraIleri.y = 0f;
            if (kameraIleri.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(kameraIleri);
            }
        }

        if (animator != null)
        {
            if (hasShootTrigger)
            {
                animator.SetTrigger(ShootHash);
            }
            animator.CrossFade("infantry_combat_shoot", 0.05f);
        }

        if (atesSesi != null && audioSource != null)
        {
            audioSource.PlayOneShot(atesSesi);
        }

        Vector3 baslangicNoktasi = GetNamluKonumu();
        if (namluNoktasi != null)
        {
            namluNoktasi.position = baslangicNoktasi;
        }

        StartCoroutine(NamluIsigiParla(baslangicNoktasi));

        Vector3 hedefNokta;

        Ray kameraRay;
        if (kamera != null)
        {
            Camera cam = kamera.GetComponent<Camera>();
            if (cam != null)
            {
                kameraRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            }
            else
            {
                kameraRay = new Ray(kamera.position, kamera.forward);
            }
        }
        else
        {
            kameraRay = new Ray(transform.position + Vector3.up * 1.2f, transform.forward);
        }

        RaycastHit[] hits = Physics.RaycastAll(kameraRay, menzil, vurulabilirKatmanlar, QueryTriggerInteraction.Ignore);
        RaycastHit hit = default;
        bool hasHit = false;
        float enYakinMesafe = float.MaxValue;

        foreach (var h in hits)
        {
            if (h.collider != null && (h.collider.transform == transform || h.collider.transform.IsChildOf(transform)))
            {
                continue;
            }

            if (h.distance < enYakinMesafe)
            {
                enYakinMesafe = h.distance;
                hit = h;
                hasHit = true;
            }
        }

        if (hasHit)
        {
            hedefNokta = hit.point;

            Rigidbody rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic)
            {
                rb.AddForceAtPosition(kameraRay.direction * vurusKuvveti, hit.point, ForceMode.Impulse);
            }

            DusmanKontrol dusman = hit.collider.GetComponentInParent<DusmanKontrol>();
            if (dusman != null)
            {
                dusman.HasarAl(25f);
            }
            else
            {
                hit.collider.SendMessageUpwards("HasarAl", 25f, SendMessageOptions.DontRequireReceiver);
            }

            CarpmaEfektiGoster(hit.point, hit.normal);
        }
        else
        {
            hedefNokta = kameraRay.origin + (kameraRay.direction * menzil);
        }

        if (mermiIziRenderer != null)
        {
            StartCoroutine(MermiIziCiz(baslangicNoktasi, hedefNokta));
        }
    }

    private void CarpmaEfektiGoster(Vector3 nokta, Vector3 normal)
    {
        GameObject spark = new GameObject("MermiCarpma");
        spark.transform.position = nokta + normal * 0.05f;
        Light spLight = spark.AddComponent<Light>();
        spLight.type = LightType.Point;
        spLight.color = new Color(1f, 0.7f, 0.2f);
        spLight.range = 2.0f;
        spLight.intensity = 3.0f;
        Destroy(spark, 0.08f);
    }

    private IEnumerator NamluIsigiParla(Vector3 namluKonum)
    {
        if (namluIsigi != null)
        {
            namluIsigi.transform.position = namluKonum;
            namluIsigi.enabled = true;
        }

        if (namluFlasRenderer != null)
        {
            Vector3 ileri = (kamera != null && KameraTakip.IsFirstPerson) ? kamera.forward : transform.forward;
            namluFlasRenderer.SetPosition(0, namluKonum);
            namluFlasRenderer.SetPosition(1, namluKonum + (ileri * 0.16f));
            namluFlasRenderer.enabled = true;
        }

        yield return new WaitForSeconds(0.04f);

        if (namluIsigi != null)
        {
            namluIsigi.enabled = false;
        }
        if (namluFlasRenderer != null)
        {
            namluFlasRenderer.enabled = false;
        }
    }

    private IEnumerator MermiIziCiz(Vector3 baslangic, Vector3 bitis)
    {
        mermiIziRenderer.enabled = true;
        mermiIziRenderer.SetPosition(0, baslangic);
        mermiIziRenderer.SetPosition(1, bitis);

        yield return new WaitForSeconds(0.05f);

        if (mermiIziRenderer != null)
        {
            mermiIziRenderer.enabled = false;
        }
    }

    private void KurLineRenderer()
    {
        GameObject lineObj = new GameObject("MermiIzi");
        lineObj.transform.SetParent(transform);
        mermiIziRenderer = lineObj.AddComponent<LineRenderer>();
        mermiIziRenderer.startWidth = 0.035f;
        mermiIziRenderer.endWidth = 0.015f;
        mermiIziRenderer.positionCount = 2;
        mermiIziRenderer.useWorldSpace = true;
        mermiIziRenderer.enabled = false;

        Material lineMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        lineMat.color = mermiIziRengi;
        mermiIziRenderer.material = lineMat;
        mermiIziRenderer.startColor = mermiIziRengi;
        mermiIziRenderer.endColor = new Color(mermiIziRengi.r, mermiIziRengi.g, mermiIziRengi.b, 0.2f);
    }

    private void KurNamluIsigi()
    {
        GameObject lightObj = new GameObject("NamluIsigi");
        lightObj.transform.SetParent(namluNoktasi != null ? namluNoktasi : transform);
        lightObj.transform.localPosition = Vector3.zero;

        namluIsigi = lightObj.AddComponent<Light>();
        namluIsigi.type = LightType.Point;
        namluIsigi.color = new Color(1f, 0.85f, 0.35f);
        namluIsigi.range = 3.5f;
        namluIsigi.intensity = 4.0f;
        namluIsigi.enabled = false;
    }

    private void KurNamluFlasi()
    {
        GameObject flashObj = new GameObject("NamluFlasi");
        flashObj.transform.SetParent(namluNoktasi != null ? namluNoktasi : transform);
        flashObj.transform.localPosition = Vector3.zero;

        namluFlasRenderer = flashObj.AddComponent<LineRenderer>();
        namluFlasRenderer.startWidth = 0.08f;
        namluFlasRenderer.endWidth = 0.02f;
        namluFlasRenderer.positionCount = 2;
        namluFlasRenderer.useWorldSpace = true;
        namluFlasRenderer.enabled = false;

        Material flashMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit"));
        flashMat.color = new Color(1f, 0.95f, 0.4f, 1f);
        namluFlasRenderer.material = flashMat;
        namluFlasRenderer.startColor = new Color(1f, 0.95f, 0.4f, 1f);
        namluFlasRenderer.endColor = new Color(1f, 0.6f, 0.1f, 0.1f);
    }

    void OnGUI()
    {
        // Hem 3. Şahısta (TPS) hem 1. Şahısta (FPS) nişangah her zaman görünür! Sadece bulmaca minigame açıkken gizlenir.
        if (!nisangahGoster || Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik) return;

        if (beyazTexture == null)
        {
            beyazTexture = new Texture2D(1, 1);
            beyazTexture.SetPixel(0, 0, Color.white);
            beyazTexture.Apply();
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float length = KameraTakip.IsFirstPerson ? 9f : 10f;
        float thickness = 2f;
        float gap = KameraTakip.IsFirstPerson ? 3.5f : 5.5f;

        Color eskiRenk = GUI.color;

        // Koyu dış gölge (Her zeminde net görünmesi için)
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(centerX - gap - length - 1f, centerY - (thickness * 0.5f) - 1f, length + 2f, thickness + 2f), beyazTexture);
        GUI.DrawTexture(new Rect(centerX + gap - 1f, centerY - (thickness * 0.5f) - 1f, length + 2f, thickness + 2f), beyazTexture);
        GUI.DrawTexture(new Rect(centerX - (thickness * 0.5f) - 1f, centerY - gap - length - 1f, thickness + 2f, length + 2f), beyazTexture);
        GUI.DrawTexture(new Rect(centerX - (thickness * 0.5f) - 1f, centerY + gap - 1f, thickness + 2f, length + 2f), beyazTexture);

        // Ana nişangah çizgileri + merkez nokta
        GUI.color = nisangahRengi;
        GUI.DrawTexture(new Rect(centerX - gap - length, centerY - (thickness * 0.5f), length, thickness), beyazTexture);
        GUI.DrawTexture(new Rect(centerX + gap, centerY - (thickness * 0.5f), length, thickness), beyazTexture);
        GUI.DrawTexture(new Rect(centerX - (thickness * 0.5f), centerY - gap - length, thickness, length), beyazTexture);
        GUI.DrawTexture(new Rect(centerX - (thickness * 0.5f), centerY + gap, thickness, length), beyazTexture);
        GUI.DrawTexture(new Rect(centerX - 1f, centerY - 1f, 2f, 2f), beyazTexture);

        GUI.color = eskiRenk;
    }
}
