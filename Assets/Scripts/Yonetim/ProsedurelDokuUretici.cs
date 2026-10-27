using UnityEngine;
using Yazlab.Mekanikler;

namespace Yazlab.Yonetim
{
    /// <summary>
    /// Kalenin dış ve iç surlarını, kulesini, ahşap asma köprüsünü, paslı demir kapılarını
    /// ve hendek suyunu gerçekçi örme taş, ahşap kalas, perçinli metal ve kostik su
    /// kaplamalarıyla (Texture2D) donatan prosedürel doku ve çevre üreticisi.
    /// </summary>
    public static class ProsedurelDokuUretici
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void SahnedekiMateryalleriKapla()
        {
            // Eğer sahnede eski düz su küpü varsa, gerçek derin taş hendek kanyonuna ve 3B dalgalı suya dönüştür
            DerinHendekVeCevreyiGarantile();

            MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            Texture2D texTas = null;
            Texture2D texKoyuTas = null;
            Texture2D texAhsap = null;
            Texture2D texMetal = null;
            Texture2D texToprak = null;
            Texture2D texSu = null;

            foreach (var r in renderers)
            {
                if (r == null || r.sharedMaterial == null) continue;
                Material m = r.sharedMaterial;
                string ad = m.name;

                if (ad.Contains("Mat_TasDuvar"))
                {
                    if (texTas == null) texTas = UretOrmeTasDokusu(new Color(0.50f, 0.47f, 0.43f), new Color(0.22f, 0.20f, 0.18f));
                    KaplamaAta(m, texTas, new Vector2(3f, 2f), 0.15f);
                }
                else if (ad.Contains("Mat_KoyuTas"))
                {
                    if (texKoyuTas == null) texKoyuTas = UretOrmeTasDokusu(new Color(0.33f, 0.31f, 0.29f), new Color(0.14f, 0.13f, 0.12f));
                    KaplamaAta(m, texKoyuTas, new Vector2(3f, 2f), 0.20f);
                }
                else if (ad.Contains("Mat_Ahsap"))
                {
                    if (texAhsap == null) texAhsap = UretAhsapKalasDokusu(new Color(0.48f, 0.34f, 0.20f), new Color(0.24f, 0.16f, 0.09f));
                    KaplamaAta(m, texAhsap, new Vector2(2f, 2f), 0.18f);
                }
                else if (ad.Contains("Mat_PasliMetal"))
                {
                    if (texMetal == null) texMetal = UretPercinliMetalDokusu(new Color(0.38f, 0.26f, 0.20f), new Color(0.55f, 0.28f, 0.14f));
                    KaplamaAta(m, texMetal, new Vector2(2f, 2f), 0.45f);
                }
                else if (ad.Contains("Mat_ToprakZemin"))
                {
                    if (texToprak == null) texToprak = UretToprakDokusu(new Color(0.34f, 0.30f, 0.23f), new Color(0.24f, 0.21f, 0.16f));
                    KaplamaAta(m, texToprak, new Vector2(16f, 16f), 0.08f);
                }
                else if (ad.Contains("Mat_HendekSuyu"))
                {
                    if (texSu == null) texSu = UretSuDokusu(new Color(0.06f, 0.28f, 0.42f), new Color(0.28f, 0.72f, 0.88f));
                    KaplamaAta(m, texSu, new Vector2(7f, 2.5f), 0.90f);
                }
            }
        }

