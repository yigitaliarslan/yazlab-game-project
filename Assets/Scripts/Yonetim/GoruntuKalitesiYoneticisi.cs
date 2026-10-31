using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Yazlab.Yonetim
{
    /// <summary>
    /// Oyun içi 3D görüntü netliğini, 1:1 doğal piksel eşleşmesini (WorldToViewportPoint -> OnGUI)
    /// ve OnGUI yazı keskinliğini / hizalamasını yöneten görüntü kalitesi yöneticisi.
    /// </summary>
    public static class GoruntuKalitesiYoneticisi
    {
        private static Font _keskinFont;
        private static GUIStyle _golgeStil;

        /// <summary>
        /// Ekranın hem genişlik hem yüksekliğine duyarlı, yazıların kutulardan taşmasını veya
        /// küçük pencerelerde devasa kalmasını önleyen dengeli UI ölçek çarpanı.
        /// </summary>
        public static float KeskinOlcek
        {
            get
            {
                float wOlcek = Screen.width / 1280f;
                float hOlcek = Screen.height / 720f;
                return Mathf.Clamp(Mathf.Min(wOlcek, hOlcek), 0.85f, 1.25f);
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

                // 2. URP Render Pipeline Varlıklarında RenderScale = 1.0f (1:1 Koordinat Eşleşmesi!)
                // RenderScale 1.0f olduğunda Camera.WorldToScreenPoint/ViewportPoint ile OnGUI koordinatları birebir örtüşür,
                // hiçbir yazı veya can barı ekranda kaymaz!
                UygulaUrpVarlikAyarlari(GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset);
                UygulaUrpVarlikAyarlari(QualitySettings.renderPipeline as UniversalRenderPipelineAsset);

                // 3. Sahnedeki Tüm Kameralarda Dinamik Çözünürlüğü Kapat ve SMAA + MSAA Aç
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

#if UNITY_EDITOR
                EditorGameViewKoordinatlariniDuzelt();
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GoruntuKalitesiYoneticisi] Görüntü ayar uyarısı: {e.Message}");
            }
        }

        private static void UygulaUrpVarlikAyarlari(UniversalRenderPipelineAsset urp)
        {
            if (urp == null) return;
            urp.renderScale = 1.0f; // 1:1 doğal piksel ölçeği (UI kaymasını %100 engeller)
            urp.msaaSampleCount = 4;
            urp.upscalingFilter = UpscalingFilterSelection.Linear;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void EditorBaslangicindaDuzelt()
        {
            UnityEditor.EditorApplication.delayCall += EditorGameViewKoordinatlariniDuzelt;
            UnityEditor.EditorApplication.playModeStateChanged += _ => EditorGameViewKoordinatlariniDuzelt();
        }

        /// <summary>
        /// Unity Editor'de Windows DPI ölçeklemesi (%125/%150) aktifken OnGUI koordinatlarıyla
        /// Screen.width/Screen.height değerlerinin birebir (1:1) örtüşmesi ve yazıların kaymaması için
        /// GameView'un standart koordinat modunu ve pencere içi sığdırma (fit) ölçeğini korur.
        /// </summary>
        public static void EditorGameViewKoordinatlariniDuzelt()
        {
            try
            {
                Type gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
                if (gameViewType == null) return;

                UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

                FieldInfo lowResField = gameViewType.GetField("m_LowResolutionForAspectRatios", flags);
                MethodInfo updateZoomMethod = gameViewType.GetMethod("UpdateZoomAreaAndParent", flags);
                MethodInfo snapZoomMethod = gameViewType.GetMethod("SnapZoom", flags, null, new[] { typeof(float) }, null);
                PropertyInfo minScaleProp = gameViewType.GetProperty("minScale", flags);
                MethodInfo repaintMethod = gameViewType.GetMethod("Repaint", flags);

                foreach (UnityEngine.Object gv in gameViews)
                {
                    if (gv == null) continue;

                    // Windows High-DPI ekranlarda OnGUI (logical points) ile Screen.width/height değerlerinin
                    // kaymaması için standart aspect ratio modunu (true) geri yükle
                    if (lowResField != null)
                    {
                        object lowResVal = lowResField.GetValue(gv);
                        if (lowResVal is IList list)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                if (list[i] is bool) list[i] = true;
                            }
                        }
                    }

                    updateZoomMethod?.Invoke(gv, null);

                    if (snapZoomMethod != null && minScaleProp != null)
                    {
                        float minScale = (float)minScaleProp.GetValue(gv);
                        snapZoomMethod.Invoke(gv, new object[] { minScale });
                    }

                    updateZoomMethod?.Invoke(gv, null);
                    repaintMethod?.Invoke(gv, null);
                }
            }
            catch
            {
                // Reflection desteklenmiyorsa sessizce geç
            }
        }
