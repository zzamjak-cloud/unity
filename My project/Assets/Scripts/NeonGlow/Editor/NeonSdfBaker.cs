using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CAT.Effects.EditorTools
{
    /// <summary>마스크에서 네온 형태로 인식할 채널.</summary>
    public enum NeonSdfChannel
    {
        Luminance,
        Alpha,
        Red,
        Green,
        Blue
    }

    /// <summary>SDF 굽기 설정.</summary>
    [Serializable]
    public struct NeonSdfBakeSettings
    {
        public NeonSdfChannel channel;
        public bool invert;
        [Range(0f, 1f)] public float threshold;
        public int padding;          // 글로우가 퍼질 여유 픽셀. 인코딩의 정규화 단위이기도 하다.
        public int downscale;        // 1 / 2 / 4. 거리장은 축소에 강해서 용량을 크게 줄일 수 있다.
        public bool subpixel;        // 안티앨리어싱된 마스크의 경계를 부드럽게 보정
        public bool importAsSprite;
        public float pixelsPerUnit;
        public bool generateMipmaps;   // UI 전용이면 꺼서 메모리 25% 절약

        public static NeonSdfBakeSettings Default => new NeonSdfBakeSettings
        {
            channel = NeonSdfChannel.Luminance,
            invert = false,
            threshold = 0.5f,
            padding = 32,
            downscale = 1,
            subpixel = true,
            importAsSprite = true,
            pixelsPerUnit = 100f,
            generateMipmaps = true
        };

        /// <summary>모바일 프리셋. 여유와 해상도를 줄이고 밉맵을 끈다.</summary>
        public static NeonSdfBakeSettings Mobile
        {
            get
            {
                var s = Default;
                s.padding = 16;       // 투명 쿼드 면적 1.56배 -> 1.27배
                s.downscale = 2;
                s.generateMipmaps = false;
                return s;
            }
        }
    }

    /// <summary>
    /// 흑백 마스크 이미지를 부호 있는 거리장(SDF) 텍스처로 굽는다.
    ///
    /// 인코딩 규약 (R 채널):
    ///   0.5 = 마스크 경계
    ///   1.0 = 경계에서 padding 픽셀만큼 안쪽
    ///   0.0 = 경계에서 padding 픽셀만큼 바깥
    /// 출력 텍스처는 사방으로 padding 만큼 확장되므로 글로우가 쿼드 경계에서 잘리지 않는다.
    ///
    /// 거리 변환은 8SSEDT(8-point Signed Sequential Euclidean Distance Transform)를 쓴다.
    /// 이미지 크기에 선형 비례하는 2패스 알고리즘이라 4K 마스크도 1초 안에 끝난다.
    /// </summary>
    public static class NeonSdfBaker
    {
        private const int Far = 20000; // 거리 제곱이 int 범위를 넘지 않는 충분히 큰 값

        private static readonly string[] MobilePlatforms = { "Android", "iPhone" };

        private struct Cell
        {
            public int dx;
            public int dy;
            public int DistSq => dx * dx + dy * dy;
        }

        private static readonly Cell Seed = new Cell { dx = 0, dy = 0 };
        private static readonly Cell Empty = new Cell { dx = Far, dy = Far };

        #region 메뉴
        [MenuItem("Assets/CAT/Bake Neon SDF", false, 1100)]
        private static void BakeSelected()
        {
            var tex = Selection.activeObject as Texture2D;
            if (tex == null) return;

            string path = Bake(tex, NeonSdfBakeSettings.Default);
            if (string.IsNullOrEmpty(path)) return;

            var baked = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Selection.activeObject = baked;
            EditorGUIUtility.PingObject(baked);
            Debug.Log($"[NeonSdfBaker] SDF 생성 완료: {path}", baked);
        }

        [MenuItem("Assets/CAT/Bake Neon SDF", true)]
        private static bool BakeSelectedValidate()
        {
            return Selection.activeObject is Texture2D;
        }
        #endregion

        /// <summary>
        /// 마스크를 SDF 로 굽고 소스 옆에 PNG 로 저장한다. 저장된 에셋 경로를 반환한다.
        /// </summary>
        public static string Bake(Texture2D source, NeonSdfBakeSettings settings)
        {
            if (source == null)
            {
                Debug.LogError("[NeonSdfBaker] 소스 텍스처가 없습니다.");
                return null;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("[NeonSdfBaker] 프로젝트 안의 텍스처 에셋만 구울 수 있습니다.");
                return null;
            }

            int srcW, srcH;
            Color32[] pixels = ReadSourcePixels(source, sourcePath, out srcW, out srcH);
            if (pixels == null)
            {
                Debug.LogError($"[NeonSdfBaker] '{sourcePath}' 의 픽셀을 읽지 못했습니다.");
                return null;
            }

            int pad = Mathf.Max(1, settings.padding);
            int down = Mathf.Max(1, settings.downscale);

            try
            {
                EditorUtility.DisplayProgressBar("Neon SDF", "커버리지 계산 중...", 0.1f);
                float[] coverage = BuildCoverage(pixels, srcW, srcH, settings);

                EditorUtility.DisplayProgressBar("Neon SDF", "거리장 계산 중...", 0.35f);
                int outW = srcW + pad * 2;
                int outH = srcH + pad * 2;
                float[] signed = BuildSignedDistance(coverage, srcW, srcH, outW, outH, pad, settings.subpixel);

                EditorUtility.DisplayProgressBar("Neon SDF", "인코딩 중...", 0.75f);
                int finalW = Mathf.Max(1, outW / down);
                int finalH = Mathf.Max(1, outH / down);
                if (down > 1) signed = Downsample(signed, outW, outH, finalW, finalH, down);

                Color32[] encoded = Encode(signed, pad);

                EditorUtility.DisplayProgressBar("Neon SDF", "저장 중...", 0.9f);
                string outPath = BuildOutputPath(sourcePath);
                WritePng(outPath, finalW, finalH, encoded);
                ConfigureImporter(outPath, Mathf.Max(finalW, finalH), pad, settings);

                return outPath;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #region 소스 읽기
        /// <summary>
        /// 임포트 설정(압축, Read/Write 비활성, 최대 크기 축소)에 영향받지 않도록
        /// 원본 파일 바이트를 직접 디코딩한다. 실패하면 GPU 블릿으로 대체한다.
        /// </summary>
        private static Color32[] ReadSourcePixels(Texture2D source, string sourcePath, out int width, out int height)
        {
            width = 0;
            height = 0;

            string full = Path.GetFullPath(sourcePath);
            if (File.Exists(full))
            {
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                if (decoded.LoadImage(File.ReadAllBytes(full), false))
                {
                    width = decoded.width;
                    height = decoded.height;
                    Color32[] px = decoded.GetPixels32();
                    UnityEngine.Object.DestroyImmediate(decoded);
                    return px;
                }
                UnityEngine.Object.DestroyImmediate(decoded);
            }

            // PSD/TGA 등 LoadImage 가 못 읽는 포맷 대비
            width = source.width;
            height = source.height;
            RenderTexture prev = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var tmp = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            tmp.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color32[] result = tmp.GetPixels32();
            UnityEngine.Object.DestroyImmediate(tmp);
            return result;
        }

        /// <summary>선택한 채널에서 0~1 커버리지를 뽑는다.</summary>
        private static float[] BuildCoverage(Color32[] pixels, int w, int h, NeonSdfBakeSettings settings)
        {
            var coverage = new float[w * h];
            const float Inv255 = 1f / 255f;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 c = pixels[i];
                float v;
                switch (settings.channel)
                {
                    case NeonSdfChannel.Alpha: v = c.a * Inv255; break;
                    case NeonSdfChannel.Red: v = c.r * Inv255; break;
                    case NeonSdfChannel.Green: v = c.g * Inv255; break;
                    case NeonSdfChannel.Blue: v = c.b * Inv255; break;
                    default:
                        v = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) * Inv255;
                        break;
                }

                if (settings.invert) v = 1f - v;

                // threshold 를 0.5 로 재매핑해서 이후 서브픽셀 보정이 일관되게 동작하도록 한다.
                float t = Mathf.Clamp(settings.threshold, 0.001f, 0.999f);
                v = v < t ? Mathf.InverseLerp(0f, t, v) * 0.5f
                          : 0.5f + Mathf.InverseLerp(t, 1f, v) * 0.5f;

                coverage[i] = v;
            }

            return coverage;
        }
        #endregion

        #region 거리장
        private static float[] BuildSignedDistance(float[] coverage, int srcW, int srcH, int outW, int outH, int pad, bool subpixel)
        {
            int count = outW * outH;
            var inside = new Cell[count];   // 내부 픽셀을 시드로 → 외부 거리 측정
            var outside = new Cell[count];  // 외부 픽셀을 시드로 → 내부 거리 측정

            for (int y = 0; y < outH; y++)
            {
                int sy = y - pad;
                for (int x = 0; x < outW; x++)
                {
                    int sx = x - pad;
                    int idx = y * outW + x;

                    // 패딩 영역은 항상 바깥으로 취급한다.
                    bool isInside = sx >= 0 && sy >= 0 && sx < srcW && sy < srcH
                                    && coverage[sy * srcW + sx] >= 0.5f;

                    inside[idx] = isInside ? Seed : Empty;
                    outside[idx] = isInside ? Empty : Seed;
                }
            }

            Propagate(inside, outW, outH);
            Propagate(outside, outW, outH);

            var signed = new float[count];
            for (int y = 0; y < outH; y++)
            {
                int sy = y - pad;
                for (int x = 0; x < outW; x++)
                {
                    int sx = x - pad;
                    int idx = y * outW + x;

                    // 양수 = 바깥, 음수 = 안쪽
                    float d = Mathf.Sqrt(inside[idx].DistSq) - Mathf.Sqrt(outside[idx].DistSq);

                    if (subpixel && Mathf.Abs(d) < 1.5f
                        && sx >= 0 && sy >= 0 && sx < srcW && sy < srcH)
                    {
                        // 경계 픽셀의 부분 커버리지만큼 거리를 밀어 계단을 없앤다.
                        d += 0.5f - coverage[sy * srcW + sx];
                    }

                    signed[idx] = d;
                }
            }

            return signed;
        }

        /// <summary>8SSEDT 전파. 전방/후방 2패스로 최근접 시드 오프셋을 퍼뜨린다.</summary>
        private static void Propagate(Cell[] grid, int w, int h)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Cell p = grid[y * w + x];
                    Compare(grid, w, h, ref p, x, y, -1, 0);
                    Compare(grid, w, h, ref p, x, y, 0, -1);
                    Compare(grid, w, h, ref p, x, y, -1, -1);
                    Compare(grid, w, h, ref p, x, y, 1, -1);
                    grid[y * w + x] = p;
                }
                for (int x = w - 1; x >= 0; x--)
                {
                    Cell p = grid[y * w + x];
                    Compare(grid, w, h, ref p, x, y, 1, 0);
                    grid[y * w + x] = p;
                }
            }

            for (int y = h - 1; y >= 0; y--)
            {
                for (int x = w - 1; x >= 0; x--)
                {
                    Cell p = grid[y * w + x];
                    Compare(grid, w, h, ref p, x, y, 1, 0);
                    Compare(grid, w, h, ref p, x, y, 0, 1);
                    Compare(grid, w, h, ref p, x, y, -1, 1);
                    Compare(grid, w, h, ref p, x, y, 1, 1);
                    grid[y * w + x] = p;
                }
                for (int x = 0; x < w; x++)
                {
                    Cell p = grid[y * w + x];
                    Compare(grid, w, h, ref p, x, y, -1, 0);
                    grid[y * w + x] = p;
                }
            }
        }

        private static void Compare(Cell[] grid, int w, int h, ref Cell p, int x, int y, int ox, int oy)
        {
            int nx = x + ox;
            int ny = y + oy;
            if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;

            Cell other = grid[ny * w + nx];
            other.dx += ox;
            other.dy += oy;
            if (other.DistSq < p.DistSq) p = other;
        }
        #endregion

        #region 출력
        /// <summary>거리장은 선형이라 박스 평균으로 축소해도 형태가 보존된다.</summary>
        private static float[] Downsample(float[] src, int srcW, int srcH, int dstW, int dstH, int factor)
        {
            var dst = new float[dstW * dstH];
            float invSamples = 1f / (factor * factor);

            for (int y = 0; y < dstH; y++)
            {
                for (int x = 0; x < dstW; x++)
                {
                    float sum = 0f;
                    for (int oy = 0; oy < factor; oy++)
                    {
                        int sy = Mathf.Min(y * factor + oy, srcH - 1);
                        for (int ox = 0; ox < factor; ox++)
                        {
                            int sx = Mathf.Min(x * factor + ox, srcW - 1);
                            sum += src[sy * srcW + sx];
                        }
                    }
                    dst[y * dstW + x] = sum * invSamples;
                }
            }

            return dst;
        }

        private static Color32[] Encode(float[] signed, int spread)
        {
            var result = new Color32[signed.Length];
            float invRange = 1f / (2f * spread);

            for (int i = 0; i < signed.Length; i++)
            {
                // 안쪽일수록 밝게. 원본 흰색 마스크와 같은 방향으로 보여 확인하기 쉽다.
                float e = Mathf.Clamp01(0.5f - signed[i] * invRange);
                byte b = (byte)Mathf.RoundToInt(e * 255f);
                result[i] = new Color32(b, b, b, 255);
            }

            return result;
        }

        private static string BuildOutputPath(string sourcePath)
        {
            string dir = Path.GetDirectoryName(sourcePath).Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(sourcePath);
            if (name.EndsWith("_NeonSDF", StringComparison.OrdinalIgnoreCase))
                return $"{dir}/{name}.png";
            return $"{dir}/{name}_NeonSDF.png";
        }

        private static void WritePng(string assetPath, int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(assetPath), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureImporter(string assetPath, int maxSide, int spread, NeonSdfBakeSettings settings)
        {
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;

            if (settings.importAsSprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = Mathf.Max(0.01f, settings.pixelsPerUnit);
            }
            else
            {
                ti.textureType = TextureImporterType.Default;
            }

            // 거리장은 색이 아니라 데이터다. 감마 변환이 끼면 경계 위치가 틀어진다.
            ti.sRGBTexture = false;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.alphaIsTransparency = false;
            ti.mipmapEnabled = settings.generateMipmaps;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.userData = $"neon_spread={spread}";

            // 셰이더는 R 채널만 읽는다. RGB 로 두면 메모리를 3배 쓴다.
            // 블록 압축은 거리장 그라데이션에 밴딩을 만들므로 R8 무압축이 정답이다.
            int size = RoundUpTextureSize(maxSide);
            var def = ti.GetDefaultPlatformTextureSettings();
            def.format = TextureImporterFormat.R8;
            def.textureCompression = TextureImporterCompression.Uncompressed;
            def.maxTextureSize = size;
            ti.SetPlatformTextureSettings(def);

            foreach (string platform in MobilePlatforms)
            {
                var ps = ti.GetPlatformTextureSettings(platform);
                ps.overridden = true;
                ps.format = TextureImporterFormat.R8;
                ps.textureCompression = TextureImporterCompression.Uncompressed;
                ps.maxTextureSize = size;
                ti.SetPlatformTextureSettings(ps);
            }

            EditorUtility.SetDirty(ti);
            ti.SaveAndReimport();
        }

        private static int RoundUpTextureSize(int size)
        {
            int[] steps = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };
            foreach (int s in steps)
                if (size <= s) return s;
            return 16384;
        }
        #endregion

        /// <summary>구운 SDF 에셋에 기록된 padding(spread) 값을 읽는다. 없으면 -1.</summary>
        public static int ReadSpread(Texture texture)
        {
            if (texture == null) return -1;
            string path = AssetDatabase.GetAssetPath(texture);
            if (string.IsNullOrEmpty(path)) return -1;

            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null || string.IsNullOrEmpty(ti.userData)) return -1;

            const string key = "neon_spread=";
            int at = ti.userData.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return -1;

            int value;
            return int.TryParse(ti.userData.Substring(at + key.Length), out value) ? value : -1;
        }
    }
}
