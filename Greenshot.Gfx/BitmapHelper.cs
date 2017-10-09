﻿#region Greenshot GNU General Public License

// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2017 Thomas Braun, Jens Klingen, Robin Krom
// 
// For more information see: http://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 1 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

#region Usings

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Dapplo.Log;
using Dapplo.Windows.Common.Extensions;
using Dapplo.Windows.Common.Structs;
using Dapplo.Windows.Dpi;
using Greenshot.Gfx.Effects;
using Greenshot.Gfx.FastBitmap;

#endregion

namespace Greenshot.Gfx
{
    /// <summary>
    ///     The BitmapHelper contains extensions for Bitmaps
    /// </summary>
    public static class BitmapHelper
	{
		private const int ExifOrientationId = 0x0112;
		private static readonly LogSource Log = new LogSource();
	    private static readonly ParallelOptions DefaultParallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4 };

        /// <summary>
        /// A function which usage image.fromstream
        /// </summary>
        public static readonly Func<Stream, string, Bitmap> FromStreamReader = (stream, s) =>
	    {
	        using (var tmpImage = Image.FromStream(stream, true, true))
	        {
	            var bitmap = tmpImage as Bitmap;
	            if (bitmap == null)
	            {
	                return null;
	            }
	            Log.Debug().WriteLine("Loaded bitmap with Size {0}x{1} and PixelFormat {2}", bitmap.Width, bitmap.Height, bitmap.PixelFormat);
	            return bitmap.CloneBitmap(PixelFormat.Format32bppArgb);
	        }
	    };

        static BitmapHelper()
		{
			

			// Fallback
			StreamConverters[""] = FromStreamReader;

			StreamConverters["gif"] = FromStreamReader;
			StreamConverters["bmp"] = FromStreamReader;
			StreamConverters["jpg"] = FromStreamReader;
			StreamConverters["jpeg"] = FromStreamReader;
			StreamConverters["png"] = FromStreamReader;
			StreamConverters["wmf"] = FromStreamReader;
			StreamConverters["svg"] = (stream, s) =>
			{
				stream.Position = 0;
				try
				{
					return SvgBitmap.FromStream(stream).Bitmap;
				}
				catch (Exception ex)
				{
					Log.Error().WriteLine(ex, "Can't load SVG");
				}
				return null;
			};

			StreamConverters["ico"] = (stream, extension) =>
			{
				// Icon logic, try to get the Vista icon, else the biggest possible
				try
				{
					using (var tmpBitmap = stream.ExtractVistaIcon())
					{
						if (tmpBitmap != null)
						{
							return tmpBitmap.CloneBitmap(PixelFormat.Format32bppArgb);
						}
					}
				}
				catch (Exception vistaIconException)
				{
					Log.Warn().WriteLine(vistaIconException, "Can't read icon");
				}
				try
				{
					// No vista icon, try normal icon
					stream.Position = 0;
					// We create a copy of the bitmap, so everything else can be disposed
					using (var tmpIcon = new Icon(stream, new Size(1024, 1024)))
					{
						using (var tmpImage = tmpIcon.ToBitmap())
						{
							return tmpImage.CloneBitmap(PixelFormat.Format32bppArgb);
						}
					}
				}
				catch (Exception iconException)
				{
					Log.Warn().WriteLine(iconException, "Can't read icon");
				}

				stream.Position = 0;
				return FromStreamReader(stream, extension);
			};
		}

		public static IDictionary<string, Func<Stream, string, Bitmap>> StreamConverters { get; } = new Dictionary<string, Func<Stream, string, Bitmap>>();

		/// <summary>
		///     Make sure the image is orientated correctly
		/// </summary>
		/// <param name="image">Image</param>
		public static void Orientate(this Image image)
		{
			try
			{
				// Get the index of the orientation property.
				var orientationIndex = Array.IndexOf(image.PropertyIdList, ExifOrientationId);
				// If there is no such property, return Unknown.
				if (orientationIndex < 0)
				{
					return;
				}
				var item = image.GetPropertyItem(ExifOrientationId);

				var orientation = (ExifOrientations) item.Value[0];
				// Orient the image.
				switch (orientation)
				{
					case ExifOrientations.Unknown:
					case ExifOrientations.TopLeft:
						break;
					case ExifOrientations.TopRight:
						image.RotateFlip(RotateFlipType.RotateNoneFlipX);
						break;
					case ExifOrientations.BottomRight:
						image.RotateFlip(RotateFlipType.Rotate180FlipNone);
						break;
					case ExifOrientations.BottomLeft:
						image.RotateFlip(RotateFlipType.RotateNoneFlipY);
						break;
					case ExifOrientations.LeftTop:
						image.RotateFlip(RotateFlipType.Rotate90FlipX);
						break;
					case ExifOrientations.RightTop:
						image.RotateFlip(RotateFlipType.Rotate90FlipNone);
						break;
					case ExifOrientations.RightBottom:
						image.RotateFlip(RotateFlipType.Rotate90FlipY);
						break;
					case ExifOrientations.LeftBottom:
						image.RotateFlip(RotateFlipType.Rotate270FlipNone);
						break;
				}
				// Set the orientation to be normal, as we rotated the image.
				item.Value[0] = (byte) ExifOrientations.TopLeft;
				image.SetPropertyItem(item);
			}
			catch (Exception orientEx)
			{
				Log.Warn().WriteLine(orientEx, "Problem orientating the image: ");
			}
		}

