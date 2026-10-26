using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class YardimciKarakterKurulum
{
    static YardimciKarakterKurulum()
    {
        if (!Application.isBatchMode)
        {
            EditorApplication.delayCall += Yapilandir;
        }
    }

    [MenuItem("Tools/Yardımcı Askeri ve Sistemleri Güncelle")]
    public static void YapilandirMenu()
    {
        Yapilandir();
        if (!Application.isBatchMode)
        {
            EditorUtility.DisplayDialog("Başarılı", "Müttefik asker karakteri ve sağ omuz üstü taktiksel kamera hazırlandı!", "Tamam");
        }
    }

    public static void Yapilandir()
    {
        // 1. Ana Kamera ayarları (Sağ Omuz Üstü Taktiksel KameraTakip)
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
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
            kt.mesafe = 3.5f;
            kt.bakisYuksekligi = 1.74f;
            kt.omuzSagOfset = 0.72f;
            kt.fareHassasiyeti = 2.0f;
            kt.takipHizi = 16.0f;
            kt.donusHizi = 18.0f;
            kt.minPitch = -32f;
            kt.maxPitch = 55f;
            kt.sagTiklaBirinciSahis = true;
            kt.birinciSahisOfset = new Vector3(0.0f, 1.82f, 0.28f);
            kt.normalFOV = 60f;
            kt.nisanFOV = 55f;
            kt.gecisHizi = 15f;
            EditorUtility.SetDirty(kt);
        }

        // 2. Yardımcı Asker Karakteri Kurulumu
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
            Vector3 spawnPos = new Vector3(2.0f, 0.05f, -21.5f);
            if (yk != null)
            {
                spawnPos = yk.transform.position;
                Object.DestroyImmediate(yk.gameObject);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonSoldiers_WW2_demo/ToonSoldier_WW2_demo_Prefab.prefab");
            Material companionMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ToonSoldiers_WW2_demo/model/Materials/US_soldier_companion.mat");
            RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ToonSoldiers_WW2_demo/KarakterAnimator.controller");

            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "YardimciKarakter";
                instance.transform.position = spawnPos;
                instance.transform.rotation = Quaternion.identity;

                KarakterHareket kh = instance.GetComponent<KarakterHareket>();
                if (kh != null) Object.DestroyImmediate(kh);
                OyuncuHareket oh = instance.GetComponent<OyuncuHareket>();
                if (oh != null) Object.DestroyImmediate(oh);
                CharacterController cc = instance.GetComponent<CharacterController>();
                if (cc != null) Object.DestroyImmediate(cc);

                yk = instance.AddComponent<YardimciKarakter>();
                yk.maksimumCan = 100f;
                yk.mevcutCan = 100f;
                yk.atesMenzili = 30f;
                yk.atesAraligi = 0.9f;
                yk.atesHasari = 20f;
                yk.donusModu = YardimciKarakter.DonusModu.OyuncuyaBak;

                KarakterHareket oyuncu = Object.FindAnyObjectByType<KarakterHareket>();
                if (oyuncu != null)
                {
                    yk.hedef = oyuncu.transform;
                }

                CapsuleCollider col = instance.GetComponent<CapsuleCollider>();
                if (col == null)
                {
                    col = instance.AddComponent<CapsuleCollider>();
                    col.height = 1.8f;
                    col.radius = 0.38f;
                    col.center = new Vector3(0f, 0.9f, 0f);
                }

                Animator anim = instance.GetComponent<Animator>();
                if (anim != null && animCtrl != null)
                {
                    anim.runtimeAnimatorController = animCtrl;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                if (companionMat != null)
                {
                    SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null)
                    {
                        smr.sharedMaterial = companionMat;
                    }
                }
            }
        }
        else
        {
            KarakterHareket oyuncu = Object.FindAnyObjectByType<KarakterHareket>();
            if (oyuncu != null)
            {
                yk.hedef = oyuncu.transform;
                EditorUtility.SetDirty(yk);
            }
        }

        if (!Application.isPlaying && !Application.isBatchMode)
        {
            var aktifSahne = EditorSceneManager.GetActiveScene();
            if (aktifSahne.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(aktifSahne);
                EditorSceneManager.SaveScene(aktifSahne);
            }
        }
    }
}