        /// <summary>
        /// Düz zemin üstündeki ince su kutusunu kaldırıp yerine:
        /// - Gerçek 3.5m derinliğinde oyulmuş taş hendek kanyonu (Kale İçi ve Kale Dışı ayrı zemin blokları),
        /// - Hendek içi taş istinat duvarları, taş köprü ayakları, su altı kayaları, kıyı köpükleri,
        /// - 1.1m aşağıda akan gerçek 3B dalgalı su ağı (GercekciSuYuzeyi) ve çevre çam ağaçları inşa eder.
        /// </summary>
        public static void DerinHendekVeCevreyiGarantile()
        {
            GameObject zeminGrup = GameObject.Find("1_Bolum1_KaleDisi_Hendek_Ve_AsmaKopru");
            if (zeminGrup == null) return;

            // 1. Ana Zemini Hendek Boyunca İkiye Ayır (Gerçek 3.5m Derinliğinde Hendek Çukuru Aç!)
            Transform anaZemin = zeminGrup.transform.Find("AnaZemin_Toprak");
            Material matToprak = anaZemin != null ? anaZemin.GetComponent<MeshRenderer>()?.sharedMaterial : null;

            if (anaZemin != null && zeminGrup.transform.Find("KaleDisi_Zemin_Toprak") == null)
            {
                // AnaZemin sadece Kale İçi zemini olsun (Z = -8.6'dan Z = +58'e kadar)
                anaZemin.position = new Vector3(0f, -0.5f, 24.7f);
                anaZemin.localScale = new Vector3(78f, 1f, 66.6f);

                // Kale Dışı Kamp Zemini (Z = -38'den Z = -14.8'e kadar -> Aradaki Z = -14.8 ile -8.6 arası GERÇEK DERİN HENDEK ÇUKURUDUR!)
                GameObject disZemin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                disZemin.name = "KaleDisi_Zemin_Toprak";
                disZemin.transform.SetParent(zeminGrup.transform);
                disZemin.transform.position = new Vector3(0f, -0.5f, -26.4f);
                disZemin.transform.localScale = new Vector3(78f, 1f, 23.2f);
                if (matToprak != null) disZemin.GetComponent<MeshRenderer>().sharedMaterial = matToprak;
            }

            // 2. Hendek Kanyonu, Taş İstinat Duvarları, Köprü Ayakları ve 3B Dalgalı Su Kurulumu
            Transform hendekGrup = zeminGrup.transform.Find("Kale_Hendegi_Moat");
            if (hendekGrup == null) return;

            if (hendekGrup.Find("Hendek_DerinTaban") != null) return; // Zaten yeni derin hendek kurulmuş

            MeshRenderer ornekTasRend = GameObject.Find("Sol_Anahtar_Sutunu")?.GetComponent<MeshRenderer>();
            Material matKoyuTas = ornekTasRend != null ? ornekTasRend.sharedMaterial : matToprak;

            Transform eskiSu = hendekGrup.Find("Hendek_Suyu");
            Material matSu = eskiSu != null ? eskiSu.GetComponent<MeshRenderer>()?.sharedMaterial : null;
            if (eskiSu != null)
            {
                Object.DestroyImmediate(eskiSu.gameObject);
            }

            // Hendek Derin Taş Tabanı (Y = -3.4m derinlikte)
            OlusturParca("Hendek_DerinTaban", new Vector3(0f, -3.4f, -11.7f), new Vector3(48f, 0.6f, 7.2f), matKoyuTas, hendekGrup);

            // Hendek Güney (Dış) ve Kuzey (Kale Tarafı) Derin Taş İstinat Duvarları (Y = 0'dan Y = -3.4m'ye inen gerçek taş kanyon duvarları)
            OlusturParca("Hendek_GuneyIstinatDuvari", new Vector3(0f, -1.65f, -14.9f), new Vector3(48f, 3.5f, 0.7f), matKoyuTas, hendekGrup);
            OlusturParca("Hendek_KuzeyIstinatDuvari", new Vector3(0f, -1.65f, -8.5f), new Vector3(48f, 3.5f, 0.7f), matKoyuTas, hendekGrup);
            OlusturParca("Hendek_SolKanyonDuvari", new Vector3(-16.2f, -1.65f, -11.7f), new Vector3(1.2f, 3.5f, 7.0f), matKoyuTas, hendekGrup);
            OlusturParca("Hendek_SagKanyonDuvari", new Vector3(16.2f, -1.65f, -11.7f), new Vector3(1.2f, 3.5f, 7.0f), matKoyuTas, hendekGrup);

            // Hendek Kenarı Ortaçağ Taş Korkuluk Siperleri (Köprü ağzı X = -2.8..+2.8 açık kalır)
            OlusturParca("Hendek_KenarSiper_Sol", new Vector3(-9.5f, 0.45f, -14.8f), new Vector3(13.2f, 0.9f, 0.75f), matKoyuTas, hendekGrup);
            OlusturParca("Hendek_KenarSiper_Sag", new Vector3(9.5f, 0.45f, -14.8f), new Vector3(13.2f, 0.9f, 0.75f), matKoyuTas, hendekGrup);

            // Asma Köprünün Karşı Kıyıda Oturduğu Kemerli Taş Köprü İskelesi & Su İçi Taş Sütunlar
            OlusturParca("Kopru_KarsiTasIskele", new Vector3(0f, -1.2f, -14.5f), new Vector3(5.8f, 2.5f, 1.4f), matKoyuTas, hendekGrup);
            OlusturParca("Kopru_SuIciSutun_Sol", new Vector3(-2.3f, -1.7f, -11.7f), new Vector3(0.95f, 3.4f, 0.95f), matKoyuTas, hendekGrup);
            OlusturParca("Kopru_SuIciSutun_Sag", new Vector3(2.3f, -1.7f, -11.7f), new Vector3(0.95f, 3.4f, 0.95f), matKoyuTas, hendekGrup);

            // Gerçek 3B Dalgalı Hendek Suyu (Zeminden 0.95m aşağıda, taş kanyonun içinde akar!)
            GameObject yeniSu = new GameObject("Hendek_Suyu_3B");
            yeniSu.transform.SetParent(hendekGrup);
            yeniSu.transform.position = new Vector3(0f, -0.95f, -11.7f);
            MeshFilter mf = yeniSu.AddComponent<MeshFilter>();
            MeshRenderer mr = yeniSu.AddComponent<MeshRenderer>();
            if (matSu != null) mr.sharedMaterial = matSu;
            GercekciSuYuzeyi suDalga = yeniSu.AddComponent<GercekciSuYuzeyi>();
            suDalga.genislik = 32f;
            suDalga.uzunluk = 6.2f;
            suDalga.SuMeshOlustur();

            // Su İçi Doğal Kayalar (Boşluk hissini tamamen ortadan kaldıran çevre detayları)
            KayaOlustur("HendekKaya_Sol1", new Vector3(-7.5f, -0.9f, -11.2f), new Vector3(1.8f, 1.4f, 1.5f), matKoyuTas, hendekGrup);
            KayaOlustur("HendekKaya_Sol2", new Vector3(-11.5f, -0.8f, -12.4f), new Vector3(2.2f, 1.6f, 1.9f), matKoyuTas, hendekGrup);
            KayaOlustur("HendekKaya_Sag1", new Vector3(7.8f, -0.9f, -12.0f), new Vector3(1.9f, 1.5f, 1.6f), matKoyuTas, hendekGrup);
            KayaOlustur("HendekKaya_Sag2", new Vector3(12.0f, -0.8f, -11.0f), new Vector3(2.4f, 1.7f, 2.0f), matKoyuTas, hendekGrup);

            // Kale Dışı Çam Ağaçları ve Siper Kayalıkları
            CamAgaciOlustur("DisOrman_Agac_Sol1", new Vector3(-11.5f, 0f, -19.5f), zeminGrup.transform);
            CamAgaciOlustur("DisOrman_Agac_Sol2", new Vector3(-13.0f, 0f, -25.0f), zeminGrup.transform);
            CamAgaciOlustur("DisOrman_Agac_Sag1", new Vector3(11.5f, 0f, -19.5f), zeminGrup.transform);
            CamAgaciOlustur("DisOrman_Agac_Sag2", new Vector3(13.0f, 0f, -25.0f), zeminGrup.transform);
        }

