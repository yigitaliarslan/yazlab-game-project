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

    [Tooltip("Karakterin bastığınız yöne dönme yumuşaklık hızı.")]
    public float donusHizi = 12.0f;

    [Header("Zıplama ve Yerçekimi")]
    [Tooltip("Space tuşuna basıldığında zıplama kuvveti.")]
    public float ziplamaGucu = 6.5f;

    [Tooltip("Yerçekimi ivmesi.")]
    public float yercekimi = 20.0f;

    [Header("Kamera Referansı")]
    [Tooltip("Hareket yönünün referans alacağı kamera (Boş bırakılırsa Main Camera kullanılır).")]
    public Transform kamera;

    private CharacterController controller;
    private Animator animator;
    private float dikeyHiz = 0f;
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private bool hasIsRunningParam = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = gameObject.AddComponent<CharacterController>();
        }

        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.3f;
        controller.slopeLimit = 45f;

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
    }

    void Update()
    {
        Vector2 input = KlavyeGirdisiAl();
        bool zipla = ZiplamaGirdisiAl();

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

        // Karakter hareket ettiği yöne doğru yumuşakça dönsün
        if (hareketEdiyor)
        {
            Quaternion hedefRotasyon = Quaternion.LookRotation(hareketYonu);
            transform.rotation = Quaternion.Slerp(transform.rotation, hedefRotasyon, Time.deltaTime * donusHizi);
        }

        // Zıplama ve yerçekimi kontrolü
        if (controller.isGrounded)
        {
            dikeyHiz = -1f;

            if (zipla)
            {
                dikeyHiz = ziplamaGucu;
            }
        }
        else
        {
            dikeyHiz -= yercekimi * Time.deltaTime;
        }

        // Hareketi uygula
        Vector3 toplamHareket = (hareketYonu * hiz) + (Vector3.up * dikeyHiz);
        controller.Move(toplamHareket * Time.deltaTime);

        // Animasyon durumunu güncelle
        AnimasyonGuncelle(hareketEdiyor, controller.isGrounded);
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
}
