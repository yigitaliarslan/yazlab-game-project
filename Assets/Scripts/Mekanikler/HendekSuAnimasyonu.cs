using System.Collections.Generic;
using UnityEngine;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// Kale hendeğindeki (Moat) suyu görsel olarak zenginleştiren animasyon sistemi:
    /// - Yarı saydam (Translucent) parlak su yüzeyi,
    /// - Su üzerinde sürekli akan beyaz köpük şeritleri ve dalga halkaları,
    /// - Hendek yan duvarlarından akan taş kemerli şelaleler ve su sıçrama/sis partikülleri (ParticleSystem),
    /// - Su altı turkuaz aydınlatma ışıkları.
    /// </summary>
    public class HendekSuAnimasyonu : MonoBehaviour
    {
        private class KopukSeridi
        {
            public Transform tr;
            public float hiz;
            public float baslangicX;
            public float bitisX;
            public float bazZ;
            public float faz;
        }

        private class DalgaHalkasi
        {
            public Transform tr;
            public MeshRenderer rend;
            public Vector3 merkez;
            public float zaman;
            public float maksCap;
        }

        private readonly List<KopukSeridi> _kopukler = new List<KopukSeridi>();
        private readonly List<DalgaHalkasi> _halkalar = new List<DalgaHalkasi>();
        private Material _kopukMat;
        private float _suSeviyesiY = -1.05f;

        private void Start()
        {
            GorselSuSisteminiKur();
        }

        public void GorselSuSisteminiKur()
        {
            // Eski alt efektler varsa temizle
            Transform eskiEfekt = transform.Find("Su_Animasyon_Detaylari");
            if (eskiEfekt != null) Destroy(eskiEfekt.gameObject);

            GameObject detayKok = new GameObject("Su_Animasyon_Detaylari");
            detayKok.transform.SetParent(transform);
            detayKok.transform.localPosition = Vector3.zero;

            Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            _kopukMat = new Material(unlitShader)
            {
                color = new Color(0.85f, 0.96f, 1.0f, 0.55f)
            };

            // 1. Ana 3B Su Yüzeyini Yarı Saydam ve Parlak Turkuaz/Mavi Yap
            Transform su3B = transform.Find("Hendek_Suyu_3B");
            if (su3B != null)
            {
                su3B.position = new Vector3(0f, _suSeviyesiY, -11.7f);
                MeshRenderer mr = su3B.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mr.sharedMaterial = OlusturSaydamSuMateryali();
                }
            }

            // 2. İkinci Katman: Derin Su Altı Parıltı Katmanı (Derinlik hissi için)
            GameObject altKatman = GameObject.CreatePrimitive(PrimitiveType.Cube);
            altKatman.name = "Su_AltDerinlikKatmani";
            altKatman.transform.SetParent(detayKok.transform);
            altKatman.transform.position = new Vector3(0f, _suSeviyesiY - 0.55f, -11.7f);
            altKatman.transform.localScale = new Vector3(32f, 0.08f, 6.1f);
            Destroy(altKatman.GetComponent<Collider>());
            Material derinMat = new Material(unlitShader)
            {
                color = new Color(0.03f, 0.22f, 0.36f, 0.85f)
            };
            altKatman.GetComponent<MeshRenderer>().sharedMaterial = derinMat;

            // 3. Su Üzerinde Akan Dinamik Beyaz Köpük ve Akıntı Şeritleri (18 adet)
            _kopukler.Clear();
            for (int i = 0; i < 18; i++)
            {
                GameObject kObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                kObj.name = $"AkintiKopugu_{i + 1}";
                kObj.transform.SetParent(detayKok.transform);
                Destroy(kObj.GetComponent<Collider>());

                float z = Random.Range(-14.2f, -9.2f);
                float x = Random.Range(-14.5f, 14.5f);
                float uzunluk = Random.Range(1.4f, 3.2f);
                float genislik = Random.Range(0.08f, 0.18f);

                kObj.transform.position = new Vector3(x, _suSeviyesiY + 0.06f, z);
                kObj.transform.localScale = new Vector3(uzunluk, 0.02f, genislik);
                kObj.GetComponent<MeshRenderer>().sharedMaterial = _kopukMat;

                _kopukler.Add(new KopukSeridi
                {
                    tr = kObj.transform,
                    hiz = Random.Range(1.4f, 2.6f),
                    baslangicX = -15.0f,
                    bitisX = 15.0f,
                    bazZ = z,
                    faz = Random.Range(0f, 6.28f)
                });
            }

            // 4. Köprü Sütunları ve Kayalar Etrafında Genişleyen Su Halkaları (6 adet)
            _halkalar.Clear();
            Vector3[] halkaMerkezleri = new Vector3[]
            {
                new Vector3(-2.3f, _suSeviyesiY + 0.07f, -11.7f),
                new Vector3(2.3f, _suSeviyesiY + 0.07f, -11.7f),
                new Vector3(-7.5f, _suSeviyesiY + 0.07f, -11.2f),
                new Vector3(7.8f, _suSeviyesiY + 0.07f, -12.0f),
                new Vector3(-12.5f, _suSeviyesiY + 0.07f, -11.7f),
                new Vector3(12.5f, _suSeviyesiY + 0.07f, -11.7f)
            };

            for (int i = 0; i < halkaMerkezleri.Length; i++)
            {
                GameObject hObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hObj.name = $"SuDalgaHalkasi_{i + 1}";
                hObj.transform.SetParent(detayKok.transform);
                Destroy(hObj.GetComponent<Collider>());

                hObj.transform.position = halkaMerkezleri[i];
                hObj.transform.localScale = new Vector3(1f, 0.01f, 1f);
                MeshRenderer hRend = hObj.GetComponent<MeshRenderer>();
                hRend.sharedMaterial = new Material(_kopukMat);

                _halkalar.Add(new DalgaHalkasi
                {
                    tr = hObj.transform,
                    rend = hRend,
                    merkez = halkaMerkezleri[i],
                    zaman = i * 0.35f,
                    maksCap = 2.8f
                });
            }

            // 5. Sol ve Sağ Hendek Duvarlarından Akan Şelaleler + Köpük Partikül Sistemleri
            SelaleOlustur("Hendek_SolSelale", new Vector3(-15.4f, -0.1f, -11.7f), Quaternion.Euler(0f, 90f, 0f), detayKok.transform);
            SelaleOlustur("Hendek_SagSelale", new Vector3(15.4f, -0.1f, -11.7f), Quaternion.Euler(0f, -90f, 0f), detayKok.transform);

            // 6. Su Altı Turkuaz Aydınlatma Işıkları (Suyun karanlık boşluk gibi görünmesini engeller)
            float[] isikX = { -10f, -3.5f, 3.5f, 10f };
            for (int i = 0; i < isikX.Length; i++)
            {
                GameObject isikObj = new GameObject($"SuAlti_TurkuazIsik_{i + 1}");
                isikObj.transform.SetParent(detayKok.transform);
                isikObj.transform.position = new Vector3(isikX[i], _suSeviyesiY + 0.6f, -11.7f);
                Light l = isikObj.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(0.15f, 0.78f, 0.95f);
                l.range = 9.5f;
                l.intensity = 2.2f;
            }
        }

        private void SelaleOlustur(string ad, Vector3 pos, Quaternion rot, Transform parent)
        {
            GameObject selaleKok = new GameObject(ad);
            selaleKok.transform.SetParent(parent);
            selaleKok.transform.position = pos;
            selaleKok.transform.rotation = rot;

            // Taş su oluğu
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material tasMat = new Material(lit) { color = new Color(0.28f, 0.27f, 0.25f) };
            GameObject oluk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            oluk.name = "TasSuOlugu";
            oluk.transform.SetParent(selaleKok.transform);
            oluk.transform.localPosition = new Vector3(0f, 0.2f, -0.2f);
            oluk.transform.localScale = new Vector3(1.6f, 0.5f, 1.0f);
            oluk.GetComponent<MeshRenderer>().sharedMaterial = tasMat;

            // Dökülen su ve köpük partikül sistemi
            GameObject psObj = new GameObject("SelalePartikul");
            psObj.transform.SetParent(selaleKok.transform);
            psObj.transform.localPosition = new Vector3(0f, 0.15f, 0.35f);
            psObj.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);

            ParticleSystem ps = psObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.75f;
            main.startSpeed = 2.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
            main.startColor = new Color(0.70f, 0.92f, 1.0f, 0.75f);
            main.gravityModifier = 0.8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;

            var emission = ps.emission;
            emission.rateOverTime = 45f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.1f, 0.15f, 0.15f);

            ParticleSystemRenderer psr = psObj.GetComponent<ParticleSystemRenderer>();
            Shader pShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (pShader != null)
            {
                psr.sharedMaterial = new Material(pShader)
                {
                    color = new Color(0.80f, 0.95f, 1.0f, 0.70f)
                };
            }
        }

        private Material OlusturSaydamSuMateryali()
        {
            Shader suShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(suShader)
            {
                name = "Mat_CanliHendekSuyu",
                color = new Color(0.08f, 0.52f, 0.78f, 0.82f)
            };

            Texture2D suTex = Yonetim.ProsedurelDokuUretici.UretSuDokusu(
                new Color(0.05f, 0.36f, 0.58f, 0.82f),
                new Color(0.45f, 0.88f, 0.98f, 0.92f));

            m.mainTexture = suTex;
            m.mainTextureScale = new Vector2(8f, 2.5f);
            if (m.HasProperty("_BaseMap"))
            {
                m.SetTexture("_BaseMap", suTex);
                m.SetTextureScale("_BaseMap", new Vector2(8f, 2.5f));
            }
            if (m.HasProperty("_BaseColor"))
            {
                m.SetColor("_BaseColor", new Color(0.10f, 0.58f, 0.84f, 0.82f));
            }
            return m;
        }

        private void Update()
        {
            float t = Time.time;

            // 1. Akan köpük şeritlerini soldan sağa doğru hareket ettir ve dalgalandır
            for (int i = 0; i < _kopukler.Count; i++)
            {
                KopukSeridi k = _kopukler[i];
                if (k.tr == null) continue;

                Vector3 p = k.tr.position;
                p.x += k.hiz * Time.deltaTime;
                if (p.x > k.bitisX) p.x = k.baslangicX;

                p.y = _suSeviyesiY + 0.08f + Mathf.Sin(t * 2.5f + k.faz) * 0.05f;
                p.z = k.bazZ + Mathf.Cos(t * 1.4f + k.faz) * 0.15f;
                k.tr.position = p;
            }

            // 2. Sütun ve kaya etrafındaki su halkalarını büyütüp soldur
            for (int i = 0; i < _halkalar.Count; i++)
            {
                DalgaHalkasi h = _halkalar[i];
                if (h.tr == null) continue;

                h.zaman += Time.deltaTime * 0.65f;
                float oran = h.zaman % 1f;
                float cap = Mathf.Lerp(0.8f, h.maksCap, oran);
                h.tr.position = new Vector3(h.merkez.x, _suSeviyesiY + 0.06f, h.merkez.z);
                h.tr.localScale = new Vector3(cap, 0.01f, cap);

                if (h.rend != null && h.rend.material != null)
                {
                    float alpha = (1f - oran) * 0.45f;
                    h.rend.material.color = new Color(0.85f, 0.97f, 1.0f, alpha);
                }
            }
        }
    }
}
