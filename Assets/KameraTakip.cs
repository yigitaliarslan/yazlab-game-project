using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Hibrit Kamera Kontrolcüsü:
/// Normalde 3. Şahıs (Third Person) açısından karakteri takip eder.
/// Fare sağ tıkına basıldığında yumuşakça 1. Şahıs (First Person / Nişan) moduna geçer ve silahı öne getirir.
/// Sağ tık bırakıldığında tekrar 3. şahıs moduna döner.
/// Eğilme (Crouch) durumunda göz seviyesini otomatik olarak siper hizasına indirir.
/// </summary>
public class KameraTakip : MonoBehaviour
{
    [Header("Takip Edilecek Karakter")]
    [Tooltip("Kameranın takip edeceği ana oyuncu. Boş bırakılırsa sahnede otomatik bulunur.")]
    public Transform hedef;

    [Header("3. Şahıs Kamera Ayarları")]
    [Tooltip("3. şahısta kameranın karaktere olan mesafesi.")]
    public float mesafe = 4.5f;

    [Tooltip("Kameranın karakterin hangi yüksekliğine odaklanacağı.")]
    public float bakisYuksekligi = 1.50f;

    [Tooltip("Fare ile kamerayı döndürme hassasiyeti.")]
    public float fareHassasiyeti = 2.0f;

    [Tooltip("3. şahısta kameranın oyuncuyu takip etme hızı.")]
    public float takipHizi = 12.0f;

    [Tooltip("Kameranın dönüş yumuşaklık hızı.")]
    public float donusHizi = 14.0f;

    [Header("1. Şahıs (First Person / Counter-Strike) Ayarları")]
    [Tooltip("Sağ tık basıldığında Counter-Strike tarzı 1. şahıs moduna geçilsin mi?")]
    public bool sagTiklaBirinciSahis = true;

    [Tooltip("1. Şahıs modunda kameranın karaktere göre ofseti (X: Yatay merkez, Y: Counter-Strike göz seviyesi, Z: İleri ofset).")]
    public Vector3 birinciSahisOfset = new Vector3(0.0f, 1.82f, 0.28f);

    [Tooltip("Normal 3. şahıs kamera görüş açısı (FOV).")]
    public float normalFOV = 60f;

    [Tooltip("1. şahıs nişan alma kamera görüş açısı (FOV - Hafif zoom).")]
    public float nisanFOV = 58f;

    [Tooltip("1. şahıs ve 3. şahıs arasındaki geçiş yumuşaklık hızı.")]
    public float gecisHizi = 15f;

    [Header("Açı Sınırları (Dikey)")]
    [Tooltip("Kameranın aşağı bakabilme alt açısı.")]
    public float minPitch = -35f;

    [Tooltip("Kameranın yukarı bakabilme üst açısı.")]
    public float maxPitch = 60f;

    /// <summary>
    /// Kameranın şu an 1. şahıs (First Person) modunda olup olmadığını belirtir.
    /// Diğer bileşenler (örn. KarakterAtes) buradan okuma yapabilir.
    /// </summary>
    public static bool IsFirstPerson { get; private set; } = false;

    /// <summary>
    /// Kameranın anlık yatay açısı (Yaw).
    /// Karakterin ve silahın yönünü gecikmesiz senkronize etmek için kullanılır.
    /// </summary>
    public static float CurrentYaw { get; private set; } = 0f;

    /// <summary>
    /// Kameranın anlık dikey açısı (Pitch).
    /// </summary>
    public static float CurrentPitch { get; private set; } = 15f;

    private float pitch = 15f;
    private float yaw = 0f;
    private Camera cam;
    private float fpsGecis = 0f;
    private float egilmeKameraOfseti = 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam != null)
        {
            // 1. şahısta yakındaki silah ve ellerin kesilmemesi (clipping olmaması) için
            cam.nearClipPlane = 0.03f;
            cam.fieldOfView = normalFOV;
        }
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

        // Fare imlecini oyun ekranına kilitle
        ImleciKilitle(true);
    }

    void Update()
    {
        // Minigame açıkken fare serbesttir, kamerayı döndürme
        if (Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik) return;

        // Fare girdileri ile kamerayı karakter etrafında döndür
        FareyiOku();
        CurrentYaw = yaw;
        CurrentPitch = pitch;

        // ESC / Tıklama ile fare imlecini aç/kapat
        ImlecKontrolu();
    }

    void LateUpdate()
    {
        HedefiBul();

        if (hedef == null) return;

        // Eğilme (Crouch) yüksekliği yumuşak geçişi
        float hedefEgikOfset = KarakterHareket.EgiliyorMu ? -0.68f : 0f;
        egilmeKameraOfseti = Mathf.Lerp(egilmeKameraOfseti, hedefEgikOfset, Time.deltaTime * 11f);

        // Sağ tık kontrolü ile 1. Şahıs (Counter-Strike FPS) moduna geçiş
        bool sagTikBasili = sagTiklaBirinciSahis && SagTikGirdisiAl();
        IsFirstPerson = sagTikBasili;

        // Geçiş ağırlığını güncelle (0: 3. Şahıs, 1: 1. Şahıs)
        float hedefGecis = sagTikBasili ? 1f : 0f;
        fpsGecis = Mathf.MoveTowards(fpsGecis, hedefGecis, Time.deltaTime * gecisHizi);

        Quaternion rotasyon = Quaternion.Euler(pitch, yaw, 0f);

        // Counter-Strike 1. Şahıs Kamera Konumu:
        // Karakterin dünya koordinatlarındaki anlık pozisyonunu ve yatay yönünü baz alır.
        Vector3 yatayIleri = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 yataySag = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        Vector3 fpsKonum = hedef.position
            + Vector3.up * (birinciSahisOfset.y + egilmeKameraOfseti)
            + (yatayIleri * birinciSahisOfset.z)
            + (yataySag * birinciSahisOfset.x);

        // 3. Şahıs Konum Hesabı:
        Vector3 odakNoktasi = hedef.position + Vector3.up * (bakisYuksekligi + egilmeKameraOfseti * 0.75f);
        Vector3 tpsKonum = odakNoktasi - (rotasyon * Vector3.forward * mesafe);

        if (fpsGecis >= 0.999f)
        {
            // === TAM 1. ŞAHIS (COUNTER-STRIKE) MODU ===
            transform.position = fpsKonum;
            transform.rotation = rotasyon;

            // Karakter gövdesini kameranın yatay açısına tam kilitle
            hedef.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        else if (fpsGecis <= 0.001f)
        {
            // === TAM 3. ŞAHIS MODU ===
            transform.position = Vector3.Lerp(transform.position, tpsKonum, Time.deltaTime * takipHizi);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotasyon, Time.deltaTime * donusHizi);
        }
        else
        {
            // === 3. ŞAHIS İLE 1. ŞAHIS ARASINDAKİ YUMUŞAK GEÇİŞ ===
            float t = Mathf.SmoothStep(0f, 1f, fpsGecis);
            Vector3 araKonum = Vector3.Lerp(tpsKonum, fpsKonum, t);
            transform.position = araKonum;
            transform.rotation = Quaternion.Slerp(transform.rotation, rotasyon, t);

            // Karakter gövdesini de ileri yöne yumuşakça hizala
            hedef.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // Görüş açısını (FOV) geçişe göre ayarla
        if (cam != null)
        {
            cam.fieldOfView = Mathf.Lerp(normalFOV, nisanFOV, fpsGecis);
        }
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
