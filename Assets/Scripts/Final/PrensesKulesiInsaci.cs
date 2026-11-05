using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yazlab.Mekanikler
{
    /// <summary>
    /// 4. BÖLÜM (FİNAL) — "KIZIL KULE":
    /// Komutan Varg'ın koruduğu çıkış kapısının arkasına, Kuzey Burcu'nun yerine içi boş, yuvarlak ve
    /// çok katmanlı bir "Kızıl Kule" inşa eder. Kulenin içinde Prenses Betül'ün tutulduğu Mühürlü Kapsül,
    /// Kehanet Küresi ve silahla vurularak kırılan 3 Zümrüt Kristali bulunur.
    ///
    /// Sahne dosyasına dokunmadan, oyun başladığında (AfterSceneLoad) ve sahne her yeniden yüklendiğinde
    /// (SceneManager.sceneLoaded) çalışma zamanında kurulur; böylece oyun bittiğinde veya yeniden başlatıldığında
    /// çıkış kapısı arkası ve Prenses kurtarma görevi her zaman kusursuz çalışır.
    /// </summary>
    public static class PrensesKulesiInsaci
    {
        public const string KokAdi = "--- 4_FINAL_KIZIL_KULE_PRENSES ---";
        public static readonly Vector3 KuleMerkezi = new Vector3(0f, 0f, 50.2f);
        public const float ZeminY = 2.2f;
        public const float IcYaricap = 5.8f;
        public const float DuvarYaricap = 6.2f;
        public const float DuvarYukseklik = 10.5f;

        private static Material _tas, _koyuTas, _ahsap, _metal;
        private static bool _eventKayitli = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OtomatikKayitVeKurulum()
        {
            if (!_eventKayitli)
            {
                SceneManager.sceneLoaded -= OnSahneYuklendi;
                SceneManager.sceneLoaded += OnSahneYuklendi;
                _eventKayitli = true;
            }
            FinalKulesiniKur();
        }

        private static void OnSahneYuklendi(Scene sahne, LoadSceneMode mod)
        {
            FinalKulesiniKur();
        }

        public static void FinalKulesiniKur()
        {
            if (GameObject.Find("--- HARITA_SEVIYESI ---") == null) return;
            if (GameObject.Find("NpcCikisKapisi") == null) return;

            // Eğer kule zaten kurulduysa bile arka duvar ve eski tetikleyicinin kapalı olduğundan emin ol
            GameObject mevcutKok = GameObject.Find(KokAdi);
            if (mevcutKok != null)
            {
                GameObject eskiArka = GameObject.Find("KuleDuvar_Arka");
                if (eskiArka != null && eskiArka.activeSelf) eskiArka.SetActive(false);
                GameObject eskiTetik = GameObject.Find("BolumSonu_Tetikleyici");
                if (eskiTetik != null && eskiTetik.activeSelf) eskiTetik.SetActive(false);
                return;
            }

            MateryalleriTopla();

            GameObject kok = new GameObject(KokAdi);

            EngelleriTemizle(kok.transform);
            ArkaDuvaraGecitAc(kok.transform);
            KuleGovdesiniKur(kok.transform);
            IcDekorasyonuKur(kok.transform);
            AydinlatmayiKur(kok.transform);

            PrensesKurtarmaGorevi gorev = kok.AddComponent<PrensesKurtarmaGorevi>();
            PrensesKurtarmaGorevi.Ornek = gorev;
            gorev.kafesCubuklari = KafesKur(kok.transform, out gorev.muhurBariyeri);
            gorev.kehanetKuresi = KehanetKuresiKur(kok.transform, out gorev.kureIsigi);
            gorev.kristaller = new RunKristali[]
            {
                KristalKur(kok.transform, 0, "Kirmizi_Zumrut_Kristali", new Vector3(-3.6f, ZeminY, 51.2f), true),
                KristalKur(kok.transform, 1, "Mavi_Zumrut_Kristali", new Vector3(3.6f, ZeminY, 51.2f), true),
                KristalKur(kok.transform, 2, "Yesil_Zumrut_Kristali", new Vector3(0f, ZeminY + 2.45f, 51.8f), false)
            };
            gorev.kacisKapagi = KacisKapagiKur(kok.transform, out gorev.kapakDeligi);
            gorev.zincirTekerlegi = TekerlekKur(kok.transform);
            gorev.prenses = PrensesKarakteri.Olustur(kok.transform, new Vector3(0f, ZeminY + 0.3f, 53.4f));
            gorev.alarmIsiklari = AlarmIsiklariKur(kok.transform);

            Debug.Log("[PrensesKulesi] 4. Bölüm (Final) — Kızıl Kule, Zümrüt Kristaller ve Prenses Kurtarma görevi kuruldu.");
        }

        // ==========================================================
        // MATERYALLER
        // ==========================================================
        private static void MateryalleriTopla()
        {
            _tas = MateryalBul("KuleDuvar_Sol", new Color(0.62f, 0.58f, 0.53f));
            _koyuTas = MateryalBul("Kule_IkinciKat_Platformu", new Color(0.42f, 0.40f, 0.38f));
            _ahsap = MateryalBul("NobetciMasasi", new Color(0.55f, 0.38f, 0.22f));
            _metal = MateryalBul("FizikselEngelKapisi", new Color(0.45f, 0.32f, 0.24f));
        }

        private static Material MateryalBul(string objeAdi, Color yedekRenk)
        {
            GameObject go = GameObject.Find(objeAdi);
            MeshRenderer mr = go != null ? go.GetComponent<MeshRenderer>() : null;
            if (mr != null && mr.sharedMaterial != null) return mr.sharedMaterial;
            return LitMateryal(yedekRenk);
        }

        public static Material LitMateryal(Color renk)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material m = new Material(sh) { color = renk };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            return m;
        }

        /// <summary>Işıktan etkilenmeyen, kendiliğinden parlayan (glow) veya yarı saydam materyal.</summary>
        public static Material ParlakMateryal(Color renk)
        {
            Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            Material m = new Material(sh) { color = renk };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            return m;
        }

        // ==========================================================
        // ENGELLERİ TEMİZLE & ARKA DUVARA GEÇİT AÇ
        // ==========================================================
        private static void EngelleriTemizle(Transform kok)
        {
            // Eski "kapıya girince oyun biter" tetikleyicisi ve kulenin yerini kaplayan dolu burç
            string[] adlar = { "BolumSonu_Tetikleyici", "KuzeyBurcu", "KuzeyProjektoru" };
            foreach (string ad in adlar)
            {
                GameObject go = GameObject.Find(ad);
                if (go != null) go.SetActive(false);
            }

            // Kule iç hacmine taşan sur parçaları / mazgallar
            var kapatilacaklar = new List<GameObject>();
            foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (r == null || r.transform.IsChildOf(kok)) continue;
                if (r.GetComponentInParent<Animator>() != null) continue;
                if (r.name == "KuleDuvar_Arka" || r.name == "NpcCikisKapisi") continue;

                Bounds b = r.bounds;
                if (b.max.y < ZeminY + 0.3f) continue;
                if (b.size.x > 30f || b.size.z > 30f) continue;

                float dx = b.center.x - KuleMerkezi.x;
                float dz = b.center.z - KuleMerkezi.z;
                if (dx * dx + dz * dz < (IcYaricap + 0.6f) * (IcYaricap + 0.6f))
                {
                    kapatilacaklar.Add(r.gameObject);
                }
            }
            foreach (GameObject go in kapatilacaklar) go.SetActive(false);
        }

        private static void ArkaDuvaraGecitAc(Transform kok)
        {
            GameObject arka = GameObject.Find("KuleDuvar_Arka");
            Material mat = _tas;
            if (arka != null)
            {
                MeshRenderer mr = arka.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null) mat = mr.sharedMaterial;
                arka.SetActive(false);
            }

            Transform grup = new GameObject("ArkaDuvar_KemerliGecit").transform;
            grup.SetParent(kok);

            Kutu("ArkaDuvar_Sol", new Vector3(-6f, 5f, 44f), new Vector3(6f, 10f, 2f), mat, grup);
            Kutu("ArkaDuvar_Sag", new Vector3(6f, 5f, 44f), new Vector3(6f, 10f, 2f), mat, grup);
            Kutu("ArkaDuvar_UstKemer", new Vector3(0f, 8.1f, 44f), new Vector3(6f, 3.8f, 2f), mat, grup);
            Kutu("ArkaDuvar_GecitZemini", new Vector3(0f, ZeminY * 0.5f, 44f), new Vector3(6f, ZeminY, 2f), _koyuTas, grup);
            Kutu("Gecit_KemerTasi", new Vector3(0f, 6.1f, 43.0f), new Vector3(6.6f, 0.35f, 0.4f), _koyuTas, grup, false);
        }

        // ==========================================================
        // KULE GÖVDESİ (Yuvarlak duvar halkası, dolgu zemin, çatı ve kule külahı)
        // ==========================================================
        private static void KuleGovdesiniKur(Transform kok)
        {
            Transform govde = new GameObject("KizilKule_Govde").transform;
            govde.SetParent(kok);

            // Dolgu taş zemin (Y = 0 → 2.2) — altına düşüp sıkışma yok
            GameObject zemin = Silindir("Kule_DolguZemin", new Vector3(KuleMerkezi.x, ZeminY * 0.5f, KuleMerkezi.z),
                new Vector3(DuvarYaricap * 2f + 0.6f, ZeminY * 0.5f, DuvarYaricap * 2f + 0.6f), _koyuTas, govde);
            MeshColliderYap(zemin);

            // Yuvarlak duvar halkası (güney tarafı arka duvar geçidine açık)
            int segment = 24;
            float genislik = 2f * Mathf.PI * DuvarYaricap / segment + 0.18f;
            for (int i = 0; i < segment; i++)
            {
                float aci = i * 360f / segment;
                if (Mathf.Abs(Mathf.DeltaAngle(aci, 180f)) < 32f) continue;

                float rad = aci * Mathf.Deg2Rad;
                Vector3 p = new Vector3(KuleMerkezi.x + Mathf.Sin(rad) * DuvarYaricap, DuvarYukseklik * 0.5f,
                                        KuleMerkezi.z + Mathf.Cos(rad) * DuvarYaricap);
                GameObject seg = Kutu($"KuleHalka_{i:00}", p, new Vector3(genislik, DuvarYukseklik, 0.8f), _tas, govde);
                seg.transform.rotation = Quaternion.Euler(0f, aci, 0f);

                // Üstte mazgal dişleri
                if (i % 2 == 0)
                {
                    GameObject dis = Kutu($"KuleMazgal_{i:00}", p + Vector3.up * (DuvarYukseklik * 0.5f + 0.4f),
                        new Vector3(genislik * 0.7f, 0.8f, 0.9f), _koyuTas, govde, false);
                    dis.transform.rotation = Quaternion.Euler(0f, aci, 0f);
                }
            }

            // Zemin ile duvar birleşimindeki taş kuşak
            Silindir("Kule_TasKusak", new Vector3(KuleMerkezi.x, ZeminY + 0.06f, KuleMerkezi.z),
                new Vector3(IcYaricap * 2f, 0.06f, IcYaricap * 2f), _tas, govde, false);

            // Çatı ve sivri kule külahı
            Silindir("Kule_Cati", new Vector3(KuleMerkezi.x, DuvarYukseklik + 0.15f, KuleMerkezi.z),
                new Vector3(DuvarYaricap * 2f + 1.0f, 0.15f, DuvarYaricap * 2f + 1.0f), _koyuTas, govde, false);

            Material kulahMat = LitMateryal(new Color(0.55f, 0.12f, 0.10f));
            float[] yaricaplar = { 5.6f, 4.4f, 3.2f, 2.0f, 0.9f };
            for (int i = 0; i < yaricaplar.Length; i++)
            {
                Silindir($"Kule_Kulah_{i + 1}", new Vector3(KuleMerkezi.x, DuvarYukseklik + 0.9f + i * 1.05f, KuleMerkezi.z),
                    new Vector3(yaricaplar[i] * 2f, 0.55f, yaricaplar[i] * 2f), kulahMat, govde, false);
            }

            // Tepe sancağı (Kızıl Şahin)
            GameObject direk = Silindir("Kule_SancakDiregi", new Vector3(KuleMerkezi.x, DuvarYukseklik + 7.0f, KuleMerkezi.z),
                new Vector3(0.12f, 1.6f, 0.12f), _ahsap, govde, false);
            GameObject flama = Kutu("Kule_KizilSahinSancagi", direk.transform.position + new Vector3(0.85f, 1.0f, 0f),
                new Vector3(1.5f, 0.9f, 0.05f), LitMateryal(new Color(0.75f, 0.08f, 0.08f)), govde, false);
            flama.AddComponent<BayrakDalgalanma>();
        }

        // ==========================================================
        // İÇ DEKORASYON (Halı, kürsü, sancaklar, vitray pencereler)
        // ==========================================================
        private static void IcDekorasyonuKur(Transform kok)
        {
            Transform dekor = new GameObject("KizilKule_IcDekor").transform;
            dekor.SetParent(kok);

            Material hali = LitMateryal(new Color(0.55f, 0.08f, 0.12f));
            Material altin = LitMateryal(new Color(0.95f, 0.75f, 0.25f));

            // Girişten kürsüye uzanan kraliyet halısı
            Kutu("Kraliyet_Halisi", new Vector3(0f, ZeminY + 0.02f, 48.6f), new Vector3(1.6f, 0.03f, 6.4f), hali, dekor, false);
            Kutu("Hali_AltinKenar_Sol", new Vector3(-0.85f, ZeminY + 0.025f, 48.6f), new Vector3(0.08f, 0.03f, 6.4f), altin, dekor, false);
            Kutu("Hali_AltinKenar_Sag", new Vector3(0.85f, ZeminY + 0.025f, 48.6f), new Vector3(0.08f, 0.03f, 6.4f), altin, dekor, false);

            // Prensesin kürsüsü (Mühür sunağı)
            GameObject kursu = Silindir("Muhur_Sunagi", new Vector3(0f, ZeminY + 0.15f, 53.4f), new Vector3(2.9f, 0.15f, 2.9f), _koyuTas, dekor);
            MeshColliderYap(kursu);
            Silindir("Sunak_RunHalkasi", new Vector3(0f, ZeminY + 0.31f, 53.4f), new Vector3(2.4f, 0.01f, 2.4f),
                ParlakMateryal(new Color(0.20f, 0.95f, 0.50f, 0.55f)), dekor, false);

            // Duvar sancakları (mor-altın kraliyet arması)
            Material morKumas = LitMateryal(new Color(0.32f, 0.12f, 0.45f));
            float[] sancakAcilari = { 50f, -50f, 95f, -95f };
            foreach (float a in sancakAcilari)
            {
                Vector3 p = HalkaNoktasi(a, IcYaricap - 0.12f, 5.2f);
                GameObject s = Kutu($"Kraliyet_Sancagi_{a}", p, new Vector3(1.0f, 2.6f, 0.05f), morKumas, dekor, false);
                s.transform.rotation = Quaternion.Euler(0f, a, 0f);
                GameObject arma = Kutu($"Sancak_Arma_{a}", p + (KuleMerkezi.WithY(p.y) - p).normalized * 0.03f + Vector3.up * 0.4f,
                    new Vector3(0.42f, 0.42f, 0.05f), altin, dekor, false);
                arma.transform.rotation = Quaternion.Euler(0f, a, 45f);
            }

            // Vitray pencereler (iç duvarda renkli parlayan camlar)
            Color[] vitrayRenk =
            {
                new Color(0.95f, 0.30f, 0.25f, 0.9f), new Color(0.25f, 0.55f, 1.0f, 0.9f),
                new Color(0.30f, 0.95f, 0.55f, 0.9f), new Color(1.0f, 0.82f, 0.30f, 0.9f)
            };
            float[] vitrayAci = { 25f, -25f, 130f, -130f };
            for (int i = 0; i < vitrayAci.Length; i++)
            {
                Vector3 p = HalkaNoktasi(vitrayAci[i], IcYaricap - 0.05f, 7.4f);
                GameObject v = Kutu($"Vitray_{i + 1}", p, new Vector3(0.9f, 1.8f, 0.04f), ParlakMateryal(vitrayRenk[i]), dekor, false);
                v.transform.rotation = Quaternion.Euler(0f, vitrayAci[i], 0f);
            }

            // Kırık sandıklar ve kitap rafı (prensesin hücresinin eşyaları)
            Kutu("Hucre_KitapRafi", HalkaNoktasi(-70f, IcYaricap - 0.45f, ZeminY + 1.1f), new Vector3(1.6f, 2.2f, 0.5f), _ahsap, dekor)
                .transform.rotation = Quaternion.Euler(0f, -70f, 0f);
            Kutu("Hucre_Sandik", HalkaNoktasi(-118f, IcYaricap - 0.9f, ZeminY + 0.4f), new Vector3(0.9f, 0.8f, 0.9f), _ahsap, dekor)
                .transform.rotation = Quaternion.Euler(0f, 20f, 0f);
        }

        // ==========================================================
        // AYDINLATMA
        // ==========================================================
        private static void AydinlatmayiKur(Transform kok)
        {
            Transform isiklar = new GameObject("KizilKule_Aydinlatma").transform;
            isiklar.SetParent(kok);

            float[] mesaleAci = { 65f, -65f, 120f, -120f };
            for (int i = 0; i < mesaleAci.Length; i++)
            {
                Vector3 p = HalkaNoktasi(mesaleAci[i], IcYaricap - 0.35f, 4.6f);
                GameObject alev = Kure($"Kule_DuvarMesalesi_{i + 1}", p, Vector3.one * 0.32f, ParlakMateryal(new Color(1f, 0.7f, 0.2f, 0.95f)), isiklar);
                Kutu($"Mesale_Tutacak_{i + 1}", p + Vector3.down * 0.35f, new Vector3(0.12f, 0.5f, 0.12f), _metal, isiklar, false);
                Light l = alev.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.62f, 0.28f);
                l.range = 9f;
                l.intensity = 2.6f;
                alev.AddComponent<IsikTitremesi>();
            }

            GameObject tavanIsik = new GameObject("Kule_TavanAyIsigi");
            tavanIsik.transform.SetParent(isiklar);
            tavanIsik.transform.position = new Vector3(KuleMerkezi.x, 9.0f, KuleMerkezi.z);
            Light tl = tavanIsik.AddComponent<Light>();
            tl.type = LightType.Point;
            tl.color = new Color(0.75f, 0.80f, 1.0f);
            tl.range = 16f;
            tl.intensity = 2.2f;
            tl.shadows = LightShadows.None;
        }

        private static Light[] AlarmIsiklariKur(Transform kok)
        {
            var liste = new List<Light>();
            float[] acilar = { 0f, 90f, 180f, 270f };
            foreach (float a in acilar)
            {
                GameObject go = new GameObject($"Kule_AlarmIsigi_{a}");
                go.transform.SetParent(kok);
                go.transform.position = HalkaNoktasi(a, 3.5f, 8.5f);
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.12f, 0.08f);
                l.range = 12f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                liste.Add(l);
            }
            return liste.ToArray();
        }

        // ==========================================================
        // MÜHÜRLÜ KAPSÜL, KEHANET KÜRESİ, 3 ZÜMRÜT KRİSTALİ
        // ==========================================================
        private static Transform[] KafesKur(Transform kok, out GameObject bariyer)
        {
            Transform kafes = new GameObject("Muhur_Kafesi").transform;
            kafes.SetParent(kok);

            var cubuklar = new List<Transform>();
            int adet = 12;
            Vector3 merkez = new Vector3(0f, ZeminY + 0.3f, 53.4f);
            for (int i = 0; i < adet; i++)
            {
                float rad = i * Mathf.PI * 2f / adet;
                Vector3 p = merkez + new Vector3(Mathf.Sin(rad) * 1.3f, 1.4f, Mathf.Cos(rad) * 1.3f);
                GameObject c = Silindir($"Kafes_Cubugu_{i + 1}", p, new Vector3(0.08f, 1.4f, 0.08f), _metal, kafes, false);
                cubuklar.Add(c.transform);
            }
            GameObject tepe = Silindir("Kafes_TepeHalkasi", merkez + Vector3.up * 2.82f, new Vector3(2.8f, 0.07f, 2.8f), _metal, kafes, false);
            cubuklar.Add(tepe.transform);

            bariyer = Silindir("Run_Muhur_Bariyeri", merkez + Vector3.up * 1.4f, new Vector3(2.9f, 1.45f, 2.9f),
                ParlakMateryal(new Color(0.15f, 0.95f, 0.50f, 0.24f)), kafes, false);
            return cubuklar.ToArray();
        }

        private static Transform KehanetKuresiKur(Transform kok, out Light isik)
        {
            Vector3 p = new Vector3(0f, 6.6f, 53.4f);
            GameObject kure = Kure("Kehanet_Kuresi", p, Vector3.one * 0.75f, ParlakMateryal(new Color(0.35f, 1.0f, 0.65f, 0.9f)), kok);
            Kutu("Kure_AsmaZinciri", p + Vector3.up * 2.0f, new Vector3(0.06f, 3.4f, 0.06f), _metal, kok, false);
            GameObject halka = Silindir("Kure_AltinHalka", p, new Vector3(1.15f, 0.03f, 1.15f), LitMateryal(new Color(0.95f, 0.75f, 0.25f)), kok, false);
            halka.transform.SetParent(kure.transform, true);

            isik = kure.AddComponent<Light>();
            isik.type = LightType.Point;
            isik.range = 10f;
            isik.intensity = 1.6f;
            isik.color = new Color(0.25f, 1.0f, 0.55f);
            isik.shadows = LightShadows.None;
            return kure.transform;
        }

        private static RunKristali KristalKur(Transform kok, int indeks, string ad, Vector3 taban, bool kaideli)
        {
            Color renk = RunKristali.RenkAl(indeks);
            Vector3 kristalPos;

            if (kaideli)
            {
                Kutu(ad + "_Kaidesi", taban + Vector3.up * 0.55f, new Vector3(0.75f, 1.1f, 0.75f), _koyuTas, kok);
                Kutu(ad + "_KaideBasligi", taban + Vector3.up * 1.14f, new Vector3(0.95f, 0.1f, 0.95f), _tas, kok, false);
                kristalPos = taban + Vector3.up * 1.75f;
            }
            else
            {
                // Kapsülün hemen ön-üst kısmında havada asılı altın halka kaide
                Silindir(ad + "_AltinHalka", taban + Vector3.down * 0.38f, new Vector3(0.85f, 0.04f, 0.85f),
                    LitMateryal(new Color(0.95f, 0.78f, 0.25f)), kok, false);
                kristalPos = taban;
            }

            GameObject kokObj = new GameObject(ad);
            kokObj.transform.SetParent(kok);
            kokObj.transform.position = kristalPos;
            SphereCollider sc = kokObj.AddComponent<SphereCollider>();
            sc.radius = 0.85f;

            GameObject gorsel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gorsel.name = "Kristal_Gorseli";
            gorsel.transform.SetParent(kokObj.transform, false);
            gorsel.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f);
            gorsel.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            MeshRenderer mr = gorsel.GetComponent<MeshRenderer>();
            mr.sharedMaterial = ParlakMateryal(renk);

            Light l = kokObj.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = renk;
            l.range = 5.5f;
            l.intensity = 1.6f;
            l.shadows = LightShadows.None;

            RunKristali rk = kokObj.AddComponent<RunKristali>();
            rk.indeks = indeks;
            rk.gorsel = gorsel.transform;
            rk.gorselRenderer = mr;
            rk.isik = l;
            return rk;
        }

        private static Transform KacisKapagiKur(Transform kok, out GameObject delik)
        {
            Vector3 p = new Vector3(3.2f, ZeminY, 47.4f);
            delik = Kutu("Kacis_Tuneli_Agzi", p + Vector3.up * 0.012f, new Vector3(1.5f, 0.02f, 1.5f),
                ParlakMateryal(new Color(0.02f, 0.06f, 0.12f, 1f)), kok, false);
            Kutu("Kapak_DemirCerceve", p + Vector3.up * 0.02f, new Vector3(1.85f, 0.04f, 1.85f), _metal, kok, false);
            GameObject kapak = Kutu("Kacis_Kapagi", p + Vector3.up * 0.08f, new Vector3(1.6f, 0.1f, 1.6f), _ahsap, kok, false);
            return kapak.transform;
        }

        private static Transform TekerlekKur(Transform kok)
        {
            Vector3 p = new Vector3(5.0f, ZeminY + 1.2f, 49.4f);
            GameObject tekerlek = new GameObject("Zincir_Tekerlegi");
            tekerlek.transform.SetParent(kok);
            tekerlek.transform.position = p;
            return tekerlek.transform;
        }

        // ==========================================================
        // YARDIMCI İNŞA FONKSİYONLARI
        // ==========================================================
        public static Vector3 HalkaNoktasi(float aciDerece, float yaricap, float y)
        {
            float rad = aciDerece * Mathf.Deg2Rad;
            return new Vector3(KuleMerkezi.x + Mathf.Sin(rad) * yaricap, y, KuleMerkezi.z + Mathf.Cos(rad) * yaricap);
        }

        private static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);

        private static GameObject Kutu(string ad, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool collider = true)
        {
            return Primitif(PrimitiveType.Cube, ad, pos, scale, mat, parent, collider);
        }

        private static GameObject Silindir(string ad, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool collider = true)
        {
            return Primitif(PrimitiveType.Cylinder, ad, pos, scale, mat, parent, collider);
        }

        private static GameObject Kure(string ad, Vector3 pos, Vector3 scale, Material mat, Transform parent)
        {
            return Primitif(PrimitiveType.Sphere, ad, pos, scale, mat, parent, false);
        }

        private static GameObject Primitif(PrimitiveType tip, string ad, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool collider)
        {
            GameObject g = GameObject.CreatePrimitive(tip);
            g.name = ad;
            g.transform.SetParent(parent);
            g.transform.position = pos;
            g.transform.localScale = scale;
            if (mat != null) g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!collider)
            {
                Collider c = g.GetComponent<Collider>();
                if (c != null)
                {
                    if (Application.isPlaying) Object.Destroy(c);
                    else Object.DestroyImmediate(c);
                }
            }
            return g;
        }

        /// <summary>Silindirin varsayılan kapsül çarpıştırıcısını gerçek silindir şekilli MeshCollider ile değiştirir.</summary>
        private static void MeshColliderYap(GameObject g)
        {
            Collider eski = g.GetComponent<Collider>();
            if (eski != null)
            {
                if (Application.isPlaying) Object.Destroy(eski);
                else Object.DestroyImmediate(eski);
            }
            MeshCollider mc = g.AddComponent<MeshCollider>();
            mc.sharedMesh = g.GetComponent<MeshFilter>().sharedMesh;
        }
    }
}
