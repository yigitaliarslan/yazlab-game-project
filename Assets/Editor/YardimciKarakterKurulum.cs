using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class YardimciKarakterKurulum
{
    static YardimciKarakterKurulum()
    {
        EditorApplication.delayCall += Yapilandir;
    }

    [MenuItem("Tools/Yardımcı Askeri ve Sistemleri Güncelle")]
    public static void YapilandirMenu()
    {
        Yapilandir();
        EditorUtility.DisplayDialog("Başarılı", "Müttefik asker karakteri (düşmanlara ateş eden takım arkadaşı) hazırlandı!", "Tamam");
    }

    public static void Yapilandir()
    {
        // 1. Ana Kamera ayarları (KameraTakip)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            // Eski OyuncuHareket varsa kaldır
            OyuncuHareket oh = mainCam.GetComponent<OyuncuHareket>();
            if (oh != null)
            {
                Object.DestroyImmediate(oh);
            }

            KameraTakip kt = mainCam.GetComponent<KameraTakip>();
            if (kt == null)
            {
                kt = mainCam.gameObject.AddComponent<KameraTakip>();
            }

            KarakterHareket kh = Object.FindAnyObjectByType<KarakterHareket>();
            if (kh != null)
            {
                kt.hedef = kh.transform;
            }
            kt.mesafe = 4.5f;
            kt.bakisYuksekligi = 1.50f;
            kt.fareHassasiyeti = 2.0f;
            kt.takipHizi = 12.0f;
            kt.donusHizi = 14.0f;
            kt.minPitch = -35f;
            kt.maxPitch = 60f;
            kt.sagTiklaBirinciSahis = true;
            kt.birinciSahisOfset = new Vector3(0.0f, 1.82f, 0.28f);
            kt.normalFOV = 60f;
            kt.nisanFOV = 58f;
            kt.gecisHizi = 15f;
            EditorUtility.SetDirty(kt);
        }

        // 2. Yardımcı Asker Karakteri Kurulumu (Düşmanlara sıkan müttefik asker)
        YardimciKarakter yk = Object.FindAnyObjectByType<YardimciKarakter>();
        bool eskiAjanMi = false;
        if (yk != null)
        {
            SkinnedMeshRenderer smr = yk.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMaterial != null && (smr.sharedMaterial.name.Contains("Ajan") || smr.sharedMaterial.name.Contains("criminal")))
            {
                eskiAjanMi = true;
            }
        }

        if (yk == null || eskiAjanMi)
        {
            Vector3 spawnPos = new Vector3(2.5f, 0f, 2.0f);
            if (yk != null)
            {
                spawnPos = yk.transform.position;
                Object.DestroyImmediate(yk.gameObject);
            }

            // Orijinal Asker Prefabını Yükle
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonSoldiers_WW2_demo/ToonSoldier_WW2_demo_Prefab.prefab");
            Material companionMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonSoldiers_WW2_demo/model/Materials/US_soldier_companion.mat");
            RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ToonSoldiers_WW2_demo/KarakterAnimator.controller");

            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "YardimciKarakter";
                instance.transform.position = spawnPos;
                instance.transform.rotation = Quaternion.identity;

                // Fazlalık oyuncu scriptlerini temizle
                KarakterHareket kh = instance.GetComponent<KarakterHareket>();
                if (kh != null) Object.DestroyImmediate(kh);
                OyuncuHareket oh = instance.GetComponent<OyuncuHareket>();
                if (oh != null) Object.DestroyImmediate(oh);
                CharacterController cc = instance.GetComponent<CharacterController>();
                if (cc != null) Object.DestroyImmediate(cc);

                yk = instance.AddComponent<YardimciKarakter>();
                yk.donusModu = YardimciKarakter.DonusModu.OyuncuyaBak;
                yk.donusHizi = 8.0f;

                KarakterHareket oyuncu = Object.FindAnyObjectByType<KarakterHareket>();
                if (oyuncu != null)
                {
                    yk.hedef = oyuncu.transform;
                }

                if (companionMat != null)
                {
                    SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        smr.sharedMaterial = companionMat;
                        EditorUtility.SetDirty(smr);
                    }
                }

                if (animCtrl != null)
                {
                    Animator anim = instance.GetComponent<Animator>();
                    if (anim != null) anim.runtimeAnimatorController = animCtrl;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Yardimci Karakter Olusturuldu");
                EditorUtility.SetDirty(instance);
            }
        }

        // 3. Ana Oyuncu için Ateş Etme Bileşeni (KarakterAtes)
        KarakterHareket oyuncuKh = Object.FindAnyObjectByType<KarakterHareket>();
        if (oyuncuKh != null)
        {
            KarakterAtes ates = oyuncuKh.GetComponent<KarakterAtes>();
            if (ates == null)
            {
                ates = oyuncuKh.gameObject.AddComponent<KarakterAtes>();
            }
            if (Camera.main != null)
            {
                ates.kamera = Camera.main.transform;
            }
            ates.namluOfset3P = new Vector3(0.22f, 1.20f, 0.95f);
            ates.namluOfset1P = new Vector3(0.20f, -0.32f, 0.55f);
            EditorUtility.SetDirty(oyuncuKh.gameObject);
        }

        // 4. Ajan Hedefini Doğrula
        if (yk != null && oyuncuKh != null)
        {
            yk.hedef = oyuncuKh.transform;
            EditorUtility.SetDirty(yk);
        }

        // Sahneyi kaydet
        if (!Application.isPlaying)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
            }
        }
    }
}