        /// <summary>
        ///     Create a Thumbnail
        /// </summary>
        /// <param name="image">Image</param>
        /// <param name="thumbWidth">int</param>
        /// <param name="thumbHeight">int</param>
        /// <param name="maxWidth">int</param>
        /// <param name="maxHeight">int</param>
        /// <returns></returns>
        public static Bitmap CreateThumbnail(this Image image, int thumbWidth, int thumbHeight, int maxWidth = -1, int maxHeight = -1)
		{
			var srcWidth = image.Width;
			var srcHeight = image.Height;
			if (thumbHeight < 0)
			{
				thumbHeight = (int) (thumbWidth * (srcHeight / (float) srcWidth));
			}
			if (thumbWidth < 0)
			{
				thumbWidth = (int) (thumbHeight * (srcWidth / (float) srcHeight));
			}
			if (maxWidth > 0 && thumbWidth > maxWidth)
			{
				thumbWidth = Math.Min(thumbWidth, maxWidth);
				thumbHeight = (int) (thumbWidth * (srcHeight / (float) srcWidth));
			}
			if (maxHeight > 0 && thumbHeight > maxHeight)
			{
				thumbHeight = Math.Min(thumbHeight, maxHeight);
				thumbWidth = (int) (thumbHeight * (srcWidth / (float) srcHeight));
			}

			var bmp = new Bitmap(thumbWidth, thumbHeight);
			using (var graphics = Graphics.FromImage(bmp))
			{
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				var rectDestination = new NativeRect(0, 0, thumbWidth, thumbHeight);
				graphics.DrawImage(image, rectDestination, 0, 0, srcWidth, srcHeight, GraphicsUnit.Pixel);
			}
			return bmp;
		}

        /// <summary>
        ///     Crops the image to the specified rectangle
        /// </summary>
        /// <param name="bitmap">Bitmap to crop</param>
        /// <param name="cropRectangle">NativeRect with bitmap coordinates, will be "intersected" to the bitmap</param>
        public static bool Crop(ref Bitmap bitmap, ref NativeRect cropRectangle)
		{
			if (bitmap.Width * bitmap.Height > 0)
			{
			    cropRectangle = cropRectangle.Intersect(new NativeRect(0, 0, bitmap.Width, bitmap.Height));
				if (cropRectangle.Width != 0 || cropRectangle.Height != 0)
				{
					var returnImage = bitmap.CloneBitmap(PixelFormat.DontCare, cropRectangle);
					bitmap.Dispose();
					bitmap = returnImage;
					return true;
				}
			}
			Log.Warn().WriteLine("Can't crop a null/zero size image!");
			return false;
		}

        /// <summary>
        ///     Private helper method for the FindAutoCropRectangle
        /// </summary>
        /// <param name="fastBitmap">IFastBitmap</param>
        /// <param name="referenceColor">color for reference</param>
        /// <param name="cropDifference">int</param>
        /// <returns>NativeRect</returns>
        private static NativeRect FindAutoCropRectangle(this IFastBitmap fastBitmap, Color referenceColor, int cropDifference)
		{
			var cropRectangle = NativeRect.Empty;
			var min = new NativePoint(int.MaxValue, int.MaxValue);
			var max = new NativePoint(int.MinValue, int.MinValue);

			if (cropDifference > 0)
			{
				for (var y = 0; y < fastBitmap.Height; y++)
				{
					for (var x = 0; x < fastBitmap.Width; x++)
					{
						var currentColor = fastBitmap.GetColorAt(x, y);
						var diffR = Math.Abs(currentColor.R - referenceColor.R);
						var diffG = Math.Abs(currentColor.G - referenceColor.G);
						var diffB = Math.Abs(currentColor.B - referenceColor.B);
						if ((diffR + diffG + diffB) / 3 <= cropDifference)
						{
							continue;
						}
						if (x < min.X)
						{
							min = min.ChangeX(x);
						}
						if (y < min.Y)
						{
						    min = min.ChangeY(y);
                        }
						if (x > max.X)
						{
							max = max.ChangeX(x);
						}
						if (y > max.Y)
						{
						    max = max.ChangeY(y);
                        }
					}
				}
			}
			else
			{
				for (var y = 0; y < fastBitmap.Height; y++)
				{
					for (var x = 0; x < fastBitmap.Width; x++)
					{
						var currentColor = fastBitmap.GetColorAt(x, y);
						if (!referenceColor.Equals(currentColor))
						{
							continue;
						}
						if (x < min.X)
						{
						    min = min.ChangeX(x);
                        }
						if (y < min.Y)
						{
						    min = min.ChangeY(y);
                        }
						if (x > max.X)
						{
							max = max.ChangeX(x);
						}
						if (y > max.Y)
						{
						    max = max.ChangeY(y);
                        }
					}
				}
			}

			if (!(NativePoint.Empty.Equals(min) && max.Equals(new NativePoint(fastBitmap.Width - 1, fastBitmap.Height - 1))))
			{
				if (!(min.X == int.MaxValue || min.Y == int.MaxValue || max.X == int.MinValue || min.X == int.MinValue))
				{
					cropRectangle = new NativeRect(min.X, min.Y, max.X - min.X + 1, max.Y - min.Y + 1);
				}
			}
			return cropRectangle;
		}

