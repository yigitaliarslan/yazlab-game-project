using UnityEngine;

/// <summary>
/// Oyuncunun yardımcısı olan karakter kontrolcüsü.
/// Haritada sabit durur (kesinlikle hareket etmez), oyuncu hangi yöne dönerse veya nereye giderse
/// sadece yüzünü ve gövdesini oyuncuya doğru döndürür (bana doğru döner).
/// </summary>
public class YardimciKarakter : MonoBehaviour
{
    public enum DonusModu
    {
        [Tooltip("Daima oyuncunun bulunduğu yöne/konuma doğru döner (Sadece bana doğru dönsün).")]
        OyuncuyaBak,

        [Tooltip("Oyuncu hangi yöne bakıyorsa kendisini o yöne döndürür.")]
        OyuncununBaktigiYon
    }

    [Header("Dönüş Ayarları")]
    [Tooltip("Yardımcının dönme davranışı. 'OyuncuyaBak' seçildiğinde sadece bana doğru döner.")]
    public DonusModu donusModu = DonusModu.OyuncuyaBak;

    [Tooltip("Karakterin oyuncuya doğru dönme yumuşaklık hızı.")]
    public float donusHizi = 8.0f;

    [Header("Takip Edilecek Oyuncu")]
    [Tooltip("Döneceği hedef oyuncu. Boş bırakılırsa sahnede otomatik bulunur.")]
    public Transform hedef;

    private Animator animator;
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // Oyuncunun içinden geçmemesi için CapsuleCollider ekle
        CapsuleCollider col = GetComponent<CapsuleCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    void Start()
    {
        HedefiBul();

        // Karakter sabit duracağı için idle (bekleme) animasyonunda kalmasını sağla
        if (animator != null)
        {
            try
            {
                animator.SetBool(IsRunningHash, false);
                animator.CrossFade("infantry_combat_idle", 0.2f);
            }
            catch { }
        }
    }

    void Update()
    {
        HedefiBul();

        if (hedef == null) return;

        Vector3 bakisYonu = Vector3.zero;

        if (donusModu == DonusModu.OyuncuyaBak)
        {
            // Sabit durur, oyuncu neredeyse sadece ona doğru döner
            bakisYonu = hedef.position - transform.position;
        }
        else if (donusModu == DonusModu.OyuncununBaktigiYon)
        {
            // Oyuncu kendi etrafında hangi yöne bakıyorsa o da o yöne döner
            bakisYonu = hedef.forward;
        }

        // Karakterin dik durması, yere eğilmemesi veya göğe bakmaması için Y eksenini sıfırlıyoruz
        bakisYonu.y = 0f;

        if (bakisYonu.sqrMagnitude > 0.001f)
        {
            Quaternion hedefRotasyon = Quaternion.LookRotation(bakisYonu);
            transform.rotation = Quaternion.Slerp(transform.rotation, hedefRotasyon, Time.deltaTime * donusHizi);
        }
    }

    private void HedefiBul()
    {
        if (hedef != null) return;

        // 1. KarakterHareket bileşenine sahip ana oyuncuyu ara
        KarakterHareket kh = FindAnyObjectByType<KarakterHareket>();
        if (kh != null && kh.gameObject != gameObject)
        {
            hedef = kh.transform;
            return;
        }

        // 2. 'Player' etiketli nesneyi ara
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player != gameObject)
        {
            hedef = player.transform;
            return;
        }

        // 3. ToonSoldier_WW2_demo_Prefab isimli nesneyi ara
        GameObject[] nesneler = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var obj in nesneler)
        {
            if (obj != gameObject && obj.GetComponent<KarakterHareket>() != null)
            {
                hedef = obj.transform;
                return;
            }
        }
    }
}
