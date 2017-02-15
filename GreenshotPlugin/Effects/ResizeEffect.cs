﻿#region Dapplo 2017 - GNU Lesser General Public License

// Dapplo - building blocks for .NET applications
// Copyright (C) 2017 Dapplo
// 
// For more information see: http://dapplo.net/
// Dapplo repositories are hosted on GitHub: https://github.com/dapplo
// 
// This file is part of Greenshot
// 
// Greenshot is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Greenshot is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
// 
// You should have a copy of the GNU Lesser General Public License
// along with Greenshot. If not, see <http://www.gnu.org/licenses/lgpl.txt>.

#endregion

#region Usings

using System.Drawing;
using System.Drawing.Drawing2D;
using GreenshotPlugin.Core;

#endregion

namespace GreenshotPlugin.Effects
{
	/// <summary>
	///     ResizeEffect
	/// </summary>
	public class ResizeEffect : IEffect
	{
		public ResizeEffect(int width, int height, bool maintainAspectRatio)
		{
			Width = width;
			Height = height;
			MaintainAspectRatio = maintainAspectRatio;
		}

		public int Width { get; set; }

		public int Height { get; set; }

		public bool MaintainAspectRatio { get; set; }

		public void Reset()
		{
			// values don't have a default value
		}

		public Image Apply(Image sourceImage, Matrix matrix)
		{
			return ImageHelper.ResizeImage(sourceImage, MaintainAspectRatio, Width, Height, matrix);
		}
	}
}