		/// <summary>
		///     Get a rectangle for the image which crops the image of all colors equal to that on 0,0
		/// </summary>
		/// <param name="image"></param>
		/// <param name="cropDifference"></param>
		/// <returns>NativeRect</returns>
		public static NativeRect FindAutoCropRectangle(this Image image, int cropDifference)
		{
			var cropRectangle = NativeRect.Empty;
			var checkPoints = new List<NativePoint>
			{
				new NativePoint(0, 0),
				new NativePoint(0, image.Height - 1),
				new NativePoint(image.Width - 1, 0),
				new NativePoint(image.Width - 1, image.Height - 1)
			};
			// Top Left
			// Bottom Left
			// Top Right
			// Bottom Right
			using (var fastBitmap = FastBitmapFactory.Create((Bitmap) image))
			{
				// find biggest area
				foreach (var checkPoint in checkPoints)
				{
					var currentRectangle = fastBitmap.FindAutoCropRectangle(fastBitmap.GetColorAt(checkPoint.X, checkPoint.Y), cropDifference);
					if (currentRectangle.Width * currentRectangle.Height > cropRectangle.Width * cropRectangle.Height)
					{
						cropRectangle = currentRectangle;
					}
				}
			}
			return cropRectangle;
		}

		/// <summary>
		///     Load an image from file
		/// </summary>
		/// <param name="filename"></param>
		/// <returns></returns>
		public static Bitmap LoadBitmap(string filename)
		{
			if (string.IsNullOrEmpty(filename))
			{
				return null;
			}
			if (!File.Exists(filename))
			{
				return null;
			}
			Bitmap fileBitmap;
			Log.Info().WriteLine("Loading image from file {0}", filename);
			// Fixed lock problem Bug #3431881
			using (Stream imageFileStream = File.OpenRead(filename))
			{
				fileBitmap = FromStream(imageFileStream, Path.GetExtension(filename));
			}
			if (fileBitmap != null)
			{
				Log.Info().WriteLine("Information about file {0}: {1}x{2}-{3} Resolution {4}x{5}", filename, fileBitmap.Width, fileBitmap.Height, fileBitmap.PixelFormat,
					fileBitmap.HorizontalResolution, fileBitmap.VerticalResolution);
			}
			return fileBitmap;
		}

		/// <summary>
		///     Based on: http://www.codeproject.com/KB/cs/IconExtractor.aspx
		///     And a hint from: http://www.codeproject.com/KB/cs/IconLib.aspx
		/// </summary>
		/// <param name="iconStream">Stream with the icon information</param>
		/// <returns>Bitmap with the Vista Icon (256x256)</returns>
		private static Bitmap ExtractVistaIcon(this Stream iconStream)
		{
			const int sizeIconDir = 6;
			const int sizeIconDirEntry = 16;
			Bitmap bmpPngExtracted = null;
			try
			{
				var srcBuf = new byte[iconStream.Length];
				iconStream.Read(srcBuf, 0, (int) iconStream.Length);
				int iCount = BitConverter.ToInt16(srcBuf, 4);
				for (var iIndex = 0; iIndex < iCount; iIndex++)
				{
					int iWidth = srcBuf[sizeIconDir + sizeIconDirEntry * iIndex];
					int iHeight = srcBuf[sizeIconDir + sizeIconDirEntry * iIndex + 1];
				    if (iWidth != 0 || iHeight != 0)
				    {
				        continue;
				    }
				    var iImageSize = BitConverter.ToInt32(srcBuf, sizeIconDir + sizeIconDirEntry * iIndex + 8);
				    var iImageOffset = BitConverter.ToInt32(srcBuf, sizeIconDir + sizeIconDirEntry * iIndex + 12);
				    using (var destStream = new MemoryStream())
				    {
				        destStream.Write(srcBuf, iImageOffset, iImageSize);
				        destStream.Seek(0, SeekOrigin.Begin);
				        bmpPngExtracted = new Bitmap(destStream); // This is PNG! :)
				    }
				    break;
				}
			}
			catch
			{
				return null;
			}
			return bmpPngExtracted;
		}

		/// <summary>
		///     Apply the effect to the bitmap
		/// </summary>
		/// <param name="sourceBitmap">Bitmap</param>
		/// <param name="effect">IEffect</param>
		/// <param name="matrix">Matrix</param>
		/// <returns>Bitmap</returns>
		public static Bitmap ApplyEffect(this Bitmap sourceBitmap, IEffect effect, Matrix matrix)
		{
			var effects = new List<IEffect> {effect};
			return sourceBitmap.ApplyEffects(effects, matrix);
		}

