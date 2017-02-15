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

using System;

#endregion

namespace GreenshotPlugin.Interfaces.Drawing
{
	/// <summary>
	///     Description of IMemento.
	/// </summary>
	public interface IMemento : IDisposable
	{
		/// <summary>
		///     Restores target to the state memorized by this memento.
		/// </summary>
		/// <returns>
		///     A memento of the state before restoring
		/// </returns>
		IMemento Restore();

		/// <summary>
		///     Try to merge the current memento with another, preventing loads of items on the stack
		/// </summary>
		/// <param name="other">The memento to try to merge with</param>
		/// <returns></returns>
		bool Merge(IMemento other);
	}
}