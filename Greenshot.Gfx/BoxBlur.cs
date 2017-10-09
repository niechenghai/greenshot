﻿using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Greenshot.Gfx.FastBitmap;

namespace Greenshot.Gfx
{
    /// <summary>
    /// Code to apply a box blur
    /// </summary>
    public static class BoxBlur
    {
        /// <summary>
        ///     Apply BoxBlur to the destinationBitmap
        /// </summary>
        /// <param name="destinationBitmap">Bitmap to blur</param>
        /// <param name="range">Must be ODD, if not +1 is used</param>
        public static void ApplyBoxBlur(this Bitmap destinationBitmap, int range)
        {
            // We only need one fastbitmap as we use it as source and target (the reading is done for one line H/V, writing after "parsing" one line H/V)
            using (var fastBitmap = FastBitmapFactory.Create(destinationBitmap))
            {
                fastBitmap.ApplyBoxBlur(range);
            }
        }

        /// <summary>
        ///     Apply BoxBlur to the fastBitmap
        /// </summary>
        /// <param name="fastBitmap">IFastBitmap to blur</param>
        /// <param name="range">Must be ODD!</param>
        public static void ApplyBoxBlur(this IFastBitmap fastBitmap, int range)
        {
            // Range must be odd!
            if ((range & 1) == 0)
            {
                range++;
            }
            if (range <= 1)
            {
                return;
            }
            // Box blurs are frequently used to approximate a Gaussian blur.
            // By the central limit theorem, if applied 3 times on the same image, a box blur approximates the Gaussian kernel to within about 3%, yielding the same result as a quadratic convolution kernel.
            // This might be true, but the GDI+ BlurEffect doesn't look the same, a 2x blur is more simular and we only make 2x Box-Blur.
            // (Might also be a mistake in our blur, but for now it looks great)
            if (fastBitmap.HasAlphaChannel)
            {
                BoxBlurHorizontalAlpha(fastBitmap, range);
                BoxBlurVerticalAlpha(fastBitmap, range);
                BoxBlurHorizontalAlpha(fastBitmap, range);
                BoxBlurVerticalAlpha(fastBitmap, range);
            }
            else
            {
                BoxBlurHorizontal(fastBitmap, range);
                BoxBlurVertical(fastBitmap, range);
                BoxBlurHorizontal(fastBitmap, range);
                BoxBlurVertical(fastBitmap, range);
            }
        }

        /// <summary>
        ///     BoxBlurHorizontal is a private helper method for the BoxBlur
        /// </summary>
        /// <param name="targetFastBitmap">Target BitmapBuffer</param>
        /// <param name="range">Range must be odd!</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BoxBlurHorizontal(IFastBitmap targetFastBitmap, int range)
        {
            if (targetFastBitmap.HasAlphaChannel)
            {
                throw new NotSupportedException("BoxBlurHorizontal should NOT be called for bitmaps with alpha channel");
            }
            var halfRange = range / 2;
            Parallel.For(targetFastBitmap.Top, targetFastBitmap.Bottom, y =>
            {
                unsafe
                {
                    var newColors = stackalloc byte[targetFastBitmap.Width * 4];
                    var tmpColor = stackalloc byte[3];
                    var hits = 0;
                    var r = 0;
                    var g = 0;
                    var b = 0;
                    for (var x = targetFastBitmap.Left - halfRange; x < targetFastBitmap.Right; x++)
                    {
                        var oldPixel = x - halfRange - 1;
                        if (oldPixel >= targetFastBitmap.Left)
                        {
                            targetFastBitmap.GetColorAt(oldPixel, y, tmpColor);
                            r -= tmpColor[FastBitmapBase.ColorIndexR];
                            g -= tmpColor[FastBitmapBase.ColorIndexG];
                            b -= tmpColor[FastBitmapBase.ColorIndexB];
                            hits--;
                        }

                        var newPixel = x + halfRange;
                        if (newPixel < targetFastBitmap.Right)
                        {
                            targetFastBitmap.GetColorAt(newPixel, y, tmpColor);
                            r += tmpColor[FastBitmapBase.ColorIndexR];
                            g += tmpColor[FastBitmapBase.ColorIndexG];
                            b += tmpColor[FastBitmapBase.ColorIndexB];
                            hits++;
                        }

                        if (x < targetFastBitmap.Left)
                        {
                            continue;
                        }
                        var colorPos = (x - targetFastBitmap.Left) << 2;
                        newColors[colorPos++] = (byte) (r / hits);
                        newColors[colorPos++] = (byte)(g / hits);
                        newColors[colorPos] = (byte)(b / hits);
                    }
                    for (var x = targetFastBitmap.Left; x < targetFastBitmap.Right; x++)
                    {
                        var colorPos = (x - targetFastBitmap.Left) << 2;
                        targetFastBitmap.SetColorAt(x, y, newColors, colorPos);
                    }
                }
            });
        }