	    /// <summary>
	    ///     Apply the effects in the supplied order to the bitmap
	    /// </summary>
	    /// <param name="sourceBitmap"></param>
	    /// <param name="effects">List of IEffect</param>
	    /// <param name="matrix">Matrix</param>
	    /// <returns>Bitmap</returns>
	    public static Bitmap ApplyEffects(this Bitmap sourceBitmap, IEnumerable<IEffect> effects, Matrix matrix)
		{
			var currentImage = sourceBitmap;
			var disposeImage = false;
			foreach (var effect in effects)
			{
				var tmpImage = effect.Apply(currentImage, matrix);
			    if (tmpImage == null)
			    {
			        continue;
			    }
			    if (disposeImage)
			    {
			        currentImage.Dispose();
			    }
			    currentImage = tmpImage;
			    // Make sure the "new" image is disposed
			    disposeImage = true;
			}
			return currentImage;
		}

		/// <summary>
		///     This method fixes the problem that we can't apply a filter outside the target bitmap,
		///     therefor the filtered-bitmap will be shifted if we try to draw it outside the target bitmap.
		///     It will also account for the Invert flag.
		/// </summary>
		/// <param name="applySize"></param>
		/// <param name="rect"></param>
		/// <param name="invert"></param>
		/// <returns></returns>
		public static NativeRect CreateIntersectRectangle(Size applySize, NativeRect rect, bool invert)
		{
			NativeRect myRect;
			if (invert)
			{
				myRect = new NativeRect(0, 0, applySize.Width, applySize.Height);
			}
			else
			{
				var applyRect = new NativeRect(0, 0, applySize.Width, applySize.Height);
				myRect = new NativeRect(rect.X, rect.Y, rect.Width, rect.Height).Intersect(applyRect);
			}
			return myRect;
		}

		/// <summary>
		///     Create a new bitmap where the sourceBitmap has a shadow
		/// </summary>
		/// <param name="sourceBitmap">Bitmap to make a shadow on</param>
		/// <param name="darkness">How dark is the shadow</param>
		/// <param name="shadowSize">Size of the shadow</param>
		/// <param name="targetPixelformat">What pixel format must the returning bitmap have</param>
		/// <param name="shadowOffset"></param>
		/// <param name="matrix">
		///     The transform matrix which describes how the elements need to be transformed to stay at the same
		///     location
		/// </param>
		/// <returns>Bitmap with the shadow, is bigger than the sourceBitmap!!</returns>
		public static Bitmap CreateShadow(this Image sourceBitmap, float darkness, int shadowSize, NativePoint shadowOffset, Matrix matrix, PixelFormat targetPixelformat)
		{
		    var offset = shadowOffset.Offset(shadowSize - 1, shadowSize - 1);
			matrix.Translate(offset.X, offset.Y, MatrixOrder.Append);
			// Create a new "clean" image
			var returnImage = BitmapFactory.CreateEmpty(sourceBitmap.Width + shadowSize * 2, sourceBitmap.Height + shadowSize * 2, targetPixelformat, Color.Empty,
				sourceBitmap.HorizontalResolution, sourceBitmap.VerticalResolution);
			// Make sure the shadow is odd, there is no reason for an even blur!
			if ((shadowSize & 1) == 0)
			{
				shadowSize++;
			}
			// Create "mask" for the shadow
			var maskMatrix = new ColorMatrix
			{
				Matrix00 = 0,
				Matrix11 = 0,
				Matrix22 = 0,
                Matrix33 = darkness
			};
			
			var shadowRectangle = new NativeRect(new NativePoint(shadowSize, shadowSize), sourceBitmap.Size);
			ApplyColorMatrix((Bitmap) sourceBitmap, NativeRect.Empty, returnImage, shadowRectangle, maskMatrix);

			// blur "shadow", apply to whole new image

			// try normal software blur
			//returnImage = CreateBlur(returnImage, newImageRectangle, true, shadowSize, 1d, false, newImageRectangle);
			returnImage.ApplyBoxBlur(shadowSize);

			// Draw the original image over the shadow
			using (var graphics = Graphics.FromImage(returnImage))
			{
				// Make sure we draw with the best quality!
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				// draw original with a TextureBrush so we have nice antialiasing!
				using (Brush textureBrush = new TextureBrush(sourceBitmap, WrapMode.Clamp))
				{
					// We need to do a translate-transform otherwise the image is wrapped
					graphics.TranslateTransform(offset.X, offset.Y);
					graphics.FillRectangle(textureBrush, 0, 0, sourceBitmap.Width, sourceBitmap.Height);
				}
			}
			return returnImage;
		}

		/// <summary>
		///     Apply a color matrix to the image
		/// </summary>
		/// <param name="source">Image to apply matrix to</param>
		/// <param name="colorMatrix">ColorMatrix to apply</param>
		public static void ApplyColorMatrix(this Bitmap source, ColorMatrix colorMatrix)
		{
			source.ApplyColorMatrix(NativeRect.Empty, source, NativeRect.Empty, colorMatrix);
		}

		/// <summary>
		///     Apply a color matrix by copying from the source to the destination
		/// </summary>
		/// <param name="source">Image to copy from</param>
		/// <param name="sourceRect">NativeRect to copy from</param>
		/// <param name="destRect">NativeRect to copy to</param>
		/// <param name="dest">Image to copy to</param>
		/// <param name="colorMatrix">ColorMatrix to apply</param>
		public static void ApplyColorMatrix(this Bitmap source, NativeRect sourceRect, Bitmap dest, NativeRect destRect, ColorMatrix colorMatrix)
		{
			using (var imageAttributes = new ImageAttributes())
			{
				imageAttributes.ClearColorMatrix();
				imageAttributes.SetColorMatrix(colorMatrix);
				source.ApplyImageAttributes(sourceRect, dest, destRect, imageAttributes);
			}
		}

