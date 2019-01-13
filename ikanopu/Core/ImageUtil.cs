﻿using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ikanopu.Core {
    /// <summary>
    /// 画像処理系の内容はすべてここに
    /// 返却するMatの破棄は、呼び出し元が責任をもって
    /// </summary>
    static class ImageUtil {
        /// <summary>
        /// 画像切り抜きのプレビューを表示します。デバッグ用
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="src"></param>
        public static void DrawCropPreview(this Mat mat, IEnumerable<(CropOption.Team, Rect)> src) {
            foreach (var (team, rect) in src) {
                Scalar color;
                switch (team) {
                    case CropOption.Team.Alpha:
                        color = Scalar.Red;
                        break;
                    case CropOption.Team.Bravo:
                        color = Scalar.Green;
                        break;
                    case CropOption.Team.Watcher:
                        color = Scalar.Blue;
                        break;
                    default:
                        throw new NotImplementedException();
                }
                mat.Rectangle(rect, color);
            }
        }
        /// <summary>
        /// 画像の配列を手っ取り早く保存
        /// </summary>
        /// <param name="mats"></param>
        /// <param name="identifier"></param>
        public static void SaveAll(this IEnumerable<Mat> mats, string identifier = "") {
            foreach (var (m, i) in mats.Select((x, i) => (x, i))) {
                m.SaveImage($"{identifier}-{i}.bmp");
            }
        }
        /// <summary>
        /// 名前の座標を切り抜いて別のMatにコピー
        /// </summary>
        /// <param name="mat"></param>
        /// <param name="src"></param>
        /// <returns></returns>
        public static IEnumerable<(CropOption.Team, Mat)> CropNames(this Mat mat, IEnumerable<(CropOption.Team, Rect)> src) {
            foreach (var (team, rect) in src) {
                yield return (team, mat.Clone(rect));
            }
        }
    }
}