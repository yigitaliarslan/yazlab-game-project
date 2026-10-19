using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using Yazlab.Etkilesim;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Hem fiziksel engel kapıları hem de bölüm sonu NPC kapısı için kullanılabilir,
    /// pürüzsüz animasyonlu ve NavMesh uyumlu kapı kontrolcüsü.
    /// Single Responsibility: Sadece kapının açılma/kapanma fiziğini ve durumunu yönetir.
    /// </summary>
    public class KapiKontrol : MonoBehaviour, IKapi, IEtkilesimli
    {
        public enum KapiTuru
        {
            YukariKayarakAcil,   // Parmaklık/Garaj kapısı tarzı yukarı kayar
            YanaKayarakAcil,     // Otomatik kapı tarzı yana kayar
            DonerekAcil          // Standart menteşeli kapı
        }

        [Header("Kapı Türü ve Ayarları")]
        [SerializeField] private KapiTuru kapiTuru = KapiTuru.YukariKayarakAcil;
        [SerializeField] private float acilmaMiktari = 3.5f;
        [SerializeField] private float hareketHizi = 2.5f;

        [Header("Başlangıç Durumu")]
        [SerializeField] private bool baslangictaKilitli = true;
        [SerializeField] private bool baslangictaAcik = false;

        [Header("Etkinlikler (Events)")]
        public UnityEvent onKapiAcildi = new UnityEvent();
        public UnityEvent onKapiKapandi = new UnityEvent();

        private Vector3 kapaliPozisyon;
        private Vector3 acikPozisyon;
        private Quaternion kapaliRotasyon;
        private Quaternion acikRotasyon;

        private bool acikMi = false;
        private bool kilitliMi = true;
        private bool hareketHalinde = false;
        private NavMeshObstacle navMeshEngel;

        public bool AcikMi => acikMi;
        public bool KilitliMi => kilitliMi;
        public bool EtkilesimeHazirMi => !hareketHalinde;
        public string EtkilesimMetni => kilitliMi ? "Kapı kilitli!" : (acikMi ? "Kapıyı Kapat" : "Kapıyı Aç");

        private void Awake()
        {
            kapaliPozisyon = transform.localPosition;
            kapaliRotasyon = transform.localRotation;
            kilitliMi = baslangictaKilitli;

            navMeshEngel = GetComponent<NavMeshObstacle>();

            // Açık pozisyon/rotasyon hesaplama
            switch (kapiTuru)
            {
                case KapiTuru.YukariKayarakAcil:
                    acikPozisyon = kapaliPozisyon + Vector3.up * acilmaMiktari;
                    acikRotasyon = kapaliRotasyon;
                    break;
                case KapiTuru.YanaKayarakAcil:
                    acikPozisyon = kapaliPozisyon + Vector3.right * acilmaMiktari;
                    acikRotasyon = kapaliRotasyon;
                    break;
                case KapiTuru.DonerekAcil:
                    acikPozisyon = kapaliPozisyon;
                    acikRotasyon = kapaliRotasyon * Quaternion.Euler(0f, 90f, 0f);
                    break;
            }

            if (baslangictaAcik)
            {
                KapiyiAc(aninda: true);
            }
        }

        public void EtkilesimeGir(Transform etkilesimiBaslatan)
        {
            if (kilitliMi)
            {
                Debug.Log($"[{gameObject.name}] Kapı kilitli, önce şalter indirilmeli veya NPC ikna edilmeli!");
                return;
            }

            if (acikMi)
            {
                KapiyiKapat();
            }
            else
            {
                KapiyiAc();
            }
        }

        public void KilidiAc()
        {
            kilitliMi = false;
            Debug.Log($"[{gameObject.name}] Kapının kilidi açıldı!");
        }

        public void Kilitle()
        {
            kilitliMi = true;
        }

        public void KapiyiAc()
        {
            KapiyiAc(false);
        }

        public void KapiyiAc(bool aninda)
        {
            if (acikMi && !aninda) return;

            // Kapı açıldığında kilit otomatik olarak kalksın
            kilitliMi = false;

            StopAllCoroutines();
            if (aninda)
            {
                transform.localPosition = acikPozisyon;
                transform.localRotation = acikRotasyon;
                acikMi = true;
                NavMeshEngeliGuncelle(false);
                onKapiAcildi?.Invoke();
            }
            else
            {
                StartCoroutine(HareketEtCoroutine(acikPozisyon, acikRotasyon, true));
            }
        }

        public void KapiyiKapat()
        {
            if (!acikMi) return;

            StopAllCoroutines();
            StartCoroutine(HareketEtCoroutine(kapaliPozisyon, kapaliRotasyon, false));
        }

        private IEnumerator HareketEtCoroutine(Vector3 hedefPozisyon, Quaternion hedefRotasyon, bool hedefAcikMi)
        {
            hareketHalinde = true;

            // Açılıyorsa NavMesh engelini hemen kaldır ki ajan geçebilsin
            if (hedefAcikMi)
            {
                NavMeshEngeliGuncelle(false);
            }

            while (Vector3.Distance(transform.localPosition, hedefPozisyon) > 0.01f ||
                   Quaternion.Angle(transform.localRotation, hedefRotasyon) > 0.5f)
            {
                transform.localPosition = Vector3.MoveTowards(transform.localPosition, hedefPozisyon, Time.deltaTime * hareketHizi);
                transform.localRotation = Quaternion.RotateTowards(transform.localRotation, hedefRotasyon, Time.deltaTime * hareketHizi * 30f);
                yield return null;
            }

            transform.localPosition = hedefPozisyon;
            transform.localRotation = hedefRotasyon;
            acikMi = hedefAcikMi;
            hareketHalinde = false;

            // Kapanıyorsa NavMesh engelini tekrar aç
            if (!hedefAcikMi)
            {
                NavMeshEngeliGuncelle(true);
                onKapiKapandi?.Invoke();
            }
            else
            {
                onKapiAcildi?.Invoke();
            }
        }

        private void NavMeshEngeliGuncelle(bool aktifMi)
        {
            if (navMeshEngel != null)
            {
                navMeshEngel.enabled = aktifMi;
            }
        }
    }
}
