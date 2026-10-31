using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kızıl Kule'deki Rün Mühürünü oluşturan 3 kristalden biri (Kızıl / Mavi / Zümrüt).
    /// Oyuncu kristali silahıyla vurduğunda (KarakterAtes → SendMessageUpwards("HasarAl"))
    /// görev yöneticisine haber verir. Kristal kırılmaz; yalnızca parlayıp titreşir.
    /// </summary>
    public class RunKristali : MonoBehaviour
    {
        public int indeks;
        public Transform gorsel;
        public MeshRenderer gorselRenderer;
        public Light isik;

        private float _parlamaZamani = -10f;
        private float _sonVurusZamani = -10f;
        private float _taban;
        private Color _renk;

        public static readonly string[] Adlar = { "KIZIL", "MAVİ", "ZÜMRÜT" };

        public static Color RenkAl(int i)
        {
            switch (i)
            {
                case 0: return new Color(1.0f, 0.22f, 0.18f, 1f);
                case 1: return new Color(0.25f, 0.55f, 1.0f, 1f);
                default: return new Color(0.25f, 1.0f, 0.45f, 1f);
            }
        }

        private void Start()
        {
            _taban = transform.position.y;
            _renk = RenkAl(indeks);
        }

        /// <summary>KarakterAtes mermisi çarptığında SendMessageUpwards ile çağrılır.</summary>
        public void HasarAl(float miktar)
        {
            if (Time.time < _sonVurusZamani + 0.45f) return; // Seri atışta tek mermi say
            _sonVurusZamani = Time.time;
            PrensesKurtarmaGorevi.Ornek?.KristalVuruldu(this);
        }

        /// <summary>Kristali kısa süreliğine parlat (sıra gösterimi veya doğru vuruş).</summary>
        public void Parla()
        {
            _parlamaZamani = Time.time;
        }

        private void Update()
        {
            float t = Time.time;
            if (gorsel != null)
            {
                gorsel.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
            }
            Vector3 p = transform.position;
            p.y = _taban + Mathf.Sin(t * 1.8f + indeks) * 0.08f;
            transform.position = p;

            float parlama = Mathf.Clamp01(1f - (t - _parlamaZamani) / 0.7f);
            if (isik != null) isik.intensity = 0.9f + parlama * 5.0f;
            if (gorselRenderer != null)
            {
                Color c = Color.Lerp(_renk * 0.6f, Color.white, parlama * 0.65f);
                c.a = 1f;
                gorselRenderer.material.color = c;
                if (gorsel != null) gorsel.localScale = Vector3.one * (0.42f + parlama * 0.18f);
            }
        }
    }
}
