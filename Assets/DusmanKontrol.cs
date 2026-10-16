using System.Collections;
using UnityEngine;

/// <summary>
/// Gelişmiş Düşman Asker Yapay Zekası.
/// - Oyuncuyu gördüğünde ona döner ve ateş etmeye başlar.
/// - Başının üzerinde süzülen dinamik 3D / GUI Can Barı (Floating Health Bar) gösterir.
/// - Oyuncu ona sıktığında canı azalır.
/// - Oyuncuya ateş ettiğinde oyuncunun sağ alttaki can barını azaltır.
/// - Öldüğünde yere yığılma/ölüm animasyonu ve duman efektiyle kaybolur.
/// </summary>
public class DusmanKontrol : MonoBehaviour
{
    [Header("Sağlık Ayarları")]
    [Tooltip("Düşmanın maksimum canı.")]
    public float maksimumCan = 100f;

    [Tooltip("Düşmanın mevcut canı.")]
    public float mevcutCan = 100f;

    [Header("Görüş ve Algılama Ayarları")]
    [Tooltip("Düşmanın oyuncuyu görebileceği maksimum mesafe.")]
    public float gorusMenzili = 25f;

    [Tooltip("Düşmanın oyuncuya doğru dönme hızı.")]
    public float donusHizi = 6.0f;

    [Tooltip("Engel kontrolü için göz hizası yüksekliği.")]
    public float gozYuksekligi = 1.4f;

    [Header("Ateş Etme Ayarları")]
    [Tooltip("İki atış arasındaki süre (saniye).")]
    public float atesAraligi = 1.0f;

    [Tooltip("Her atışta oyuncuya vereceği hasar.")]
    public float atesHasari = 8f;

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
    private Transform oyuncuHedef;
    private OyuncuCani oyuncuCaniBileseni;
    private Animator animator;
    private AudioSource audioSource;
    private LineRenderer mermiIziRenderer;
    private Light namluIsigi;
    private LineRenderer namluFlasRenderer;
    private Transform namluUcu;
    private CapsuleCollider col;

    private float sonrakiAtesZamani = 0f;
    private bool oldu = false;
    private float sonHasarZamani = 0f;

    private Texture2D beyazDokusu;
    private GUIStyle canMetniStili;
    private GUIStyle dusmanBaslikStili;

    public bool IsDead => oldu;

    void Awake()
    {
        maksimumCan = 140f;
        mevcutCan = maksimumCan;
        atesHasari = 9f;
        atesAraligi = 0.75f;
        animator = GetComponent<Animator>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.8f;
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

        // Namlu ucu referansı
        GameObject namlu = new GameObject("DusmanNamluUcu");
        namlu.transform.SetParent(transform);
        namlu.transform.localPosition = new Vector3(0.22f, 1.15f, 0.85f);
        namluUcu = namlu.transform;

        KurMermiEfektleri();
    }

    void Start()
    {
        HedefiBul();
        RengiUygula();
        OlusturDokular();

        // Başlangıçta bekleme animasyonuna al
        if (animator != null)
        {
            animator.CrossFade("infantry_combat_idle", 0.2f);
        }
    }

