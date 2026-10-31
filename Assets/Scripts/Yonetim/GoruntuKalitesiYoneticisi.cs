using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Yazlab.Yonetim
{
    /// <summary>
    /// Oyun içi 3D çözünürlük bulanıklığını, yüksek DPI ölçekleme sorunlarını,
    /// Unity Editor GameView 1.25x yakınlaştırma / düşük çözünürlük (Low Resolution Aspect Ratios) bulanıklığını
    /// ve OnGUI yazı keskinliğini otomatik olarak düzelten görüntü kalitesi yöneticisi.
    /// </summary>
    public static class GoruntuKalitesiYoneticisi
    {
        private static Font _keskinFont;
        private static GUIStyle _golgeStil;

        /// <summary>
        /// Küçük pencerelerde (ör. Unity Editor GameView) yazıların küçülüp bulanıklaşmasını engellemek için
        /// asla 1.0x'in altına düşmeyen keskin UI ölçek çarpanı.
        /// </summary>
        public static float KeskinOlcek
        {
            get
            {
                return Mathf.Clamp(Screen.height / 750f, 1.0f, 1.35f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void SahneOncesiCozunurlukAyarla()
        {
            YuksekCozunurlukVeKeskinligiUygula();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void YuksekCozunurlukVeKeskinligiUygula()
        {
            try
            {
                // 1. Tam Çözünürlüklü Dokular ve 4x MSAA / Anisotropic Filtreleme
                QualitySettings.globalTextureMipmapLimit = 0;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
                QualitySettings.antiAliasing = 4;
                QualitySettings.resolutionScalingFixedDPIFactor = 1.0f;

                // Dinamik çözünürlük tamponlarını %100 tam boyuta kilitle
                ScalableBufferManager.ResizeBuffers(1.0f, 1.0f);

                // 2. Standalone / Tam Ekran modunda monitörün doğal (native) piksel çözünürlüğünü zorla
#if !UNITY_EDITOR
                int hedefGenislik = Display.main != null ? Display.main.systemWidth : Screen.currentResolution.width;
                int hedefYukseklik = Display.main != null ? Display.main.systemHeight : Screen.currentResolution.height;
                if (hedefGenislik >= 1280 && hedefYukseklik >= 720)
                {
                    Screen.SetResolution(hedefGenislik, hedefYukseklik, Screen.fullScreenMode);
                }
#endif

                // 3. URP Render Pipeline Varlıklarında (PC_RPAsset / Mobile_RPAsset) Süper Örnekleme (1.25x) ve MSAA 4x
                UygulaUrpVarlikAyarlari(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
                UygulaUrpVarlikAyarlari(QualitySettings.renderPipeline as UniversalRenderPipelineAsset);

                // 4. Sahnedeki Tüm Kameralarda Dinamik Çözünürlük Bulanıklığını Kapat ve SMAA + MSAA Aç
                Camera[] kameralar = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
                foreach (Camera cam in kameralar)
                {
                    if (cam == null) continue;
                    cam.allowDynamicResolution = false;
                    cam.allowMSAA = true;

                    UniversalAdditionalCameraData urpCam = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (urpCam != null)
                    {
                        urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                        urpCam.antialiasingQuality = AntialiasingQuality.High;
                        urpCam.allowHDROutput = true;
                    }
                }

                // 5. Unity Editor GameView penceresindeki 1.25x bulanık yakınlaştırmayı ve Low-Res DPI modunu sıfırla
#if UNITY_EDITOR
                EditorGameViewBulanikliginiDuzelt();
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GoruntuKalitesiYoneticisi] Görüntü keskinleştirme uyarısı: {e.Message}");
            }
        }

        private static void UygulaUrpVarlikAyarlari(UniversalRenderPipelineAsset urp)
        {
            if (urp == null) return;
            urp.renderScale = 1.25f; // %125 Süper Örnekleme (Supersampling) ile jilet gibi net görüntü
            urp.msaaSampleCount = 4;
            urp.upscalingFilter = UpscalingFilterSelection.Linear;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorBaslangicindaDuzelt()
        {
            UnityEditor.EditorApplication.delayCall += EditorGameViewBulanikliginiDuzelt;
            UnityEditor.EditorApplication.playModeStateChanged += _ => EditorGameViewBulanikliginiDuzelt();
        }

        /// <summary>
        /// Unity Editor'de Windows %125/%150 ölçekleme açıkken GameView'un düşük çözünürlükte render alıp
        /// 1.25x yakınlaştırma (m_Scale: 1.25) yaparak tüm yazıları ve oyunu bulanıklaştırmasını Reflection ile engeller.
        /// </summary>
        public static void EditorGameViewBulanikliginiDuzelt()
        {
            try
            {
                Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null) return;

                UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

                FieldInfo lowResField = gameViewType.GetField("m_LowResolutionForAspectRatios", flags);
                FieldInfo defaultScaleField = gameViewType.GetField("m_defaultScale", flags);
                FieldInfo zoomAreaField = gameViewType.GetField("m_ZoomArea", flags);
                MethodInfo updateZoomMethod = gameViewType.GetMethod("UpdateZoomAreaAndParent", flags);
                MethodInfo repaintMethod = gameViewType.GetMethod("Repaint", flags);

                foreach (UnityEngine.Object gv in gameViews)
                {
                    if (gv == null) continue;

                    // 1. Low Resolution Aspect Ratios seçeneğini tüm görünüm oranları için KAPAT (Tam High-DPI 1:1 piksel!)
                    if (lowResField != null)
                    {
                        object lowResVal = lowResField.GetValue(gv);
                        if (lowResVal is IList list)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                if (list[i] is bool) list[i] = false;
                            }
                        }
                    }

                    // 2. Varsayılan ölçeği 1.0x yap
                    if (defaultScaleField != null)
                    {
                        defaultScaleField.SetValue(gv, 1.0f);
                    }

                    // 3. ZoomArea içindeki m_Scale değerini {1, 1} (1.0x doğal piksel) yap
                    if (zoomAreaField != null)
                    {
                        object zoomArea = zoomAreaField.GetValue(gv);
                        if (zoomArea != null)
                        {
                            FieldInfo scaleField = zoomArea.GetType().GetField("m_Scale", flags);
                            if (scaleField != null)
                            {
                                scaleField.SetValue(zoomArea, Vector2.one);
                            }
                        }
                    }

                    updateZoomMethod?.Invoke(gv, null);
                    repaintMethod?.Invoke(gv, null);
                }
            }
            catch
            {
                // Reflection hatası olursa sessizce geç
            }
        }
#endif

        /// <summary>
        /// İşletim sisteminden yüksek çözünürlüklü dinamik TrueType font (Segoe UI / Arial Bold) yükler.
        /// </summary>
        public static Font KeskinFontAl()
        {
            if (_keskinFont == null)
            {
                try
                {
                    _keskinFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI Semibold", "Segoe UI", "Arial", "Tahoma" }, 16);
                }
                catch { }
            }
            return _keskinFont;
        }

        /// <summary>
        /// Verilen GUIStyle nesnesine işletim sisteminin keskin TrueType fontunu atar ve tam sayı boyuta yuvarlar.
        /// </summary>
        public static void StiliKeskinlestir(GUIStyle stil, int fontBoyutu)
        {
            if (stil == null) return;
            Font f = KeskinFontAl();
            if (f != null) stil.font = f;
            stil.fontSize = Mathf.Max(12, fontBoyutu);
        }

        /// <summary>
        /// OnGUI yazılarını arka plan üzerinde çok daha net ve okunaklı kılmak için 1px koyu kontrast gölgeyle çizer.
        /// </summary>
        public static void KeskinEtiketCiz(Rect rect, string metin, GUIStyle stil)
        {
            if (string.IsNullOrEmpty(metin) || stil == null) return;

            if (_golgeStil == null)
            {
                _golgeStil = new GUIStyle();
            }

            _golgeStil.font = stil.font;
            _golgeStil.fontSize = stil.fontSize;
            _golgeStil.fontStyle = stil.fontStyle;
            _golgeStil.alignment = stil.alignment;
            _golgeStil.wordWrap = stil.wordWrap;
            _golgeStil.padding = stil.padding;
            _golgeStil.normal.textColor = new Color(0f, 0f, 0f, 0.92f);

            Color eski = GUI.color;
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), metin, _golgeStil);
            GUI.color = eski;
            GUI.Label(rect, metin, stil);
        }
    }
}
