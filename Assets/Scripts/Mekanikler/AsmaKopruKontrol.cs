using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// 1. Bölümdeki Hendek (Moat) üzerinde yer alan Askı Kapı / Asma Köprü kontrolcüsü.
    /// Başlangıçta dik açıyla (-78 derece) kalkık durur ve kaleye girişi engeller.
    /// Çift anahtarlı co-op mekanizması çözüldüğünde zincir çarklarını döndürerek
    /// hendeğin üzerine yatay (0 derece) olarak iner ve geçidi açar.
    /// </summary>
    public class AsmaKopruKontrol : MonoBehaviour, IKapi
    {
        [Header("Asma Köprü (Askı Kapı) Ayarları")]
        [Tooltip("Köprünün menteşe (pivot) noktası.")]
        public Transform kopruMentese;

        [Tooltip("Sol ve sağ zincir makaraları (Köprü inerken döner).")]
        public Transform solMakara;
        public Transform sagMakara;

        [Tooltip("Köprü kapalıyken hendeğin üzerinden geçilmesini önleyen görünmez bariyer.")]
        public Collider hendekGecisEngeli;

        [Tooltip("Köprü kapalıyken X ekseni açısı (Dik duruş).")]
        public float kapaliAciX = -78f;

        [Tooltip("Köprü açıkken X ekseni açısı (Yatay köprü duruşu).")]
        public float acikAciX = 0f;

        [Tooltip("Köprünün inme/kalkma açısal hızı.")]
        public float inmeHizi = 28f;

        [Header("Olaylar")]
        public UnityEvent onKopruIndi = new UnityEvent();

        private bool _acikMi = false;
        private bool _kilitliMi = true;
        private float _mevcutAciX;
        private NavMeshObstacle _navEngel;
        private AudioSource _sesKaynagi;

        public bool AcikMi => _acikMi;
        public bool KilitliMi => _kilitliMi;

        private void Awake()
        {
            if (kopruMentese == null) kopruMentese = transform;
            _navEngel = GetComponent<NavMeshObstacle>();

            _mevcutAciX = _acikMi ? acikAciX : kapaliAciX;
            kopruMentese.localRotation = Quaternion.Euler(_mevcutAciX, 0f, 0f);

            _sesKaynagi = GetComponent<AudioSource>();
            if (_sesKaynagi == null)
            {
                _sesKaynagi = gameObject.AddComponent<AudioSource>();
                _sesKaynagi.playOnAwake = false;
                _sesKaynagi.spatialBlend = 0.8f;
            }
        }

        private void Update()
        {
            float hedefAci = _acikMi ? acikAciX : kapaliAciX;
            if (Mathf.Abs(_mevcutAciX - hedefAci) > 0.05f)
            {
                _mevcutAciX = Mathf.MoveTowards(_mevcutAciX, hedefAci, inmeHizi * Time.deltaTime);
                if (kopruMentese != null)
                {
                    kopruMentese.localRotation = Quaternion.Euler(_mevcutAciX, 0f, 0f);
                }

                // Zincir makaralarını döndür
                float makaraDonus = inmeHizi * 4.5f * Time.deltaTime;
                if (solMakara != null) solMakara.Rotate(Vector3.right, makaraDonus, Space.Self);
                if (sagMakara != null) sagMakara.Rotate(Vector3.right, makaraDonus, Space.Self);
            }
            else if (_acikMi && hendekGecisEngeli != null && hendekGecisEngeli.enabled)
            {
                // Köprü tamamen indiğinde geçiş bariyerini kaldır
                hendekGecisEngeli.enabled = false;
            }
        }

        public void KilidiAc()
        {
            _kilitliMi = false;
            KapiyiAc();
        }

        public void Kilitle()
        {
            _kilitliMi = true;
        }

        public void KapiyiAc()
        {
            Ac();
        }

        public void KapiyiKapat()
        {
            Kapat();
        }

        public void Ac()
        {
            if (_acikMi) return;
            _kilitliMi = false;
            _acikMi = true;

            if (_navEngel != null)
            {
                _navEngel.carving = false;
                _navEngel.enabled = false;
            }

            if (hendekGecisEngeli != null)
            {
                hendekGecisEngeli.enabled = false;
            }

            ZincirSesiCal();
            onKopruIndi?.Invoke();
            Debug.Log("[AsmaKopruKontrol] Askı kapı (Asma Köprü) hendeğin üzerine indiriliyor! 1. Bölüm geçidi açıldı.");
        }

        public void Kapat()
        {
            if (!_acikMi) return;
            _acikMi = false;

            if (_navEngel != null)
            {
                _navEngel.enabled = true;
                _navEngel.carving = true;
            }

            if (hendekGecisEngeli != null)
            {
                hendekGecisEngeli.enabled = true;
            }
        }

        private void ZincirSesiCal()
        {
            if (_sesKaynagi == null) return;

            int sampleRate = 44100;
            float sure = 1.6f;
            int ornekSayisi = (int)(sampleRate * sure);
            float[] veri = new float[ornekSayisi];

            for (int i = 0; i < ornekSayisi; i++)
            {
                float t = (float)i / sampleRate;
                float halkaMod = Mathf.Abs(Mathf.Sin(t * 28f));
                float dusukGicirti = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.4f;
                float metalGurultu = (Random.value * 2f - 1f) * 0.35f * halkaMod;
                float zarf = Mathf.Clamp01(1f - (t / sure) * 0.5f);
                veri[i] = (dusukGicirti + metalGurultu) * zarf * 0.65f;
            }

            AudioClip klip = AudioClip.Create("AsmaKopruZincirSesi", ornekSayisi, 1, sampleRate, false);
            klip.SetData(veri, 0);
            _sesKaynagi.PlayOneShot(klip);
        }
    }
}