#endif

        /// <summary>
        /// 3D dünya koordinatını (ör. düşman/taret can barı, etkileşim ipucu) URP renderScale veya
        /// DPI farklarından etkilenmeden doğrudan OnGUI ekran koordinatına (sol-üst orijinli) çevirir.
        /// </summary>
        public static bool DunyaNoktasiniEkranaCevir(Camera cam, Vector3 dunyaKonumu, out Vector2 guiKonumu)
        {
            guiKonumu = Vector2.zero;
            if (cam == null) return false;

            Vector3 vp = cam.WorldToViewportPoint(dunyaKonumu);
            if (vp.z <= 0.1f) return false;

            guiKonumu = new Vector2(vp.x * Screen.width, (1f - vp.y) * Screen.height);
            return true;
        }

        /// <summary>
        /// İşletim sisteminden yüksek çözünürlüklü dinamik TrueType font (Segoe UI / Arial) yükler.
        /// </summary>
        public static Font KeskinFontAl()
        {
            if (_keskinFont == null)
            {
                try
                {
                    _keskinFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma" }, 16);
                }
                catch { }
            }
            return _keskinFont;
        }

        /// <summary>
        /// Verilen GUIStyle nesnesine işletim sisteminin keskin TrueType fontunu atar,
        /// taşma/kesilme (clipping) sorunlarını önler ve tam sayı boyuta yuvarlar.
        /// </summary>
        public static void StiliKeskinlestir(GUIStyle stil, int fontBoyutu)
        {
            if (stil == null) return;
            Font f = KeskinFontAl();
            if (f != null) stil.font = f;
            stil.fontSize = Mathf.Max(11, fontBoyutu);
            stil.clipping = TextClipping.Overflow;
        }

        /// <summary>
        /// OnGUI yazılarını arka plan üzerinde çok daha net ve okunaklı kılmak için
        /// stilin tüm hizalama/padding özelliklerini birebir koruyarak 1px koyu kontrast gölgeyle çizer.
        /// </summary>
        public static void KeskinEtiketCiz(Rect rect, string metin, GUIStyle stil)
        {
            if (string.IsNullOrEmpty(metin) || stil == null) return;

            stil.clipping = TextClipping.Overflow;

            if (_golgeStil == null)
            {
                _golgeStil = new GUIStyle(stil);
            }
            else
            {
                _golgeStil.font = stil.font;
                _golgeStil.fontSize = stil.fontSize;
                _golgeStil.fontStyle = stil.fontStyle;
                _golgeStil.alignment = stil.alignment;
                _golgeStil.wordWrap = stil.wordWrap;
                _golgeStil.clipping = TextClipping.Overflow;
                _golgeStil.padding = stil.padding;
                _golgeStil.margin = stil.margin;
                _golgeStil.contentOffset = stil.contentOffset;
            }

            _golgeStil.normal.textColor = new Color(0f, 0f, 0f, 0.90f);

            Color eski = GUI.color;
            GUI.color = Color.white;
            GUI.Label(new Rect(Mathf.Round(rect.x) + 1f, Mathf.Round(rect.y) + 1f, rect.width, rect.height), metin, _golgeStil);
            GUI.color = eski;
            GUI.Label(new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), rect.width, rect.height), metin, stil);
        }
    }
}
