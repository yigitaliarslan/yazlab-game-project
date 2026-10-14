using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 3D modda W, A, S, D veya yön tuşları ile çalışan oyuncu hareket kontrolcüsü.
/// Hem Yeni Input System hem de Eski Input Manager ile tam uyumludur.
/// KarakterController üzerinde veya doğrudan Ana Kamera üzerinde çalışabilir.
/// </summary>
public class OyuncuHareket : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    [Tooltip("Karakterin yürüme/koşma hızı.")]
    public float hareketHizi = 6.0f;

    [Tooltip("Karakterin hareket ettiği yöne dönme yumuşaklık hızı.")]
    public float donusHizi = 12.0f;

    [Tooltip("Yerçekimi kuvveti.")]
    public float yercekimi = 20.0f;

    [Header("Kamera Referansı")]
    [Tooltip("Hareket yönünün referans alacağı kamera (Boş bırakılırsa Main Camera kullanılır).")]
    public Transform kamera;

    [Header("Fare ile Bakış (Kamera üzerinde çalışıyorsa)")]
    public bool fareyleBakma = true;
    public float fareHassasiyeti = 2.0f;

    private CharacterController characterController;
    private Animator animator;
    private float dikeyHiz = 0f;
    private float rotasyonX = 0f;
    private bool isCameraSelf = false;
    private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    private bool hasIsRunningParam = false;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
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
        if (kamera == null)
        {
            if (Camera.main != null && Camera.main.transform != transform)
            {
                kamera = Camera.main.transform;
            }
        }

        isCameraSelf = (kamera == null || kamera == transform || GetComponent<Camera>() != null);

        // Tag ayarı
        if (!gameObject.CompareTag("Player") && !isCameraSelf)
        {
            try { gameObject.tag = "Player"; } catch { }
        }
    }

    void Update()
    {
        // 1. Eğer doğrudan kamera üzerindeysek fare ile etrafa bakma
        if (isCameraSelf && fareyleBakma)
        {
            FareBakisiniUygula();
        }

        // 2. Girdileri al (WASD)
        Vector2 input = GirdiAl();

        // 3. Kamera açısına göre 3D hareket yönü hesapla
        Vector3 ileri = isCameraSelf ? transform.forward : (kamera != null ? kamera.forward : Vector3.forward);
        Vector3 sag = isCameraSelf ? transform.right : (kamera != null ? kamera.right : Vector3.right);
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

        // Karakter üzerinde ise bastığınız hareket yönüne doğru yumuşakça döndür
        if (!isCameraSelf && hareketEdiyor)
        {
            Quaternion hedefRotasyon = Quaternion.LookRotation(hareketYonu);
            transform.rotation = Quaternion.Slerp(transform.rotation, hedefRotasyon, Time.deltaTime * donusHizi);
        }

        // 4. Karakteri hareket ettir
        if (characterController != null)
        {
            if (characterController.isGrounded)
            {
                dikeyHiz = -1f;
            }
            else
            {
                dikeyHiz -= yercekimi * Time.deltaTime;
            }

            Vector3 toplamHareket = (hareketYonu * hareketHizi) + (Vector3.up * dikeyHiz);
            characterController.Move(toplamHareket * Time.deltaTime);
        }
        else
        {
            transform.position += hareketYonu * (hareketHizi * Time.deltaTime);
        }

        // 5. Animasyon güncelle
        AnimasyonGuncelle(hareketEdiyor);
    }

    private void FareBakisiniUygula()
    {
        Vector2 fareDelta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            fareDelta = Mouse.current.delta.ReadValue() * (fareHassasiyeti * 0.1f);
        }
#endif

        if (fareDelta == Vector2.zero)
        {
            try
            {
                fareDelta.x = Input.GetAxis("Mouse X") * fareHassasiyeti;
                fareDelta.y = Input.GetAxis("Mouse Y") * fareHassasiyeti;
            }
            catch { }
        }

        rotasyonX -= fareDelta.y;
        rotasyonX = Mathf.Clamp(rotasyonX, -80f, 80f);

        transform.localEulerAngles = new Vector3(rotasyonX, transform.localEulerAngles.y + fareDelta.x, 0f);
    }

    private Vector2 GirdiAl()
    {
        float x = 0f;
        float y = 0f;

        // Yeni Input System
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

        // Eski Input Manager (Fallback)
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

    private void AnimasyonGuncelle(bool kosuyor)
    {
        if (animator == null) return;

        if (hasIsRunningParam)
        {
            animator.SetBool(IsRunningHash, kosuyor);
        }
        else
        {
            string hedefState = kosuyor ? "infantry_combat_run" : "infantry_combat_idle";
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (!current.IsName(hedefState))
            {
                animator.CrossFade(hedefState, 0.2f);
            }
        }
    }
}