        private static GameObject OlusturParca(string ad, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = ad;
            g.transform.SetParent(parent);
            g.transform.position = pos;
            g.transform.localScale = scale;
            if (mat != null) g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return g;
        }

        private static void KayaOlustur(string ad, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            GameObject k = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            k.name = ad;
            k.transform.SetParent(parent);
            k.transform.position = pos;
            k.transform.localScale = scale;
            k.transform.rotation = Quaternion.Euler(12f, 35f, 8f);
            if (mat != null) k.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void CamAgaciOlustur(string ad, Vector3 pos, Transform parent)
        {
            GameObject agac = new GameObject(ad);
            agac.transform.SetParent(parent);
            agac.transform.position = pos;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material matGovde = new Material(sh) { color = new Color(0.32f, 0.21f, 0.12f) };
            Material matYaprak = new Material(sh) { color = new Color(0.14f, 0.34f, 0.18f) };

            GameObject govde = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            govde.name = "Govde";
            govde.transform.SetParent(agac.transform);
            govde.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            govde.transform.localScale = new Vector3(0.55f, 1.4f, 0.55f);
            govde.GetComponent<MeshRenderer>().sharedMaterial = matGovde;

            for (int i = 0; i < 3; i++)
            {
                GameObject kat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                kat.name = $"YaprakKat_{i + 1}";
                kat.transform.SetParent(agac.transform);
                float y = 2.6f + i * 1.15f;
                float cap = 2.6f - i * 0.55f;
                kat.transform.localPosition = new Vector3(0f, y, 0f);
                kat.transform.localScale = new Vector3(cap, 1.5f, cap);
                kat.GetComponent<MeshRenderer>().sharedMaterial = matYaprak;
            }
        }

        public static void KaplamaAta(Material mat, Texture2D tex, Vector2 tiling, float smoothness)
        {
            if (mat == null || tex == null) return;

            mat.mainTexture = tex;
            mat.mainTextureScale = tiling;

            if (mat.HasProperty("_BaseMap"))
            {
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", tiling);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }
            if (mat.HasProperty("_Glossiness"))
            {
                mat.SetFloat("_Glossiness", smoothness);
            }
        }

        public static Texture2D UretOrmeTasDokusu(Color anaTasRengi, Color derzRengi)
        {
            int boyut = 256;
            Texture2D tex = new Texture2D(boyut, boyut, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            int satirSayisi = 8;
            int sutunSayisi = 4;
            int satirYukseklik = boyut / satirSayisi;
            int sutunGenislik = boyut / sutunSayisi;
            int derzKalinligi = 3;

            for (int y = 0; y < boyut; y++)
            {
                int satir = y / satirYukseklik;
                int yIci = y % satirYukseklik;
                int xKaydirma = (satir % 2 == 1) ? (sutunGenislik / 2) : 0;

                for (int x = 0; x < boyut; x++)
                {
                    int xKaydirilmis = (x + xKaydirma) % boyut;
                    int sutun = xKaydirilmis / sutunGenislik;
                    int xIci = xKaydirilmis % sutunGenislik;

                    bool yatayDerz = (yIci < derzKalinligi || yIci >= satirYukseklik - 1);
                    bool dikeyDerz = (xIci < derzKalinligi || xIci >= sutunGenislik - 1);

                    if (yatayDerz || dikeyDerz)
                    {
                        float derzGurultu = Mathf.PerlinNoise(x * 0.12f, y * 0.12f) * 0.08f;
                        tex.SetPixel(x, y, derzRengi + new Color(derzGurultu, derzGurultu, derzGurultu, 0f));
                    }
                    else
                    {
                        float blokTon = ((satir * 7 + sutun * 13) % 9 - 4) * 0.018f;
                        float puruz = (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.14f;
                        float inceDetay = (Mathf.PerlinNoise(x * 0.25f, y * 0.25f) - 0.5f) * 0.06f;

                        float kenarFaktor = 1.0f;
                        if (yIci < derzKalinligi + 3 || xIci < derzKalinligi + 3) kenarFaktor = 0.88f;
                        else if (yIci > satirYukseklik - 5) kenarFaktor = 1.07f;

                        Color c = (anaTasRengi + new Color(blokTon + puruz + inceDetay, blokTon + puruz + inceDetay, blokTon + puruz + inceDetay, 0f)) * kenarFaktor;
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        public static Texture2D UretAhsapKalasDokusu(Color acikAhsap, Color koyuDamar)
        {
            int boyut = 256;
            Texture2D tex = new Texture2D(boyut, boyut, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            int kalasSayisi = 6;
            int kalasGenislik = boyut / kalasSayisi;

            for (int y = 0; y < boyut; y++)
            {
                for (int x = 0; x < boyut; x++)
                {
                    int kalasIciX = x % kalasGenislik;
                    if (kalasIciX < 2 || y % 128 < 2)
                    {
                        tex.SetPixel(x, y, koyuDamar * 0.65f);
                    }
                    else
                    {
                        float damar = Mathf.Sin((x * 0.35f) + Mathf.PerlinNoise(x * 0.05f, y * 0.02f) * 8f) * 0.5f + 0.5f;
                        Color c = Color.Lerp(acikAhsap, koyuDamar, damar * 0.45f);
                        c.a = 1f;
                        tex.SetPixel(x, y, c);
                    }
                }
            }

            tex.Apply();
            return tex;
        }

        public static Texture2D UretPercinliMetalDokusu(Color metalRengi, Color pasRengi)
        {
            int boyut = 256;
            Texture2D tex = new Texture2D(boyut, boyut, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < boyut; y++)
            {
                for (int x = 0; x < boyut; x++)
                {
                    bool cerceve = (x < 4 || x > boyut - 5 || y < 4 || y > boyut - 5 || Mathf.Abs(x - 128) < 2 || Mathf.Abs(y - 128) < 2);
                    float pasOrani = Mathf.PerlinNoise(x * 0.04f + 10f, y * 0.04f + 10f);
                    float fircaIzi = (Mathf.PerlinNoise(x * 0.3f, y * 0.05f) - 0.5f) * 0.08f;

                    Color c = Color.Lerp(metalRengi, pasRengi, Mathf.Clamp01((pasOrani - 0.45f) * 2.2f));
                    c += new Color(fircaIzi, fircaIzi, fircaIzi, 0f);
                    if (cerceve) c *= 0.68f;
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return tex;
        }

        public static Texture2D UretToprakDokusu(Color acikToprak, Color koyuToprak)
        {
            int boyut = 256;
            Texture2D tex = new Texture2D(boyut, boyut, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < boyut; y++)
            {
                for (int x = 0; x < boyut; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                    float n2 = Mathf.PerlinNoise(x * 0.22f, y * 0.22f) * 0.35f;
                    Color c = Color.Lerp(koyuToprak, acikToprak, Mathf.Clamp01(n1 + n2 * 0.5f));
                    c.a = 1f;
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Su kostik yansımaları (caustics) ve dalga köpüğü içeren gerçekçi su dokusu.
        /// </summary>
        public static Texture2D UretSuDokusu(Color derinSu, Color dalgaKopugu)
        {
            int boyut = 256;
            Texture2D tex = new Texture2D(boyut, boyut, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < boyut; y++)
            {
                for (int x = 0; x < boyut; x++)
                {
                    float n1 = Mathf.PerlinNoise(x * 0.045f, y * 0.045f);
                    float n2 = Mathf.PerlinNoise(x * 0.09f + 30f, y * 0.09f + 30f);
                    float kostik = Mathf.Pow(1f - Mathf.Abs(n1 - n2) * 2.2f, 3f);
                    float dalga = Mathf.Sin(x * 0.08f + n1 * 6f) * 0.5f + 0.5f;

                    Color c = Color.Lerp(derinSu, dalgaKopugu, Mathf.Clamp01(dalga * 0.35f + kostik * 0.65f));
                    c.a = 0.95f;
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