		/// <summary>
		///     Apply image attributes to the image
		/// </summary>
		/// <param name="source">Image to apply matrix to</param>
		/// <param name="imageAttributes">ImageAttributes to apply</param>
		public static void ApplyColorMatrix(this Bitmap source, ImageAttributes imageAttributes)
		{
			source.ApplyImageAttributes(NativeRect.Empty, source, NativeRect.Empty, imageAttributes);
		}

		/// <summary>
		///     Apply a color matrix by copying from the source to the destination
		/// </summary>
		/// <param name="source">Image to copy from</param>
		/// <param name="sourceRect">NativeRect to copy from</param>
		/// <param name="destRect">NativeRect to copy to</param>
		/// <param name="dest">Image to copy to</param>
		/// <param name="imageAttributes">ImageAttributes to apply</param>
		public static void ApplyImageAttributes(this Bitmap source, NativeRect sourceRect, Bitmap dest, NativeRect destRect, ImageAttributes imageAttributes)
		{
			if (sourceRect == NativeRect.Empty)
			{
				sourceRect = new NativeRect(0, 0, source.Width, source.Height);
			}
			if (dest == null)
			{
				dest = source;
			}
			if (destRect == NativeRect.Empty)
			{
				destRect = new NativeRect(0, 0, dest.Width, dest.Height);
			}
			using (var graphics = Graphics.FromImage(dest))
			{
				// Make sure we draw with the best quality!
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.CompositingMode = CompositingMode.SourceCopy;

				graphics.DrawImage(source, destRect, sourceRect.X, sourceRect.Y, sourceRect.Width, sourceRect.Height, GraphicsUnit.Pixel, imageAttributes);
			}
		}

		/// <summary>
		///     Checks if the supplied Bitmap has a PixelFormat we support
		/// </summary>
		/// <param name="image">bitmap to check</param>
		/// <returns>bool if we support it</returns>
		public static bool IsPixelFormatSupported(this Image image)
		{
			return image.PixelFormat.IsPixelFormatSupported();
		}

		/// <summary>
		///     Checks if we support the pixel format
		/// </summary>
		/// <param name="pixelformat">PixelFormat to check</param>
		/// <returns>bool if we support it</returns>
		public static bool IsPixelFormatSupported(this PixelFormat pixelformat)
		{
			return pixelformat.Equals(PixelFormat.Format32bppArgb) ||
			       pixelformat.Equals(PixelFormat.Format32bppPArgb) ||
			       pixelformat.Equals(PixelFormat.Format32bppRgb) ||
			       pixelformat.Equals(PixelFormat.Format24bppRgb);
		}


		/// <summary>
		///     Rotate the bitmap
		/// </summary>
		/// <param name="sourceBitmap">Image</param>
		/// <param name="rotateFlipType">RotateFlipType</param>
		/// <returns>Image</returns>
		public static Bitmap ApplyRotateFlip(this Bitmap sourceBitmap, RotateFlipType rotateFlipType)
		{
			var returnImage = sourceBitmap.CloneBitmap();
			returnImage.RotateFlip(rotateFlipType);
			return returnImage;
		}


        /// <summary>
        ///     Get a scaled version of the sourceBitmap
        /// </summary>
        /// <param name="sourceImage">Image</param>
        /// <param name="percent">1-99 to make smaller, use 101 and more to make the picture bigger</param>
        /// <returns>Bitmap</returns>
        public static Bitmap ScaleByPercent(this Image sourceImage, int percent)
		{
			var nPercent = (float) percent / 100;

			var sourceWidth = sourceImage.Width;
			var sourceHeight = sourceImage.Height;
			var destWidth = (int) (sourceWidth * nPercent);
			var destHeight = (int) (sourceHeight * nPercent);

			var scaledBitmap = BitmapFactory.CreateEmpty(destWidth, destHeight, sourceImage.PixelFormat, Color.Empty, sourceImage.HorizontalResolution, sourceImage.VerticalResolution);
			using (var graphics = Graphics.FromImage(scaledBitmap))
			{
				graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
				graphics.DrawImage(sourceImage, new NativeRect(0, 0, destWidth, destHeight), new NativeRect(0, 0, sourceWidth, sourceHeight), GraphicsUnit.Pixel);
			}
			return scaledBitmap;
		}

        /// <summary>
        ///     Resize canvas with pixel to the left, right, top and bottom
        /// </summary>
        /// <param name="sourceBitmap">Bitmap</param>
        /// <param name="backgroundColor">The color to fill with, or Color.Empty to take the default depending on the pixel format</param>
        /// <param name="left">int</param>
        /// <param name="right">int</param>
        /// <param name="top">int</param>
        /// <param name="bottom">int</param>
        /// <param name="matrix">Matrix</param>
        /// <returns>a new bitmap with the source copied on it</returns>
        public static Bitmap ResizeCanvas(this Bitmap sourceBitmap, Color backgroundColor, int left, int right, int top, int bottom, Matrix matrix)
		{
			matrix.Translate(left, top, MatrixOrder.Append);
			var newBitmap = BitmapFactory.CreateEmpty(sourceBitmap.Width + left + right, sourceBitmap.Height + top + bottom, sourceBitmap.PixelFormat, backgroundColor,
				sourceBitmap.HorizontalResolution, sourceBitmap.VerticalResolution);
			using (var graphics = Graphics.FromImage(newBitmap))
			{
				graphics.DrawImageUnscaled(sourceBitmap, left, top);
			}
			return newBitmap;
		}

