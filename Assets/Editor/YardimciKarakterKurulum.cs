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

    [MenuItem("Tools/Yardımcı Karakter ve 3. Şahıs Kamerayı Güncelle")]
    public static void YapilandirMenu()
    {
        Yapilandir();
        EditorUtility.DisplayDialog("Başarılı", "Ana karakter 3. şahıs kameraya geçirildi ve yardımcı karakter hazırlandı!", "Tamam");
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
            kt.bakisYuksekligi = 1.4f;
            kt.fareHassasiyeti = 2.0f;
            kt.takipHizi = 12.0f;
            kt.donusHizi = 12.0f;
            kt.minPitch = -20f;
            kt.maxPitch = 60f;
            EditorUtility.SetDirty(kt);
        }

        // 2. Yardımcı Karakter kontrolü
        YardimciKarakter yk = Object.FindAnyObjectByType<YardimciKarakter>();
        if (yk == null)
        {
            // Prefab yükle
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ToonSoldiers_WW2_demo/ToonSoldier_WW2_demo_Prefab.prefab");
            if (prefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "YardimciKarakter";
                instance.transform.position = new Vector3(2.5f, 0f, 2.0f);
                instance.transform.rotation = Quaternion.identity;

                // KarakterHareket varsa kaldır (hareket etmesin)
                KarakterHareket kh = instance.GetComponent<KarakterHareket>();
                if (kh != null) Object.DestroyImmediate(kh);

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

                // Animator Controller
                RuntimeAnimatorController animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/ToonSoldiers_WW2_demo/KarakterAnimator.controller");
                if (animCtrl != null)
                {
                    Animator anim = instance.GetComponent<Animator>();
                    if (anim != null) anim.runtimeAnimatorController = animCtrl;
                }

                Undo.RegisterCreatedObjectUndo(instance, "Yardimci Karakter Olusturuldu");
                EditorUtility.SetDirty(instance);
            }
        }
        else
        {
            KarakterHareket oyuncu = Object.FindAnyObjectByType<KarakterHareket>();
            if (oyuncu != null)
            {
                yk.hedef = oyuncu.transform;
            }
            yk.donusModu = YardimciKarakter.DonusModu.OyuncuyaBak;
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
