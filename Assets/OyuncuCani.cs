using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ana oyuncunun canını, modern AAA taktiksel HUD can barını,
/// hasar izi efektini (Ghost Health) ve yeniden başlama sistemini yönetir.
/// </summary>
public class OyuncuCani : MonoBehaviour
{
    [Header("Sağlık Değerleri")]
    [Tooltip("Karakterin başlangıçtaki maksimum canı.")]
    public float maksimumCan = 100f;

    [Tooltip("Karakterin şu anki güncel canı.")]
    public float mevcutCan = 100f;

    [Header("HUD Tasarım Ayarları")]
    public float hudGenisligi = 290f;
    public float hudYuksekligi = 68f;

    [Header("Hasar Koruması ve Ölüm")]
    [Tooltip("Tek bir mermiden alınabilecek maksimum hasar.")]
    public float mermiHasarLimiti = 8f;

    [Tooltip("Mermi spamını engelleyen hasar koruması.")]
    public float hasarKorumasi = 0.20f;

    [Tooltip("Öldükten sonra yeniden başlama süresi.")]
    public float yenidenBaslamaGecikmesi = 1.8f;

    private float ghostCan = 100f; // Yumuşak azalan hasar izi (Ghost bar)
    private float sonHasarAlmaZamani = -1f;
    private float hasarFlasSure = 0f;
    private bool oldu = false;

    private Texture2D beyazDokusu;
    private GUIStyle barYaziStili;
    private GUIStyle olumStili;

    public bool IsDead => oldu;

    void Awake()
    {
        mevcutCan = maksimumCan;
        ghostCan = maksimumCan;
    }

    void Start()
    {
        OlusturDokular();
    }

    void Update()
    {
        // Ghost bar yumuşakça asıl cana yaklaşsın
        if (ghostCan > mevcutCan)
        {
            ghostCan = Mathf.Lerp(ghostCan, mevcutCan, Time.deltaTime * 3.5f);
        }
        else
        {
            ghostCan = mevcutCan;
        }

        if (hasarFlasSure > 0f)
        {
            hasarFlasSure -= Time.deltaTime * 2.5f;
            if (hasarFlasSure < 0f) hasarFlasSure = 0f;
        }
    }

    /// <summary>
    /// Düşman mermisi veya harici bir hasar çarptığında çağrılır.
    /// </summary>
    public void HasarAl(float miktar)
    {
        if (oldu) return;

        // Üst üste mermi spamını engelle
        if (Time.time < sonHasarAlmaZamani + hasarKorumasi) return;
        sonHasarAlmaZamani = Time.time;

        // Hasar miktarı (dengeli artırılmış hasar)
        float gercekHasar = Mathf.Min(miktar, mermiHasarLimiti);
        mevcutCan -= gercekHasar;
        hasarFlasSure = 1.0f; // Kırmızı ekran flaşı

        Debug.Log($"[Oyuncu Canı] Hasar Alındı: -{gercekHasar} HP | Kalan Can: {mevcutCan} / {maksimumCan}");

        if (mevcutCan <= 0f)
        {
            mevcutCan = 0f;
            Olum();
        }
    }

    public void CanYenile(float miktar)
    {
        if (oldu) return;
        mevcutCan = Mathf.Min(maksimumCan, mevcutCan + miktar);
        ghostCan = mevcutCan;
    }

    private void Olum()
    {
        if (oldu) return;
        oldu = true;

        KarakterHareket kh = GetComponent<KarakterHareket>();
        if (kh != null) kh.enabled = false;

        KarakterAtes ka = GetComponent<KarakterAtes>();
        if (ka != null) ka.enabled = false;

        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null) anim.CrossFade("infantry_combat_idle", 0.1f);