        /// <summary>
        ///     Wrapper for the more complex Resize, this resize could be used for e.g. Thumbnails
        /// </summary>
        /// <param name="sourceBitmap">Bitmap</param>
        /// <param name="maintainAspectRatio">true to maintain the aspect ratio</param>
        /// <param name="newWidth">int</param>
        /// <param name="newHeight">int</param>
        /// <param name="matrix">Matrix</param>
        /// <param name="interpolationMode">InterpolationMode</param>
        /// <returns>Image</returns>
        public static Bitmap Resize(this Bitmap sourceBitmap, bool maintainAspectRatio, int newWidth, int newHeight, Matrix matrix = null, InterpolationMode interpolationMode = InterpolationMode.HighQualityBicubic)
		{
			return Resize(sourceBitmap, maintainAspectRatio, false, Color.Empty, newWidth, newHeight, matrix, interpolationMode);
		}

        /// <summary>
        ///     Scale the bitmap, keeping aspect ratio, but the canvas will always have the specified size.
        /// </summary>
        /// <param name="sourceImage">Image to scale</param>
        /// <param name="maintainAspectRatio">true to maintain the aspect ratio</param>
        /// <param name="canvasUseNewSize">Makes the image maintain aspect ratio, but the canvas get's the specified size</param>
        /// <param name="backgroundColor">The color to fill with, or Color.Empty to take the default depending on the pixel format</param>
        /// <param name="newWidth">new width</param>
        /// <param name="newHeight">new height</param>
        /// <param name="matrix">Matrix</param>
        /// <param name="interpolationMode">InterpolationMode</param>
        /// <returns>a new bitmap with the specified size, the source-Image scaled to fit with aspect ratio locked</returns>
        public static Bitmap Resize(this Image sourceImage, bool maintainAspectRatio, bool canvasUseNewSize, Color backgroundColor, int newWidth, int newHeight, Matrix matrix, InterpolationMode interpolationMode = InterpolationMode.HighQualityBicubic)
		{
			var destX = 0;
			var destY = 0;

			var nPercentW = newWidth / (float) sourceImage.Width;
			var nPercentH = newHeight / (float) sourceImage.Height;
			if (maintainAspectRatio)
			{
				if ((int) nPercentW == 1)
				{
					nPercentW = nPercentH;
					if (canvasUseNewSize)
					{
						destX = Math.Max(0, Convert.ToInt32((newWidth - sourceImage.Width * nPercentW) / 2));
					}
				}
				else if ((int) nPercentH == 1)
				{
					nPercentH = nPercentW;
					if (canvasUseNewSize)
					{
						destY = Math.Max(0, Convert.ToInt32((newHeight - sourceImage.Height * nPercentH) / 2));
					}
				}
				else if ((int) nPercentH != 0 && nPercentH < nPercentW)
				{
					nPercentW = nPercentH;
					if (canvasUseNewSize)
					{
						destX = Math.Max(0, Convert.ToInt32((newWidth - sourceImage.Width * nPercentW) / 2));
					}
				}
				else
				{
					nPercentH = nPercentW;
					if (canvasUseNewSize)
					{
						destY = Math.Max(0, Convert.ToInt32((newHeight - sourceImage.Height * nPercentH) / 2));
					}
				}
			}

			var destWidth = (int) (sourceImage.Width * nPercentW);
			var destHeight = (int) (sourceImage.Height * nPercentH);
			if (newWidth == 0)
			{
				newWidth = destWidth;
			}
			if (newHeight == 0)
			{
				newHeight = destHeight;
			}
		    Bitmap newBitmap;
			if (maintainAspectRatio && canvasUseNewSize)
			{
				newBitmap = BitmapFactory.CreateEmpty(newWidth, newHeight, sourceImage.PixelFormat, backgroundColor, sourceImage.HorizontalResolution, sourceImage.VerticalResolution);
				matrix?.Scale((float) newWidth / sourceImage.Width, (float) newHeight / sourceImage.Height, MatrixOrder.Append);
			}
			else
			{
				newBitmap = BitmapFactory.CreateEmpty(destWidth, destHeight, sourceImage.PixelFormat, backgroundColor, sourceImage.HorizontalResolution, sourceImage.VerticalResolution);
				matrix?.Scale((float) destWidth / sourceImage.Width, (float) destHeight / sourceImage.Height, MatrixOrder.Append);
			}

			using (var graphics = Graphics.FromImage(newBitmap))
			{
				graphics.CompositingQuality = CompositingQuality.HighQuality;
				graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
				graphics.SmoothingMode = SmoothingMode.HighQuality;
				graphics.InterpolationMode = interpolationMode;
				using (var wrapMode = new ImageAttributes())
				{
					wrapMode.SetWrapMode(WrapMode.TileFlipXY);
					graphics.DrawImage(sourceImage, new NativeRect(destX, destY, destWidth, destHeight), 0, 0, sourceImage.Width, sourceImage.Height, GraphicsUnit.Pixel, wrapMode);
				}
			}
			return newBitmap;
		}

