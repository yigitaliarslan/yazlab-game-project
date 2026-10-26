using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Hibrit Taktiksel Kamera Kontrolcüsü:
/// - 3. Şahıs (Third Person) modunda "Sağ Omuz Üstü" (Over-the-Shoulder) açısıyla takip eder.
///   Böylece karakter ekranın sol-alt kısmında konumlanır, ekranın tam ortasındaki nişangah (crosshair)
///   asla karakterin sırtına/koluna denk gelmez ve hedef her zaman net görünür!
/// - Duvar çarpışma koruması (Camera Collision) sayesinde dar odalarda duvarların içine girmez.
/// - Fare sağ tıkına basıldığında yumuşakça 1. Şahıs (First Person / Nişan) moduna geçer.
/// - Eğilme (Crouch) durumunda göz seviyesini otomatik olarak siper hizasına indirir.
/// </summary>
public class KameraTakip : MonoBehaviour
{
    [Header("Takip Edilecek Karakter")]
    [Tooltip("Kameranın takip edeceği ana oyuncu. Boş bırakılırsa sahnede otomatik bulunur.")]
    public Transform hedef;

    [Header("3. Şahıs (Sağ Omuz Üstü / Over-The-Shoulder) Kamera Ayarları")]
    [Tooltip("3. şahısta kameranın karaktere olan mesafesi.")]
    public float mesafe = 3.5f;

    [Tooltip("Kameranın karakterin hangi yüksekliğine odaklanacağı (Baş/Omuz üstü hizası).")]
    public float bakisYuksekligi = 1.74f;

    [Tooltip("Kameranın sağ omuz dışına yatay kaydırma miktarı (Nişangahın karakterin üstüne binmemesi için).")]
    public float omuzSagOfset = 0.72f;

    [Tooltip("Fare ile kamerayı döndürme hassasiyeti.")]
    public float fareHassasiyeti = 2.0f;

    [Tooltip("3. şahısta kameranın oyuncuyu takip etme hızı.")]
    public float takipHizi = 16.0f;

    [Tooltip("Kameranın dönüş yumuşaklık hızı.")]
    public float donusHizi = 18.0f;

    [Header("1. Şahıs (First Person / Counter-Strike) Ayarları")]
    [Tooltip("Sağ tık basıldığında Counter-Strike tarzı 1. şahıs moduna geçilsin mi?")]
    public bool sagTiklaBirinciSahis = true;

    [Tooltip("1. Şahıs modunda kameranın karaktere göre ofseti (X: Yatay merkez, Y: Göz seviyesi, Z: İleri ofset).")]
    public Vector3 birinciSahisOfset = new Vector3(0.0f, 1.82f, 0.28f);

    [Tooltip("Normal 3. şahıs kamera görüş açısı (FOV).")]
    public float normalFOV = 60f;

    [Tooltip("1. şahıs nişan alma kamera görüş açısı (FOV - Hafif zoom).")]
    public float nisanFOV = 55f;

    [Tooltip("1. şahıs ve 3. şahıs arasındaki geçiş yumuşaklık hızı.")]
    public float gecisHizi = 15f;

    [Header("Açı Sınırları (Dikey)")]
    [Tooltip("Kameranın aşağı bakabilme alt açısı.")]
    public float minPitch = -32f;

    [Tooltip("Kameranın yukarı bakabilme üst açısı.")]
    public float maxPitch = 55f;

    public static bool IsFirstPerson { get; private set; } = false;
    public static float CurrentYaw { get; private set; } = 0f;
    public static float CurrentPitch { get; private set; } = 8f;

