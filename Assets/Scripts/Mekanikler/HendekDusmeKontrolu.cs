using System.Collections;
using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Oyuncunun kale hendeğine (suya) düşmesini algılayan, su sıçrama (splash) ses ve
    /// partikül efektini oynatan ve oyuncuyu başlangıç noktasına yeniden başlatan sistem.
    /// </summary>
    public class HendekDusmeKontrolu : MonoBehaviour
    {
        [Header("Başlangıç (Yeniden Doğuş) Ayarları")]
        public Vector3 yenidenDogusKonumu = new Vector3(-1.5f, 0.15f, -21.5f);
        public float suDusmeSeviyesiY = -0.45f;

        private Transform _oyuncu;
        private CharacterController _cc;
        private AudioSource _sesKaynagi;
        private AudioClip _suSicramaSesi;
        private bool _yenidenBaslatiliyor = false;
        private float _ekranEfektAlpha = 0f;

        private Texture2D _maviEkranTex;
        private Texture2D _kutuTex;
        private GUIStyle _baslikStil;
        private GUIStyle _altStil;

        private void Start()
        {
            // Hendek üzerindeki görünmez geçiş engellerini kaldır ki oyuncu gerçekten suya düşebilsin!
            GorunmezHendekEngelleriniKaldir();

            _sesKaynagi = GetComponent<AudioSource>();
            if (_sesKaynagi == null)
            {
                _sesKaynagi = gameObject.AddComponent<AudioSource>();
                _sesKaynagi.playOnAwake = false;
                _sesKaynagi.spatialBlend = 0.3f;
            }

            _suSicramaSesi = UretSuSicramaSesi();
        }

        public static void GorunmezHendekEngelleriniKaldir()
        {
            string[] kaldirilacaklar = new string[]
            {
                "Hendek_Gecis_Engeli",
                "Hendek_SolSuSiniri",
                "Hendek_SagSuSiniri",
                "Hendek_KenarSiper_Sol",
                "Hendek_KenarSiper_Sag"
            };

            foreach (string ad in kaldirilacaklar)
            {
                GameObject obj = GameObject.Find(ad);
                if (obj != null)
                {
                    Collider c = obj.GetComponent<Collider>();
                    if (c != null) c.enabled = false;
                    MeshRenderer mr = obj.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }
            }
        }

        private void Update()
        {
            if (_yenidenBaslatiliyor) return;

            if (_oyuncu == null)
            {
                KarakterHareket kh = Object.FindAnyObjectByType<KarakterHareket>();
                if (kh != null)
                {
                    _oyuncu = kh.transform;
                    _cc = kh.GetComponent<CharacterController>();
                }
            }

            if (_oyuncu == null) return;

            // Oyuncu hendek çukuruna / suya düştü mü? (Y < -0.45m)
            if (_oyuncu.position.y < suDusmeSeviyesiY)
            {
                StartCoroutine(SuyaDusmeVeYenidenBaslatSekansi(_oyuncu.position));
            }
        }

        private IEnumerator SuyaDusmeVeYenidenBaslatSekansi(Vector3 dusmeNoktasi)
        {
            _yenidenBaslatiliyor = true;

            // 1. Su sıçrama sesi ve 3B su damlası partiküllerini oluştur
            if (_sesKaynagi != null && _suSicramaSesi != null)
            {
                _sesKaynagi.PlayOneShot(_suSicramaSesi, 1.0f);
            }
            SuSicramaPartikuluOlustur(new Vector3(dusmeNoktasi.x, -0.90f, dusmeNoktasi.z));

            // 2. Ekranı su altı mavisiyle kapla ve 1.15 saniye bilgi göster
            float sure = 1.15f;
            float gecen = 0f;
            while (gecen < sure)
            {
                gecen += Time.deltaTime;
                _ekranEfektAlpha = Mathf.Sin(Mathf.Clamp01(gecen / sure) * Mathf.PI) * 0.72f;
                yield return null;
            }

            // 3. Oyuncuyu başlangıç noktasına (1. Bölüm Kamp Alanı) sıfırla ve canını doldur
            if (_oyuncu != null)
            {
                if (_cc == null) _cc = _oyuncu.GetComponent<CharacterController>();
                if (_cc != null) _cc.enabled = false;

                _oyuncu.position = yenidenDogusKonumu;
                _oyuncu.rotation = Quaternion.identity;

                if (_cc != null) _cc.enabled = true;

                OyuncuCani can = _oyuncu.GetComponent<OyuncuCani>();
                if (can != null)
                {
                    can.mevcutCan = can.maksimumCan;
                }
            }

            _ekranEfektAlpha = 0f;
            yield return new WaitForSeconds(0.25f);
            _yenidenBaslatiliyor = false;
        }

        private void SuSicramaPartikuluOlustur(Vector3 pos)
        {
            GameObject splashObj = new GameObject("SuSicramaEfekti");
            splashObj.transform.position = pos;

            ParticleSystem ps = splashObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.85f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.40f);
            main.startColor = new Color(0.75f, 0.94f, 1.0f, 0.90f);
            main.gravityModifier = 1.4f;
            main.loop = false;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 45) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.5f;
            splashObj.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);

            ParticleSystemRenderer psr = splashObj.GetComponent<ParticleSystemRenderer>();
            Shader pShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pShader != null)
            {
                psr.sharedMaterial = new Material(pShader)
                {
                    color = new Color(0.82f, 0.96f, 1.0f, 0.88f)
                };
            }

            ps.Play();
            Destroy(splashObj, 1.5f);
        }

        private AudioClip UretSuSicramaSesi()
        {
            int sampleRate = 44100;
            float sure = 0.65f;
            int n = (int)(sampleRate * sure);
            float[] veri = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sampleRate;
                float zarf = Mathf.Exp(-t * 5.2f);
                float kabarcik = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(320f, 95f, t / sure) * t) * 0.45f;
                float suGurultu = (Random.value * 2f - 1f) * 0.65f * Mathf.Sin(t * 45f);
                veri[i] = (kabarcik + suGurultu) * zarf * 0.85f;
            }

            AudioClip klip = AudioClip.Create("SuSicramaSesi", n, 1, sampleRate, false);
            klip.SetData(veri, 0);
            return klip;
        }

        private void OnGUI()
        {
            if (!_yenidenBaslatiliyor) return;

            if (_maviEkranTex == null)
            {
                _maviEkranTex = new Texture2D(1, 1);
                _maviEkranTex.SetPixel(0, 0, Color.white);
                _maviEkranTex.Apply();
            }

            if (_kutuTex == null)
            {
                _kutuTex = new Texture2D(1, 1);
                _kutuTex.SetPixel(0, 0, new Color(0.03f, 0.14f, 0.24f, 0.92f));
                _kutuTex.Apply();
            }

            Color eski = GUI.color;

            // Su altı mavi ekran efekti
            GUI.color = new Color(0.06f, 0.42f, 0.68f, _ekranEfektAlpha);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _maviEkranTex);

            // Uyarı Kutusu
            GUI.color = Color.white;
            GUIStyle kutuStil = new GUIStyle(GUI.skin.box);
            kutuStil.normal.background = _kutuTex;

            if (_baslikStil == null)
            {
                _baslikStil = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _baslikStil.normal.textColor = new Color(0.35f, 0.92f, 1.0f);
            }

            if (_altStil == null)
            {
                _altStil = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _altStil.normal.textColor = Color.white;
            }

            float w = 520f;
            float h = 86f;
            Rect r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.42f, w, h);
            GUI.Box(r, GUIContent.none, kutuStil);
            GUI.Label(new Rect(r.x, r.y + 14f, r.width, 30f), "🌊 HENDEK SUYUNA DÜŞTÜNÜZ!", _baslikStil);
            GUI.Label(new Rect(r.x, r.y + 46f, r.width, 26f), "Başlangıç Noktasından Yeniden Başlatılıyor...", _altStil);

            GUI.color = eski;
        }
    }
}