	    /// <summary>
	    ///     Count how many times the supplied color exists
	    /// </summary>
	    /// <param name="sourceImage">Image to count the pixels of</param>
	    /// <param name="colorToCount">Color to count</param>
	    /// <param name="includeAlpha">true if Alpha needs to be checked</param>
	    /// <returns>int with the number of pixels which have colorToCount</returns>
	    public static int CountColors(this Image sourceImage, Color colorToCount, bool includeAlpha)
	    {
	        var colors = 0;
	        var toCount = colorToCount.ToArgb();
	        if (!includeAlpha)
	        {
	            toCount = toCount & 0xffffff;
	        }
	        using (var bb = FastBitmapFactory.Create((Bitmap)sourceImage))
	        {
	            for (var y = 0; y < bb.Height; y++)
	            {
	                for (var x = 0; x < bb.Width; x++)
	                {
	                    var bitmapcolor = bb.GetColorAt(x, y).ToArgb();
	                    if (!includeAlpha)
	                    {
	                        bitmapcolor = bitmapcolor & 0xffffff;
	                    }
	                    if (bitmapcolor == toCount)
	                    {
	                        colors++;
	                    }
	                }
	            }
	            return colors;
	        }
	    }

        /// <summary>
        ///     Create an image from a stream, if an extension is supplied more formats are supported.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="extension"></param>
        /// <returns>Image</returns>
        public static Bitmap FromStream(Stream stream, string extension = null)
		{
			if (stream == null)
			{
				return null;
			}
			if (!string.IsNullOrEmpty(extension))
			{
				extension = extension.Replace(".", "");
			}

			// Make sure we can try multiple times
			if (!stream.CanSeek)
			{
				var memoryStream = new MemoryStream();
				stream.CopyTo(memoryStream);
				stream = memoryStream;
			}

			Bitmap returnBitmap = null;
		    if (StreamConverters.TryGetValue(extension ?? "", out var converter))
			{
				returnBitmap = converter(stream, extension);
			}
		    if (returnBitmap != null || converter == FromStreamReader)
		    {
		        return returnBitmap;
		    }
		    // Fallback to default converter
		    stream.Position = 0;
		    returnBitmap = FromStreamReader(stream, extension);
		    return returnBitmap;
		}

        /// <summary>
        ///     Prepare an "icon" to be displayed correctly scaled
        /// </summary>
        /// <param name="original">original icon Bitmap</param>
        /// <param name="dpi">double with the dpi value</param>
        /// <param name="baseSize">the base size of the icon, default is 16</param>
        /// <returns>Bitmap</returns>
        public static Bitmap ScaleIconForDisplaying(this Bitmap original, double dpi, int baseSize = 16)
		{
			if (original == null)
			{
				return null;
			}
			if (dpi < DpiHandler.DefaultScreenDpi)
			{
				dpi = DpiHandler.DefaultScreenDpi;
			}
			var width = DpiHandler.ScaleWithDpi(baseSize, dpi);
			if (original.Width == width)
			{
				return original;
			}

			if (width == original.Width * 2)
			{
				return original.Scale2X();
			}
			if (width == original.Width * 3)
			{
				return original.Scale3X();
			}
			if (width == original.Width * 4)
			{
				using (var scale2X = original.Scale2X())
				{
					return scale2X.Scale2X();
				}
			}
			return original.Resize(true, width, width, interpolationMode: InterpolationMode.NearestNeighbor);
		}

		/// <summary>
		///     Use "Scale2x" algorithm to produce bitmap from the original.
		/// </summary>
		/// <param name="original">Bitmap to scale 2x</param>
		public static Bitmap Scale2X(this Bitmap original)
		{
			using (var source = (IFastBitmapWithClip)FastBitmapFactory.Create(original))
			using (var destination = (IFastBitmapWithClip)FastBitmapFactory.CreateEmpty(new Size(original.Width * 2, original.Height * 2), original.PixelFormat))
			{
				// Every pixel from input texture produces 4 output pixels, for more details check out http://scale2x.sourceforge.net/algorithm.html
			    Parallel.For(0, source.Height, DefaultParallelOptions, y =>
			    {        
					var x = 0;
					while (x < source.Width)
					{
						var colorB = source.GetColorAt(x, y - 1);
						var colorH = source.GetColorAt(x, y + 1);
						var colorD = source.GetColorAt(x - 1, y);
						var colorF = source.GetColorAt(x + 1, y);
						var colorE = source.GetColorAt(x, y);

						Color colorE0;
						Color colorE1;
						Color colorE2;
						Color colorE3;
						if (!AreColorsSame(colorB, colorH) && !AreColorsSame(colorD, colorF))
						{
							colorE0 = AreColorsSame(colorD, colorB) ? colorD : colorE;
							colorE1 = AreColorsSame(colorB, colorF) ? colorF : colorE;
							colorE2 = AreColorsSame(colorD, colorH) ? colorD : colorE;
							colorE3 = AreColorsSame(colorH, colorF) ? colorF : colorE;
						}
						else
						{
							colorE0 = colorE;
							colorE1 = colorE;
							colorE2 = colorE;
							colorE3 = colorE;
						}
						destination.SetColorAt(2 * x, 2 * y, ref colorE0);
						destination.SetColorAt(2 * x + 1, 2 * y, ref colorE1);
						destination.SetColorAt(2 * x, 2 * y + 1, ref colorE2);
						destination.SetColorAt(2 * x + 1, 2 * y + 1, ref colorE3);

						x++;
					}
			    });
				return destination.UnlockAndReturnBitmap();
			}
		}

