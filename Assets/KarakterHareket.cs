using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


[RequireComponent(typeof(CharacterController))]
public class KarakterHareket : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    [Tooltip("Karakterin yürüme/koşma hızı.")]
    public float hiz = 5.0f;

    [Tooltip("Eğilirken (Crouch) taktiksel ilerleme hızı.")]
    public float egilmeHizi = 2.6f;

    [Tooltip("Karakterin bastığınız yöne dönme yumuşaklık hızı.")]
    public float donusHizi = 16.0f;

    [Header("Eğilme (Crouch / Siper Gizliliği) Ayarları")]
    [Tooltip("Normal ayakta duruş yüksekliği.")]
    public float normalBoy = 1.8f;

    [Tooltip("Eğildiğinde karakter kapsül yüksekliği (Alçak siperlerin arkasında gizlenmek için).")]
    public float egilmeBoy = 0.95f;

    [Tooltip("Eğilme/kalkma geçiş yumuşaklığı.")]
    public float egilmeGecisHizi = 10.0f;

    [Header("Zıplama ve Yerçekimi")]
    [Tooltip("Space tuşuna basıldığında zıplama kuvveti.")]
    public float ziplamaGucu = 6.5f;

    [Tooltip("Yerçekimi ivmesi.")]
    public float yercekimi = 20.0f;

    [Header("Kamera Referansı")]
    [Tooltip("Hareket yönünün referans alacağı kamera (Boş bırakılırsa Main Camera kullanılır).")]
    public Transform kamera;

    /// <summary>
    /// Oyuncunun şu anda eğilip eğilmediğini tüm sistemlere (Kamera, Taret, Siper) bildirir.
    /// </summary>
    public static bool EgiliyorMu { get; private set; }

    private CharacterController controller;
    private Animator animator;
    private float dikeyHiz = 0f;
    private bool egilmeToggle = false;
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private bool hasIsRunningParam = false;

    private GUIStyle durusKutuStili;
    private GUIStyle durusYaziStili;
    private Texture2D durusArkaTex;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        controller.height = normalBoy;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, normalBoy * 0.5f, 0f);
        controller.stepOffset = 0.38f;
        controller.slopeLimit = 50f;
        EgiliyorMu = false;

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == "IsRunning")
                {
                    hasIsRunningParam = true;
                    break;
                }
            }
        }

        if (GetComponent<KarakterAtes>() == null)
        {
            gameObject.AddComponent<KarakterAtes>();
        }

        if (GetComponent<OyuncuCani>() == null)
        {
            gameObject.AddComponent<OyuncuCani>();
        }
    }

    void Start()
    {
        if (kamera == null && Camera.main != null)
        {
            kamera = Camera.main.transform;
        }

        if (!gameObject.CompareTag("Player"))
        {
            try { gameObject.tag = "Player"; } catch { }
        }

        // Eğer sahnede eski merdiven korkulukları / yan boşlukları kalmışsa çalışma zamanında da koruma duvarlarını ve rampayı garantile
        MerdivenBosluklariniGuvenliKapat();
    }

    private void MerdivenBosluklariniGuvenliKapat()
    {
        GameObject bolge3 = GameObject.Find("4_Bolum3_IcKaleKulesi_Ve_NpcOdasi") ?? GameObject.Find("5_Bolge3_YikikKule_Merdivenler_Ve_Npc");
        if (bolge3 == null) return;

        // Eski ince platform varsa altını doldur
        Transform ustPlat = bolge3.transform.Find("Kule_IkinciKat_Platformu");
        if (ustPlat != null && ustPlat.localScale.y < 1.5f)
        {
            ustPlat.position = new Vector3(0f, 1.1f, 36.5f);
            ustPlat.localScale = new Vector3(16.5f, 2.2f, 15f);
        }

        // Merdivenin sol ve sağ yanlarındaki boşlukları kapat (Aşağı düşüp sıkışmayı %100 engeller)
        if (GameObject.Find("MerdivenHolu_SolDuvar") == null)
        {
            Material mat = ustPlat != null ? ustPlat.GetComponent<MeshRenderer>()?.sharedMaterial : null;

            GameObject solDuvar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solDuvar.name = "MerdivenHolu_SolDuvar";
            solDuvar.transform.SetParent(bolge3.transform);
            solDuvar.transform.position = new Vector3(-5.6f, 4.5f, 25.6f);
            solDuvar.transform.localScale = new Vector3(5.2f, 9.0f, 7.2f);
            if (mat != null) solDuvar.GetComponent<MeshRenderer>().sharedMaterial = mat;

            GameObject sagDuvar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sagDuvar.name = "MerdivenHolu_SagDuvar";
            sagDuvar.transform.SetParent(bolge3.transform);
            sagDuvar.transform.position = new Vector3(5.6f, 4.5f, 25.6f);
            sagDuvar.transform.localScale = new Vector3(5.2f, 9.0f, 7.2f);
            if (mat != null) sagDuvar.GetComponent<MeshRenderer>().sharedMaterial = mat;

            // Merdiven basamaklarını genişlet ve pürüzsüz rampa ekle
            Transform merdivenGrup = bolge3.transform.Find("Tas_Merdivenler");
            if (merdivenGrup != null)
            {
                Transform kSol = merdivenGrup.Find("Korkuluk_Sol");
                if (kSol != null) Destroy(kSol.gameObject);
                Transform kSag = merdivenGrup.Find("Korkuluk_Sag");
                if (kSag != null) Destroy(kSag.gameObject);

                for (int i = 0; i < merdivenGrup.childCount; i++)
                {
                    Transform b = merdivenGrup.GetChild(i);
                    if (b.name.StartsWith("Basamak_"))
                    {
                        Vector3 s = b.localScale;
                        b.localScale = new Vector3(6.0f, s.y, s.z);
                    }
                }

                GameObject rampaObj = new GameObject("Merdiven_PuruzsuzRampa");
                rampaObj.transform.SetParent(merdivenGrup);
                rampaObj.transform.position = new Vector3(0f, 1.1f, 25.5f);
                rampaObj.transform.rotation = Quaternion.Euler(-17.4f, 0f, 0f);
                BoxCollider rampaCol = rampaObj.AddComponent<BoxCollider>();
                rampaCol.size = new Vector3(6.0f, 0.15f, 7.4f);
            }
        }
    }

    void Update()
    {
        if ((Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik || Yazlab.Yonetim.OllamaNpcDiyalogUI.DiyalogAcikMi))
        {
            if (animator != null && hasIsRunningParam)
            {
                animator.SetBool(IsRunningHash, false);
            }
            return;
        }

        Vector2 input = KlavyeGirdisiAl();
        bool zipla = ZiplamaGirdisiAl();
        EgilmeGirdisiKontrolEt(zipla);

        // Eğilme kapsül boyutu ve görsel ölçek yumuşak geçişi
        float hedefBoy = EgiliyorMu ? egilmeBoy : normalBoy;
        controller.height = Mathf.Lerp(controller.height, hedefBoy, Time.deltaTime * egilmeGecisHizi);
        controller.center = new Vector3(0f, controller.height * 0.5f, 0f);

        Vector3 hedefOlcek = EgiliyorMu ? new Vector3(1.04f, 0.62f, 1.04f) : Vector3.one;
        transform.localScale = Vector3.Lerp(transform.localScale, hedefOlcek, Time.deltaTime * egilmeGecisHizi);

        // Kameranın baktığı açıya göre 3D hareket vektörü
        Vector3 ileri = kamera != null ? kamera.forward : Vector3.forward;
        Vector3 sag = kamera != null ? kamera.right : Vector3.right;
        ileri.y = 0f;
        sag.y = 0f;
        ileri.Normalize();
        sag.Normalize();

        Vector3 hareketYonu = (ileri * input.y + sag * input.x);
        if (hareketYonu.sqrMagnitude > 1f)
        {
            hareketYonu.Normalize();
        }

        bool hareketEdiyor = hareketYonu.sqrMagnitude > 0.01f;

        // Taktiksel Nişancı Dönüş Kontrolü:
        // Hem 1. Şahıs hem 3. Şahıs (Sağ Omuz Üstü) modunda karakter daima kameranın baktığı nişan yönüne (CurrentYaw) bakar!
        // Böylece A/D ile sağa-sola yürürken (strafe) karakter yan dönüp ekranın ortasındaki nişangahın önünü ASLA kapatmaz!
        Quaternion nisanRotasyonu = Quaternion.Euler(0f, KameraTakip.CurrentYaw, 0f);
        if (KameraTakip.IsFirstPerson)
        {
            transform.rotation = nisanRotasyonu;
        }
        else
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, nisanRotasyonu, Time.deltaTime * donusHizi);
        }

        // Zıplama ve yerçekimi kontrolü
        if (controller.isGrounded)
        {
            dikeyHiz = -1f;

            if (zipla && !EgiliyorMu)
            {
                dikeyHiz = ziplamaGucu;
            }
        }
        else
        {
            dikeyHiz -= yercekimi * Time.deltaTime;
        }

        float aktifHiz = EgiliyorMu ? egilmeHizi : hiz;
        Vector3 toplamHareket = (hareketYonu * aktifHiz) + (Vector3.up * dikeyHiz);
        controller.Move(toplamHareket * Time.deltaTime);

        AnimasyonGuncelle(hareketEdiyor, controller.isGrounded);
    }

    private void EgilmeGirdisiKontrolEt(bool zipladi)
    {
        bool cBasildi = false;
        bool ctrlBasili = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.cKey.wasPressedThisFrame) cBasildi = true;
            if (Keyboard.current.leftCtrlKey.isPressed) ctrlBasili = true;
        }
