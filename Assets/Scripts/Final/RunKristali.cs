using System.Collections;
using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kızıl Kule'deki Prenses Betül'ün kapsül mührünü tutan 3 renkli Zümrüt Kristalden biri:
    /// 0: KIRMIZI ZÜMRÜT, 1: MAVİ ZÜMRÜT, 2: YEŞİL ZÜMRÜT.
    /// Oyuncu silahıyla (KarakterAtes) doğru sırada (Kırmızı -> Mavi -> Yeşil) vurmalıdır.
    /// Yanlış sırada vurulursa görev yöneticisi tüm zümrütleri Sifirla() ile yeniden aktif eder.
    /// </summary>
    public class RunKristali : MonoBehaviour
    {
        public int indeks; // 0: Kırmızı, 1: Mavi, 2: Yeşil
        public Transform gorsel;
        public MeshRenderer gorselRenderer;
        public Light isik;

        public bool VurulduMu { get; private set; } = false;

        private float _parlamaZamani = -10f;
        private float _sonAtisZamani = -10f;
        private float _taban;
        private Color _renk;

        public static readonly string[] Adlar = { "KIRMIZI", "MAVİ", "YEŞİL" };

        public static Color RenkAl(int i)
        {
            switch (i)
            {
                case 0: return new Color(1.0f, 0.20f, 0.18f, 1f);  // 1. Kırmızı Zümrüt
                case 1: return new Color(0.20f, 0.58f, 1.0f, 1f);  // 2. Mavi Zümrüt
                default: return new Color(0.18f, 0.98f, 0.45f, 1f); // 3. Yeşil Zümrüt
            }
        }

        private void Start()
        {
            _taban = transform.position.y;
            _renk = RenkAl(indeks);
            GorseliGuncelle();
        }

        /// <summary>KarakterAtes silah mermisi çarptığında doğrudan veya SendMessageUpwards ile çağrılır.</summary>
        public void HasarAl(float miktar)
        {
            SilahlaVuruldu();
        }

        public void SilahlaVuruldu()
        {
            if (VurulduMu) return;
            if (Time.time < _sonAtisZamani + 0.25f) return;
            _sonAtisZamani = Time.time;

            PrensesKurtarmaGorevi.Ornek?.KristalVuruldu(this);
        }

        /// <summary>Doğru sırada vurulduğunda zümrütü kırar/kapatır (seri ateşte tekrar vurulup sıfırlamasını önler).</summary>
        public void DogruVurusKapat()
        {
            VurulduMu = true;
            _parlamaZamani = Time.time;

            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            StartCoroutine(KirilmaEfekti());
        }

        /// <summary>Yanlış sırada vurulduğunda tüm zümrütleri başlangıç durumuna döndürür.</summary>
        public void Sifirla()
        {
            StopAllCoroutines();
            VurulduMu = false;
            _parlamaZamani = Time.time;

            if (gorsel != null)
            {
                gorsel.gameObject.SetActive(true);
                gorsel.localScale = Vector3.one * 0.48f;
            }

            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                col.enabled = true;
            }

            GorseliGuncelle();
        }

        /// <summary>Kristali kısa süreliğine parlat.</summary>
        public void Parla()
        {
            _parlamaZamani = Time.time;
        }

        private void GorseliGuncelle()
        {
            _renk = RenkAl(indeks);
            if (isik != null)
            {
                isik.color = _renk;
                isik.intensity = 1.6f;
            }
            if (gorselRenderer != null)
            {
                gorselRenderer.material.color = _renk;
            }
        }

        private IEnumerator KirilmaEfekti()
        {
            Vector3 merkez = transform.position;
            Material parcaMat = PrensesKulesiInsaci.ParlakMateryal(_renk);

            Transform[] parcalar = new Transform[8];
            Vector3[] hizlar = new Vector3[8];
            for (int i = 0; i < parcalar.Length; i++)
            {
                GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.name = "ZumrutParca";
                Destroy(p.GetComponent<Collider>());
                p.transform.position = merkez + Random.insideUnitSphere * 0.15f;
                p.transform.localScale = Vector3.one * Random.Range(0.09f, 0.16f);
                p.transform.rotation = Random.rotation;
                p.GetComponent<MeshRenderer>().sharedMaterial = parcaMat;
                parcalar[i] = p.transform;
                hizlar[i] = (Random.onUnitSphere + Vector3.up * 0.6f).normalized * Random.Range(2.2f, 4.2f);
            }

            if (isik != null)
            {
                isik.color = _renk;
                isik.intensity = 6.5f;
            }

            if (gorsel != null)
            {
                gorsel.gameObject.SetActive(false);
            }

            float gecen = 0f;
            float sure = 0.50f;
            while (gecen < sure)
            {
                gecen += Time.deltaTime;
                float oran = gecen / sure;
                for (int i = 0; i < parcalar.Length; i++)
                {
                    if (parcalar[i] != null)
                    {
                        hizlar[i] += Vector3.down * (6.5f * Time.deltaTime);
                        parcalar[i].position += hizlar[i] * Time.deltaTime;
                        parcalar[i].Rotate(240f * Time.deltaTime, 180f * Time.deltaTime, 0f);
                        parcalar[i].localScale = Vector3.Lerp(Vector3.one * 0.14f, Vector3.zero, oran);
                    }
                }
                if (isik != null)
                {
                    isik.intensity = Mathf.Lerp(6.5f, 0.35f, oran);
                }
                yield return null;
            }

            for (int i = 0; i < parcalar.Length; i++)
            {
                if (parcalar[i] != null) Destroy(parcalar[i].gameObject);
            }
        }

        private void Update()
        {
            if (VurulduMu) return;

            float t = Time.time;
            if (gorsel != null)
            {
                gorsel.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);
            }
            Vector3 p = transform.position;
            p.y = _taban + Mathf.Sin(t * 2.0f + indeks) * 0.09f;
            transform.position = p;

            float parlama = Mathf.Clamp01(1f - (t - _parlamaZamani) / 0.7f);
            if (isik != null) isik.intensity = 1.5f + parlama * 5.0f;
            if (gorselRenderer != null)
            {
                Color c = Color.Lerp(_renk * 0.88f, Color.white, parlama * 0.65f);
                c.a = 1f;
                gorselRenderer.material.color = c;
                if (gorsel != null) gorsel.localScale = Vector3.one * (0.48f + parlama * 0.18f);
            }
        }
    }
}
