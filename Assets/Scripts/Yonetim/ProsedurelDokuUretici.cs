using UnityEngine;

namespace Yazlab.Yonetim
{
    /// <summary>
    /// Kalenin dış ve iç surlarını, kulesini, ahşap asma köprüsünü, paslı demir kapılarını
    /// ve hendek suyunu düz renkli küpler olmaktan çıkarıp gerçekçi örme taş, ahşap kalas
    /// ve perçinli metal kaplamalarla (Texture2D) donatan prosedürel doku üreticisi.
    /// </summary>
    public static class ProsedurelDokuUretici
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void SahnedekiMateryalleriKapla()
        {
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
                    KaplamaAta(m, texToprak, new Vector2(18f, 18f), 0.08f);
                }
                else if (ad.Contains("Mat_HendekSuyu"))
                {
                    if (texSu == null) texSu = UretSuDokusu(new Color(0.10f, 0.32f, 0.44f), new Color(0.18f, 0.48f, 0.62f));
                    KaplamaAta(m, texSu, new Vector2(6f, 3f), 0.80f);
                }
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

        /// <summary>
        /// Gerçekçi Ortaçağ Kalesi Örme Taş Duvar Kaplaması (Derz dolgulu, eskitmeli taş bloklar).
        /// </summary>
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
                        // Her taş bloğuna kendine özgü ton farkı + yüzey pürüzü
                        float blokTon = ((satir * 7 + sutun * 13) % 9 - 4) * 0.018f;
                        float puruz = (Mathf.PerlinNoise(x * 0.09f, y * 0.09f) - 0.5f) * 0.14f;
                        float inceDetay = (Mathf.PerlinNoise(x * 0.25f, y * 0.25f) - 0.5f) * 0.06f;

                        // Kenar gölgelendirmesi (Bevel hissi)
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

        /// <summary>
        /// Asma Köprü (Askı Kapı) ve Sandıklar için Ahşap Kalas & Damar Kaplaması.
        /// </summary>
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
                        // Kalas birleşim derzi
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

        /// <summary>
        /// Zırhlı Kapılar, Taret ve Jeneratör için Perçinli Paslı Metal Kaplaması.
        /// </summary>
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

        /// <summary>
        /// Kale Avlusu ve Dış Zemin için Doğal Toprak & Çakıl Kaplaması.
        /// </summary>
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
        /// Kale Hendeği (Moat) için Dalgalı Su Kaplaması.
        /// </summary>
        public static Texture2D UretSuDokusu(Color derinSu, Color dalgaKöpügü)
        {
            int boyut = 256;
            Texture2D tex = new Texture2D(boyut, boyut, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < boyut; y++)
            {
                for (int x = 0; x < boyut; x++)
                {
                    float dalga = Mathf.Sin(x * 0.08f + Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 6f) * 0.5f + 0.5f;
                    Color c = Color.Lerp(derinSu, dalgaKöpügü, dalga * 0.45f);
                    c.a = 0.92f;
                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply();
            return tex;
        }
    }
}