    public void RengiUygula()
    {
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
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

    private Transform aktifHedef;

    void Update()
    {
        if (oldu) return;

        // En uygun hedefi belirle (Oyuncu veya Müttefik Asker)
        HedefSec();

        if (aktifHedef != null)
        {
            // Hedefe doğru dön
            Vector3 yon = (aktifHedef.position - transform.position);
            yon.y = 0f;
            if (yon.sqrMagnitude > 0.01f)
            {
                Quaternion hedefRot = Quaternion.LookRotation(yon);
                transform.rotation = Quaternion.Slerp(transform.rotation, hedefRot, Time.deltaTime * donusHizi * 1.3f);
            }

            // Ateş etme kontrolü (Daha seri ve zorlayıcı atış!)
            if (Time.time >= sonrakiAtesZamani)
            {
                sonrakiAtesZamani = Time.time + atesAraligi + Random.Range(-0.08f, 0.15f);
                AtesEt();
            }
        }
    }

    private void HedefSec()
    {
        HedefiBul();

        Transform enIyi = null;
        float enKisaMesafe = gorusMenzili;

        // 1. Oyuncu
        if (oyuncuHedef != null && (oyuncuCaniBileseni == null || !oyuncuCaniBileseni.IsDead))
        {
            float mesafe = Vector3.Distance(transform.position, oyuncuHedef.position);
            if (mesafe <= enKisaMesafe && HedefiGoruyorMu(oyuncuHedef))
            {
                enKisaMesafe = mesafe;
                enIyi = oyuncuHedef;
            }
        }

        // 2. Müttefik Asker (Yardımcı Karakter)
        if (YardimciKarakter.Instance != null && !YardimciKarakter.Instance.IsDead)
        {
            Transform yk = YardimciKarakter.Instance.transform;
            float mesafe = Vector3.Distance(transform.position, yk.position);
            if (mesafe <= enKisaMesafe && HedefiGoruyorMu(yk))
            {
                enKisaMesafe = mesafe;
                enIyi = yk;
            }
        }

        aktifHedef = enIyi;
    }

    private bool HedefiGoruyorMu(Transform t)
    {
        if (t == null) return false;
        Vector3 goz = transform.position + Vector3.up * gozYuksekligi;
        Vector3 hedefNokta = t.position + Vector3.up * 1.0f;
        RaycastHit hit;
        if (Physics.Raycast(goz, (hedefNokta - goz).normalized, out hit, gorusMenzili))
        {
            if (hit.transform == t || hit.transform.IsChildOf(t)) return true;
        }
        return false;
    }

    /// <summary>
    /// Düşmanın oyuncuya veya yardımcı askere ateş etme mekaniği.
    /// </summary>
    private void AtesEt()
    {
        if (oldu || aktifHedef == null) return;

        // 1. Ateş Animasyonu
        if (animator != null)
        {
            animator.CrossFade("infantry_combat_shoot", 0.05f);
        }

        // 2. Ateş Sesi
        if (audioSource != null)
        {
            if (atesSesi != null)
            {
                audioSource.PlayOneShot(atesSesi);
            }
            else
            {
                audioSource.pitch = Random.Range(0.85f, 1.15f);
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

        // 7. Hasar Ver (Oyuncu veya Müttefik Karakter)
        if (aktifHedef == oyuncuHedef && oyuncuCaniBileseni != null && !oyuncuCaniBileseni.IsDead)
        {
            oyuncuCaniBileseni.HasarAl(atesHasari);
        }
        else
        {
            aktifHedef.SendMessage("HasarAl", atesHasari, SendMessageOptions.DontRequireReceiver);
        }
    }

    /// <summary>
    /// Oyuncunun mermisi düşmana çarptığında KarakterAtes tarafından çağrılır.
    /// </summary>
    public void HasarAl(float miktar)
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
        // Karakterin rengini anlık beyaza/parlak kırmızıya flaşlat
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
    private void Olum()
    {
        if (oldu) return;
        oldu = true;

        // Çarpışmayı ve fiziksel etkileri kaldır
        if (col != null) col.enabled = false;

        // Efektleri kapat
        if (namluIsigi != null) namluIsigi.enabled = false;
        if (namluFlasRenderer != null) namluFlasRenderer.enabled = false;
        if (mermiIziRenderer != null) mermiIziRenderer.enabled = false;

        // Ölüm sekansını başlat: Yere yığılma + duman partikülleri + yok oluş
        StartCoroutine(OlumSekansi());
    }

    private IEnumerator OlumSekansi()
    {
        // 1. Aşamada: Karakter geriye doğru yere yığılır (Yumuşak yere düşme hareketi)
        float gecenSure = 0f;
        float dususSuresi = 0.7f;
        Quaternion baslangicRot = transform.rotation;
        // Geriye doğru 85 derece devrilme
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

        // 2. Aşamada: Yerde 1.2 saniye bekler (Ölüm anı net görünsün)
        yield return new WaitForSeconds(1.2f);

        // 3. Aşamada: Küçülerek veya saydamlaşarak kaybolur (Disappear)
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

        // Tamamen sahneden kaldır
        Destroy(gameObject);
    }

    private void HedefiBul()
    {
        if (oyuncuHedef != null && oyuncuCaniBileseni != null) return;

        // 1. KarakterHareket'e sahip ana oyuncuyu bul
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

        // 2. 'Player' etiketli nesneyi bul
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null && player != gameObject)
        {
            oyuncuHedef = player.transform;
            oyuncuCaniBileseni = player.GetComponent<OyuncuCani>();
        }
    }

    private void KurMermiEfektleri()
    {
        // Mermi İzi (LineRenderer)
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

        // Namlu Işığı (Kırmızı-Sarı Namlu Flaş Işığı)
        GameObject lightObj = new GameObject("DusmanNamluIsigi");
        lightObj.transform.SetParent(namluUcu != null ? namluUcu : transform);
        lightObj.transform.localPosition = Vector3.zero;

        namluIsigi = lightObj.AddComponent<Light>();
        namluIsigi.type = LightType.Point;
        namluIsigi.color = new Color(1f, 0.4f, 0.1f);
        namluIsigi.range = 3.5f;
        namluIsigi.intensity = 4.0f;
        namluIsigi.enabled = false;

        // Namlu Görsel Flaşı
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

    void OnGUI()
    {
        // Öldüyse can barı çizilmez
        if (oldu) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Düşmanın başının üzerindeki 3D dünya noktası
        Vector3 dunyaKonumu = transform.position + Vector3.up * canBariYukseklikOfseti;

        // Kameranın önünde mi kontrol et
        Vector3 ekranNoktasi = cam.WorldToScreenPoint(dunyaKonumu);
        if (ekranNoktasi.z <= 0.1f) return; // Kamera arkasında, çizme

        // Unity GUI Y koordinatı ters olduğundan (0 üsttedir):
        float guiY = Screen.height - ekranNoktasi.y;
        float guiX = ekranNoktasi.x;

        // Mesafe perspektifine göre hafif ölçekleme (uzaktayken çok devasa olmasın)
        float mesafe = Vector3.Distance(cam.transform.position, dunyaKonumu);
        float olcek = Mathf.Clamp(15f / Mathf.Max(mesafe, 5f), 0.75f, 1.35f);

        float genislik = canBariGenislik * olcek;
        float yukseklik = canBariYukseklik * olcek;

        float solX = guiX - (genislik * 0.5f);
        float ustY = guiY - yukseklik;

        Color eskiRenk = GUI.color;

        // 1. Arka Plan Siyah Çerçeve Paneli
        GUI.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);
        GUI.DrawTexture(new Rect(solX - 2, ustY - 2, genislik + 4, yukseklik + 4), beyazDokusu);

        // 2. Boş Bar Zemini (Koyu kırmızı zemin)
        GUI.color = new Color(0.25f, 0.05f, 0.05f, 0.9f);
        GUI.DrawTexture(new Rect(solX, ustY, genislik, yukseklik), beyazDokusu);

        // 3. Kırmızı Can Dolgusu
        float oran = Mathf.Clamp01(mevcutCan / maksimumCan);
        float dolguGenislik = genislik * oran;

        // Hasar yeni alındıysa can barı anlık parlak sarı/beyaz parlasın
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

        // 5. "DÜŞMAN" Başlığı (Barın üzerinde)
        if (dusmanBaslikStili == null)
        {
            dusmanBaslikStili = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(10 * olcek),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerCenter
            };
            dusmanBaslikStili.normal.textColor = new Color(1f, 0.4f, 0.4f, 0.95f);
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(solX - 20, ustY - 16 * olcek, genislik + 40, 16 * olcek), "⚔ DÜŞMAN", dusmanBaslikStili);

        // 6. Can Miktarı Metni: "75 / 100" (Barın ortasında)
        if (canMetniStili == null)
        {
            canMetniStili = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(9 * olcek),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            canMetniStili.normal.textColor = Color.white;
        }

        string canYazisi = $"{Mathf.CeilToInt(mevcutCan)} / {Mathf.CeilToInt(maksimumCan)}";
        GUI.Label(new Rect(solX, ustY, genislik, yukseklik), canYazisi, canMetniStili);

        GUI.color = eskiRenk;
    }
}
