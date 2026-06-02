using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Features.Utils.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Features.Utils.Editor
{
    public class TexturePackerPostProcessor : AssetPostprocessor
    {
        // サフィックス定義（小文字で比較する）
        private const string SuffixRoughness = "_roughness";
        private const string SuffixMetallic = "_metalness";
        private const string SuffixAo = "_ambientocclusion";
        private const string SuffixOutput = "_mask";

        /// <summary>
        ///     インポート後処理、3ファイル揃ったらパッキング実行
        /// </summary>
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                var baseName = GetBaseName(path);
                if (baseName == null) continue;

                var dir = Path.GetDirectoryName(path);
                TryPackTextures(dir, baseName);
            }
        }

        /// <summary>
        ///     インポート前処理
        /// </summary>
        private void OnPreprocessTexture()
        {
            var baseName = GetBaseName(assetPath);
            if (baseName == null) return; // 関係ないテクスチャ

            var importer = (TextureImporter)assetImporter;
            importer.isReadable = true;
            importer.mipmapEnabled = false; // ソース素材はミップ不要
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = false; // Roughness/Metallic/AO はリニア
        }

        /// <summary>
        ///     パッキング処理
        /// </summary>
        /// <param name="dir">ディレクトリパス</param>
        /// <param name="baseName">テクスチャのベースネーム</param>
        private static void TryPackTextures(string dir, string baseName)
        {
            // 各ソースファイルを探す（拡張子は問わない）
            var roughnessPath = FindFile(dir, baseName + SuffixRoughness);
            var metallicPath = FindFile(dir, baseName + SuffixMetallic);
            var aoPath = FindFile(dir, baseName + SuffixAo);

            if (roughnessPath == null && metallicPath == null && aoPath == null)
                return; // 一つもなければスキップ

            Debug.Log($"[TexturePacker] {baseName} パッキング開始...");

            var roughnessTex = LoadLinearTexture(roughnessPath);
            var metallicTex = LoadLinearTexture(metallicPath);
            var aoTex = LoadLinearTexture(aoPath);
            var textures = new (Uniforms Uniforms, string Path, Texture2D Tex)[]
            {
                (Uniforms._Roughness, roughnessPath, roughnessTex),
                (Uniforms._Metallic, metallicPath, metallicTex),
                (Uniforms._Ao, aoPath, aoTex)
            };

            if (textures.Any(t => t.Path != null && t.Tex == null))
            {
                Debug.LogWarning($"[TexturePacker] テクスチャの読み込みに失敗しました: {baseName}");
                return;
            }

            // マテリアル作成
            var mat = new MaterialWrapper<Uniforms>(
                CoreUtils.CreateEngineMaterial("Hidden/TexturePacking")
            );
            foreach (var v in textures.Where(t => t.Path != null))
                mat.SetTexture(v.Uniforms, v.Tex);

            // 出力テクスチャ生成
            var width = roughnessTex.width;
            var height = roughnessTex.height;
            var output = RenderTexture.GetTemporary(
                width, height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear
            );

            // 描画コマンドを作成して実行
            var cmd = CommandBufferPool.Get("TexturePacker");
            cmd.SetRenderTarget(output);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.DrawProcedural(
                Matrix4x4.identity, mat.Material, 0,
                MeshTopology.Triangles, 3, 1
            );
            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            // PNG として保存
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            var prev = RenderTexture.active;
            RenderTexture.active = output;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(output);

            var outputPath = Path.Combine(dir, baseName + SuffixOutput + ".png");
            File.WriteAllBytes(outputPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            // AssetDatabase に登録してインポート設定を適用
            var assetOutputPath = outputPath.Replace('\\', '/');
            AssetDatabase.ImportAsset(assetOutputPath, ImportAssetOptions.ForceUpdate);
            ConfigureMaskTexture(assetOutputPath);

            Debug.Log($"[TexturePacker] パック完了: {assetOutputPath}");
        }

        // -------------------------------------------------------------------
        // 出力テクスチャのインポート設定（Mask Map 向け）
        // -------------------------------------------------------------------
        private static void ConfigureMaskTexture(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false; // リニア空間
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.isReadable = false;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            // プラットフォーム共通設定
            var settings = importer.GetDefaultPlatformTextureSettings();
            settings.format = TextureImporterFormat.RGBA32;
            settings.overridden = true;
            importer.SetPlatformTextureSettings(settings);

            importer.SaveAndReimport();
        }

        // -------------------------------------------------------------------
        // ユーティリティ
        // -------------------------------------------------------------------

        /// <summary>
        ///     パスが Roughness/Metallic/AO のいずれかなら BaseName を返す。それ以外は null。
        /// </summary>
        private static string GetBaseName(string path)
        {
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(path).ToLower();

            return (from suffix in new[] { SuffixRoughness, SuffixMetallic, SuffixAo }
                where fileNameWithoutExt.EndsWith(suffix)
                let original = Path.GetFileNameWithoutExtension(path)
                select original[..^suffix.Length]).FirstOrDefault();
        }

        /// <summary>
        ///     拡張子を問わず dir 内の baseName+suffix ファイルを探す。
        /// </summary>
        private static string FindFile(string dir, string fileNameWithoutExt)
        {
            // Assets 以下の相対パスで検索
            var guids = AssetDatabase.FindAssets(
                fileNameWithoutExt,
                new[] { dir.Replace('\\', '/') }
            );

            return guids.Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p =>
                string.Equals(
                    Path.GetFileNameWithoutExtension(p), fileNameWithoutExt,
                    StringComparison.CurrentCultureIgnoreCase
                )
            );
        }

        /// <summary>
        ///     アセットパスからリニアテクスチャとして Texture2D を読み込む。
        /// </summary>
        private static Texture2D LoadLinearTexture(string assetPath)
        {
            // AssetDatabase 経由でロード（isReadable が有効になっているはず）
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null) return null;

            // GetPixels が使えるようにコピー
            var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, true);
            copy.SetPixels(tex.GetPixels());
            copy.Apply();
            return copy;
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private enum Uniforms
        {
            _Roughness,
            _Metallic,
            _Ao
        }
    }
}