        /// <summary>
        ///     BoxBlurVertical is a private helper method for the BoxBlur
        /// </summary>
        /// <param name="targetFastBitmap">BitmapBuffer which previously was created with BoxBlurHorizontal</param>
        /// <param name="range">Range must be odd!</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BoxBlurVertical(IFastBitmap targetFastBitmap, int range)
        {
            if (targetFastBitmap.HasAlphaChannel)
            {
                throw new NotSupportedException("BoxBlurVertical should NOT be called for bitmaps with alpha channel");
            }
            var halfRange = range / 2;

            Parallel.For(targetFastBitmap.Left, targetFastBitmap.Right, x =>
            {
                unsafe
                {
                    var newColors = stackalloc byte[targetFastBitmap.Height * 4];
                    var tmpColor = stackalloc byte[3];
                    var hits = 0;
                    var r = 0;
                    var g = 0;
                    var b = 0;
                    for (var y = targetFastBitmap.Top - halfRange; y < targetFastBitmap.Bottom; y++)
                    {
                        var oldPixel = y - halfRange - 1;
                        if (oldPixel >= targetFastBitmap.Top)
                        {
                            targetFastBitmap.GetColorAt(x, oldPixel, tmpColor);
                            r -= tmpColor[FastBitmapBase.ColorIndexR];
                            g -= tmpColor[FastBitmapBase.ColorIndexG];
                            b -= tmpColor[FastBitmapBase.ColorIndexB];
                            hits--;
                        }

                        var newPixel = y + halfRange;
                        if (newPixel < targetFastBitmap.Bottom)
                        {
                            targetFastBitmap.GetColorAt(x, newPixel, tmpColor);
                            r += tmpColor[FastBitmapBase.ColorIndexR];
                            g += tmpColor[FastBitmapBase.ColorIndexG];
                            b += tmpColor[FastBitmapBase.ColorIndexB];
                            hits++;
                        }

                        if (y < targetFastBitmap.Top)
                        {
                            continue;
                        }
                        var colorPos = (y - targetFastBitmap.Top) << 2;
                        newColors[colorPos++] = (byte)(r / hits);
                        newColors[colorPos++] = (byte)(g / hits);
                        newColors[colorPos] = (byte)(b / hits);
                    }

                    for (var y = targetFastBitmap.Top; y < targetFastBitmap.Bottom; y++)
                    {
                        var colorPos = (y - targetFastBitmap.Top) << 2;
                        targetFastBitmap.SetColorAt(x, y, newColors, colorPos);
                    }
                }
            });
        }