    private float pitch = 8f;
    private float yaw = 0f;
    private Camera cam;
    private float fpsGecis = 0f;
    private float egilmeKameraOfseti = 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.nearClipPlane = 0.03f;
            cam.fieldOfView = normalFOV;
        }

        // Sahnedeki eski Inspector değerlerini yeni omuz üstü (Over-the-Shoulder) standartlarına yükselt
        if (bakisYuksekligi < 1.65f) bakisYuksekligi = 1.74f;
        if (omuzSagOfset < 0.55f) omuzSagOfset = 0.72f;
        if (mesafe > 3.8f) mesafe = 3.5f;
    }

    void Start()
    {
        HedefiBul();

        if (hedef != null)
        {
            yaw = hedef.eulerAngles.y;
        }
        CurrentYaw = yaw;
        CurrentPitch = pitch;

        ImleciKilitle(true);
    }

    void Update()
    {
        if (Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik) return;

        FareyiOku();
        CurrentYaw = yaw;
        CurrentPitch = pitch;

        ImlecKontrolu();
    }

    void LateUpdate()
    {
        HedefiBul();
        if (hedef == null) return;

        // Eğilme (Crouch) yüksekliği yumuşak geçişi
        float hedefEgikOfset = KarakterHareket.EgiliyorMu ? -0.62f : 0f;
        egilmeKameraOfseti = Mathf.Lerp(egilmeKameraOfseti, hedefEgikOfset, Time.deltaTime * 12f);

        // Sağ tık kontrolü ile 1. Şahıs (FPS) moduna geçiş
        bool sagTikBasili = sagTiklaBirinciSahis && SagTikGirdisiAl();
        IsFirstPerson = sagTikBasili;

        float hedefGecis = sagTikBasili ? 1f : 0f;
        fpsGecis = Mathf.MoveTowards(fpsGecis, hedefGecis, Time.deltaTime * gecisHizi);

        Quaternion rotasyon = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 yatayIleri = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 yataySag = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        // 1. Şahıs (FPS) Kamera Konumu:
        Vector3 fpsKonum = hedef.position
            + Vector3.up * (birinciSahisOfset.y + egilmeKameraOfseti)
            + (yatayIleri * birinciSahisOfset.z)
            + (yataySag * birinciSahisOfset.x);

        // 3. Şahıs (Sağ Omuz Üstü / Over-the-Shoulder) Kamera Konumu:
        // Odak noktası karakterin sağ omzunun dışına (+yataySag * omuzSagOfset) ve baş hizasının üstüne alınır.
        // Böylece karakter ekranın solunda kalır, ekranın tam ortasındaki nişangahın önü tamamen AÇIK olur!
        Vector3 kafaMerkez = hedef.position + Vector3.up * (bakisYuksekligi + egilmeKameraOfseti * 0.7f);
        Vector3 omuzOdakNoktasi = kafaMerkez + (yataySag * omuzSagOfset);
        Vector3 istenenTpsKonum = omuzOdakNoktasi - (rotasyon * Vector3.forward * mesafe);

        // Kamera Duvar Çarpışma Koruması (Dar koridorlarda ve kapı önlerinde duvarın arkasına geçmesini önler)
        Vector3 tpsKonum = DuvarCarpismasiniOnle(kafaMerkez, istenenTpsKonum);

        if (fpsGecis >= 0.999f)
        {
            transform.position = fpsKonum;
            transform.rotation = rotasyon;
            hedef.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        else if (fpsGecis <= 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, tpsKonum, Time.deltaTime * takipHizi);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotasyon, Time.deltaTime * donusHizi);
        }
        else
        {
            float t = Mathf.SmoothStep(0f, 1f, fpsGecis);
            transform.position = Vector3.Lerp(tpsKonum, fpsKonum, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotasyon, t);
            hedef.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(normalFOV, nisanFOV, fpsGecis);
        }
    }

    private Vector3 DuvarCarpismasiniOnle(Vector3 baslangic, Vector3 hedefKameraKonumu)
    {
        Vector3 yon = hedefKameraKonumu - baslangic;
        float toplamMesafe = yon.magnitude;
        if (toplamMesafe < 0.05f) return hedefKameraKonumu;

        yon /= toplamMesafe;
        RaycastHit[] hits = Physics.SphereCastAll(baslangic, 0.22f, yon, toplamMesafe, ~0, QueryTriggerInteraction.Ignore);

        float enYakinEngel = toplamMesafe;
        bool engelBulundu = false;

        foreach (var h in hits)
        {
            if (h.collider == null || h.collider.isTrigger) continue;
            if (hedef != null && (h.transform == hedef || h.transform.IsChildOf(hedef))) continue;
            if (h.collider.GetComponentInParent<YardimciKarakter>() != null) continue;

            if (h.distance < enYakinEngel && h.distance > 0.4f)
            {
                enYakinEngel = h.distance;
                engelBulundu = true;
            }
        }

        if (engelBulundu)
        {
            return baslangic + yon * Mathf.Max(0.85f, enYakinEngel - 0.15f);
        }

        return hedefKameraKonumu;
    }

    private void FareyiOku()
    {
        Vector2 delta = FareGirdisiAl();

        yaw += delta.x * fareHassasiyeti;
        pitch -= delta.y * fareHassasiyeti;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private Vector2 FareGirdisiAl()
    {
        Vector2 delta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            delta = Mouse.current.delta.ReadValue() * 0.1f;
        }
#endif

        if (delta == Vector2.zero)
        {
            try
            {
                delta.x = Input.GetAxis("Mouse X");
                delta.y = Input.GetAxis("Mouse Y");
            }
            catch { }
        }

        return delta;
    }

    private bool SagTikGirdisiAl()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
        {
            return true;
        }
#endif
        try
        {
            if (Input.GetMouseButton(1))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    private void ImlecKontrolu()
    {
        bool escBasildi = false;
        bool tiklandi = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) escBasildi = true;
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) tiklandi = true;
#endif

        try
        {
            if (!escBasildi && Input.GetKeyDown(KeyCode.Escape)) escBasildi = true;
            if (!tiklandi && Input.GetMouseButtonDown(0)) tiklandi = true;
        }
        catch { }

        if (escBasildi) ImleciKilitle(false);
        if (tiklandi) ImleciKilitle(true);
    }

    private void ImleciKilitle(bool kilitli)
    {
        Cursor.lockState = kilitli ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !kilitli;
    }

    private void HedefiBul()
    {
        if (hedef != null) return;

        KarakterHareket kh = FindAnyObjectByType<KarakterHareket>();
        if (kh != null)
        {
            hedef = kh.transform;
            return;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            hedef = player.transform;
        }
    }
}