        StartCoroutine(YenidenBaslatRutini());
    }

    private IEnumerator YenidenBaslatRutini()
    {
        yield return new WaitForSeconds(yenidenBaslamaGecikmesi);
        string aktifSahne = SceneManager.GetActiveScene().name;
        if (!string.IsNullOrEmpty(aktifSahne))
        {
            SceneManager.LoadScene(aktifSahne);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
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

    void OnGUI()
    {
        if (beyazDokusu == null) OlusturDokular();

        // 1. Ekran Hasar Flaşı (Kırmızı Vignette)
        if (hasarFlasSure > 0.01f)
        {
            Color eskiRenk = GUI.color;
            GUI.color = new Color(0.9f, 0.12f, 0.12f, hasarFlasSure * 0.32f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), beyazDokusu);
            GUI.color = eskiRenk;
        }

        // 2. Ölüm Ekranı
        if (oldu)
        {
            if (olumStili == null)
            {
                olumStili = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            Color eski = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), beyazDokusu);

            Yazlab.Yonetim.GoruntuKalitesiYoneticisi.StiliKeskinlestir(olumStili, Mathf.Clamp(Screen.width / 26, 26, 48));
            olumStili.normal.textColor = new Color(1f, 0.25f, 0.25f, 1f);
            GUI.Label(new Rect(0, Screen.height * 0.40f - 35, Screen.width, 65), "GÖREV BAŞARISIZ // ÖLDÜNÜZ", olumStili);

            Yazlab.Yonetim.GoruntuKalitesiYoneticisi.StiliKeskinlestir(olumStili, Mathf.Clamp(Screen.width / 45, 16, 28));
            olumStili.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height * 0.40f + 35, Screen.width, 45), "Oyun Yeniden Başlatılıyor...", olumStili);

            GUI.color = eski;
            return;
        }

        // Bulmaca veya NPC Diyalog penceresi açıkken sağ alt HUD barlarını gizle
        if (Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik || Yazlab.Yonetim.OllamaNpcDiyalogUI.DiyalogAcikMi)
            return;

        // 3. GTA 5 Tarzı Sade Küçük Dikdörtgen Can Barları
        CizGTA5CanBari();
    }

    private void CizGTA5CanBari()
    {
        float olcek = Yazlab.Yonetim.GoruntuKalitesiYoneticisi.KeskinOlcek;
        float genislik = Mathf.Round(170f * olcek);
        float yukseklik = Mathf.Round(10f * olcek);
        float posX = Mathf.Round(Screen.width - genislik - 22f);

        // 1. Oyuncunun Kendi Can Barı
        float oyuncuBarY = Mathf.Round(Screen.height - 48f * olcek);

        if (barYaziStili == null)
        {
            barYaziStili = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
        }
        Yazlab.Yonetim.GoruntuKalitesiYoneticisi.StiliKeskinlestir(barYaziStili, Mathf.RoundToInt(12 * olcek));
        barYaziStili.normal.textColor = new Color(0.92f, 0.96f, 1.0f, 0.95f);

        Color eskiRenk = GUI.color;

        // "Oyuncu Canı" Etiketi (Yeterli yükseklik ile kesilmeden tam hizalı çizilir)
        GUI.color = Color.white;
        float etiketW = 110f * olcek;
        float etiketH = 22f * olcek;
        Yazlab.Yonetim.GoruntuKalitesiYoneticisi.KeskinEtiketCiz(
            new Rect(posX - etiketW - 8f, oyuncuBarY + (yukseklik - etiketH) * 0.5f, etiketW, etiketH),
            "Oyuncu Canı", barYaziStili);

        // Oyuncu Siyah Çerçeve ve Zemin
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(posX - 1, oyuncuBarY - 1, genislik + 2, yukseklik + 2), beyazDokusu);
        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        GUI.DrawTexture(new Rect(posX, oyuncuBarY, genislik, yukseklik), beyazDokusu);

        // Ghost (Yumuşak Hasar İzi) Dolgusu
        float ghostOran = Mathf.Clamp01(ghostCan / maksimumCan);
        GUI.color = new Color(0.95f, 0.35f, 0.20f, 0.55f);
        GUI.DrawTexture(new Rect(posX, oyuncuBarY, genislik * ghostOran, yukseklik), beyazDokusu);

        // Oyuncu Renk Hesabı (Yeşil -> Turuncu -> Kırmızı)
        float oran = Mathf.Clamp01(mevcutCan / maksimumCan);
        Color barRengi = GetGtaRenk(oran);
        GUI.color = barRengi;
        GUI.DrawTexture(new Rect(posX, oyuncuBarY, genislik * oran, yukseklik), beyazDokusu);

        // 2. Ajanın / Yardımcının Can Barı (Hemen Kendi Barımızın Altında)
        YardimciKarakter ajan = YardimciKarakter.Instance;
        if (ajan == null) ajan = Object.FindAnyObjectByType<YardimciKarakter>();

        float ajanBarY = Mathf.Round(Screen.height - 24f * olcek);

        // "Ajanın Barı" Yazısı (Yeterli yükseklik ile kesilmeden tam hizalı çizilir)
        GUI.color = Color.white;
        Yazlab.Yonetim.GoruntuKalitesiYoneticisi.KeskinEtiketCiz(
            new Rect(posX - etiketW - 8f, ajanBarY + (yukseklik - etiketH) * 0.5f, etiketW, etiketH),
            "Ajanın Barı", barYaziStili);

        // Ajan Siyah Çerçeve ve Zemin
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(new Rect(posX - 1, ajanBarY - 1, genislik + 2, yukseklik + 2), beyazDokusu);
        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        GUI.DrawTexture(new Rect(posX, ajanBarY, genislik, yukseklik), beyazDokusu);

        // Ajan Doluluk ve Renk
        float ajanOran = 0f;
        if (ajan != null)
        {
            ajanOran = Mathf.Clamp01(ajan.mevcutCan / ajan.maksimumCan);
        }
        Color ajanBarRengi = GetGtaRenk(ajanOran);
        GUI.color = ajanBarRengi;
        GUI.DrawTexture(new Rect(posX, ajanBarY, genislik * ajanOran, yukseklik), beyazDokusu);

        GUI.color = eskiRenk;
    }

    private Color GetGtaRenk(float oran)
    {
        Color yesil = new Color(0.20f, 0.82f, 0.28f);   // Canlı Yeşil
        Color turuncu = new Color(0.98f, 0.60f, 0.10f); // Turuncu
        Color kirmizi = new Color(0.90f, 0.15f, 0.15f); // Kırmızı

        if (oran > 0.50f)
        {
            return Color.Lerp(turuncu, yesil, (oran - 0.50f) / 0.50f);
        }
        else
        {
            return Color.Lerp(kirmizi, turuncu, oran / 0.50f);
        }
    }
}