        /// <summary>
        ///     BoxBlurHorizontal is a private helper method for the BoxBlur, only for IFastBitmaps with alpha channel
        /// </summary>
        /// <param name="targetFastBitmap">Target BitmapBuffer</param>
        /// <param name="range">Range must be odd!</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BoxBlurHorizontalAlpha(IFastBitmap targetFastBitmap, int range)
        {
            if (!targetFastBitmap.HasAlphaChannel)
            {
                throw new NotSupportedException("BoxBlurHorizontalAlpha should be called for bitmaps with alpha channel");
            }
            var halfRange = range / 2;
            Parallel.For(targetFastBitmap.Top, targetFastBitmap.Bottom, y =>
            {
                unsafe
                {
                    var newColors = stackalloc byte[targetFastBitmap.Height * 4];
                    var tmpColor = stackalloc byte[4];
                    var hits = 0;
                    var a = 0;
                    var r = 0;
                    var g = 0;
                    var b = 0;
                    for (var x = targetFastBitmap.Left - halfRange; x < targetFastBitmap.Right; x++)
                    {
                        var oldPixel = x - halfRange - 1;
                        if (oldPixel >= targetFastBitmap.Left)
                        {
                            targetFastBitmap.GetColorAt(oldPixel, y, tmpColor);
                            a -= tmpColor[FastBitmapBase.ColorIndexA];
                            r -= tmpColor[FastBitmapBase.ColorIndexR];
                            g -= tmpColor[FastBitmapBase.ColorIndexG];
                            b -= tmpColor[FastBitmapBase.ColorIndexB];
                            hits--;
                        }

                        var newPixel = x + halfRange;
                        if (newPixel < targetFastBitmap.Right)
                        {
                            targetFastBitmap.GetColorAt(newPixel, y, tmpColor);
                            a += tmpColor[FastBitmapBase.ColorIndexA];
                            r += tmpColor[FastBitmapBase.ColorIndexR];
                            g += tmpColor[FastBitmapBase.ColorIndexG];
                            b += tmpColor[FastBitmapBase.ColorIndexB];
                            hits++;
                        }

                        if (x < targetFastBitmap.Left)
                        {
                            continue;
                        }
                        var colorPos = (x - targetFastBitmap.Left) << 2;
                        newColors[colorPos++] = (byte)(a / hits);
                        newColors[colorPos++] = (byte)(r / hits);
                        newColors[colorPos++] = (byte)(g / hits);
                        newColors[colorPos] = (byte)(b / hits);
                    }
                    for (var x = targetFastBitmap.Left; x < targetFastBitmap.Right; x++)
                    {
                        var colorPos = (x - targetFastBitmap.Left) << 2;
                        targetFastBitmap.SetColorAt(x, y, newColors, colorPos);
                    }
                }
            });
        }

        /// <summary>
        ///     BoxBlurVertical is a private helper method for the BoxBlur
        /// </summary>
        /// <param name="targetFastBitmap">BitmapBuffer which previously was created with BoxBlurHorizontal</param>
        /// <param name="range">Range must be odd!</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void BoxBlurVerticalAlpha(IFastBitmap targetFastBitmap, int range)
        {
            if (!targetFastBitmap.HasAlphaChannel)
            {
                throw new NotSupportedException("BoxBlurVerticalAlpha should be called for bitmaps with alpha channel");
            }

            var halfRange = range / 2;
            Parallel.For(targetFastBitmap.Left, targetFastBitmap.Right, x =>
            {
                unsafe
                {
                    var newColors = stackalloc byte[targetFastBitmap.Height * 4];
                    var tmpColor = stackalloc byte[4];
                    var hits = 0;
                    var a = 0;
                    var r = 0;
                    var g = 0;
                    var b = 0;
                    for (var y = targetFastBitmap.Top - halfRange; y < targetFastBitmap.Bottom; y++)
                    {
                        var oldPixel = y - halfRange - 1;
                        if (oldPixel >= targetFastBitmap.Top)
                        {
                            targetFastBitmap.GetColorAt(x, oldPixel, tmpColor);
                            a -= tmpColor[FastBitmapBase.ColorIndexA];
                            r -= tmpColor[FastBitmapBase.ColorIndexR];
                            g -= tmpColor[FastBitmapBase.ColorIndexG];
                            b -= tmpColor[FastBitmapBase.ColorIndexB];
                            hits--;
                        }

                        var newPixel = y + halfRange;
                        if (newPixel < targetFastBitmap.Bottom)
                        {
                            targetFastBitmap.GetColorAt(x, newPixel, tmpColor);
                            a += tmpColor[FastBitmapBase.ColorIndexA];
                            r += tmpColor[FastBitmapBase.ColorIndexR];
                            g += tmpColor[FastBitmapBase.ColorIndexG];
                            b += tmpColor[FastBitmapBase.ColorIndexB];
                            hits++;
                        }

                        if (y < targetFastBitmap.Top)
                        {
                            continue;
                        }
                        var colorPos = (y - targetFastBitmap.Top) << 2;
                        newColors[colorPos++] = (byte) (a / hits);
                        newColors[colorPos++] = (byte) (r / hits);
                        newColors[colorPos++] = (byte) (g / hits);
                        newColors[colorPos] = (byte) (b / hits);
                    }

                    for (var y = targetFastBitmap.Top; y < targetFastBitmap.Bottom; y++)
                    {
                        var colorPos = (y - targetFastBitmap.Top) << 2;
                        targetFastBitmap.SetColorAt(x, y, newColors, colorPos);
                    }
                }
            });
        }
    }
}