#endif

        if (!cBasildi)
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.C)) cBasildi = true;
            }
            catch { }
        }

        if (!ctrlBasili)
        {
            try
            {
                if (Input.GetKey(KeyCode.LeftControl)) ctrlBasili = true;
            }
            catch { }
        }

        if (cBasildi)
        {
            egilmeToggle = !egilmeToggle;
        }

        if (zipladi && EgiliyorMu)
        {
            egilmeToggle = false;
        }

        EgiliyorMu = egilmeToggle || ctrlBasili;
    }

    private Vector2 KlavyeGirdisiAl()
    {
        float x = 0f;
        float y = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var kb = Keyboard.current;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
        }
#endif

        if (x == 0f && y == 0f)
        {
            try
            {
                x = Input.GetAxisRaw("Horizontal");
                y = Input.GetAxisRaw("Vertical");
            }
            catch { }
        }

        return new Vector2(x, y);
    }

    private bool ZiplamaGirdisiAl()
    {
        bool zipla = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            zipla = true;
        }
#endif

        if (!zipla)
        {
            try
            {
                if (Input.GetKeyDown(KeyCode.Space)) zipla = true;
            }
            catch { }
        }

        return zipla;
    }

    private void AnimasyonGuncelle(bool kosuyor, bool yerde)
    {
        if (animator == null) return;

        animator.speed = EgiliyorMu ? 0.68f : 1.0f;
        bool animasyonKosu = kosuyor || !yerde;

        if (hasIsRunningParam)
        {
            animator.SetBool(IsRunningHash, animasyonKosu);
        }
        else
        {
            string hedefState = animasyonKosu ? "infantry_combat_run" : "infantry_combat_idle";
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (!current.IsName(hedefState))
            {
                animator.CrossFade(hedefState, 0.2f);
            }
        }
    }

    private void OnGUI()
    {
        if ((Yazlab.Mekanikler.KapiBulmacaPaneli.MinigameAcik || Yazlab.Yonetim.OllamaNpcDiyalogUI.DiyalogAcikMi)) return;

        if (durusArkaTex == null)
        {
            durusArkaTex = new Texture2D(1, 1);
            durusArkaTex.SetPixel(0, 0, new Color(0.08f, 0.11f, 0.14f, 0.82f));
            durusArkaTex.Apply();
        }

        if (durusKutuStili == null)
        {
            durusKutuStili = new GUIStyle(GUI.skin.box);
            durusKutuStili.normal.background = durusArkaTex;
        }

        if (durusYaziStili == null)
        {
            durusYaziStili = new GUIStyle(GUI.skin.label);
            Yazlab.Yonetim.GoruntuKalitesiYoneticisi.StiliKeskinlestir(durusYaziStili, 15);
            durusYaziStili.fontStyle = FontStyle.Bold;
            durusYaziStili.alignment = TextAnchor.MiddleLeft;
        }

        Rect r = new Rect(18f, Screen.height - 46f, 340f, 30f);
        GUI.Box(r, GUIContent.none, durusKutuStili);

        durusYaziStili.normal.textColor = EgiliyorMu
            ? new Color(0.30f, 0.95f, 0.55f)
            : new Color(0.85f, 0.88f, 0.92f);

        string metin = EgiliyorMu
            ? "  [C / CTRL] DURUŞ: EĞİLDİ (SİPERDE GİZLİ)"
            : "  [C / CTRL] DURUŞ: AYAKTA";

        GUI.Label(r, metin, durusYaziStili);
    }
}