		/// <summary>
		///     Use "Scale3x" algorithm to produce bitmap from the original.
		/// </summary>
		/// <param name="original">Bitmap to scale 3x</param>
		public static Bitmap Scale3X(this Bitmap original)
		{
			using (var source = (IFastBitmapWithClip)FastBitmapFactory.Create(original))
			using (var destination = (IFastBitmapWithClip)FastBitmapFactory.CreateEmpty(new Size(original.Width * 3, original.Height * 3), original.PixelFormat))
			{
                // Every pixel from input texture produces 6 output pixels, for more details check out http://scale2x.sourceforge.net/algorithm.html
			    Parallel.For(0, source.Height, DefaultParallelOptions, y =>
			    {
			        var x = 0;
			        while (x < source.Width)
			        {
			            var colorA = source.GetColorAt(x - 1, y - 1);
			            var colorB = source.GetColorAt(x, y - 1);
			            var colorC = source.GetColorAt(x + 1, y - 1);

			            var colorD = source.GetColorAt(x - 1, y);
			            var colorE = source.GetColorAt(x, y);
			            var colorF = source.GetColorAt(x + 1, y);

			            var colorG = source.GetColorAt(x - 1, y + 1);
			            var colorH = source.GetColorAt(x, y + 1);
			            var colorI = source.GetColorAt(x + 1, y + 1);

			            Color colorE0, colorE1, colorE2;
			            Color colorE3, colorE4, colorE5;
			            Color colorE6, colorE7, colorE8;

			            if (!AreColorsSame(colorB, colorH) && !AreColorsSame(colorD, colorF))
			            {
			                colorE0 = AreColorsSame(colorD, colorB) ? colorD : colorE;
			                colorE1 = AreColorsSame(colorD, colorB) && !AreColorsSame(colorE, colorC) || AreColorsSame(colorB, colorF) && !AreColorsSame(colorE, colorA) ? colorB : colorE;
			                colorE2 = AreColorsSame(colorB, colorF) ? colorF : colorE;
			                colorE3 = AreColorsSame(colorD, colorB) && !AreColorsSame(colorE, colorG) || AreColorsSame(colorD, colorH) && !AreColorsSame(colorE, colorA) ? colorD : colorE;

			                colorE4 = colorE;
			                colorE5 = AreColorsSame(colorB, colorF) && !AreColorsSame(colorE, colorI) || AreColorsSame(colorH, colorF) && !AreColorsSame(colorE, colorC) ? colorF : colorE;
			                colorE6 = AreColorsSame(colorD, colorH) ? colorD : colorE;
			                colorE7 = AreColorsSame(colorD, colorH) && !AreColorsSame(colorE, colorI) || AreColorsSame(colorH, colorF) && !AreColorsSame(colorE, colorG) ? colorH : colorE;
			                colorE8 = AreColorsSame(colorH, colorF) ? colorF : colorE;
			            }
			            else
			            {
			                colorE0 = colorE;
			                colorE1 = colorE;
			                colorE2 = colorE;
			                colorE3 = colorE;
			                colorE4 = colorE;
			                colorE5 = colorE;
			                colorE6 = colorE;
			                colorE7 = colorE;
			                colorE8 = colorE;
			            }
			            int multipliedX = 3 * x;
			            int multipliedY = 3 * y;

			            destination.SetColorAt(multipliedX - 1, multipliedY - 1, ref colorE0);
			            destination.SetColorAt(multipliedX, multipliedY - 1, ref colorE1);
			            destination.SetColorAt(multipliedX + 1, multipliedY - 1, ref colorE2);

			            destination.SetColorAt(multipliedX - 1, multipliedY, ref colorE3);
			            destination.SetColorAt(multipliedX, multipliedY, ref colorE4);
			            destination.SetColorAt(multipliedX + 1, multipliedY, ref colorE5);

			            destination.SetColorAt(multipliedX - 1, multipliedY + 1, ref colorE6);
			            destination.SetColorAt(multipliedX, multipliedY + 1, ref colorE7);
			            destination.SetColorAt(multipliedX + 1, multipliedY + 1, ref colorE8);

			            x++;
			        }
			    });
				return destination.UnlockAndReturnBitmap();
			}
		}


		/// <summary>
		///     Checks if the colors are the same.
		/// </summary>
		/// <param name="aColor">Color first</param>
		/// <param name="bColor">Color second</param>
		/// <returns>
		///     True if they are; otherwise false
		/// </returns>
		private static bool AreColorsSame(Color aColor, Color bColor)
		{
			return aColor.R == bColor.R && aColor.G == bColor.G && aColor.B == bColor.B && aColor.A == bColor.A;
		}
	}
}