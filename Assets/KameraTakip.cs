using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Karakteri 3. Şahıs (Third Person) açısından takip eden kamera kontrolcüsü.
/// Fare ile karakterin etrafında serbestçe dönülebilir (Orbit) ve karakteri arkasından yumuşakça takip eder.
/// 1. Şahıs (First Person) modu tamamen kaldırılmıştır.
/// </summary>
public class KameraTakip : MonoBehaviour
{
    [Header("Takip Edilecek Karakter")]
    [Tooltip("Kameranın takip edeceği ana oyuncu. Boş bırakılırsa sahnede otomatik bulunur.")]
    public Transform hedef;

    [Header("3. Şahıs Kamera Ayarları")]
    [Tooltip("Kameranın karaktere olan mesafesi.")]
    public float mesafe = 4.5f;

    [Tooltip("Kameranın karakterin hangi yüksekliğine odaklanacağı.")]
    public float bakisYuksekligi = 1.4f;

    [Tooltip("Fare ile kamerayı döndürme hassasiyeti.")]
    public float fareHassasiyeti = 2.0f;

    [Tooltip("Kameranın oyuncuyu takip etme yumuşaklık hızı.")]
    public float takipHizi = 12.0f;

    [Tooltip("Kameranın dönüş yumuşaklık hızı.")]
    public float donusHizi = 12.0f;

    [Header("Açı Sınırları (Dikey)")]
    [Tooltip("Kameranın aşağı bakabilme alt açısı.")]
    public float minPitch = -20f;

    [Tooltip("Kameranın yukarı bakabilme üst açısı.")]
    public float maxPitch = 60f;

    private float pitch = 15f;
    private float yaw = 0f;

    void Start()
    {
        HedefiBul();

        if (hedef != null)
        {
            yaw = hedef.eulerAngles.y;
        }

        // Fare imlecini oyun ekranına kilitle
        ImleciKilitle(true);
    }

    void Update()
    {
        // Fare girdileri ile kamerayı karakter etrafında döndür
        FareyiOku();

        // ESC / Tıklama ile fare imlecini aç/kapat
        ImlecKontrolu();
    }

    void LateUpdate()
    {
        HedefiBul();

        if (hedef == null) return;

        // 3. Şahıs (Third Person) kamera hesaplaması
        Quaternion rotasyon = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 odakNoktasi = hedef.position + Vector3.up * bakisYuksekligi;
        Vector3 istenenKonum = odakNoktasi - (rotasyon * Vector3.forward * mesafe);

        // Kameranın pozisyonunu ve açısını yumuşakça güncelle
        transform.position = Vector3.Lerp(transform.position, istenenKonum, Time.deltaTime * takipHizi);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotasyon, Time.deltaTime * donusHizi);
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
