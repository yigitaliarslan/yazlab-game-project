using UnityEngine;

/// <summary>
/// Oyuncunun yanında duran ve oyuncu nereye giderse gitsin ona doğru bakan yoldaş karakter kontrolcüsü.
/// </summary>
public class YoldasKarakter : MonoBehaviour
{
    [Header("Hedef (Oyuncu)")]
    [Tooltip("Karakterin bakacağı ve yanında duracağı oyuncu. Boş bırakılırsa otomatik bulunur.")]
    public Transform hedef;

    [Header("Bakış Ayarları")]
    [Tooltip("Karakter daima oyuncuya doğru baksın mı?")]
    public bool oyuncuyaBaksin = true;

    [Tooltip("Karakterin oyuncuya doğru dönme yumuşaklık hızı.")]
    public float donusHizi = 8.0f;

    [Header("Yanında Durma ve Takip Ayarları")]
    [Tooltip("Oyuncu hareket ettiğinde yanında kalmak için onu takip etsin mi?")]
    public bool beniTakipEtsin = true;

    [Tooltip("Oyuncunun ne kadar yanında duracağı (X: Sağ/Sol, Z: İleri/Geri).")]
    public Vector3 yanOfset = new Vector3(1.8f, 0f, -0.2f);

    [Tooltip("Karakterin oyuncuyu takip ederkenki yürüme/koşma hızı.")]
    public float takipHizi = 4.5f;

    [Tooltip("Hedef noktaya bu mesafeden daha yakınsa durur.")]
    public float durmaMesafesi = 0.4f;

    private Animator animator;
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private bool hasIsRunningParam = false;

    void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == "IsRunning")
                {
                    hasIsRunningParam = true;
                    break;
                }
            }
        }
    }

    void Start()
    {
        HedefiKontrolEt();

        // Eğer başlangıçta hedef bulunamadıysa sahnede ara
        HedefiKontrolEt();

        // Eğer başlangıçta hedef varsa, doğrudan yanına yerleştir
        if (hedef != null)
        {
            Vector3 baslangicPozisyonu = hedef.TransformPoint(yanOfset);
            baslangicPozisyonu.y = transform.position.y;
            transform.position = baslangicPozisyonu;

            // İlk bakış yönünü ayarla
            BakisYonuGuncelle(true);
        }
    }

    void Update()
    {
        HedefiKontrolEt();

        if (hedef == null) return;

        // 1. Oyuncunun her gittiği yönde ona doğru bakması
        if (oyuncuyaBaksin)
        {
            BakisYonuGuncelle(false);
        }

        // 2. Oyuncunun yanında durması ve takip etmesi
        if (beniTakipEtsin)
        {
            TakipEt();
        }
        else
        {
            AnimasyonGuncelle(false);
        }
    }

    private void HedefiKontrolEt()
    {
        if (hedef != null) return;

        // 1. 'Player' tagine sahip nesneyi ara
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null && playerObj != gameObject)
        {
            hedef = playerObj.transform;
            return;
        }

        // 2. 'Oyuncu' veya 'Player' isimli nesneyi ara
        playerObj = GameObject.Find("Oyuncu");
        if (playerObj == null) playerObj = GameObject.Find("Player");
        if (playerObj != null && playerObj != gameObject)
        {
            hedef = playerObj.transform;
            return;
        }

        // 3. OyuncuHareket bileşenine sahip nesneyi ara
        OyuncuHareket hareket = FindAnyObjectByType<OyuncuHareket>();
        if (hareket != null && hareket.gameObject != gameObject)
        {
            hedef = hareket.transform;
            return;
        }

        // 4. Son çare olarak Ana Kamerayı hedef al
        if (Camera.main != null && Camera.main.gameObject != gameObject)
        {
            hedef = Camera.main.transform;
        }
    }

    private void BakisYonuGuncelle(bool aninda)
    {
        if (hedef == null) return;

        // Karakterin dik durması ve yere eğilmemesi için Y eksenini sıfırlıyoruz
        Vector3 bakisYonu = hedef.position - transform.position;
        bakisYonu.y = 0f;

        if (bakisYonu.sqrMagnitude > 0.001f)
        {
            Quaternion hedefRotasyon = Quaternion.LookRotation(bakisYonu);
            if (aninda)
            {
                transform.rotation = hedefRotasyon;
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRotasyon, Time.deltaTime * donusHizi);
            }
        }
    }

    private void TakipEt()
    {
        // Hedefin yerel eksenine göre yanındaki konumu hesapla
        Vector3 hedefKonum = hedef.TransformPoint(yanOfset);
        hedefKonum.y = transform.position.y; // Zemin yüksekliğini koru

        float mesafe = Vector3.Distance(transform.position, hedefKonum);

        if (mesafe > durmaMesafesi)
        {
            // Hedeflenen yan konuma doğru yumuşakça hareket et
            Vector3 hareketYonu = (hedefKonum - transform.position).normalized;
            transform.position += hareketYonu * (takipHizi * Time.deltaTime);

            AnimasyonGuncelle(true);
        }
        else
        {
            // Durma mesafesindeyiz, yanındayız
            AnimasyonGuncelle(false);
        }
    }

    private void AnimasyonGuncelle(bool kosuyor)
    {
        if (animator == null) return;

        if (hasIsRunningParam)
        {
            animator.SetBool(IsRunningHash, kosuyor);
        }
        else
        {
            // Parametre tanımlı değilse doğrudan state geçişi yap
            string hedefState = kosuyor ? "infantry_combat_run" : "infantry_combat_idle";
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (!current.IsName(hedefState))
            {
                animator.CrossFade(hedefState, 0.2f);
            }
        }
    }
}
